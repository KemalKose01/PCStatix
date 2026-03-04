using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;
using LhmSensorType = LibreHardwareMonitor.Hardware.SensorType;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;


namespace HardwareMonitor
{
    public sealed partial class MainWindow : Window
    {

        private FpsMonitor _fpsMonitor;
        private  Computer _computer;
        private  DispatcherTimer _timer;
        private List<PerformanceCounter> _igpuCounters = new();

        private AppWindow _appWindow;

        // =========================
        // FPS
        // =========================
        private TraceEventSession? _fpsSession;
        private int _frameCount;
        private readonly Stopwatch _fpsWatch = new();

        public MainWindow()
        {
            InitializeComponent();

            SetupWindow();
            SetupHardwareMonitor();
            SetupTimer();
            StartFpsMonitor();
            this.Closed += MainWindow_Closed;
        }

        // =========================
        // WINDOW SETUP
        // =========================
        private void SetupWindow()
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            _appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        }

        // =========================
        // HARDWARE INIT
        // =========================
        private void SetupHardwareMonitor()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true
            };

            _computer.Open();
            InitIGpuCounter();
        }

        // =========================
        // TIMER
        // =========================
        private void SetupTimer()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _timer.Tick += (_, _) => RefreshAll();
            _timer.Start();
        }

        // =========================
        // FPS MONITOR (REAL DXGI)
        // =========================
        private void StartFpsMonitor()
        {
            Task.Run(() =>
            {
                try
                {
                    _fpsWatch.Start();

                    using (_fpsSession = new TraceEventSession("FPSMonitorSession"))
                    {
                        _fpsSession.EnableProvider("Microsoft-Windows-DXGI");

                        _fpsSession.Source.Dynamic.All += traceEvent =>
                        {
                            if (traceEvent.EventName.Contains("Present"))
                            {
                                _frameCount++;

                                if (_fpsWatch.ElapsedMilliseconds >= 1000)
                                {
                                    int fps = _frameCount;
                                    _frameCount = 0;
                                    _fpsWatch.Restart();

                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        TxtFps.Text = $"FPS: {fps}";

                                        if (fps >= 60)
                                            TxtFps.Foreground =
                                                new SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
                                        else if (fps >= 30)
                                            TxtFps.Foreground =
                                                new SolidColorBrush(Microsoft.UI.Colors.Orange);
                                        else
                                            TxtFps.Foreground =
                                                new SolidColorBrush(Microsoft.UI.Colors.Red);
                                    });
                                }
                            }
                        };

                        _fpsSession.Source.Process();
                    }
                }
                catch
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        TxtFps.Text = "FPS: Admin gerekli";
                        TxtFps.Foreground =
                            new SolidColorBrush(Microsoft.UI.Colors.Red);
                    });
                }
            });
        }

        // =========================
        // REFRESH ALL
        // =========================
        private void RefreshAll()
        {
            ReadCpu();
            ReadDGpu();
            ReadMemory();
            ReadDisk();
            ReadIGpu();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            _fpsMonitor?.Stop();
            _computer?.Close();
        }
        // =========================
        // CPU
        // =========================
        private void ReadCpu()
        {
            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Cpu))
            {
                hw.Update();
                TxtCpuName.Text = hw.Name;

                var pkgTemp = hw.Sensors.FirstOrDefault(s =>
                    s.SensorType == LhmSensorType.Temperature &&
                    (s.Name.Contains("Package") || s.Name.Contains("Tctl")));

                TxtCpuTemp.Text = pkgTemp?.Value != null
                    ? $"CPU: {pkgTemp.Value:0}°C"
                    : "CPU: --";
            }
        }

        // =========================
        // DGPU
        // =========================
        private void ReadDGpu()
        {
            foreach (var hw in _computer.Hardware.Where(h =>
                h.HardwareType == HardwareType.GpuNvidia ||
                h.HardwareType == HardwareType.GpuAmd))
            {
                hw.Update();
                TxtDGpuName.Text = hw.Name;

                var temp = hw.Sensors.FirstOrDefault(s => s.SensorType == LhmSensorType.Temperature);
                var load = hw.Sensors.FirstOrDefault(s => s.SensorType == LhmSensorType.Load);

                TxtDGpuTemp.Text = temp?.Value != null
                    ? $"Sıcaklık: {temp.Value:0}°C"
                    : "--";

                TxtDGpuLoad.Text = load?.Value != null
                    ? $"Kullanım: %{load.Value:0}"
                    : "--";
            }
        }

        // =========================
        // MEMORY
        // =========================
        private void ReadMemory()
        {
            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Memory))
            {
                hw.Update();

                var used = hw.Sensors.FirstOrDefault(s => s.Name == "Memory Used");
                var avail = hw.Sensors.FirstOrDefault(s => s.Name == "Memory Available");

                if (used?.Value == null || avail?.Value == null)
                    return;

                double usedGb = used.Value.Value;
                double totalGb = usedGb + avail.Value.Value;

                TxtRamInfo.Text = $"RAM: {usedGb:0.0}/{totalGb:0.0} GB";
            }
        }

        // =========================
        // DISK
        // =========================
        private void ReadDisk()
        {
            try
            {
                var d = new DriveInfo("C");
                if (!d.IsReady) return;

                double total = d.TotalSize / 1073741824.0;
                double used = (d.TotalSize - d.AvailableFreeSpace) / 1073741824.0;

                TxtDiskInfo.Text = $"Disk: {used:0}/{total:0} GB";
            }
            catch { }
        }

        // =========================
        // IGPU
        // =========================
        private void InitIGpuCounter()
        {
            try
            {
                if (!PerformanceCounterCategory.Exists("GPU Engine"))
                    return;

                var category = new PerformanceCounterCategory("GPU Engine");
                var instances = category.GetInstanceNames();

                foreach (var name in instances)
                {
                    if (name.ToLower().Contains("engtype_3d"))
                    {
                        _igpuCounters.Add(
                            new PerformanceCounter("GPU Engine",
                            "Utilization Percentage",
                            name));
                    }
                }

                foreach (var c in _igpuCounters)
                    c.NextValue();
            }
            catch { }
        }

        private void ReadIGpu()
        {
            float total = 0;

            foreach (var counter in _igpuCounters.ToList())
            {
                try
                {
                    total += counter.NextValue();
                }
                catch
                {
                    _igpuCounters.Remove(counter);
                    counter.Dispose();
                }
            }

            total = Math.Min(100, total);
            TxtIGpuLoad.Text = $"iGPU: %{total:0}";
        }

        // =========================
        // CLEANUP
        // =========================
       
    }
}
