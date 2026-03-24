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
        //  DRAW CHART
        // ══════════════════════════════════════════════════════════════════

        private void DrawChart()
        {
            ChartCanvas.Children.Clear();
            var allData = TrendData;

            // ── 1. Validasi Data ────────────────────────────────────────────
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

            // ── 2. Persiapan Koordinat & Padding (DIBUAT LEBIH LEGA) ─────────
            int n = Math.Min(TrendDays, allData.Count);
            var data = allData.Skip(allData.Count - n).ToList();

            double W = ChartCanvas.ActualWidth;
            double H = ChartCanvas.ActualHeight;
            if (W < 20 || H < 20) return;

            // PL ditambah ke 65 agar tidak menempel angka Y-Axis
            // PT ditambah ke 45 agar label % di puncak tidak terpotong
            const double PL = 65, PR = 45, PT = 45, PB = 45;
            double chartW = W - PL - PR;
            double chartH = H - PT - PB;

            // ── 3. Grid Lines & Y Labels (Lebih Soft) ───────────────────────
            foreach (int pct in new[] { 0, 25, 50, 75, 100 })
            {
                double y = PT + chartH * (1.0 - pct / 100.0);

                ChartCanvas.Children.Add(new Line
                {
                    X1 = PL,
                    Y1 = y,
                    X2 = W - PR,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)), // Grid lebih samar
                    StrokeThickness = 0.8
                });

                var yLabel = new TextBlock
                {
                    Text = pct + "%",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                    Width = 40,
                    TextAlignment = TextAlignment.Right
                };
                Canvas.SetLeft(yLabel, 15);
                Canvas.SetTop(yLabel, y - 7);
                ChartCanvas.Children.Add(yLabel);
            }

            // ── 4. Target Line 80% (Dashed) ──────────────────────────────────
            double y80 = PT + chartH * 0.20;
            ChartCanvas.Children.Add(new Line
            {
                X1 = PL,
                Y1 = y80,
                X2 = W - PR,
                Y2 = y80,
                Stroke = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)),
                StrokeThickness = 1.2,
                StrokeDashArray = new DoubleCollection { 6, 4 },
                Opacity = 0.5
            });

            // ── 5. Bar & Data Points Loop ────────────────────────────────────
            double step = n > 1 ? chartW / (n - 1) : 0;
            var linePoints = new PointCollection();
            double barW = 16; // Lebar tetap yang elegan

            for (int i = 0; i < data.Count; i++)
            {
                var pt = data[i];
                // Tambahkan CX dengan logic gap
                double cx = n > 1 ? PL + i * step : PL + chartW / 2.0;

                // A. Background Shade (Full Rounded)
                var shade = new Rectangle
                {
                    Width = barW,
                    Height = chartH,
                    RadiusX = 8,
                    RadiusY = 8,
                    Fill = new SolidColorBrush(Color.FromArgb(12, 0, 0, 0))
                };
                ChartCanvas.Children.Add(shade);
                Canvas.SetLeft(shade, cx - barW / 2);
                Canvas.SetTop(shade, PT);

                // B. Uptime Bar (Modern Threshold Colors)
                double upH = Math.Max(8, chartH * (pt.UptimePercent / 100.0));
                Color barColor = pt.UptimePercent >= 80 ? Color.FromRgb(0x22, 0xC5, 0x5E) : // Hijau
                                 pt.UptimePercent >= 60 ? Color.FromRgb(0xF5, 0x9E, 0x0B) : // Oranye
                                                          Color.FromRgb(0xEF, 0x44, 0x44);   // Merah

                var upBar = new Rectangle
                {
                    Width = barW,
                    Height = upH,
                    RadiusX = 8,
                    RadiusY = 8,
                    Fill = new SolidColorBrush(barColor)
                };

                // Glow Effect pada Bar
                upBar.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 10,
                    ShadowDepth = 0,
                    Opacity = 0.2,
                    Color = barColor
                };

                ChartCanvas.Children.Add(upBar);
                Canvas.SetLeft(upBar, cx - barW / 2);
                Canvas.SetTop(upBar, PT + chartH - upH);

                // C. Label Persentase (Posisikan Mengambang di atas Bar)
                double py = PT + chartH * (1.0 - pt.UptimePercent / 100.0);
                var pctLbl = new TextBlock
                {
                    Text = pt.UptimePercent + "%",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(barColor),
                    TextAlignment = TextAlignment.Center,
                    Width = 45
                };
                ChartCanvas.Children.Add(pctLbl);
                Canvas.SetLeft(pctLbl, cx - 22.5);
                Canvas.SetTop(pctLbl, py - 30); // Offset -30 agar tidak tabrakan dengan garis biru 

                linePoints.Add(new Point(cx, py));

                // D. Label X-Axis (Tanggal)
                bool isLast = i == data.Count - 1;
                var xLbl = new TextBlock
                {
                    Text = pt.DateLabel,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(isLast ? Color.FromRgb(0x1E, 0x40, 0xAF) : Color.FromRgb(0x64, 0x74, 0x8B)),
                    TextAlignment = TextAlignment.Center,
                    Width = 50,
                    FontWeight = isLast ? FontWeights.Bold : FontWeights.Normal
                };
                ChartCanvas.Children.Add(xLbl);
                Canvas.SetLeft(xLbl, cx - 25);
                Canvas.SetTop(xLbl, PT + chartH + 12);
            }

            // ── 6. Garis Tren Lengkung (Spline/Bezier) ───────────────────────
            if (linePoints.Count > 1)
            {
                PathGeometry geometry = new PathGeometry();
                PathFigure figure = new PathFigure { StartPoint = linePoints[0] };

                for (int i = 1; i < linePoints.Count; i++)
                {
                    var p0 = linePoints[i - 1];
                    var p1 = linePoints[i];
                    double controlX = p0.X + (p1.X - p0.X) / 2;
                    figure.Segments.Add(new BezierSegment(new Point(controlX, p0.Y), new Point(controlX, p1.Y), p1, true));
                }

                geometry.Figures.Add(figure);
                Path smoothLine = new Path
                {
                    Data = geometry,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                    StrokeThickness = 3.5,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };

                smoothLine.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 12,
                    ShadowDepth = 4,
                    Opacity = 0.25,
                    Color = Colors.DeepSkyBlue
                };
                ChartCanvas.Children.Add(smoothLine);
            }

            // ── 7. Titik Data (Dots) ──────────────────────────────────────────
            for (int i = 0; i < linePoints.Count; i++)
            {
                bool isLast = i == linePoints.Count - 1;
                var dot = new Ellipse
                {
                    Width = isLast ? 11 : 8,
                    Height = isLast ? 11 : 8,
                    Fill = isLast ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x1E, 0x40, 0xAF)),
                    StrokeThickness = 2
                };
                double r = dot.Width / 2;
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

        private void CmbTrendDays_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbTrendDays.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                if (int.TryParse(item.Tag.ToString(), out int days))
                {
                    TrendDays = days; // Ini akan memicu fungsi DrawChart() otomatis [cite: 9]
                }
            }
        }
    }
}