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

// Gunakan Alias untuk Admin agar tidak bentrok dengan Namespace
using AdminWindow = MonitoringApp.Pages.Admin;

namespace MonitoringApp.Pages
{
    public partial class MainWindow : Window
    {
        private readonly SummaryService _summaryService;
        private readonly CsvLogService _csvService;

        private string? _selectedLine;
        private DispatcherTimer _refreshTimer;
        private bool _isUpdating = false;

        private string _userRole;

        private ObservableCollection<LineSummary> _dashboardCollection = new ObservableCollection<LineSummary>();
        private ObservableCollection<MachineDetailViewModel> _detailCollection = new ObservableCollection<MachineDetailViewModel>();

        public MainWindow(string role = "Admin")
        {

            _userRole = role;

            ConfigureAccessControl();


            InitializeComponent();

            // 1. Init Database Service
            _summaryService = new SummaryService();

            // 2. Init CSV Service (untuk download excel)
            _csvService = new CsvLogService();

            // 3. Binding Data ke UI
            cardContainer.ItemsSource = _dashboardCollection;
            mesinListView.ItemsSource = _detailCollection;

            // 4. Init Timer Refresh (1 detik)
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(1000);
            _refreshTimer.Tick += RefreshTimer_Tick;

            // 5. Tampilan Awal (Dashboard)
            ShowDashboard();

            // 6. Mulai Timer
            _refreshTimer.Start();
        }

        private void ConfigureAccessControl()
        {
            // Jika yang login BUKAN Admin, sembunyikan menu "Admin Panel"
            if (_userRole != "Admin")
            {
                // Kita cari item menu di dalam HamburgerMenu
                // Item pertama (index 0) adalah Admin Panel
                if (HamburgerMenu.Items.Count > 0 && HamburgerMenu.Items[0] is MenuItem adminItem)
                {
                    adminItem.Visibility = Visibility.Collapsed;
                }
            }
        }

        // --- LOGIKA TIMER ---
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

        // --- UPDATE DATA DASHBOARD ---
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

        // --- UPDATE DATA DETAIL ---
        private async Task UpdateDetailDataAsync()
        {
            if (_selectedLine == null) return;

            var newDataList = await Task.Run(() => _summaryService.GetMachineDetailByLine(_selectedLine));

            foreach (var newItem in newDataList)
            {
                var existingItem = _detailCollection.FirstOrDefault(x => x.Name == newItem.Name);

                if (existingItem != null)
                {
                    bool isChanged = existingItem.IsDifferentFrom(newItem);


                    existingItem.LastUpdate = newItem.LastUpdate;
                    existingItem.PartHours = newItem.PartHours;
                    existingItem.Cycle = newItem.Cycle;
                    existingItem.AvgCycle = newItem.AvgCycle;
                    existingItem.NilaiA0 = newItem.NilaiA0;
                    existingItem.Remark = newItem.Remark; // Update Remark

                    if (existingItem.Shift1 == null) existingItem.Shift1 = new ShiftSummaryViewModel();
                    if (existingItem.Shift2 == null) existingItem.Shift2 = new ShiftSummaryViewModel();
                    if (existingItem.Shift3 == null) existingItem.Shift3 = new ShiftSummaryViewModel();

                    UpdateShiftData(existingItem.Shift1, newItem.Shift1);
                    UpdateShiftData(existingItem.Shift2, newItem.Shift2);
                    UpdateShiftData(existingItem.Shift3, newItem.Shift3);

                    if (isChanged)
                    {
                        existingItem.TriggerFlash();
                    }

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

        // --- NAVIGATION HELPERS ---

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

        // --- EVENT HANDLERS ---

        // 1. Klik Kartu Line di Dashboard -> Masuk Detail
        private void Card_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ViewModels.LineSummary summary)
            {
                ShowDetail(summary.lineProduction);
            }
        }

        // 2. Klik Tombol Back -> Balik Dashboard
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDashboard();
        }

        // 3. Klik Tombol Hamburger -> Buka Context Menu
        private void BtnHamburger_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        // 4. Menu: Buka Admin Panel
        private void BtnOpenAdmin_Click(object sender, RoutedEventArgs e)
        {
            var admin = new AdminWindow();
            admin.Show();
        }

        // 5. Menu: Buka Report History Explorer (File Explorer)
        private void BtnDownloadExcel_Click(object sender, RoutedEventArgs e)
        {
            // Buka window explorer yang sudah kita buat sebelumnya
            var history = new ReportHistoryWindow();
            history.Owner = this;
            history.Show();
        }
    }
}