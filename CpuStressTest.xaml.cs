using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using HardwareMonitor.data;
using LibreHardwareMonitor.Hardware;

namespace HardwareMonitor
{
    public partial class CpuStressTest : System.Windows.Controls.UserControl
    {
        private Computer _computer;
        private CancellationTokenSource _cts;
        private float _maxTemp = 0, _maxLoad = 0;

        public CpuStressTest()
        {
            InitializeComponent();
            _computer = new Computer { IsCpuEnabled = true };
            _computer.Open();
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            ResetUI();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _ = Task.Run(() => RunCpuLoad(token), token);

            try
            {
                for (int i = 0; i <= 45; i++)
                {
                    if (token.IsCancellationRequested) break;

                    CpuProgressBar.Value = i;
                    TxtCpuTimer.Text = $"{45 - i}s";

                    UpdateSensors();
                    await Task.Delay(1000);
                }
            }
            finally
            {
                StopTest();
            }
        }

        private void UpdateSensors()
        {
            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Cpu))
            {
                hw.Update();
                var temp = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature)?.Value ?? 0;
                var load = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Total"))?.Value ?? 0;

                if (temp > _maxTemp) _maxTemp = temp;
                if (load > _maxLoad) _maxLoad = load;

                TxtCpuTemp.Text = $"{temp:0.0}°C";
                TxtCpuLoad.Text = $"%{load:0}";
                TxtCpuTemp.Foreground = GetColorGradient(temp, 40, 85);
            }
        }

        private void StopTest()
        {
            if (_cts != null && !_cts.IsCancellationRequested) _cts.Cancel();

            Dispatcher.Invoke(() => {
               
                BtnStart.IsEnabled = true;
                BtnStop.IsEnabled = false;
                BtnStart.Visibility = Visibility.Visible;
                BtnStop.Visibility = Visibility.Visible;

                TxtCpuTimer.Text = "45s";
                CpuProgressBar.Value = 0;

                TxtCpuTemp.Text = "--°C";
                TxtCpuLoad.Text = "%--";

                ShowReport();
            });
        }

        private void ShowReport()
        {
            string name = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu)?.Name ?? "CPU";
            try
            {
                using (var db = new PcStatixContext())
                {
                    db.SaveCpuData(name, (int)_maxTemp);
                    string health = db.CpuHealth(db.GetAverageCpuTemp(name), _maxTemp);

                    TxtCpuNameResult.Text = $"Cihaz: {name}";
                    TxtCpuStatsResult.Text = $"Maks Sıcaklık: {_maxTemp:0}°C  |  Maks Yük: %{_maxLoad:0}";
                    TxtCpuHealthResult.Text = health;
                    TxtCpuHealthResult.Foreground = health.Contains("Sağlıklı") ? Brushes.LimeGreen : Brushes.OrangeRed;
                    CpuResultCard.Visibility = Visibility.Visible;
                }
            }
            catch { }
        }

        private void RunCpuLoad(CancellationToken t)
        {
            try
            {
                Parallel.For(0, Environment.ProcessorCount, i => {
                    while (!t.IsCancellationRequested) { Math.Sqrt(Math.Pow(123.45, 67.89)); }
                });
            }
            catch { }
        }

        private void ResetUI()
        {
            CpuResultCard.Visibility = Visibility.Collapsed;

            _maxTemp = 0;
            _maxLoad = 0;

            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;

           
            TxtCpuTemp.Text = "--°C";
            TxtCpuLoad.Text = "%--";
        }

        private SolidColorBrush GetColorGradient(float val, float min, float max)
        {
            float p = Math.Max(0, Math.Min(1, (val - min) / (max - min)));
            return new SolidColorBrush(Color.FromRgb((byte)(255 * p), (byte)(255 * (1 - p)), 0));
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) => StopTest();
    }
}
