using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.Generic;
using System.Windows;

namespace PCStatix
{
    public partial class MainWindow : Window
    {
        private Random rand = new Random();

        public ISeries[] CpuUsageSeries { get; set; }
        public ISeries[] GpuUsageSeries { get; set; }

        public string CpuUsageText { get; set; }
        public string GpuUsageText { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            CpuUsageSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = new List<double> {10, 20, 15, 30, 25, 40},
                    GeometrySize = 0,
                    Fill = null
                }
            };

            GpuUsageSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = new List<double> {5, 40, 30, 10, 50, 60},
                    GeometrySize = 0,
                    Fill = null
                }
            };

            CpuUsageText = "25%";
            GpuUsageText = "43%";

            DataContext = this;
        }
    }
}


