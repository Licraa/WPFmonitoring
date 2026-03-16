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
            _refreshTimer.Interval = TimeSpan.FromSeconds(1);
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
            using (var scope = _scopeFactory.CreateScope())
            {
                var scopedSummaryService = scope.ServiceProvider.GetRequiredService<SummaryService>();
                var newDataList = await Task.Run(() => scopedSummaryService.GetLineSummary());

                foreach (var newItem in newDataList)
                {
                    var existingItem = _dashboardCollection.FirstOrDefault(x => x.lineProduction == newItem.lineProduction);
                    if (existingItem != null)
                    {
                        // Update Properti Dasar
                        existingItem.Active = newItem.Active;
                        existingItem.Inactive = newItem.Inactive;
                        existingItem.TotalMachine = newItem.TotalMachine;
                        existingItem.Count = newItem.Count;
                        existingItem.PartHours = newItem.PartHours;

                        // FIX: Update Properti Cycle & Persentase
                        existingItem.Cycle = newItem.Cycle;
                        existingItem.AvgCycle = newItem.AvgCycle;
              
                    }
                    else
                    {
                        _dashboardCollection.Add(newItem);
                    }
                }

                // Hapus data yang sudah tidak ada di DB
                var itemsToRemove = _dashboardCollection
                    .Where(x => !newDataList.Any(n => n.lineProduction == x.lineProduction))
                    .ToList();
                foreach (var item in itemsToRemove) _dashboardCollection.Remove(item);

                // Update DataContext untuk Header
                DashboardPanel.DataContext = new
                {
                    TotalMachine = _dashboardCollection.Sum(s => s.TotalMachine),
                    Active = _dashboardCollection.Sum(s => s.Active),
                    Inactive = _dashboardCollection.Sum(s => s.Inactive)
                };
            }
        }


        private async Task UpdateDetailDataAsync()
        {
            if (_selectedLine == null) return;

            using (var scope = _scopeFactory.CreateScope())
            {
                var scopedSummaryService = scope.ServiceProvider.GetRequiredService<SummaryService>();

                var newDataList = await Task.Run(() => scopedSummaryService.GetMachineDetailByLine(_selectedLine));

                foreach (var newItem in newDataList)
                {
                    var existingItem = _detailCollection.FirstOrDefault(x => x.Id == newItem.Id);

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

                var itemsToRemove = _detailCollection.Where(x => !newDataList.Any(n => n.Id == x.Id)).ToList();
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
            var existingWindow = Application.Current.Windows.OfType<AdminWindow>().FirstOrDefault();
            if (existingWindow != null)
            {
               
                if (existingWindow.WindowState == WindowState.Minimized)
                {
                    existingWindow.WindowState = WindowState.Normal;
                }

               
                existingWindow.Activate();
            }
            else
            {
                
                var admin = App.ServiceProvider.GetRequiredService<AdminWindow>();
                admin.Show();
            }
        }

        private void BtnDownloadExcel_Click(object sender, RoutedEventArgs e)
        {
            // 1. Cari apakah jendela ReportHistoryWindow sudah terbuka
            var existingWindow = Application.Current.Windows
                .OfType<ReportHistoryWindow>()
                .FirstOrDefault();

            if (existingWindow != null)
            {
                // 2. Jika sudah ada, bawa ke depan (Focus)
                if (existingWindow.WindowState == WindowState.Minimized)
                {
                    existingWindow.WindowState = WindowState.Normal;
                }
                existingWindow.Activate();
            }
            else
            {
                // 3. Jika belum ada, baru buat instance baru
                var history = new ReportHistoryWindow();
                history.Owner = this; // Menjaga agar tetap di atas MainWindow
                history.Show();
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to logout?", "Confirm Logout",
                                         MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _refreshTimer.Stop(); // WAJIB: Hentikan timer agar tidak leak

                // Membersihkan koleksi di RAM
                _dashboardCollection.Clear();
                _detailCollection.Clear();

                var loginWindow = App.ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
                this.Close();

                // Paksa GC turun setelah logout
                GC.Collect();
            }
        }

        private async void DetailCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is MachineDetailViewModel clickedMachine)
            {
                // 1. Tutup semua kartu lain
                foreach (var machine in _detailCollection)
                {
                    if (machine != clickedMachine)
                    {
                        machine.IsExpanded = false;
                    }
                }

                if (!clickedMachine.IsExpanded)
                {
                    int currentIndex = _detailCollection.IndexOf(clickedMachine);

                    // Hitung target posisi ke paling kiri di barisnya
                    double cardTotalWidth = 410.0;
                    double containerWidth = mesinListView.ActualWidth;
                    int cardsPerRow = (int)(containerWidth / cardTotalWidth);
                    if (cardsPerRow <= 0) cardsPerRow = 1;

                    int rowIndex = currentIndex / cardsPerRow;
                    int targetIndex = rowIndex * cardsPerRow;

                    // Jika kartu BUKAN di posisi paling kiri
                    if (currentIndex != targetIndex)
                    {
                        // Pindahkan kartu. XAML akan merespons ini dengan meluncurkannya secara smooth!
                        _detailCollection.Move(currentIndex, targetIndex);

                        // TUNGGU KARTU SELESAI MELUNCUR (sesuaikan dengan Duration di XAML)
                        await Task.Delay(350);
                    }

                    // Setelah kartu sampai dan stabil, baru mekarkan grafiknya
                    clickedMachine.IsExpanded = true;
                }
                else
                {
                    // Jika diklik lagi untuk menutup
                    clickedMachine.IsExpanded = false;
                }
            }
        }

        private void AppHeader_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}