namespace TaskbarMonitor;

/// <summary>
/// Picks a GPU utilization source that works on the machine we're running on.
/// The WDDM performance counters come first because they cover every vendor,
/// cost almost nothing to read, and agree with Task Manager; nvidia-smi is only
/// reached for NVIDIA cards those counters don't expose.
/// </summary>
static class GpuMonitor
{
    public static async Task<int?> GetUtilizationPercentAsync()
    {
        if (GpuEngineCounters.IsAvailable)
        {
            // A null here means "no reading this tick", not "no GPU", so it must not
            // fall through to nvidia-smi - the counters are still the right source.
            return await Task.Run(GpuEngineCounters.Sample);
        }

        return await NvidiaSmi.GetUtilizationPercentAsync();
    }

    /// <summary>
    /// Establishes the counter baseline ahead of the first tick, so the tray shows a
    /// real number immediately instead of a placeholder. Also absorbs the one-off
    /// category initialization cost off the UI thread.
    /// </summary>
    public static Task PrimeAsync() => Task.Run(() => GpuEngineCounters.Sample());
}
