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
            var token = _cts.Token;

            
            _ = Task.Run(() => RunD3D11Stress(token), token);

            try
            {
               
                for (int i = 0; i <= 45; i++)
                {
                    if (token.IsCancellationRequested) break;

                    TestProgressBar.Value = i;
                    TxtTimer.Text = $"{45 - i}s";
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
         
            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd))
            {
                hw.Update();
                var temp = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature)?.Value ?? 0;

              
                var loadSensor = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load &&
                                 (s.Name.Contains("Core") || s.Name.Contains("GPU Load") || s.Name.Contains("Video")));
                var load = loadSensor?.Value ?? 0;

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
            if (_cts != null && !_cts.IsCancellationRequested) _cts.Cancel();

            Dispatcher.Invoke(() => {
                BtnStart.IsEnabled = true;
                BtnStop.IsEnabled = false;
                BtnStart.Visibility = Visibility.Visible;
                BtnStop.Visibility = Visibility.Visible;

                ShowReport();

              
                TxtCurrentTemp.Text = "--°C";
                TxtCurrentTemp.Foreground = Brushes.White;
                TxtCurrentLoad.Text = "%--";
                TxtTimer.Text = "45s";
                TestProgressBar.Value = 0;
            });
        }

        private void ShowReport()
        {
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
            catch { }
        }

        private void ResetUI()
        {
            GpuResultCard.Visibility = Visibility.Collapsed;
            _maxTemp = 0;
            _maxLoad = 0;
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
        }

        private string GetDedicatedGpuName()
        {
            var gpu = _computer.Hardware.FirstOrDefault(h => (h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd)
                      && !h.Name.ToLower().Contains("uhd") && !h.Name.ToLower().Contains("graphics") && !h.Name.ToLower().Contains("intel"));
            return gpu?.Name ?? "Harici GPU";
        }

        
        private SharpDX.DXGI.Adapter GetDedicatedAdapter()
        {
            using (var factory = new SharpDX.DXGI.Factory1())
            {
                foreach (var adapter in factory.Adapters)
                {
                    string name = adapter.Description.Description.ToLower();
                    if (!name.Contains("intel") && !name.Contains("uhd") && !name.Contains("graphics"))
                    {
                        return adapter;
                    }
                }
                return factory.Adapters.FirstOrDefault();
            }
        }

        private void RunD3D11Stress(CancellationToken token)
        {
            try
            {
                var targetAdapter = GetDedicatedAdapter();
              
                using (var device = new Device(targetAdapter, DeviceCreationFlags.None))
                {
                    var context = device.ImmediateContext;
                   
                    var bufferDesc = new BufferDescription
                    {
                        SizeInBytes = 128 * 1024 * 1024,
                        Usage = ResourceUsage.Default,
                        BindFlags = BindFlags.None
                    };

                    using (var bA = new SharpDX.Direct3D11.Buffer(device, bufferDesc))
                    using (var bB = new SharpDX.Direct3D11.Buffer(device, bufferDesc))
                    {
                        while (!token.IsCancellationRequested)
                        {
                            
                            for (int i = 0; i < 200; i++) context.CopyResource(bA, bB);
                            context.Flush();
                            Thread.Sleep(1);
                        }
                    }
                }
            }
            catch { }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) => StopTest();
    }
}
