using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LibreHardwareMonitor.Hardware;

namespace HardwareMonitor
{
    public partial class CpuStressTest : UserControl
    {
        private Computer _computer;
        private CancellationTokenSource? _cts; // Nullable yapıldı (CS8618 uyarısı için)
        private float _maxTemp = 0;
        private float _maxLoad = 0;

        public CpuStressTest()
        {
            InitializeComponent();
            _computer = new Computer { IsCpuEnabled = true };
            _computer.Open();
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            _maxTemp = 0;
            _maxLoad = 0;
            _cts = new CancellationTokenSource();

            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            CpuResultCard.Visibility = Visibility.Collapsed;
            CpuProgressBar.Foreground = new SolidColorBrush(Color.FromRgb(243, 156, 18));
            CpuProgressBar.Value = 0;

            // Arka planda CPU yükü oluştur
            _ = Task.Run(() => RunCpuLoad(_cts.Token), _cts.Token);

            try
            {
                for (int i = 0; i <= 20; i++)
                {
                    if (_cts.Token.IsCancellationRequested) break;

                    CpuProgressBar.Value = i;
                    TxtCpuTimer.Text = $"Kalan Süre: {20 - i}s";
                    UpdateCpuSensors();
                    await Task.Delay(1000);
                }
            }
            catch (Exception) { /* Hata yönetimi */ }
            finally
            {
                StopTest();
            }
        }

        private void RunCpuLoad(CancellationToken token)
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            try
            {
                Parallel.For(0, Environment.ProcessorCount, options, i => {
                    while (!token.IsCancellationRequested)
                    {
                        // İşlemciyi meşgul edecek matematiksel işlem
                        Math.Sqrt(Math.Pow(123.45, 67.89));
                    }
                });
            }
            catch (OperationCanceledException) { }
        }

        private void UpdateCpuSensors()
        {
            foreach (var hardware in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Cpu))
            {
                hardware.Update();

                var tempSensor = hardware.Sensors.FirstOrDefault(s =>
                    s.SensorType == SensorType.Temperature &&
                    (s.Name.Contains("Package") || s.Name.Contains("Tctl") || s.Name.Contains("Core (Max)")));

                var loadSensor = hardware.Sensors.FirstOrDefault(s =>
                    s.SensorType == SensorType.Load && s.Name.Contains("Total"));

                Dispatcher.Invoke(() => {
                    if (tempSensor?.Value != null)
                    {
                        float currentTemp = tempSensor.Value.Value;
                        if (currentTemp > _maxTemp) _maxTemp = currentTemp;
                        TxtCpuTemp.Text = $"{currentTemp:0.0}°C";
                    }
                    if (loadSensor?.Value != null)
                    {
                        float currentLoad = loadSensor.Value.Value;
                        if (currentLoad > _maxLoad) _maxLoad = currentLoad;
                        TxtCpuLoad.Text = $"%{currentLoad:0}";
                    }
                });
            }
        }

        private void StopTest()
        {
            if (_cts == null || _cts.IsCancellationRequested) return;

            _cts.Cancel();
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;

            Dispatcher.Invoke(() => {
                CpuResultCard.Visibility = Visibility.Visible;
                TxtCpuFinalResult.Text = $"Maks Sıcaklık: {_maxTemp:0.0}°C | Maks Kullanım Oranı: %{_maxLoad:0}";
                CpuProgressBar.Foreground = Brushes.Lime;
                TxtCpuTimer.Text = "Test Tamamlandı";
            });
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) => StopTest();
    }
}
