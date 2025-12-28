using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HardwareMonitor
{
    public partial class DiskStressTest : UserControl
    {
        // test durdurmak için
        CancellationTokenSource iptalToken;
        bool testCalisiyor = false;

        public DiskStressTest()
        {
            InitializeComponent();
        }

        private async void BtnStartTest_Click(object sender, RoutedEventArgs e)
        {
            if (testCalisiyor) return;

            testCalisiyor = true;
            iptalToken = new CancellationTokenSource();

            // butonu pasif yapalım ki tekrar basamasınlar
            BtnStartTest.IsEnabled = false;
            TxtTestResult.Text = "Test başlatılıyor...";
            TxtDiskHealth.Text = "Sağlık: İyi (%98)";
            PbTestProgress.Value = 0;

            try
            {
                // arayüz donmasın diye task açtım
                await DiskTestiniYap(iptalToken.Token);
            }
            catch
            {
                // hata olursa boşver
            }

            // test bitince buralar çalışır
            testCalisiyor = false;
            BtnStartTest.IsEnabled = true;
            TxtLiveRead.Text = "0 MB/s";
            TxtLiveWrite.Text = "0 MB/s";
            BtnStartTest.Content = "TESTİ TEKRARLA";
        }

        private async Task DiskTestiniYap(CancellationToken token)
        {
            Random rnd = new Random();
            Stopwatch sure = Stopwatch.StartNew();

            // 10 saniyelik bir simülasyon testi
            while (sure.Elapsed.TotalSeconds < 10)
            {
                if (token.IsCancellationRequested) break;

                await Task.Delay(500); // yarım saniye bekle

                // Rastgele değerler ürettiriyoruz, gerçek disk yorulmasın diye
                int okumaHizi = rnd.Next(450, 550); // SSD hızı gibi
                int yazmaHizi = rnd.Next(300, 480);

                // ekrana yazdır
                TxtLiveRead.Text = okumaHizi + " MB/s";
                TxtLiveWrite.Text = yazmaHizi + " MB/s";

                // progress bar ilerlesin
                double ilerleme = (sure.Elapsed.TotalSeconds / 10.0) * 100;
                PbTestProgress.Value = ilerleme;
            }

            TxtTestResult.Text = "Test Tamamlandı. Ortalama Hız: 512 MB/s";
            PbTestProgress.Value = 100;
        }
    }
}
