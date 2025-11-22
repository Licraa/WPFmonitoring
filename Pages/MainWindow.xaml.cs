using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // Wajib
using System.Linq;                    // Wajib untuk pencarian data
using System.Threading.Tasks;         // Wajib untuk async
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;       // Wajib untuk Timer
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

        // KOLEKSI UTAMA: Menggunakan ObservableCollection agar UI otomatis tahu ada perubahan
        private ObservableCollection<LineSummary> _dashboardCollection = new ObservableCollection<LineSummary>();
        private ObservableCollection<MachineDetailViewModel> _detailCollection = new ObservableCollection<MachineDetailViewModel>();

        public MainWindow()
        {
            InitializeComponent();

            // Init Service
            var db = new DatabaseService();
            _summaryService = new SummaryService(db);

            // BINDING DATA SATU KALI DI AWAL
            // Kita tidak akan pernah mengganti .ItemsSource lagi setelah ini.
            cardContainer.ItemsSource = _dashboardCollection;
            mesinListView.ItemsSource = _detailCollection;

            // Init Timer (1000ms / 1 detik)
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(1000);
            _refreshTimer.Tick += RefreshTimer_Tick;

            // Load awal
            ShowDashboard();

            // Mulai Loop
            _refreshTimer.Start();
        }

        private async void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            if (_isUpdating) return; // Cegah tumpukan request

            _isUpdating = true;
            try
            {
                // Cek panel mana yang sedang aktif untuk efisiensi
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

        // ==================================================================
        // LOGIKA DASHBOARD (Smart Update)
        // ==================================================================
        private async Task UpdateDashboardDataAsync()
        {
            // 1. Ambil data baru di Background Thread (Database)
            var newDataList = await Task.Run(() => _summaryService.GetLineSummary());

            // 2. Kembali ke UI Thread untuk update tampilan
            // Loop data baru dan sinkronkan ke _dashboardCollection
            foreach (var newItem in newDataList)
            {
                // Cari apakah item ini sudah ada di layar?
                var existingItem = _dashboardCollection.FirstOrDefault(x => x.lineProduction == newItem.lineProduction);

                if (existingItem != null)
                {
                    // JIKA ADA: Update angkanya saja (Trigger PropertyChanged)
                    // Karena ViewModelBase, UI hanya render text yang berubah.
                    existingItem.Active = newItem.Active;
                    existingItem.Inactive = newItem.Inactive;
                    existingItem.TotalMachine = newItem.TotalMachine;
                    existingItem.MachineCount = newItem.MachineCount; // jika ada
                    existingItem.Count = newItem.Count;
                    existingItem.PartHours = newItem.PartHours;
                    existingItem.Cycle = newItem.Cycle;
                    existingItem.AvgCycle = newItem.AvgCycle;
                    existingItem.DowntimePercent = newItem.DowntimePercent;
                    existingItem.UptimePercent = newItem.UptimePercent;
                }
                else
                {
                    // JIKA BARU: Tambahkan ke list
                    _dashboardCollection.Add(newItem);
                }
            }

            // (Opsional) Hapus item di UI yang sudah tidak ada di database
            var itemsToRemove = _dashboardCollection
                .Where(x => !newDataList.Any(n => n.lineProduction == x.lineProduction))
                .ToList();
            foreach (var item in itemsToRemove) _dashboardCollection.Remove(item);

            // Update Header Total Dashboard
            int totalMachine = 0, totalActive = 0, totalInactive = 0;
            foreach (var s in _dashboardCollection)
            {
                totalMachine += s.TotalMachine;
                totalActive += s.Active;
                totalInactive += s.Inactive;
            }
            DashboardPanel.DataContext = new { TotalMachine = totalMachine, Active = totalActive, Inactive = totalInactive };
        }

        // ==================================================================
        // LOGIKA DETAIL MESIN (Smart Update + Nested Shift)
        // ==================================================================
        private async Task UpdateDetailDataAsync()
        {
            if (_selectedLine == null) return;

            // 1. Ambil data database
            var newDataList = await Task.Run(() => _summaryService.GetMachineDetailByLine(_selectedLine));

            // 2. Sinkronisasi UI
            foreach (var newItem in newDataList)
            {
                // Kita anggap "Name" adalah kunci unik mesin dalam satu line
                var existingItem = _detailCollection.FirstOrDefault(x => x.Name == newItem.Name);

                if (existingItem != null)
                {
                    // Update properti utama
                    existingItem.LastUpdate = newItem.LastUpdate;
                    existingItem.PartHours = newItem.PartHours;
                    existingItem.Cycle = newItem.Cycle;
                    existingItem.AvgCycle = newItem.AvgCycle;
                    existingItem.NilaiA0 = newItem.NilaiA0; // Ini otomatis update Status "Active/Inactive" via ViewModel

                    // Update Nested Objects (Shift 1, 2, 3)
                    // Kita buat Helper Function agar rapi
                    UpdateShiftData(existingItem.Shift1, newItem.Shift1);
                    UpdateShiftData(existingItem.Shift2, newItem.Shift2);
                    UpdateShiftData(existingItem.Shift3, newItem.Shift3);
                }
                else
                {
                    _detailCollection.Add(newItem);
                }
            }

            // Hapus mesin yang sudah tidak ada (jika perlu)
            var itemsToRemove = _detailCollection
                .Where(x => !newDataList.Any(n => n.Name == x.Name))
                .ToList();
            foreach (var item in itemsToRemove) _detailCollection.Remove(item);

            // Update Header Detail Panel
            int active = 0, inactive = 0;
            foreach (var m in _detailCollection)
            {
                // Baca langsung dari properti ViewModel
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

        // Helper untuk update data shift tanpa mengganti objeknya
        private void UpdateShiftData(ShiftSummaryViewModel target, ShiftSummaryViewModel source)
        {
            if (target == null || source == null) return;

            target.Count = source.Count;
            target.Downtime = source.Downtime;
            target.Uptime = source.Uptime;
            target.DowntimePercent = source.DowntimePercent;
            target.UptimePercent = source.UptimePercent;
        }

        // ==================================================================
        // NAVIGATION & UI EVENTS
        // ==================================================================

        private async void ShowDashboard()
        {
            DashboardPanel.Visibility = Visibility.Visible;
            DetailPanel.Visibility = Visibility.Collapsed;
            _selectedLine = null;

            // Update instan agar user tidak menunggu timer
            await UpdateDashboardDataAsync();
        }

        private async void ShowDetail(string lineProduction)
        {
            DashboardPanel.Visibility = Visibility.Collapsed;
            DetailPanel.Visibility = Visibility.Visible;

            // Jika pindah line, kita harus membersihkan list detail lama
            if (_selectedLine != lineProduction)
            {
                _detailCollection.Clear();
            }
            _selectedLine = lineProduction;

            // Update instan
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
    }
}