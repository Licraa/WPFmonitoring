using Microsoft.Extensions.DependencyInjection; // WAJIB
using MonitoringApp.Controls;
using MonitoringApp.Services;
using MonitoringApp.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AdminWindow = MonitoringApp.Pages.Admin;

namespace MonitoringApp.Pages
{
    public partial class MainWindow : Window
    {
        // GANTI SummaryService DENGAN IServiceScopeFactory
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly CsvLogService _csvService;
        private readonly string _userRole;
        private readonly SerialPortService _serialService;

        private string? _selectedLine;
        private DispatcherTimer _refreshTimer;
        private bool _isUpdating = false;

        private ObservableCollection<LineSummary> _dashboardCollection = new ObservableCollection<LineSummary>();
        private ObservableCollection<MachineDetailViewModel> _detailCollection = new ObservableCollection<MachineDetailViewModel>();

        private bool _isFullScreen = false;
        private WindowState _previousWindowState;
        private WindowStyle _previousWindowStyle;
        private ResizeMode _previousResizeMode;

        private bool _lastPortState = false;

        private ObservableCollection<MachineDetailViewModel> _pagedCollection = new ObservableCollection<MachineDetailViewModel>();
        private int _currentPage = 1;
        private int _itemsPerPage = 6; // Sesuaikan angka ini dengan berapa card yang muat di satu layar (misal 6 atau 8)
        private DispatcherTimer _slideshowTimer;
        private bool _isAnimating = false; // Mencegah user spam klik saat animasi berjalan

        private List<List<MachineDetailViewModel>> _pages = new List<List<MachineDetailViewModel>>();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Jika tombol F11 ditekan, toggle (masuk/keluar) full screen
            if (e.Key == Key.F11)
            {
                ToggleFullScreen();
            }
            // Jika tombol ESC ditekan dan sedang dalam mode full screen, maka keluar
            else if (e.Key == Key.Escape && _isFullScreen)
            {
                ToggleFullScreen();
            }
        }

        private void ToggleFullScreen()
        {
            if (!_isFullScreen)
            {
                // -- MASUK FULL SCREEN --
                // 1. Simpan status jendela saat ini agar bisa dikembalikan nanti
                _previousWindowState = this.WindowState;
                _previousWindowStyle = this.WindowStyle;
                _previousResizeMode = this.ResizeMode;

                // 2. Hilangkan border dan tombol close/minimize/maximize
                this.WindowStyle = WindowStyle.None;
                this.ResizeMode = ResizeMode.NoResize;

                // 3. Trik WPF: Ubah ke Normal dulu, baru Maximized agar Taskbar Windows benar-benar tertutup
                this.WindowState = WindowState.Normal;
                this.WindowState = WindowState.Maximized;

                _isFullScreen = true;
            }
            else
            {
                // -- KELUAR FULL SCREEN --
                // Kembalikan semua pengaturan seperti semula
                this.WindowStyle = _previousWindowStyle;
                this.ResizeMode = _previousResizeMode;
                this.WindowState = _previousWindowState;

                _isFullScreen = false;
            }
        }

        // Constructor Berubah: Terima Factory, bukan Service langsung
        public MainWindow(string role, IServiceScopeFactory scopeFactory, CsvLogService csvService, SerialPortService serialService)
        {
            InitializeComponent();

            _userRole = role;
            _scopeFactory = scopeFactory; // Simpan Factory
            _csvService = csvService;
            _serialService = serialService;

            ConfigureAccessControl();

            cardContainer.ItemsSource = _dashboardCollection;
            mesinListView.ItemsSource = _pagedCollection;

            _slideshowTimer = new DispatcherTimer();
            _slideshowTimer.Interval = TimeSpan.FromSeconds(10);
            _slideshowTimer.Tick += SlideshowTimer_Tick;

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

                // ✨ PENGECEKAN ANTI-GAGAL SETIAP DETIK ✨
                if (_serialService != null)
                {
                    bool currentState = _serialService.IsRunning;

                    // Hanya update warna UI JIKA statusnya benar-benar berubah 
                    // (Agar animasi kedip tidak kerestart tiap detik)
                    if (currentState != _lastPortState)
                    {
                        _lastPortState = currentState;
                        UpdatePortStatusUI(currentState);
                    }
                }
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

                        if (existingItem.TrendData.Count == 0)
                        {
                            // 👇 Sisipkan 'existingItem.SelectedTrendShift' sebagai pesanannya
                            var trendList = scopedSummaryService.GetDailyTrend(existingItem.Id, existingItem.SelectedTrendShift);
                            foreach (var pt in trendList)
                            {
                                existingItem.TrendData.Add(pt);
                            }
                        }

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
                RefreshPageItems();
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

                if (!clickedMachine.IsExpanded)
                {
                    int clickedPageIndex = _pagedCollection.IndexOf(clickedMachine);

                    if (clickedPageIndex != -1)
                    {
                        int tempSum = 0;
                        int targetPagedIndex = 0;

                        for (int i = 0; i <= clickedPageIndex; i++)
                        {
                            int size = _pagedCollection[i].IsExpanded ? 4 : 1;
                            if (tempSum + size > 4)
                            {
                                targetPagedIndex = i;
                                tempSum = 0;
                            }
                            tempSum += size;
                        }

                        var targetMachineOnCurrentRow = _pagedCollection[targetPagedIndex];

                        if (clickedMachine != targetMachineOnCurrentRow)
                        {
                            int globalCurrentIndex = _detailCollection.IndexOf(clickedMachine);
                            int globalTargetIndex = _detailCollection.IndexOf(targetMachineOnCurrentRow);

                            if (globalCurrentIndex != -1 && globalTargetIndex != -1)
                            {
                                _detailCollection.Move(globalCurrentIndex, globalTargetIndex);
                                RefreshPageItems();
                                await Task.Delay(400); // Tunggu kartu meluncur selesai
                            }
                        }
                    }

                    clickedMachine.AllowAnimation = true;
                    clickedMachine.IsExpanded = true;                
                    RefreshPageItems();
                }
                
                else
                {
                    clickedMachine.AllowAnimation = true;
                    clickedMachine.IsExpanded = false;               
                    await Task.Delay(100);                   
                    RefreshPageItems();
                    clickedMachine.AllowAnimation = false;
                }
            }
        }

        private void UpdatePortStatusUI(bool isActive)
        {
            Dispatcher.Invoke(() => {
                // Ambil animasi kedip dari XAML Resources
                var sb = (System.Windows.Media.Animation.Storyboard)this.Resources["BlinkAnimation"];

                if (isActive)
                {                
                    DetailPortIndicator.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 255, 0));
                    sb.Begin(DetailPortIndicator, true);
                }
                else
                {                   
                    sb.Stop(DetailPortIndicator);
                    DetailPortIndicator.Opacity = 1.0;
                    DetailPortIndicator.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 0));
                }
            });
        }

        private async Task AnimateSlide(double slideOutTargetX, double slideInStartX)
        {
            if (_isAnimating) return;
            _isAnimating = true;

            // 1. Animasi keluar layar
            var slideOut = new System.Windows.Media.Animation.DoubleAnimation(
                slideOutTargetX, TimeSpan.FromMilliseconds(300))
            { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn } };

            PageSlideTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
            await Task.Delay(300);

            // 2. Ganti Data saat Card sedang di luar layar
            RefreshPageItems();

            // 3. Pindahkan posisi mulai ke ujung layar sebaliknya
            PageSlideTransform.X = slideInStartX;

            // 4. Animasi masuk kembali ke tengah layar (X = 0)
            var slideIn = new System.Windows.Media.Animation.DoubleAnimation(
                0, TimeSpan.FromMilliseconds(300))
            { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };

            PageSlideTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
            await Task.Delay(300);

            _isAnimating = false;
        }
        private void DisableAllAnimations()
        {
            // MATIKAN IZIN ANIMASI agar saat kembali ke halaman ini, WPF menampilkan ukuran 1200px secara instan.
            // KITA TIDAK MENGUBAH IsExpanded = false, agar status mekarnya tetap terjaga!
            foreach (var machine in _detailCollection)
            {
                machine.AllowAnimation = false;
            }
        }

        private async void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            // Gunakan jumlah halaman dinamis dari hasil kalkulasi
            int totalPages = _pages.Count > 0 ? _pages.Count : 1;

            if (_currentPage >= totalPages || _isAnimating) return;

            DisableAllAnimations();

            _currentPage++;
            await AnimateSlide(-2000, 2000); // Slide ke kiri, masuk dari kanan
        }

        private async void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage <= 1 || _isAnimating) return;

            DisableAllAnimations();

            _currentPage--;
            await AnimateSlide(2000, -2000); // Slide ke kanan, masuk dari kiri
        }

        private void ToggleSlideshow_Click(object sender, RoutedEventArgs e)
        {
            if (ToggleSlideshow.IsChecked == true)
            {
                ToggleSlideshow.Content = "⏸ Pause Auto Play";
                ToggleSlideshow.Background = new SolidColorBrush(Color.FromRgb(198, 40, 40)); // Warna merah
                _slideshowTimer.Start();
            }
            else
            {
                ToggleSlideshow.Content = "▶ Auto Play";
                ToggleSlideshow.Background = new SolidColorBrush(Color.FromRgb(46, 125, 50)); // Warna hijau
                _slideshowTimer.Stop();
            }
        }

        private async void SlideshowTimer_Tick(object? sender, EventArgs e)
        {
            // 1. WAJIB: Hitung ulang jumlah halaman terbaru (antisipasi jika ada kartu dihapus)
            CalculatePages();
            int totalPages = _pages.Count > 0 ? _pages.Count : 1;

            // 2. CEGAH BUG: Jika kartu yang tersisa hanya muat di 1 halaman, BATALKAN animasi geser
            if (totalPages <= 1)
            {
                _currentPage = 1;
                RefreshPageItems(); // Tetap sinkronkan data tanpa animasi
                return;
            }

            // 3. Logika siklus Auto Play normal
            if (_currentPage >= totalPages)
                _currentPage = 1;
            else
                _currentPage++;

            DisableAllAnimations();

            await AnimateSlide(-2000, 2000);
        }

        private void CalculatePages()
        {
            _pages.Clear();
            // Salin data ke list sementara untuk diproses (Packing)
            var remainingCards = _detailCollection.ToList();
            const int maxColsPerRow = 4;
            const int maxRowsPerPage = 2;

            while (remainingCards.Count > 0)
            {
                var currentPageItems = new List<MachineDetailViewModel>();

                for (int row = 0; row < maxRowsPerPage; row++)
                {
                    int filledCols = 0;
                    while (filledCols < maxColsPerRow)
                    {
                        int spaceLeft = maxColsPerRow - filledCols;
                        var fitCard = remainingCards.FirstOrDefault(c => (c.IsExpanded ? 4 : 1) <= spaceLeft);

                        if (fitCard != null)
                        {
                            currentPageItems.Add(fitCard);
                            filledCols += (fitCard.IsExpanded ? 4 : 1);
                            remainingCards.Remove(fitCard);
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (remainingCards.Count == 0) break;
                }
                _pages.Add(currentPageItems);
            }
        }

        private void RefreshPageItems()
        {
            CalculatePages();

            int totalPages = _pages.Count == 0 ? 1 : _pages.Count;
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1) _currentPage = 1;

            PageIndicatorText.Text = $"Page {_currentPage} of {totalPages}";

            var targetPageItems = _pages.Count > 0 ? _pages[_currentPage - 1] : new List<MachineDetailViewModel>();

            // SMART SYNC: Geser index agar FluidMoveBehavior WPF bisa menganimasi pergerakan ke kanan/bawah
            for (int i = 0; i < targetPageItems.Count; i++)
            {
                if (i < _pagedCollection.Count)
                {
                    if (_pagedCollection[i] != targetPageItems[i])
                    {
                        int oldIndex = _pagedCollection.IndexOf(targetPageItems[i]);
                        if (oldIndex != -1)
                        {
                            _pagedCollection.Move(oldIndex, i); // Memicu animasi geser
                        }
                        else
                        {
                            _pagedCollection.Insert(i, targetPageItems[i]); // Memicu animasi masuk dari kiri/bawah
                        }
                    }
                }
                else
                {
                    _pagedCollection.Add(targetPageItems[i]);
                }
            }

            // Buang item sisa (yang tumpah ke page selanjutnya)
            while (_pagedCollection.Count > targetPageItems.Count)
            {
                _pagedCollection.RemoveAt(_pagedCollection.Count - 1);
            }
        }
        private void AppHeader_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}