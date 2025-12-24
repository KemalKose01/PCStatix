using LibreHardwareMonitor.Hardware;
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
        private PerformanceCounter? _igpuCounter;
        private List<PerformanceCounter> _igpuCounters = new();
        public MainWindow()
        {
            InitializeComponent();

            _computer = new Computer
            {
                
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true
               

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

        // ---------------- iGPU (Task Manager mantığı) ----------------
        private void InitIGpuCounter()
        {
            try
            {
                var category = new PerformanceCounterCategory("GPU Engine");
                var instances = category.GetInstanceNames();

                _igpuCounters.Clear();

                foreach (var name in instances)
                {
                    // Sadece 3D yükünü al
                    if (name.ToLower().Contains("engtype_3d"))
                    {
                        _igpuCounters.Add(new PerformanceCounter(
                            "GPU Engine",
                            "Utilization Percentage",
                            name));
                    }
                }

                // Counter'ı ısıt (ilk okuma her zaman 0 gelir)
                foreach (var c in _igpuCounters)
                    c.NextValue();

                TxtIGpuName.Text = "Integrated GPU";
            }
            catch
            {
                TxtIGpuName.Text = "iGPU bulunamadı";
            }
        }








        // ---------------- Refresh ----------------
        private void RefreshAll()
        {
            ReadCpu();
            ReadDGpu();
            ReadMemory();
            ReadDisk();
            ReadIGpu();
        }

        // ---------------- CPU ----------------
        private void ReadCpu()
        {
            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Cpu))
            {
                hw.Update();
                TxtCpuName.Text = hw.Name;

                var cores = new List<CpuCoreModel>();

                // Clock sensörleri
                var clocks = hw.Sensors
                    .Where(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core"))
                    .OrderBy(s => s.Name)
                    .ToList();

                // Load sensörleri
                var loads = hw.Sensors
                    .Where(s => s.SensorType == SensorType.Load && s.Name.Contains("Core"))
                    .OrderBy(s => s.Name)
                    .ToList();

                for (int i = 0; i < clocks.Count; i++)
                {
                    var c = clocks[i];
                    var l = loads.Count > i ? loads[i] : null;

                    cores.Add(new CpuCoreModel
                    {
                        Name = c.Name, // Core #1 gibi
                        Load = l?.Value != null ? $"%{l.Value:0}" : "%--",
                        Clock = c.Value != null ? $"{c.Value:0} MHz" : "--"
                    });
                }

                IcCpuCores.ItemsSource = cores;

                // Paket sıcaklığı
                var pkgTemp = hw.Sensors.FirstOrDefault(s =>
                    s.SensorType == SensorType.Temperature &&
                    (s.Name.Contains("Package") || s.Name.Contains("Tctl")));

                TxtCpuTemp.Text = pkgTemp?.Value != null
                    ? $"CPU Sıcaklığı: {pkgTemp.Value:0}°C"
                    : "CPU Sıcaklığı: --";
            }
        }


        // ---------------- dGPU ----------------
        private void ReadDGpu()
        {
            foreach (var hw in _computer.Hardware.Where(h =>
                     h.HardwareType == HardwareType.GpuNvidia ||
                     h.HardwareType == HardwareType.GpuAmd))
            {
                hw.Update();
                TxtDGpuName.Text = hw.Name;

                var temp = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
                var load = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load);

                TxtDGpuTemp.Text = temp?.Value != null ? $"Sıcaklık: {temp.Value:0}°C" : "Sıcaklık: --";
                TxtDGpuLoad.Text = load?.Value != null ? $"Kullanım: %{load.Value:0}" : "Kullanım: --";
            }
        }

        // ---------------- iGPU ----------------
        private void ReadIGpu()
        {
            if (_igpuCounters.Count == 0)
            {
                TxtIGpuLoad.Text = "Kullanım: %0";
                return;
            }

            float total = 0;

            foreach (var c in _igpuCounters)
                total += c.NextValue();

            if (total > 100)
                total = 100;

            TxtIGpuLoad.Text = $"Kullanım: %{total:0}";
        }




        // ---------------- RAM ----------------
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

        // ---------------- Disk ----------------
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
