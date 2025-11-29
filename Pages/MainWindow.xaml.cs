using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection; // WAJIB
using MonitoringApp.Services;
using MonitoringApp.ViewModels;

using AdminWindow = MonitoringApp.Pages.Admin;

namespace MonitoringApp.Pages
{
    public partial class MainWindow : Window
    {
        // GANTI SummaryService DENGAN IServiceScopeFactory
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly CsvLogService _csvService;
        private readonly string _userRole;

        private string? _selectedLine;
        private DispatcherTimer _refreshTimer;
        private bool _isUpdating = false;

        private ObservableCollection<LineSummary> _dashboardCollection = new ObservableCollection<LineSummary>();
        private ObservableCollection<MachineDetailViewModel> _detailCollection = new ObservableCollection<MachineDetailViewModel>();

        // Constructor Berubah: Terima Factory, bukan Service langsung
        public MainWindow(string role, IServiceScopeFactory scopeFactory, CsvLogService csvService)
        {
            InitializeComponent();

            _userRole = role;
            _scopeFactory = scopeFactory; // Simpan Factory
            _csvService = csvService;

            ConfigureAccessControl();

            cardContainer.ItemsSource = _dashboardCollection;
            mesinListView.ItemsSource = _detailCollection;

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(1000);
            _refreshTimer.Tick += RefreshTimer_Tick;

            ShowDashboard();

            _refreshTimer.Start();
        }

        private void ConfigureAccessControl()
        {
            if (_userRole != "Admin")
            {
                if (HamburgerMenu.Items.Count > 0 && HamburgerMenu.Items[0] is MenuItem adminItem)
                    adminItem.Visibility = Visibility.Collapsed;
            }
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

        // --- REVISI: UPDATE DASHBOARD DENGAN SCOPE BARU ---
        private async Task UpdateDashboardDataAsync()
        {
            // Buat Scope Baru -> Buat DbContext Baru -> Aman dari tabrakan Thread
            using (var scope = _scopeFactory.CreateScope())
            {
                // Minta Service dari Scope ini (Bukan dari constructor MainWindow)
                var scopedSummaryService = scope.ServiceProvider.GetRequiredService<SummaryService>();

                // Jalankan di Background Thread
                var newDataList = await Task.Run(() => scopedSummaryService.GetLineSummary());

                // Update UI (ObservableCollection harus di UI Thread, tapi karena kita pakai await, 
                // kita kembali ke context UI secara otomatis setelah Task selesai, atau aman diakses)
                // NAMUN, karena ObservableCollection tidak thread-safe, manipulasi sebaiknya di UI Thread.
                // Jika error "Collection changed", gunakan Dispatcher.

                // Update logika sinkronisasi data (sama seperti sebelumnya)
                foreach (var newItem in newDataList)
                {
                    var existingItem = _dashboardCollection.FirstOrDefault(x => x.lineProduction == newItem.lineProduction);
                    if (existingItem != null)
                    {
                        existingItem.Active = newItem.Active;
                        existingItem.Inactive = newItem.Inactive;
                        existingItem.TotalMachine = newItem.TotalMachine;
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
        }

        // --- REVISI: UPDATE DETAIL DENGAN SCOPE BARU ---
        private async Task UpdateDetailDataAsync()
        {
            if (_selectedLine == null) return;

            using (var scope = _scopeFactory.CreateScope())
            {
                var scopedSummaryService = scope.ServiceProvider.GetRequiredService<SummaryService>();

                var newDataList = await Task.Run(() => scopedSummaryService.GetMachineDetailByLine(_selectedLine));

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
                        existingItem.Remark = newItem.Remark;

                        if (existingItem.Shift1 == null) existingItem.Shift1 = new ShiftSummaryViewModel();
                        if (existingItem.Shift2 == null) existingItem.Shift2 = new ShiftSummaryViewModel();
                        if (existingItem.Shift3 == null) existingItem.Shift3 = new ShiftSummaryViewModel();

                        UpdateShiftData(existingItem.Shift1, newItem.Shift1);
                        UpdateShiftData(existingItem.Shift2, newItem.Shift2);
                        UpdateShiftData(existingItem.Shift3, newItem.Shift3);

                        if (isChanged) existingItem.TriggerFlash();
                    }
                    else
                    {
                        _detailCollection.Add(newItem);
                    }
                }

                var itemsToRemove = _detailCollection.Where(x => !newDataList.Any(n => n.Name == x.Name)).ToList();
                foreach (var item in itemsToRemove) _detailCollection.Remove(item);

                int active = 0, inactive = 0;
                foreach (var m in _detailCollection)
                {
                    if (m.NilaiA0 == 1) active++; else inactive++;
                }

                DetailPanel.DataContext = new { Active = active, Inactive = inactive, TotalMachine = _detailCollection.Count, Line = _selectedLine };
            }
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

        // --- NAVIGATION & EVENTS (Sama seperti sebelumnya) ---

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
            if (_selectedLine != lineProduction) _detailCollection.Clear();
            _selectedLine = lineProduction;
            await UpdateDetailDataAsync();
        }

        private void Card_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is LineSummary summary)
                ShowDetail(summary.lineProduction);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) => ShowDashboard();

        private void BtnHamburger_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void BtnOpenAdmin_Click(object sender, RoutedEventArgs e)
        {
            var admin = App.ServiceProvider.GetRequiredService<AdminWindow>();
            admin.Show();
        }

        private void BtnDownloadExcel_Click(object sender, RoutedEventArgs e)
        {
            var history = new ReportHistoryWindow();
            history.Owner = this;
            history.Show();
        }
    }
}