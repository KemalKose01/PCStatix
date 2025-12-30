using HardwareMonitor.data;
using LibreHardwareMonitor.Hardware;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Device = SharpDX.Direct3D11.Device;

namespace HardwareMonitor
{
    public partial class GpuStressTest : System.Windows.Controls.UserControl
    {
        private Computer _computer;
        private CancellationTokenSource _cts;
        private float _maxTemp = 0, _maxLoad = 0;

        public GpuStressTest()
        {
            InitializeComponent();
            _computer = new Computer { IsGpuEnabled = true };
            _computer.Open();
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            ResetUI();
            _cts = new CancellationTokenSource();

          
            _ = Task.Run(() => RunD3D11Stress(_cts.Token), _cts.Token);

            try
            {
                for (int i = 0; i <= 60; i++)
                {
                    if (_cts.Token.IsCancellationRequested) break;
                    TestProgressBar.Value = i;
                    TxtTimer.Text = $"{60 - i}s";
                    UpdateSensors();
                    await Task.Delay(1000);
                }
            }
            finally { StopTest(); }
        }

        private void UpdateSensors()
        {
            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd))
            {
                hw.Update();
                var temp = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature)?.Value ?? 0;
                var load = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Core"))?.Value ?? 0;

                if (temp > _maxTemp) _maxTemp = temp;
                if (load > _maxLoad) _maxLoad = load;

                TxtCurrentTemp.Text = $"{temp:0.0}°C";
                TxtCurrentLoad.Text = $"%{load:0}";

               
                float p = Math.Max(0, Math.Min(1, (temp - 40) / 45));
                TxtCurrentTemp.Foreground = new SolidColorBrush(Color.FromRgb((byte)(255 * p), (byte)(255 * (1 - p)), 0));
            }
        }

        private void StopTest()
        {
            _cts?.Cancel();
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;

            string gpuName = GetDedicatedGpuName();

            try
            {
                using (var db = new PcStatixContext())
                {
                    db.SaveGpuData(gpuName, (int)_maxTemp);
                    string health = db.GpuHealth(db.GetAverageGpuTemp(gpuName), _maxTemp);

                  
                    TxtGpuNameResult.Text = $"Ekran Kartı: {gpuName}";
                    TxtGpuStatsResult.Text = $"Maks Isı: {_maxTemp:0.0}°C | Maks Yük: %{_maxLoad:0}";
                    TxtGpuHealthResult.Text = health;
                    TxtGpuHealthResult.Foreground = health.Contains("Sağlıklı") ? Brushes.LimeGreen : Brushes.OrangeRed;
                    GpuResultCard.Visibility = Visibility.Visible;
                }
            }
            catch {}

           
            TxtCurrentTemp.Text = "--°C";
            TxtCurrentTemp.Foreground = Brushes.White;
            TxtCurrentLoad.Text = "%--";
            TxtTimer.Text = "60s";
            TestProgressBar.Value = 0;
        }

        private void ResetUI()
        {
            GpuResultCard.Visibility = Visibility.Collapsed;
            _maxTemp = 0; _maxLoad = 0;
            BtnStart.IsEnabled = false; BtnStop.IsEnabled = true;
        }

        private string GetDedicatedGpuName()
        {
            var gpu = _computer.Hardware.FirstOrDefault(h => (h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd)
                      && !h.Name.ToLower().Contains("uhd") && !h.Name.ToLower().Contains("graphics"));
            return gpu?.Name ?? "Harici GPU";
        }

        private void RunD3D11Stress(CancellationToken token)
        {
            try
            {
                using (var device = new Device(DriverType.Hardware, DeviceCreationFlags.None))
                {
                    var context = device.ImmediateContext;
                    var bufferDesc = new BufferDescription { SizeInBytes = 64 * 1024 * 1024, Usage = ResourceUsage.Default, BindFlags = BindFlags.None };
                    using (var bA = new SharpDX.Direct3D11.Buffer(device, bufferDesc))
                    using (var bB = new SharpDX.Direct3D11.Buffer(device, bufferDesc))
                    {
                        while (!token.IsCancellationRequested)
                        {
                            for (int i = 0; i < 100; i++) context.CopyResource(bA, bB);
                            context.Flush();
                            Thread.Sleep(10);
                        }
                    }
                }
            }
            catch { }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) => StopTest();
    }
}
