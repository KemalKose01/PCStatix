using LibreHardwareMonitor.Hardware;
// SharpDX Kütüphaneleri
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Vortice.Mathematics;
// İsim çakışmasını önlemek için Device'ı tanımlıyoruz
using Device = SharpDX.Direct3D11.Device;

namespace HardwareMonitor
{
    public partial class GpuStressTest : UserControl
    {
        private Computer _computer;
        private CancellationTokenSource _cts;
        private bool _isRunning = false;
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
            _isRunning = true;
            _cts = new CancellationTokenSource();
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            _temps.Clear(); _loads.Clear();

            // 1. ADIM: DirectX 11 Yükünü Arka Planda Başlat (UI'ı dondurmaz)
            _ = Task.Run(() => RunD3D11Stress(_cts.Token), _cts.Token);

            // 2. ADIM: Sensör ve UI Güncelleme Döngüsü
            try
            {
                for (int i = 0; i <= 60; i++)
                {
                    if (_cts.Token.IsCancellationRequested) break;

                    // UI Güncelleme
                    TestProgressBar.Value = i;
                    TxtTimer.Text = $"{60 - i}s";

                    UpdateSensors();
                    await Task.Delay(1000); // 1 saniye bekle (Donmayı önler)
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            finally { StopTest(true); }
        }

        private void RunD3D11Stress(CancellationToken token)
        {
            try
            {
                // Donanım cihazını oluştur (Hardware zorlaması önemli)
                using (var device = new Device(DriverType.Hardware, DeviceCreationFlags.None))
                {
                    var context = device.ImmediateContext;

                    // GPU'yu yormak için devasa bir Buffer (128 MB) oluşturuyoruz
                    int bufferSize = 128 * 1024 * 1024;
                    var bufferDesc = new BufferDescription()
                    {
                        SizeInBytes = bufferSize,
                        Usage = ResourceUsage.Default,
                        BindFlags = BindFlags.None,
                        CpuAccessFlags = CpuAccessFlags.None,
                        OptionFlags = ResourceOptionFlags.None
                    };

                    using (var bufferA = new SharpDX.Direct3D11.Buffer(device, bufferDesc))
                    using (var bufferB = new SharpDX.Direct3D11.Buffer(device, bufferDesc))
                    {
                        while (!token.IsCancellationRequested)
                        {
                            // RTX 4050'nin bellek yolunu (Bus) ve Copy motorunu meşgul ediyoruz
                            for (int i = 0; i < 200; i++)
                            {
                                // Veriyi sürekli GPU içinde bir yerden bir yere taşıyoruz
                                context.CopyResource(bufferA, bufferB);
                            }

                            // Flush komutu kartın "uykuya dalmasını" engeller
                            context.Flush();

                            // UI'ın (Bar ve Sayı) donmaması için çok kısa bir nefes
                            Thread.Sleep(1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show("GPU SDK Hatası: " + ex.Message));
            }
        }
        private void UpdateSensors()
        {
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.GpuNvidia)
                {
                    hardware.Update();
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            _temps.Add(sensor.Value ?? 0);
                            TxtCurrentTemp.Text = $"{Math.Round(sensor.Value ?? 0, 1)}°C";
                        }
                        if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core"))
                        {
                            _loads.Add(sensor.Value ?? 0);
                            TxtCurrentLoad.Text = $"Yük: %{Math.Round(sensor.Value ?? 0, 1)}";
                        }
                    }
                }
            }
        }

        private void StopTest(bool showReport)
        {
            _isRunning = false;
            _cts?.Cancel();
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;

            if (showReport && _temps.Count > 0)
            {
                MessageBox.Show($"Test Tamamlandı!\nMaksimum Sıcaklık: {_temps.Max()}°C\nMaksimum Yük: %{_loads.Max()}", "Rapor");
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) => StopTest(false);
    }
}
