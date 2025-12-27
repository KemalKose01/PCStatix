using LibreHardwareMonitor.Hardware;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Device = SharpDX.Direct3D11.Device;

namespace HardwareMonitor
{
    public partial class GpuStressTest : UserControl
    {
        private Computer _computer;
        private CancellationTokenSource _cts;
        private List<float> _temps = new List<float>();
        private List<float> _loads = new List<float>();

        public GpuStressTest()
        {
            InitializeComponent();
            _computer = new Computer { IsGpuEnabled = true };
            _computer.Open();
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();

            // UI ve Veri Hazırlığı
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            GpuResultCard.Visibility = Visibility.Collapsed;
            _temps.Clear();
            _loads.Clear();

            // Arka planda DirectX yükünü başlat
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
            catch { }
            finally
            {
                StopTest();
            }
        }

        private void RunD3D11Stress(CancellationToken token)
        {
            try
            {
                using (var device = new Device(DriverType.Hardware, DeviceCreationFlags.None))
                {
                    var context = device.ImmediateContext;
                    int bufferSize = 128 * 1024 * 1024;
                    var bufferDesc = new BufferDescription()
                    {
                        SizeInBytes = bufferSize,
                        Usage = ResourceUsage.Default,
                        BindFlags = BindFlags.None
                    };

                    using (var bufferA = new SharpDX.Direct3D11.Buffer(device, bufferDesc))
                    using (var bufferB = new SharpDX.Direct3D11.Buffer(device, bufferDesc))
                    {
                        while (!token.IsCancellationRequested)
                        {
                            for (int i = 0; i < 200; i++) context.CopyResource(bufferA, bufferB);
                            context.Flush();
                            Thread.Sleep(1);
                        }
                    }
                }
            }
            catch { /* Hatalar sessizce geçilebilir */ }
        }

        private void UpdateSensors()
        {
            foreach (var hardware in _computer.Hardware.Where(h => h.HardwareType == HardwareType.GpuNvidia))
            {
                hardware.Update();
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Temperature)
                    {
                        float val = sensor.Value ?? 0;
                        _temps.Add(val);
                        TxtCurrentTemp.Text = $"{Math.Round(val, 1)}°C";
                    }
                    if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core"))
                    {
                        float val = sensor.Value ?? 0;
                        _loads.Add(val);
                        TxtCurrentLoad.Text = $"Yük: %{Math.Round(val, 1)}";
                    }
                }
            }
        }

        private void StopTest()
        {
            _cts?.Cancel();
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;

            // Sonuçları Panelde Göster
            if (_temps.Count > 0 && _loads.Count > 0)
            {
                float maxT = _temps.Max();
                float maxL = _loads.Max();

                GpuResultCard.Visibility = Visibility.Visible;
                TxtGpuFinalResult.Text = $"Max Sıcaklık: {maxT:0.0}°C | Max Kullanım Oranı: %{maxL:0}";
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) => StopTest();
    }
}
