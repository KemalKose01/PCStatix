using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace HardwareMonitor
{
    public class CpuCoreModel
    {
        public string Name { get; set; } = "";
        public string Load { get; set; } = "";
        public string Clock { get; set; } = "";
    }

    public partial class MainWindow : Window
    {
        private readonly Computer _computer;
        private readonly DispatcherTimer _timer;
        private List<PerformanceCounter> _igpuCounters = new();

        public MainWindow()
        {
            InitializeComponent();
            _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = true };
            _computer.Open();
            InitIGpuCounter();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => RefreshAll();
            _timer.Start();
        }

        private void InitIGpuCounter()
        {
            try
            {
                var category = new PerformanceCounterCategory("GPU Engine");
                var instances = category.GetInstanceNames();
                foreach (var name in instances.Where(n => n.ToLower().Contains("engtype_3d")))
                    _igpuCounters.Add(new PerformanceCounter("GPU Engine", "Utilization Percentage", name));
                foreach (var c in _igpuCounters) c.NextValue();
            }
            catch { TxtIGpuName.Text = "iGPU Monitoring Not Available"; }
        }

        private void RefreshAll() { ReadCpu(); ReadDGpu(); ReadMemory(); ReadDisk(); ReadIGpu(); }

        private void ReadCpu()
        {
            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Cpu))
            {
                hw.Update();
                TxtCpuName.Text = hw.Name;

                var clocks = hw.Sensors.Where(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core")).OrderBy(s => s.Name).ToList();
                var loads = hw.Sensors.Where(s => s.SensorType == SensorType.Load && s.Name.Contains("Core")).OrderBy(s => s.Name).ToList();

                var cores = new List<CpuCoreModel>();

                for (int i = 0; i < clocks.Count; i++)
                {
                   
                    cores.Add(new CpuCoreModel
                    {
                        Name = $"Çekirdek {i + 1}",
                        Load = loads.Count > i ? $"%{loads[i].Value:0}" : "%--",
                        Clock = $"{clocks[i].Value:0} MHz"
                    });
                }

                IcCpuCores.ItemsSource = cores;

                var pkgTemp = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && (s.Name.Contains("Package") || s.Name.Contains("Tctl")));
                TxtCpuTemp.Text = pkgTemp?.Value != null ? $"Sıcaklık: {pkgTemp.Value:0}°C" : "Sıcaklık: --";
            }
        }

        private void ReadDGpu()
        {
            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd))
            {
                hw.Update();
                TxtDGpuName.Text = hw.Name;

                var temp = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
                var load = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("GPU Core"));
                var clock = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Clock && s.Name.Contains("GPU Core"));
                var pwr = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name.Contains("GPU Package"));
                var vramUsed = hw.Sensors.FirstOrDefault(s => s.Name == "GPU Memory Used");
                var vramTotal = hw.Sensors.FirstOrDefault(s => s.Name == "GPU Memory Total");

                TxtDGpuTemp.Text = temp?.Value != null ? $"Sıcaklık: {temp.Value:0}°C" : "Sıcaklık: --";
                TxtDGpuLoad.Text = load?.Value != null ? $"Kullanım: %{load.Value:0}" : "Kullanım: --";
                TxtDGpuClock.Text = clock?.Value != null ? $"Frekans: {clock.Value:0} MHz" : "Frekans: --";
                TxtDGpuPower.Text = pwr?.Value != null ? $"Tüketim: {pwr.Value:0.0} Watt" : "Tüketim: --";

                if (vramUsed != null && vramTotal != null)
                    TxtDGpuVram.Text = $"VRAM: {vramUsed.Value / 1024:0.0} / {vramTotal.Value / 1024:0.0} GB";
            }
        }

        private void ReadIGpu()
        {
            float total = 0;
            foreach (var c in _igpuCounters) total += c.NextValue();
            TxtIGpuLoad.Text = $"Kullanım: %{(total > 100 ? 100 : total):0}";
        }

        private void ReadMemory()
        {
            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Memory))
            {
                hw.Update();
                var used = hw.Sensors.FirstOrDefault(s => s.Name == "Memory Used");
                var avail = hw.Sensors.FirstOrDefault(s => s.Name == "Memory Available");

                if (used?.Value != null && avail?.Value != null)
                {
                    // Değerleri baştan double olarak alıyoruz
                    double usedVal = (double)used.Value.Value;
                    double availVal = (double)avail.Value.Value;
                    double totalVal = usedVal + availVal;

                    TxtRamInfo.Text = $"RAM: {usedVal:0.0} / {totalVal:0.0} GB";

                    // Hata veren satırın düzeltilmiş hali:
                    PbRam.Value = (usedVal / totalVal) * 100.0;
                }
            }
        }

        private void ReadDisk()
        {
            try
            {
                var d = new DriveInfo("C");
                double u = (d.TotalSize - d.AvailableFreeSpace) / 1073741824.0, t = d.TotalSize / 1073741824.0;
                TxtDiskInfo.Text = $"Sürücü (C:): {u:0} / {t:0} GB";
                PbDisk.Value = (u / t) * 100;
            }
            catch { }
        }

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
