using System.ComponentModel;
using System.Diagnostics;

namespace TaskbarMonitor;

/// <summary>
/// Vendor-neutral GPU utilization from the WDDM "GPU Engine" performance counters.
/// Windows populates these for every adapter it drives - NVIDIA, Intel and AMD alike -
/// and they are what Task Manager's GPU column is built on.
/// </summary>
static class GpuEngineCounters
{
    private const string CategoryName = "GPU Engine";

    // "Running Time" accumulates busy time in 100ns units, so utilization is the
    // growth of that counter measured against the wall clock between two samples.
    private const string CounterName = "Running Time";
    private const double UnitsPerSecond = 10_000_000.0;

    private static PerformanceCounterCategory? _category;
    private static Dictionary<string, long>? _previous;
    private static long _previousTimestamp;

    /// <summary>
    /// False once the counter category has proven unreadable on this machine,
    /// which is the signal for the caller to try another source.
    /// </summary>
    public static bool IsAvailable { get; private set; } = true;

    /// <summary>
    /// Returns the busiest engine's utilization, or null when no figure can be
    /// produced yet - the first call only establishes the baseline to diff against.
    /// </summary>
    public static int? Sample()
    {
        try
        {
            _category ??= new PerformanceCounterCategory(CategoryName);

            var current = ReadBusyTimePerEngine(_category);
            long timestamp = Stopwatch.GetTimestamp();

            var previous = _previous;
            long previousTimestamp = _previousTimestamp;
            _previous = current;
            _previousTimestamp = timestamp;

            if (previous is null)
            {
                return null;
            }

            double elapsedUnits = (timestamp - previousTimestamp) * UnitsPerSecond / Stopwatch.Frequency;
            if (elapsedUnits <= 0)
            {
                return null;
            }

            // Engines (3D, Copy, Video Decode, ...) run concurrently, so their loads
            // are not additive - summing them would sail past 100%. Report the busiest
            // one instead, which is what Task Manager does. The engine key carries the
            // adapter LUID, so on a hybrid laptop this naturally tracks whichever of the
            // integrated and discrete GPUs is under load.
            double busiest = 0;
            foreach (var (engine, busyUnits) in current)
            {
                if (!previous.TryGetValue(engine, out long before))
                {
                    continue;
                }

                // A process exiting shrinks its engine's total, which shows up as a
                // negative delta; those are discarded by never beating the running max.
                double utilization = (busyUnits - before) / elapsedUnits * 100.0;
                if (utilization > busiest)
                {
                    busiest = utilization;
                }
            }

            return (int)Math.Round(Math.Clamp(busiest, 0, 100));
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or Win32Exception)
        {
            IsAvailable = false;
            _previous = null;
            return null;
        }
    }

    private static Dictionary<string, long> ReadBusyTimePerEngine(PerformanceCounterCategory category)
    {
        var totals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        var instances = category.ReadCategory()[CounterName];
        if (instances is null)
        {
            throw new InvalidOperationException($"'{CategoryName}' has no '{CounterName}' counter.");
        }

        foreach (InstanceData instance in instances.Values)
        {
            string? engine = EngineKey(instance.InstanceName);
            if (engine is null)
            {
                continue;
            }

            totals.TryGetValue(engine, out long busyUnits);
            totals[engine] = busyUnits + instance.RawValue;
        }

        return totals;
    }

    /// <summary>
    /// Instances are reported per process, e.g.
    /// <c>pid_4392_luid_0x00000000_0x0000C7CF_phys_0_eng_0_engtype_3D</c>.
    /// Dropping the pid prefix leaves the physical engine, so every process's slice
    /// of the same engine collapses onto one key.
    /// </summary>
    private static string? EngineKey(string instanceName)
    {
        int start = instanceName.IndexOf("luid_", StringComparison.OrdinalIgnoreCase);
        return start < 0 ? null : instanceName[start..];
    }
}
