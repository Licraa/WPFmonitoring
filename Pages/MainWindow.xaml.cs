using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MonitoringApp.Services;
using MonitoringApp.ViewModels;

namespace MonitoringApp.Pages
{
    public partial class MainWindow : Window
    {
        private readonly SummaryService _summaryService;
        private string? _selectedLine;
        private DispatcherTimer _refreshTimer;
        private bool _isUpdating = false;
        private readonly CsvLogService _csvService;
        

        private ObservableCollection<LineSummary> _dashboardCollection = new ObservableCollection<LineSummary>();
        private ObservableCollection<MachineDetailViewModel> _detailCollection = new ObservableCollection<MachineDetailViewModel>();

        public MainWindow()
        {
            InitializeComponent();

            var db = new DatabaseService();
            _summaryService = new SummaryService(db);

            cardContainer.ItemsSource = _dashboardCollection;
            mesinListView.ItemsSource = _detailCollection;

            _csvService = new CsvLogService();

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(1000);
            _refreshTimer.Tick += RefreshTimer_Tick;

            ShowDashboard();

            _refreshTimer.Start();
        }

        private async void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            if (_isUpdating) return;

            _isUpdating = true;
            try
            {
                if (DashboardPanel.Visibility == Visibility.Visible)
                {
                    await UpdateDashboardDataAsync();
                }
                else if (DetailPanel.Visibility == Visibility.Visible && !string.IsNullOrEmpty(_selectedLine))
                {
                    await UpdateDetailDataAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error Update: {ex.Message}");
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private async Task UpdateDashboardDataAsync()
        {
            var newDataList = await Task.Run(() => _summaryService.GetLineSummary());

            foreach (var newItem in newDataList)
            {
                var existingItem = _dashboardCollection.FirstOrDefault(x => x.lineProduction == newItem.lineProduction);

                if (existingItem != null)
                {
                    existingItem.Active = newItem.Active;
                    existingItem.Inactive = newItem.Inactive;
                    existingItem.TotalMachine = newItem.TotalMachine;
                    existingItem.MachineCount = newItem.MachineCount;
                    existingItem.Count = newItem.Count;
                    existingItem.PartHours = newItem.PartHours;
                    existingItem.Cycle = newItem.Cycle;
                    existingItem.AvgCycle = newItem.AvgCycle;
                    existingItem.DowntimePercent = newItem.DowntimePercent;
                    existingItem.UptimePercent = newItem.UptimePercent;
                }
                else
                {
                    _dashboardCollection.Add(newItem);
                }
            }

            var itemsToRemove = _dashboardCollection
                .Where(x => !newDataList.Any(n => n.lineProduction == x.lineProduction))
                .ToList();
            foreach (var item in itemsToRemove) _dashboardCollection.Remove(item);

            int totalMachine = 0, totalActive = 0, totalInactive = 0;
            foreach (var s in _dashboardCollection)
            {
                totalMachine += s.TotalMachine;
                totalActive += s.Active;
                totalInactive += s.Inactive;
            }
            DashboardPanel.DataContext = new { TotalMachine = totalMachine, Active = totalActive, Inactive = totalInactive };
        }

        private async Task UpdateDetailDataAsync()
        {
            if (_selectedLine == null) return;

            var newDataList = await Task.Run(() => _summaryService.GetMachineDetailByLine(_selectedLine));

            foreach (var newItem in newDataList)
            {
                var existingItem = _detailCollection.FirstOrDefault(x => x.Name == newItem.Name);

                if (existingItem != null)
                {
                    existingItem.LastUpdate = newItem.LastUpdate;
                    existingItem.PartHours = newItem.PartHours;
                    existingItem.Cycle = newItem.Cycle;
                    existingItem.AvgCycle = newItem.AvgCycle;
                    existingItem.NilaiA0 = newItem.NilaiA0;

                    UpdateShiftData(existingItem.Shift1, newItem.Shift1);
                    UpdateShiftData(existingItem.Shift2, newItem.Shift2);
                    UpdateShiftData(existingItem.Shift3, newItem.Shift3);
                }
                else
                {
                    _detailCollection.Add(newItem);
                }
            }

            var itemsToRemove = _detailCollection
                .Where(x => !newDataList.Any(n => n.Name == x.Name))
                .ToList();
            foreach (var item in itemsToRemove) _detailCollection.Remove(item);

            int active = 0, inactive = 0;
            foreach (var m in _detailCollection)
            {
                if (m.NilaiA0 == 1) active++; else inactive++;
            }

            DetailPanel.DataContext = new
            {
                Active = active,
                Inactive = inactive,
                TotalMachine = _detailCollection.Count,
                Line = _selectedLine
            };
        }

        private void UpdateShiftData(ShiftSummaryViewModel target, ShiftSummaryViewModel source)
        {
            if (target == null || source == null) return;

            target.Count = source.Count;
            target.Downtime = source.Downtime;
            target.Uptime = source.Uptime;
            target.DowntimePercent = source.DowntimePercent;
            target.UptimePercent = source.UptimePercent;
        }

        private async void ShowDashboard()
        {
            DashboardPanel.Visibility = Visibility.Visible;
            DetailPanel.Visibility = Visibility.Collapsed;
            _selectedLine = null;

            await UpdateDashboardDataAsync();
        }

        private async void ShowDetail(string lineProduction)
        {
            DashboardPanel.Visibility = Visibility.Collapsed;
            DetailPanel.Visibility = Visibility.Visible;

            if (_selectedLine != lineProduction)
            {
                _detailCollection.Clear();
            }
            _selectedLine = lineProduction;

            await UpdateDetailDataAsync();
        }

        private void Card_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ViewModels.LineSummary summary)
            {
                ShowDetail(summary.lineProduction);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDashboard();
        }

        // --- FUNGSI BARU: BUKA ADMIN WINDOW ---
        private void BtnOpenAdmin_Click(object sender, RoutedEventArgs e)
        {
            // Menggunakan .Show() agar window MainWindow tetap bisa diakses (tidak modal)
            var adminWindow = new Admin();
            adminWindow.Show();
        }

        private void BtnHamburger_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn; // Agar menu muncul pas di bawah tombol
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void BtnDownloadExcel_Click(object sender, RoutedEventArgs e)
        {
            try 
            {
                // Ambil info shift saat ini
                var info = _csvService.GetCurrentShiftInfo();
                
                // Beri feedback loading (cursor wait)
                Mouse.OverrideCursor = Cursors.Wait;

                // Jalankan di background biar UI tidak macet
                Task.Run(() => 
                {
                    // Generate Excel dari data CSV terkini
                    _csvService.FinalizeExcel(info.shiftName, info.shiftDate);
                    
                    // Balik ke UI Thread untuk tampilkan pesan sukses
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        Mouse.OverrideCursor = null;
                        string path = _csvService.GetCsvPath(info.shiftDate, info.shiftName).Replace(".csv", ".xlsx");
                        
                        MessageBoxResult result = MessageBox.Show(
                            $"Excel Report Generated Successfully!\n\nShift: {info.shiftName}\nLocation: {path}\n\nDo you want to open the folder?", 
                            "Export Success", 
                            MessageBoxButton.YesNo, 
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            // Buka folder di File Explorer
                            string folder = System.IO.Path.GetDirectoryName(path);
                            System.Diagnostics.Process.Start("explorer.exe", folder);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"Failed to export Excel: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}