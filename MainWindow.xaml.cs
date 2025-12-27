using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace HardwareMonitor
{
    // Çakışmayı önlemek için ismini değiştirdik: CpuCoreInfo
    public class CpuCoreInfo
    {
        public string Name { get; set; } = "";
        public string Load { get; set; } = "";
        public string Clock { get; set; } = "";
    }

    public partial class MainWindow : Window
    {
        // Burada sadece BİR tane _computer tanımlı.
        private readonly Computer _computer;
        private readonly DispatcherTimer _timer;
        private List<PerformanceCounter> _igpuCounters = new();

        public MainWindow()
        {
            InitializeComponent();

            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true
            };
            _computer.Open();

            InitIGpuCounter();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (_, _) => RefreshAll();
            _timer.Start();
        }

        // --- PENCERE BUTONLARI ---
        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2) Maximize_Click(sender, e);
                else DragMove();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        // --- VERİ OKUMA ---
        private void RefreshAll()
        {
            ReadCpu();
            ReadDGpu();
            ReadMemory();
            ReadDisk();
            ReadIGpu();
        }

        private void InitIGpuCounter()
        {
            try
            {
                var category = new PerformanceCounterCategory("GPU Engine");
                var instances = category.GetInstanceNames();
                _igpuCounters.Clear();
                foreach (var name in instances)
                {
                    if (name.ToLower().Contains("engtype_3d"))
                    {
                        _igpuCounters.Add(new PerformanceCounter("GPU Engine", "Utilization Percentage", name));
                    }
                }
                foreach (var c in _igpuCounters) c.NextValue();
                TxtIGpuName.Text = "Integrated GPU";
            }
            catch { TxtIGpuName.Text = "iGPU (Yok/Hata)"; }
        }

        private void ReadCpu()
        {
            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Cpu))
            {
                hw.Update();
                TxtCpuName.Text = hw.Name;

                var cores = new List<CpuCoreInfo>(); // Güncellendi

                var clocks = hw.Sensors.Where(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core")).OrderBy(s => s.Name.Length).ThenBy(s => s.Name).ToList();
                var loads = hw.Sensors.Where(s => s.SensorType == SensorType.Load && s.Name.Contains("Core")).OrderBy(s => s.Name.Length).ThenBy(s => s.Name).ToList();

                for (int i = 0; i < clocks.Count; i++)
                {
                    var c = clocks[i];
                    var l = loads.Count > i ? loads[i] : null;
                    cores.Add(new CpuCoreInfo // Güncellendi
                    {
                        Name = c.Name,
                        Load = l?.Value != null ? $"%{l.Value:0}" : "%--",
                        Clock = c.Value != null ? $"{c.Value:0} MHz" : "--"
                    });
                }
                IcCpuCores.ItemsSource = cores;

                var pkgTemp = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && (s.Name.Contains("Package") || s.Name.Contains("Tctl")));
                TxtCpuTemp.Text = pkgTemp?.Value != null ? $"CPU Sıcaklığı: {pkgTemp.Value:0}°C" : "CPU Sıcaklığı: --";
            }
        }

        private void ReadDGpu()
        {
            bool gpuFound = false;
            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd))
            {
                gpuFound = true;
                hw.Update();
                TxtDGpuName.Text = hw.Name;
                var temp = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
                var load = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load);
                TxtDGpuTemp.Text = temp?.Value != null ? $"Sıcaklık: {temp.Value:0}°C" : "Sıcaklık: --";
                TxtDGpuLoad.Text = load?.Value != null ? $"Kullanım: %{load.Value:0}" : "Kullanım: --";
            }
            if (!gpuFound) TxtDGpuName.Text = "Harici GPU Bulunamadı";
        }

        private void ReadIGpu()
        {
            if (_igpuCounters.Count == 0) { TxtIGpuLoad.Text = "Kullanım: %0"; return; }
            float total = 0;
            foreach (var c in _igpuCounters) total += c.NextValue();
            if (total > 100) total = 100;
            TxtIGpuLoad.Text = $"Kullanım: %{total:0}";
        }

        private void ReadMemory()
        {
            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Memory))
            {
                hw.Update();
                var used = hw.Sensors.FirstOrDefault(s => s.Name == "Memory Used");
                var avail = hw.Sensors.FirstOrDefault(s => s.Name == "Memory Available");
                if (used?.Value == null || avail?.Value == null) return;
                double usedGb = used.Value.Value;
                double totalGb = usedGb + avail.Value.Value;
                TxtRamInfo.Text = $"RAM: {usedGb:0.0} GB / {totalGb:0.0} GB";
                PbRam.Value = (usedGb / totalGb) * 100;
            }
        }

        private void ReadDisk()
        {
            try
            {
                var d = new DriveInfo("C");
                if (!d.IsReady) return;
                double total = d.TotalSize / 1073741824.0;
                double used = (d.TotalSize - d.AvailableFreeSpace) / 1073741824.0;
                TxtDiskInfo.Text = $"Sürücü (C:): {used:0} GB / {total:0} GB";
                PbDisk.Value = (used / total) * 100;
            }
            catch { }
        }
    }
}
