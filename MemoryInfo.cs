using System.Runtime.InteropServices;

namespace TaskbarMonitor;

static class MemoryInfo
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public record Snapshot(int UsedPercent, double UsedGb, double TotalGb);

    public static Snapshot GetSnapshot()
    {
        var status = new MEMORYSTATUSEX();
        status.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
        if (!GlobalMemoryStatusEx(ref status))
        {
            return new Snapshot(0, 0, 0);
        }

        double totalGb = status.ullTotalPhys / 1024.0 / 1024.0 / 1024.0;
        double availGb = status.ullAvailPhys / 1024.0 / 1024.0 / 1024.0;
        double usedGb = totalGb - availGb;
        int usedPercent = (int)Math.Round(usedGb / totalGb * 100.0);

        return new Snapshot(usedPercent, usedGb, totalGb);
    }
}
