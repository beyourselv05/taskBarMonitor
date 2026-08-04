using System.Diagnostics;
using System.Globalization;

namespace TaskbarMonitor;

static class GpuMonitor
{
    // Once nvidia-smi is confirmed missing, stop paying the process-spawn cost every tick.
    private static bool _unavailable;

    public static async Task<int?> GetUtilizationPercentAsync()
    {
        if (_unavailable)
        {
            return null;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=utilization.gpu --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            string firstLine = output.Split('\n')[0].Trim();
            if (int.TryParse(firstLine, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return value;
            }

            return null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // nvidia-smi isn't installed/reachable on this machine; don't keep retrying.
            _unavailable = true;
            return null;
        }
        catch
        {
            return null;
        }
    }
}
