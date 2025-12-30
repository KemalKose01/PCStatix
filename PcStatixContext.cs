using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using HardwareMonitor.Models;

namespace HardwareMonitor.data
{
    public class PcStatixContext : DbContext
    {
        public DbSet<EkranKarti> EkranKartlari { get; set; }
        public DbSet<Islemci> Islemciler { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            
            optionsBuilder.UseSqlServer(@"Server=DESKTOP-IDT1MPS\SQLEXPRESS;Database=pcstatix;Trusted_Connection=True;TrustServerCertificate=True;");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EkranKarti>().ToTable("ekrankarti");
            modelBuilder.Entity<Islemci>().ToTable("islemci");
        }

        
        public void SaveCpuData(string cpuName, int sicaklik)
        {
            int newId = this.Islemciler.Any() ? this.Islemciler.Max(x => x.id) + 1 : 1;
            this.Islemciler.Add(new Islemci { id = newId, islemci_isim = cpuName, islemci_sicaklik = sicaklik });
            this.SaveChanges();
        }

        public double GetAverageCpuTemp(string cpuName)
        {
            return this.Islemciler.Where(x => x.islemci_isim == cpuName).Average(x => (double?)x.islemci_sicaklik) ?? 0.0;
        }

        public string CpuHealth(double ortalama, double sicaklik)
        {
            double fark = sicaklik - ortalama;
            if (fark <= 5) return "İşlemciniz Sağlıklı";
            if (fark <= 10) return "İşlemciniz Dikkat Gerektiriyor";
            return "İşlemciniz Risk Altında";
        }

     
        public void SaveGpuData(string gpuName, int sicaklik)
        {
            int newId = this.EkranKartlari.Any() ? this.EkranKartlari.Max(x => x.id) + 1 : 1;
            this.EkranKartlari.Add(new EkranKarti { id = newId, ekrank_isim = gpuName, ekrank_sicaklik = sicaklik });
            this.SaveChanges();
        }

        public double GetAverageGpuTemp(string gpuName)
        {
            return this.EkranKartlari.Where(x => x.ekrank_isim == gpuName).Average(x => (double?)x.ekrank_sicaklik) ?? 0.0;
        }

        public string GpuHealth(double ortalama, double sicaklik)
        {
            double fark = sicaklik - ortalama;
            if (fark <= 5) return "Ekran Kartınız Sağlıklı";
            return "Ekran Kartınız Dikkat Gerektiriyor";
        }
    }
}