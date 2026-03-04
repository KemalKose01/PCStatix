using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;
using Microsoft.UI;
using Windows.UI;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Dispatching;

namespace HardwareMonitor
{
    public sealed partial class CpuStressTest : Page
    {
        private readonly Computer _computer;
        private CancellationTokenSource _cts;

        private float _maxTemp;
        private float _maxLoad;

        public CpuStressTest()
        {
            InitializeComponent();

            _computer = new Computer
            {
                IsCpuEnabled = true
            };

            _computer.Open();
        }

        #region START TEST

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            ResetUI();

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;

            // CPU yükünü arka planda başlat
            _ = Task.Run(() => RunCpuLoad(token), token);

            try
            {
                for (int i = 0; i <= 45; i++)
                {
                    if (token.IsCancellationRequested)
                        break;

                    CpuProgressBar.Value = i;
                    TxtCpuTimer.Text = $"{45 - i}s";

                    UpdateSensors();

                    await Task.Delay(1000, token);
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                StopTest();
            }
        }

        #endregion

        #region SENSOR UPDATE

        private void UpdateSensors()
        {
            var cpu = _computer.Hardware
                .FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);

            if (cpu == null)
                return;

            cpu.Update();

            float temp = cpu.Sensors
                .FirstOrDefault(s => s.SensorType == SensorType.Temperature)?.Value ?? 0;

            float load = cpu.Sensors
                .FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Total"))?.Value ?? 0;

            if (temp > _maxTemp) _maxTemp = temp;
            if (load > _maxLoad) _maxLoad = load;

            TxtCpuTemp.Text = $"{temp:0.0}°C";
            TxtCpuLoad.Text = $"%{load:0}";

            TxtCpuTemp.Foreground = GetColorGradient(temp, 40, 85);
        }

        #endregion

        #region STOP TEST

        private void StopTest()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
                _cts.Cancel();
        }

        #endregion



        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            StopTest();
        }

        #region REPORT

        private void ShowReport()
        {
            string cpuName = _computer.Hardware
                .FirstOrDefault(h => h.HardwareType == HardwareType.Cpu)?.Name ?? "CPU";

            TxtCpuNameResult.Text = $"Cihaz: {cpuName}";
            TxtCpuStatsResult.Text =
                $"Maks Sıcaklık: {_maxTemp:0}°C  |  Maks Yük: %{_maxLoad:0}";

            string health = EvaluateHealth(_maxTemp);

            TxtCpuHealthResult.Text = health;
            TxtCpuHealthResult.Foreground =
                new SolidColorBrush(
                    health.Contains("Sağlıklı")
                    ? Colors.LimeGreen
                    : Colors.OrangeRed
                );

            CpuResultCard.Visibility = Visibility.Visible;
        }

        private string EvaluateHealth(float temp)
        {
            if (temp < 75)
                return "Sağlıklı";

            if (temp < 90)
                return "Sınırda";

            return "Yüksek Sıcaklık!";
        }

        #endregion

        #region CPU LOAD

        private void RunCpuLoad(CancellationToken token)
        {
            try
            {
                Parallel.For(0, Environment.ProcessorCount, i =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        _ = 123.45 * 678.90;
                    }
                });
            }
            catch { }
        }

        #endregion

        #region UI RESET

        private void ResetUI()
        {
            CpuResultCard.Visibility = Visibility.Collapsed;

            _maxTemp = 0;
            _maxLoad = 0;

            TxtCpuTemp.Text = "--°C";
            TxtCpuLoad.Text = "%--";
        }

        private void FinalizeTest()
        {
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;

            TxtCpuTimer.Text = "45s";
            CpuProgressBar.Value = 0;

            ShowReport();

            _cts?.Dispose();
        }

        #endregion

        #region COLOR

        private SolidColorBrush GetColorGradient(float value, float min, float max)
        {
            float percent = Math.Clamp((value - min) / (max - min), 0, 1);

            byte r = (byte)(255 * percent);
            byte g = (byte)(255 * (1 - percent));

            return new SolidColorBrush(
    Color.FromArgb(255, r, g, 0)
);
        }

        #endregion

        #region CLEANUP

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _computer?.Close();

            base.OnNavigatedFrom(e);
        }

        #endregion
    }
}
