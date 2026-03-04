using LibreHardwareMonitor.Hardware;
using SharpDX.Direct3D11;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Dispatching;
using Windows.UI;
using Device = SharpDX.Direct3D11.Device;

namespace HardwareMonitor
{
    public sealed partial class GpuStressTest : Page
    {
        private readonly Computer _computer;
        private CancellationTokenSource _cts;

        private float _maxTemp;
        private float _maxLoad;

        public GpuStressTest()
        {
            InitializeComponent();

            _computer = new Computer
            {
                IsGpuEnabled = true
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

            _ = Task.Run(() => RunD3D11Stress(token), token);

            try
            {
                for (int i = 0; i <= 45; i++)
                {
                    if (token.IsCancellationRequested)
                        break;

                    TestProgressBar.Value = i;
                    TxtTimer.Text = $"{45 - i}s";

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
            var gpu = _computer.Hardware
                .FirstOrDefault(h =>
                    h.HardwareType == HardwareType.GpuNvidia ||
                    h.HardwareType == HardwareType.GpuAmd);

            if (gpu == null)
                return;

            gpu.Update();

            float temp = gpu.Sensors
                .FirstOrDefault(s => s.SensorType == SensorType.Temperature)?.Value ?? 0;

            float load = gpu.Sensors
                .FirstOrDefault(s =>
                    s.SensorType == SensorType.Load &&
                    (s.Name.Contains("Core") ||
                     s.Name.Contains("GPU Load") ||
                     s.Name.Contains("Video")))?.Value ?? 0;

            if (temp > _maxTemp) _maxTemp = temp;
            if (load > _maxLoad) _maxLoad = load;

            TxtCurrentTemp.Text = $"{temp:0.0}°C";
            TxtCurrentLoad.Text = $"%{load:0}";

            float percent = Math.Clamp((temp - 40) / 45f, 0, 1);

            byte r = (byte)(255 * percent);
            byte g = (byte)(255 * (1 - percent));

            TxtCurrentTemp.Foreground =
                new SolidColorBrush(Color.FromArgb(255, r, g, 0));
        }

        #endregion

        #region STOP TEST

        private async void StopTest()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
                _cts.Cancel();

            DispatcherQueue.TryEnqueue(() =>
            {
                BtnStart.IsEnabled = true;
                BtnStop.IsEnabled = false;

                ShowReport();

                TxtCurrentTemp.Text = "--°C";
                TxtCurrentTemp.Foreground =
                    new SolidColorBrush(Colors.White);

                TxtCurrentLoad.Text = "%--";
                TxtTimer.Text = "45s";
                TestProgressBar.Value = 0;
            });

            _cts?.Dispose();
        }

        #endregion

        #region REPORT

        private void ShowReport()
        {
            string gpuName = GetDedicatedGpuName();

            
        }

        #endregion

        #region GPU HELPERS

        private string GetDedicatedGpuName()
        {
            var gpu = _computer.Hardware.FirstOrDefault(h =>
                (h.HardwareType == HardwareType.GpuNvidia ||
                 h.HardwareType == HardwareType.GpuAmd) &&
                !h.Name.ToLower().Contains("intel") &&
                !h.Name.ToLower().Contains("uhd") &&
                !h.Name.ToLower().Contains("graphics"));

            return gpu?.Name ?? "Harici GPU";
        }

        private void RunD3D11Stress(CancellationToken token)
        {
            try
            {
                using (var device =
                    new Device(SharpDX.Direct3D.DriverType.Hardware,
                               DeviceCreationFlags.None))
                {
                    var context = device.ImmediateContext;

                    var bufferDesc = new BufferDescription
                    {
                        SizeInBytes = 128 * 1024 * 1024,
                        Usage = ResourceUsage.Default,
                        BindFlags = BindFlags.None
                    };

                    using var bA = new SharpDX.Direct3D11.Buffer(device, bufferDesc);
                    using var bB = new SharpDX.Direct3D11.Buffer(device, bufferDesc);

                    while (!token.IsCancellationRequested)
                    {
                        for (int i = 0; i < 200; i++)
                            context.CopyResource(bA, bB);

                        context.Flush();
                        Thread.Sleep(1);
                    }
                }
            }
            catch { }
        }

        #endregion

        #region RESET

        private void ResetUI()
        {
            GpuResultCard.Visibility = Visibility.Collapsed;

            _maxTemp = 0;
            _maxLoad = 0;
        }

        protected override void OnNavigatedFrom(
            Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _computer?.Close();

            base.OnNavigatedFrom(e);
        }

        #endregion

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            StopTest();
        }
    }
}
