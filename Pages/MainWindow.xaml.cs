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

        private bool _isAnimating = false; 
        private bool _isTransitioningCard = false;
        private DispatcherTimer _pageSlideshowTimer;

        private bool _isAllExpanded = false;

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

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(1);
            _refreshTimer.Tick += RefreshTimer_Tick;

            // Timer untuk Page (10 Detik)
            _pageSlideshowTimer = new DispatcherTimer();
            _pageSlideshowTimer.Interval = TimeSpan.FromSeconds(10);
            _pageSlideshowTimer.Tick += PageSlideshowTimer_Tick;

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
                if (clickedMachine.IsExpanded) return;

                // 🌟 KUNCI GEMBOK TRANSISI
                _isTransitioningCard = true;

                var currentPageList = _pages.FirstOrDefault(p => p.Contains(clickedMachine));

                if (currentPageList != null)
                {
                    bool isAnyOtherCardClosing = false;
                    foreach (var machine in currentPageList)
                    {
                        if (machine != clickedMachine && machine.IsExpanded)
                        {
                            machine.AllowAnimation = true;
                            machine.IsExpanded = false;
                            isAnyOtherCardClosing = true;
                            _ = Task.Delay(300).ContinueWith(t => machine.AllowAnimation = false);
                        }
                    }

                    if (isAnyOtherCardClosing)
                    {
                        await Task.Delay(300);
                        RefreshPageItems();
                    }
                }

                await ProcessExpandCardAsync(clickedMachine);

                // 🌟 BUKA KEMBALI GEMBOK
                _isTransitioningCard = false;
            }
        }
        private async void PageSlideshowTimer_Tick(object? sender, EventArgs e)
        {
            if (_isAnimating || _detailCollection.Count == 0) return;

            _isTransitioningCard = true;

            CalculatePages();
            int totalPages = _pages.Count > 0 ? _pages.Count : 1;

            if (totalPages <= 1)
            {
                _currentPage = 1;
                RefreshPageItems();
                _isTransitioningCard = false;
                return;
            }

            if (_currentPage >= totalPages) _currentPage = 1;
            else _currentPage++;

            DisableAllAnimations();
            await AnimateFadeAsync();

            _isTransitioningCard = false;
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
        private async Task AnimateFadeAsync()
        {
            if (_isAnimating) return;
            _isAnimating = true;

            // Durasi yang nyaman: 400ms (tidak terlalu cepat, tidak terlalu lambat)
            TimeSpan duration = TimeSpan.FromMilliseconds(400);

            // Easing Function agar transisi terasa natural (tidak kaku)
            var easing = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
            };

            // 1. Fase Keluar: Fade Out & Sedikit Turun
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, duration) { EasingFunction = easing };
            var moveDown = new System.Windows.Media.Animation.DoubleAnimation(0, 20, duration) { EasingFunction = easing };

            mesinListView.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            PageSlideTransform.BeginAnimation(TranslateTransform.YProperty, moveDown);

            await Task.Delay(duration);

            // 2. Ganti Data
            RefreshPageItems();

            // Siapkan posisi awal untuk halaman baru (muncul dari atas sedikit)
            PageSlideTransform.Y = -20;

            // 3. Fase Masuk: Fade In & Settle ke Tengah
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, duration) { EasingFunction = easing };
            var moveReset = new System.Windows.Media.Animation.DoubleAnimation(-20, 0, duration) { EasingFunction = easing };

            mesinListView.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            PageSlideTransform.BeginAnimation(TranslateTransform.YProperty, moveReset);

            await Task.Delay(duration);

            _isAnimating = false;
        }
        private void DisableAllAnimations()
        {
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
            await AnimateFadeAsync();
        }
        private async void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage <= 1 || _isAnimating) return;

            DisableAllAnimations();

            _currentPage--;
            await AnimateFadeAsync();
        }
        private void TogglePageSlideshow_Click(object sender, RoutedEventArgs e)
        {
            if (TogglePageSlideshow.IsChecked == true)
            {
                TogglePageSlideshow.Content = "⏸ Pause Page Play";
                TogglePageSlideshow.Background = new SolidColorBrush(Color.FromRgb(198, 40, 40)); // Merah
                _pageSlideshowTimer.Start();
            }
            else
            {
                TogglePageSlideshow.Content = "▶ Page Auto Play";
                TogglePageSlideshow.Background = new SolidColorBrush(Color.FromRgb(46, 125, 50)); // Hijau
                _pageSlideshowTimer.Stop();
            }
        }
        private void CalculatePages()
        {
            _pages.Clear();
            var remainingCards = _detailCollection.ToList();

            // Kapasitas Grid UI kamu
            const int maxColsPerRow = 4;
            const int maxRowsPerPage = 2;

            while (remainingCards.Count > 0)
            {
                var currentPageItems = new List<MachineDetailViewModel>();

                for (int row = 0; row < maxRowsPerPage; row++)
                {
                    int filledCols = 0;

                    // Isi baris ini selama masih ada slot kosong
                    while (filledCols < maxColsPerRow)
                    {
                        int spaceLeft = maxColsPerRow - filledCols;

                        // CARI: Kartu yang ukurannya muat di sisa slot baris ini.
                        // Jika Expand = butuh 4 slot. Jika Normal = butuh 1 slot.
                        var fitCard = remainingCards.FirstOrDefault(c => (c.IsExpanded ? 4 : 1) <= spaceLeft);

                        if (fitCard != null)
                        {
                            currentPageItems.Add(fitCard);
                            filledCols += (fitCard.IsExpanded ? 4 : 1);
                            remainingCards.Remove(fitCard);
                        }
                        else
                        {
                            // Jika tidak ada kartu yang muat di sisa baris ini (misal sisa 3 slot, tapi kartu selanjutnya butuh 4)
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

            // Jika "Expand All" mati, sistem tetap mewajibkan minimal 1 kartu terbuka (Kartu pertama)
            if (!_isTransitioningCard && !_isAllExpanded && targetPageItems.Count > 0 && !targetPageItems.Any(m => m.IsExpanded))
            {
                targetPageItems[0].AllowAnimation = false;
                targetPageItems[0].IsExpanded = true;
            }

            // 🌟 PERBAIKAN: Hanya paksa Expand jika sistem TIDAK SEDANG DALAM TRANSISI
            if (!_isTransitioningCard && targetPageItems.Count > 0 && !targetPageItems.Any(m => m.IsExpanded))
            {
                targetPageItems[0].AllowAnimation = false;
                targetPageItems[0].IsExpanded = true;
            }

            // Smart Sync Collection (Sama seperti sebelumnya)
            for (int i = 0; i < targetPageItems.Count; i++)
            {
                if (i < _pagedCollection.Count)
                {
                    if (_pagedCollection[i] != targetPageItems[i])
                    {
                        int oldIndex = _pagedCollection.IndexOf(targetPageItems[i]);
                        if (oldIndex != -1) _pagedCollection.Move(oldIndex, i);
                        else _pagedCollection.Insert(i, targetPageItems[i]);
                    }
                }
                else
                {
                    _pagedCollection.Add(targetPageItems[i]);
                }
            }

            while (_pagedCollection.Count > targetPageItems.Count)
            {
                _pagedCollection.RemoveAt(_pagedCollection.Count - 1);
            }
        }
        private async Task ProcessExpandCardAsync(MachineDetailViewModel targetMachine)
        {
            int globalCurrentIndex = _detailCollection.IndexOf(targetMachine);

            if (globalCurrentIndex != -1)
            {
                // Karena 1 halaman pasti 5 kartu, awal baris/page adalah kelipatan 5
                int pageStartIndex = (globalCurrentIndex / 5) * 5;

                if (globalCurrentIndex != pageStartIndex)
                {
                    // Pindahkan ke posisi pertama di halamannya
                    _detailCollection.Move(globalCurrentIndex, pageStartIndex);
                    RefreshPageItems();
                    await Task.Delay(350);
                }
            }

            targetMachine.AllowAnimation = true;
            targetMachine.IsExpanded = true;
            RefreshPageItems();
        }
        private void BtnExpandAll_Click(object sender, RoutedEventArgs e)
        {
            // Toggle status
            _isAllExpanded = !_isAllExpanded;

            // Update semua kartu di koleksi utama
            foreach (var machine in _detailCollection)
            {
                machine.AllowAnimation = true;
                machine.IsExpanded = _isAllExpanded;
            }

            // Ubah tampilan tombol
            BtnExpandAll.Content = _isAllExpanded ? "curtail Collapse All" : "↔ Expand All";
            BtnExpandAll.Background = _isAllExpanded ?
                new SolidColorBrush(Color.FromRgb(211, 47, 47)) : // Merah saat Collapse
                new SolidColorBrush(Color.FromRgb(69, 90, 100));  // Gray-Blue saat Expand

            RefreshPageItems();
        }
        private void AppHeader_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}