using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HardwareMonitor
{
    public partial class DiskStressTest : UserControl
    {
        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private readonly string _testFilePath;

        public DiskStressTest()
        {
            InitializeComponent();
          
            _testFilePath = Path.Combine(Path.GetTempPath(), "PcStatix_10GB_Test.dat");
        }

        private async void BtnStartTest_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();

         
            BtnStartTest.IsEnabled = false;
            BtnStartTest.Content = "TEST SÜRÜYOR...";
            PbTestProgress.Value = 0;

            TxtFinalWrite.Text = "-- MB/s";
            TxtFinalRead.Text = "-- MB/s";

            try
            {
                await RunRealDiskTest(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Test kullanıcı tarafından iptal edildi.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Test sırasında bir hata oluştu: " + ex.Message);
            }
            finally
            {
              
                if (File.Exists(_testFilePath))
                {
                    try { File.Delete(_testFilePath); } catch {  }
                }

                _isRunning = false;
                BtnStartTest.IsEnabled = true;
                BtnStartTest.Content = "TESTİ TEKRARLA";
                TxtLiveRead.Text = "0 MB/s";
                TxtLiveWrite.Text = "0 MB/s";
            }
        }

        private async Task RunRealDiskTest(CancellationToken token)
        {
         
            const long fileSize = 1024L * 1024L * 1024L;
            byte[] buffer = new byte[8 * 1024 * 1024]; 
            new Random().NextBytes(buffer);

            Stopwatch sw = new Stopwatch();

            
            sw.Start();
            using (FileStream fs = new FileStream(_testFilePath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, FileOptions.WriteThrough))
            {
                long totalWritten = 0;
                while (totalWritten < fileSize)
                {
                    token.ThrowIfCancellationRequested();

                    await fs.WriteAsync(buffer, 0, buffer.Length, token);
                    totalWritten += buffer.Length;

                   
                    double progress = (totalWritten / (double)fileSize) * 50;
                    PbTestProgress.Value = progress;

                    double currentSpeed = (totalWritten / 1024.0 / 1024.0) / sw.Elapsed.TotalSeconds;
                    TxtLiveWrite.Text = $"{(int)currentSpeed} MB/s";
                }
            }
            sw.Stop();
            double finalWriteSpeed = (fileSize / 1024.0 / 1024.0) / sw.Elapsed.TotalSeconds;
            TxtFinalWrite.Text = $"{(int)finalWriteSpeed} MB/s";

          
            sw.Restart();
            using (FileStream fs = new FileStream(_testFilePath, FileMode.Open, FileAccess.Read, FileShare.None, buffer.Length, FileOptions.SequentialScan))
            {
                long totalRead = 0;
                while (totalRead < fileSize)
                {
                    token.ThrowIfCancellationRequested();

                    int bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead == 0) break;
                    totalRead += bytesRead;

                   
                    double progress = 50 + ((totalRead / (double)fileSize) * 50);
                    PbTestProgress.Value = progress;

                  
                    double currentSpeed = (totalRead / 1024.0 / 1024.0) / sw.Elapsed.TotalSeconds;
                    TxtLiveRead.Text = $"{(int)currentSpeed} MB/s";
                }
            }
            sw.Stop();
            double finalReadSpeed = (fileSize / 1024.0 / 1024.0) / sw.Elapsed.TotalSeconds;
            TxtFinalRead.Text = $"{(int)finalReadSpeed} MB/s";

            PbTestProgress.Value = 100;
        }
    }
}
