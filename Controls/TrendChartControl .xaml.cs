using MonitoringApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MonitoringApp.Controls
{
    public partial class TrendChartControl : UserControl
    {
        // ══════════════════════════════════════════════════════════════════
        //  DEPENDENCY PROPERTIES
        // ══════════════════════════════════════════════════════════════════

        public static readonly DependencyProperty TrendDataProperty =
            DependencyProperty.Register(
                nameof(TrendData),
                typeof(ObservableCollection<DailyUptimePoint>),
                typeof(TrendChartControl),
                new PropertyMetadata(null, OnTrendDataChanged));

        public static readonly DependencyProperty TrendDaysProperty =
            DependencyProperty.Register(
                nameof(TrendDays),
                typeof(int),
                typeof(TrendChartControl),
                new PropertyMetadata(7, OnAnyPropertyChanged));

        public ObservableCollection<DailyUptimePoint>? TrendData
        {
            get => (ObservableCollection<DailyUptimePoint>?)GetValue(TrendDataProperty);
            set => SetValue(TrendDataProperty, value);
        }

        public int TrendDays
        {
            get => (int)GetValue(TrendDaysProperty);
            set => SetValue(TrendDaysProperty, value);
        }

        // ══════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════════

        public TrendChartControl()
        {
            InitializeComponent();
            Loaded += (_, _) => DrawChart();
            SizeChanged += (_, _) => DrawChart();
        }

        // ══════════════════════════════════════════════════════════════════
        //  DP CALLBACKS
        // ══════════════════════════════════════════════════════════════════

        private static void OnTrendDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (TrendChartControl)d;
            if (e.OldValue is ObservableCollection<DailyUptimePoint> oldCol)
                oldCol.CollectionChanged -= ctrl.Data_CollectionChanged;
            if (e.NewValue is ObservableCollection<DailyUptimePoint> newCol)
                newCol.CollectionChanged += ctrl.Data_CollectionChanged;
            ctrl.DrawChart();
        }

        private static void OnAnyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((TrendChartControl)d).DrawChart();

        private void Data_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => Dispatcher.InvokeAsync(DrawChart);

        // ══════════════════════════════════════════════════════════════════
        //  PAGINATION BUTTONS
        // ══════════════════════════════════════════════════════════════════

        private void Btn7_Click(object sender, RoutedEventArgs e) { TrendDays = 7; RefreshPaginationStyle(); }
        private void Btn14_Click(object sender, RoutedEventArgs e) { TrendDays = 14; RefreshPaginationStyle(); }
        private void Btn30_Click(object sender, RoutedEventArgs e) { TrendDays = 30; RefreshPaginationStyle(); }

        private void RefreshPaginationStyle()
        {
            var inactiveBg = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
            var inactiveFg = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
            var inactiveBorder = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB));
            var activeBg = new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8));
            var activeFg = Brushes.White;
            var activeBorder = new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8));

            foreach (var btn in new[] { Btn7, Btn14, Btn30 })
            {
                btn.Background = inactiveBg;
                btn.Foreground = inactiveFg;
                btn.BorderBrush = inactiveBorder;
            }

            var active = TrendDays switch { 7 => Btn7, 14 => Btn14, _ => Btn30 };
            active.Background = activeBg;
            active.Foreground = activeFg;
            active.BorderBrush = activeBorder;
        }

        // ══════════════════════════════════════════════════════════════════
        //  DRAW CHART
        // ══════════════════════════════════════════════════════════════════

        private void DrawChart()
        {
            ChartCanvas.Children.Clear();

            var allData = TrendData;

            // ── Tidak ada data ────────────────────────────────────────────
            if (allData == null || allData.Count == 0)
            {
                var tb = new TextBlock
                {
                    Text = "Belum ada data tren harian",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0x9A, 0x92)),
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Canvas.SetLeft(tb, Math.Max(0, ChartCanvas.ActualWidth / 2 - 120));
                Canvas.SetTop(tb, Math.Max(0, ChartCanvas.ActualHeight / 2 - 10));
                ChartCanvas.Children.Add(tb);
                UpdateStats(null);
                return;
            }

            // ── Ambil N hari terakhir ─────────────────────────────────────
            int n = Math.Min(TrendDays, allData.Count);
            var data = allData.Skip(allData.Count - n).ToList();

            double W = ChartCanvas.ActualWidth;
            double H = ChartCanvas.ActualHeight;
            if (W < 10 || H < 10) return;

            // Padding: Left, Right, Top, Bottom
            const double PL = 42, PR = 30, PT = 18, PB = 30;
            double chartW = W - PL - PR;
            double chartH = H - PT - PB;

            // ── Grid lines horizontal + label Y ──────────────────────────
            foreach (int pct in new[] { 0, 25, 50, 75, 100 })
            {
                double y = PT + chartH * (1.0 - pct / 100.0);

                var gridLine = new Line
                {
                    X1 = PL,
                    Y1 = y,
                    X2 = W - PR,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                    StrokeThickness = 0.5
                };
                ChartCanvas.Children.Add(gridLine);

                var yLabel = new TextBlock
                {
                    Text = pct + "%",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0x9A, 0x92)),
                    Width = 36,
                    TextAlignment = TextAlignment.Right
                };
                Canvas.SetLeft(yLabel, 0);
                Canvas.SetTop(yLabel, y - 7);
                ChartCanvas.Children.Add(yLabel);
            }

            // ── Target 80% dashed ─────────────────────────────────────────
            double y80 = PT + chartH * 0.20;
            ChartCanvas.Children.Add(new Line
            {
                X1 = PL,
                Y1 = y80,
                X2 = W - PR,
                Y2 = y80,
                Stroke = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),
                StrokeThickness = 1.0,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Opacity = 0.65
            });
            var t80 = new TextBlock
            {
                Text = "80%",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32))
            };
            Canvas.SetLeft(t80, W - PR + 3);
            Canvas.SetTop(t80, y80 - 7);
            ChartCanvas.Children.Add(t80);

            // ── Hitung ukuran dan posisi bar ──────────────────────────────
            double step = n > 1 ? chartW / (n - 1) : 0;
            double barW = Math.Max(8, Math.Min(26, (n > 1 ? step : chartW) * 0.52));

            var linePoints = new PointCollection();

            for (int i = 0; i < data.Count; i++)
            {
                var pt = data[i];
                double cx = n > 1 ? PL + i * step : PL + chartW / 2.0;

                // Downtime shade (background bar)
                var shade = new Rectangle
                {
                    Width = barW,
                    Height = chartH,
                    RadiusX = 3,
                    RadiusY = 3,
                    Fill = new SolidColorBrush(Color.FromArgb(28, 0xE2, 0x4B, 0x4A))
                };
                ChartCanvas.Children.Add(shade);
                Canvas.SetLeft(shade, cx - barW / 2);
                Canvas.SetTop(shade, PT);

                // Uptime bar — warna sesuai threshold
                double upH = Math.Max(2, chartH * (pt.UptimePercent / 100.0));
                Color barColor = pt.UptimePercent >= 80
                    ? Color.FromRgb(0x63, 0x99, 0x22)
                    : pt.UptimePercent >= 60
                        ? Color.FromRgb(0xBA, 0x75, 0x17)
                        : Color.FromRgb(0xE2, 0x4B, 0x4A);

                var upBar = new Rectangle
                {
                    Width = barW,
                    Height = upH,
                    RadiusX = 3,
                    RadiusY = 3,
                    Fill = new SolidColorBrush(barColor)
                };
                ChartCanvas.Children.Add(upBar);
                Canvas.SetLeft(upBar, cx - barW / 2);
                Canvas.SetTop(upBar, PT + chartH - upH);

                // Label % di atas bar (jika ada ruang)
                if (upH < chartH - 14)
                {
                    var pctLbl = new TextBlock
                    {
                        Text = pt.UptimePercent + "%",
                        FontSize = 8,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(barColor),
                        TextAlignment = TextAlignment.Center,
                        Width = barW + 14
                    };
                    ChartCanvas.Children.Add(pctLbl);
                    Canvas.SetLeft(pctLbl, cx - (barW + 14) / 2);
                    Canvas.SetTop(pctLbl, PT + chartH - upH - 13);
                }

                // Titik untuk polyline
                double py = PT + chartH * (1.0 - pt.UptimePercent / 100.0);
                linePoints.Add(new Point(cx, py));

                // Label X-axis
                bool isLast = i == data.Count - 1;
                var xLbl = new TextBlock
                {
                    Text = pt.DateLabel,
                    FontSize = 9,
                    FontWeight = isLast ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(isLast
                                       ? Color.FromRgb(0xE2, 0x4B, 0x4A)
                                       : Color.FromRgb(0x9C, 0x9A, 0x92)),
                    TextAlignment = TextAlignment.Center,
                    Width = 36
                };
                ChartCanvas.Children.Add(xLbl);
                Canvas.SetLeft(xLbl, cx - 18);
                Canvas.SetTop(xLbl, PT + chartH + 5);
            }

            // ── Polyline tren ─────────────────────────────────────────────
            if (linePoints.Count > 1)
            {
                ChartCanvas.Children.Add(new Polyline
                {
                    Points = linePoints,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x37, 0x8A, 0xDD)),
                    StrokeThickness = 2.0,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                });
            }

            // ── Dots ─────────────────────────────────────────────────────
            for (int i = 0; i < linePoints.Count; i++)
            {
                bool isLast = i == linePoints.Count - 1;
                var dot = new Ellipse
                {
                    Width = isLast ? 10 : 7,
                    Height = isLast ? 10 : 7,
                    Fill = new SolidColorBrush(isLast
                                          ? Color.FromRgb(0x18, 0x5F, 0xA5)
                                          : Color.FromRgb(0x37, 0x8A, 0xDD)),
                    Stroke = Brushes.White,
                    StrokeThickness = isLast ? 2 : 1.5
                };
                double r = isLast ? 5 : 3.5;
                ChartCanvas.Children.Add(dot);
                Canvas.SetLeft(dot, linePoints[i].X - r);
                Canvas.SetTop(dot, linePoints[i].Y - r);
            }

            UpdateStats(data);
        }

        // ══════════════════════════════════════════════════════════════════
        //  UPDATE STAT CARDS
        // ══════════════════════════════════════════════════════════════════

        private void UpdateStats(List<DailyUptimePoint>? data)
        {
            if (data == null || data.Count == 0)
            {
                TxtAvgUp.Text = TxtAvgDown.Text = "—";
                TxtAvgUpSub.Text = TxtAvgDownSub.Text = "";
                TxtBestDay.Text = TxtBestVal.Text = "";
                TxtWorstDay.Text = TxtWorstVal.Text = "";
                return;
            }

            int avgUp = (int)Math.Round(data.Average(d => d.UptimePercent));
            var best = data.MaxBy(d => d.UptimePercent)!;
            var worst = data.MinBy(d => d.UptimePercent)!;

            TxtAvgUp.Text = avgUp + "%";
            TxtAvgUpSub.Text = $"dari {data.Count} hari";
            TxtAvgDown.Text = (100 - avgUp) + "%";
            TxtAvgDownSub.Text = $"dari {data.Count} hari";

            TxtBestDay.Text = best.DateLabelFull;
            TxtBestVal.Text = $"▲ Uptime {best.UptimePercent}%";
            TxtWorstDay.Text = worst.DateLabelFull;
            TxtWorstVal.Text = $"▼ Uptime {worst.UptimePercent}%";
        }
    }
}