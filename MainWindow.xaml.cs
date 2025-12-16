using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using System.Linq;

namespace DonanimBilgiWPF
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        DispatcherTimer timer;
        PerformanceCounter cpuCounter;
        PerformanceCounter[] gpuCounters;
        Random rnd = new Random();

        double cpuUsage, gpuUsage, ramUsage;

        public string CpuUsageText => $"%{(int)cpuUsage}";
        public double CpuBarWidth => cpuUsage * 3;

        public string GpuUsageText => $"%{(int)gpuUsage}";
        public double GpuBarWidth => gpuUsage * 3;

        public string RamUsageText => $"%{(int)ramUsage}";
        public double RamBarWidth => ramUsage * 3;

        // 🔥 SICAKLIKLAR (Simülasyon – görünür ve hatasız)
        public string CpuTempText => $"CPU: {45 + (int)(cpuUsage / 4)} °C";
        public string GpuTempText => $"GPU: {50 + (int)(gpuUsage / 3)} °C";
        public string RamTempText => $"RAM: {35 + rnd.Next(0, 5)} °C";


        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

            gpuCounters = new PerformanceCounterCategory("GPU Engine")
                .GetInstanceNames()
                .Where(n => n.EndsWith("engtype_3D"))
                .Select(n => new PerformanceCounter("GPU Engine", "Utilization Percentage", n))
                .ToArray();

            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += Timer_Tick;
            timer.Start();
            
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            cpuUsage = cpuCounter.NextValue();
            gpuUsage = gpuCounters.Length > 0 ? gpuCounters.Sum(c => c.NextValue()) : 0;
            ramUsage = GetRamUsage();

            OnPropertyChanged(nameof(CpuUsageText));
            OnPropertyChanged(nameof(CpuBarWidth));
            OnPropertyChanged(nameof(GpuUsageText));
            OnPropertyChanged(nameof(GpuBarWidth));
            OnPropertyChanged(nameof(RamUsageText));
            OnPropertyChanged(nameof(RamBarWidth));

            OnPropertyChanged(nameof(CpuTempText));
            OnPropertyChanged(nameof(GpuTempText));
            OnPropertyChanged(nameof(RamTempText));
        }

        private double GetRamUsage()
        {
            var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");

            foreach (ManagementObject obj in searcher.Get())
            {
                double total = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                double free = Convert.ToDouble(obj["FreePhysicalMemory"]);
                return ((total - free) / total) * 100;
            }
            return 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
