using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LibreHardwareMonitor.Hardware;

namespace HardwareMonitor
{
    public partial class CpuStressTest : UserControl
    {
        private Computer _computer;
        private CancellationTokenSource _cts = new CancellationTokenSource(); // Initialize _cts to avoid nullability issues  
        private bool _isRunning = false;

        public CpuStressTest()
        {
            InitializeComponent();
            _computer = new Computer { IsCpuEnabled = true };
            _computer.Open();
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            _isRunning = true;
            _cts = new CancellationTokenSource();
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;

            // Arka planda CPU'yu zorla  
            _ = Task.Run(() => RunCpuLoad(_cts.Token), _cts.Token);

            // UI Güncelleme Döngüsü  
            try
            {
                for (int i = 0; i <= 60; i++)
                {
                    if (_cts.Token.IsCancellationRequested) break;
                    CpuProgressBar.Value = i;
                    TxtCpuTimer.Text = $"Kalan Süre: {60 - i}s";
                    UpdateCpuSensors();
                    await Task.Delay(1000);
                }
            }
            finally { StopTest(); }
        }

        private void RunCpuLoad(CancellationToken token)
        {
            // Tüm çekirdekleri (Logical Processors) kullan  
            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            try
            {
                Parallel.For(0, Environment.ProcessorCount, options, i =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        // Matematiksel yoğunluk oluştur (Isınmayı sağlar)  
                        double val = Math.Sqrt(Math.Pow(123.45, 67.89));
                    }
                });
            }
            catch (OperationCanceledException) { }
        }

        private void UpdateCpuSensors()
        {
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    hardware.Update();
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Package"))
                            Dispatcher.Invoke(() => TxtCpuTemp.Text = $"{sensor.Value:0.0}°C");
                        if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Total"))
                            Dispatcher.Invoke(() => TxtCpuLoad.Text = $"Yük: %{sensor.Value:0.0}");
                    }
                }
            }
        }

        private void StopTest()
        {
            _isRunning = false;
            _cts?.Cancel();
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) => StopTest();
    }
}
