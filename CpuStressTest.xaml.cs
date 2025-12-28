using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HardwareMonitor
{
    public partial class CpuStressTest : UserControl
    {
        // Testi durdurmak için gerekli token
        private CancellationTokenSource _iptalTokeni;
        private bool _calisiyorMu = false;

        public CpuStressTest()
        {
            InitializeComponent();
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_calisiyorMu) return;

            // Başlangıç ayarları
            _calisiyorMu = true;
            _iptalTokeni = new CancellationTokenSource();

            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            CpuProgressBar.Value = 0;

            try
            {
                // Testi asenkron olarak başlatıyoruz ki arayüz donmasın
                await CpuTestiBaslat(_iptalTokeni.Token);
            }
            catch (OperationCanceledException)
            {
                // Test durdurulduğunda buraya düşer, sorun yok.
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata oldu: " + ex.Message);
            }
            finally
            {
                // Her durumda butonları eski haline getir
                TestiSifirla();
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            // Durdur butonuna basınca iptal isteği gönder
            if (_iptalTokeni != null)
            {
                _iptalTokeni.Cancel();
            }
        }

        private async Task CpuTestiBaslat(CancellationToken token)
        {
            // İşlemci çekirdek sayısı kadar görev oluşturuyoruz
            int cekirdekSayisi = Environment.ProcessorCount;
            Task[] gorevler = new Task[cekirdekSayisi];

            var kronometre = Stopwatch.StartNew();

            // İlerleme çubuğunu güncelleyen döngü
            var arayuzGorevi = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && kronometre.Elapsed.TotalSeconds < 60)
                {
                    await Task.Delay(1000, token); // 1 saniye bekle

                    // UI güncellemesi
                    Dispatcher.Invoke(() =>
                    {
                        int gecenSure = (int)kronometre.Elapsed.TotalSeconds;
                        TxtCpuTimer.Text = $"{gecenSure}s";
                        CpuProgressBar.Value = gecenSure;

                        // Rastgele sıcaklık ve yük değerleri (Simülasyon)
                        Random rnd = new Random();
                        TxtCpuTemp.Text = $"{rnd.Next(65, 85)}°C";
                        TxtCpuLoad.Text = $"%{rnd.Next(90, 100)}";
                    });
                }
            }, token);

            // Çekirdekleri zorlayan matematik işlemleri
            for (int i = 0; i < cekirdekSayisi; i++)
            {
                gorevler[i] = Task.Run(() =>
                {
                    while (!token.IsCancellationRequested && kronometre.Elapsed.TotalSeconds < 60)
                    {
                        // İşlemciyi yormak için anlamsız matematik işlemleri
                        double sayi = 0.0001;
                        for (int j = 0; j < 10000; j++)
                        {
                            sayi = Math.Sqrt(sayi * j) + Math.Sin(j);
                        }
                    }
                }, token);
            }

            // Arayüz ve işlemci görevlerinin bitmesini bekle
            await Task.WhenAll(gorevler);
            await arayuzGorevi; // Süre dolunca biter
        }

        private void TestiSifirla()
        {
            _calisiyorMu = false;
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            TxtCpuLoad.Text = "%0";
            TxtCpuTimer.Text = "60s";
            CpuProgressBar.Value = 0;
        }
    }
}
