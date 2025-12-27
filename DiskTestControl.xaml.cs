using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HardwareMonitor
{
    public partial class DiskTestControl : UserControl
    {
        private PerformanceCounter _readCounter;
        private PerformanceCounter _writeCounter;
        private DispatcherTimer _liveTimer;

        public DiskTestControl()
        {
            InitializeComponent();
            InitCounters();

            
            TxtDiskHealth.Text = "Sağlıklı";
            TxtDiskHealth.Foreground = System.Windows.Media.Brushes.Lime;

            _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _liveTimer.Tick += (s, e) => UpdateLiveStats();
            _liveTimer.Start();
        }

        private void InitCounters()
        {
            try
            {
                _readCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
                _writeCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
            }
            catch { }
        }

        private void UpdateLiveStats()
        {
            if (_readCounter == null) return;
            try
            {
                float rs = _readCounter.NextValue() / 1024 / 1024;
                float ws = _writeCounter.NextValue() / 1024 / 1024;
                TxtLiveSpeed.Text = $"Okuma: {rs:0.0} MB/s | Yazma: {ws:0.0} MB/s";
            }
            catch { }
        }

        private async void BtnStartTest_Click(object sender, RoutedEventArgs e)
        {
            // 10 GB Test Dosyası (10240 MB)
            long totalMb = 10240;
            string testFile = Path.Combine(Path.GetTempPath(), "pcstatix_10gb_test.tmp");

            try
            {
                BtnStartTest.IsEnabled = false;
                TxtTestResult.Text = "10 GB Test Hazırlanıyor...";
                PbTestProgress.Value = 0;

                // 1 MB'lık veri bloğu oluştur (Bellek dostu)
                byte[] data = new byte[1024 * 1024];
                new Random().NextBytes(data);

                // YAZMA TESTİ
                TxtTestResult.Text = "Yazma Testi Başladı (10 GB)...";
                var sw = Stopwatch.StartNew();
                using (FileStream fs = new FileStream(testFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    for (int i = 0; i < totalMb; i++)
                    {
                        await fs.WriteAsync(data, 0, data.Length);
                        // İlerleme çubuğunu %0-50 arası güncelle
                        if (i % 100 == 0) PbTestProgress.Value = (i / (double)totalMb) * 50;
                    }
                }
                sw.Stop();
                double writeSpeed = totalMb / sw.Elapsed.TotalSeconds;

                // OKUMA TESTİ
                TxtTestResult.Text = "Okuma Testi Başladı (10 GB)...";
                PbTestProgress.Value = 50;
                sw.Restart();
                using (FileStream fs = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.None, 4096, true))
                {
                    byte[] buffer = new byte[1024 * 1024];
                    for (int i = 0; i < totalMb; i++)
                    {
                        await fs.ReadAsync(buffer, 0, buffer.Length);
                        // İlerleme çubuğunu %50-100 arası güncelle
                        if (i % 100 == 0) PbTestProgress.Value = 50 + ((i / (double)totalMb) * 50);
                    }
                }
                sw.Stop();
                double readSpeed = totalMb / sw.Elapsed.TotalSeconds;

                PbTestProgress.Value = 100;
                TxtTestResult.Text = $"10GB Sonucu -> Yazma: {writeSpeed:0} MB/s | Okuma: {readSpeed:0} MB/s";

                if (File.Exists(testFile)) File.Delete(testFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: Disk alanı yetersiz olabilir.\n" + ex.Message);
            }
            finally
            {
                BtnStartTest.IsEnabled = true;
            }
        }
    }
}