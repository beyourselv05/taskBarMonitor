using System.Diagnostics;

namespace TaskbarMonitor;

class TrayAppContext : ApplicationContext
{
    private static readonly Color TextColor = Color.FromArgb(0, 230, 90);

    private readonly NotifyIcon _gpuIcon;
    private readonly NotifyIcon _ramIcon;
    private readonly NotifyIcon _cpuIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly PerformanceCounter _cpuCounter;
    private readonly int _iconSize;

    private bool _isUpdating;
    private string? _lastCpuText;
    private string? _lastRamText;
    private string? _lastGpuText;

    public TrayAppContext()
    {
        _iconSize = Math.Max(SystemInformation.SmallIconSize.Width, SystemInformation.SmallIconSize.Height);

        var menu = new ContextMenuStrip();
        menu.Items.Add("종료", null, (_, _) => ExitThread());

        _gpuIcon = new NotifyIcon
        {
            Icon = IconRenderer.CreateNumberIcon("0", TextColor, _iconSize),
            Text = "GPU: 계산 중...",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _ramIcon = new NotifyIcon
        {
            Icon = IconRenderer.CreateNumberIcon("0", TextColor, _iconSize),
            Text = "RAM: 계산 중...",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _cpuIcon = new NotifyIcon
        {
            Icon = IconRenderer.CreateNumberIcon("0", TextColor, _iconSize),
            Text = "CPU: 계산 중...",
            ContextMenuStrip = menu,
            Visible = true,
        };

        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _cpuCounter.NextValue();

        // Both counters need a first reading to measure the next one against.
        _ = GpuMonitor.PrimeAsync();

        _timer = new System.Windows.Forms.Timer { Interval = 1500 };
        _timer.Tick += async (_, _) => await UpdateAsync();
        _timer.Start();
    }

    private async Task UpdateAsync()
    {
        if (_isUpdating)
        {
            return;
        }

        _isUpdating = true;
        try
        {
            int cpuPercent = (int)Math.Round(_cpuCounter.NextValue());
            cpuPercent = Math.Clamp(cpuPercent, 0, 100);
            string cpuText = cpuPercent.ToString();
            if (cpuText != _lastCpuText)
            {
                IconRenderer.SafeReplace(_cpuIcon, IconRenderer.CreateNumberIcon(cpuText, TextColor, _iconSize));
                _lastCpuText = cpuText;
            }
            _cpuIcon.Text = $"CPU: {cpuPercent}%";

            var ram = MemoryInfo.GetSnapshot();
            string ramText = ram.UsedPercent.ToString();
            if (ramText != _lastRamText)
            {
                IconRenderer.SafeReplace(_ramIcon, IconRenderer.CreateNumberIcon(ramText, TextColor, _iconSize));
                _lastRamText = ramText;
            }
            _ramIcon.Text = $"RAM: {ram.UsedPercent}% ({ram.UsedGb:F1} / {ram.TotalGb:F1} GB)";

            int? gpuPercent = await GpuMonitor.GetUtilizationPercentAsync();
            string gpuText = gpuPercent.HasValue ? gpuPercent.Value.ToString() : "-";
            if (gpuText != _lastGpuText)
            {
                IconRenderer.SafeReplace(_gpuIcon, IconRenderer.CreateNumberIcon(gpuText, TextColor, _iconSize));
                _lastGpuText = gpuText;
            }
            _gpuIcon.Text = gpuPercent.HasValue ? $"GPU: {gpuPercent.Value}%" : "GPU: 확인 불가";
        }
        finally
        {
            _isUpdating = false;
        }
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _timer.Dispose();
        _cpuCounter.Dispose();

        _gpuIcon.Visible = false;
        _ramIcon.Visible = false;
        _cpuIcon.Visible = false;
        _gpuIcon.Dispose();
        _ramIcon.Dispose();
        _cpuIcon.Dispose();

        base.ExitThreadCore();
    }
}
