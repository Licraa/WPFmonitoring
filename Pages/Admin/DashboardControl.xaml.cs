using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection; // WAJIB: Untuk App.ServiceProvider
using MonitoringApp.Services;
using MonitoringApp.ViewModels;

namespace MonitoringApp.Pages
{
    public partial class DashboardControl : UserControl
    {
        private readonly SummaryService _summaryService;
        private DispatcherTimer _refreshTimer;
        private bool _isUpdating = false;

        // Event agar Admin.xaml.cs bisa mendengarkan klik kartu
        public event EventHandler<string> OnLineSelected;

        public ObservableCollection<LineSummary> DashboardCollection { get; set; }

        public DashboardControl()
        {
            InitializeComponent();

            DashboardCollection = new ObservableCollection<LineSummary>();

            // ❌ KODE LAMA (HAPUS INI):
            // var db = new DatabaseService();
            // _summaryService = new SummaryService(db);

            // ✅ KODE BARU (PAKAI DI):
            // Kita minta instance SummaryService yang sudah dikonfigurasi otomatis oleh App.xaml.cs
            _summaryService = App.ServiceProvider.GetRequiredService<SummaryService>();

            this.DataContext = this;

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(2);
            _refreshTimer.Tick += RefreshTimer_Tick;

            this.Loaded += DashboardControl_Loaded;
            this.Unloaded += DashboardControl_Unloaded;
        }

        private async void DashboardControl_Loaded(object sender, RoutedEventArgs e)
        {
            await UpdateDashboardDataAsync();
            _refreshTimer.Start();
        }

        private void DashboardControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Stop();
        }

        private async void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            try
            {
                await UpdateDashboardDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dashboard Error: {ex.Message}");
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
                var existingItem = DashboardCollection.FirstOrDefault(x => x.lineProduction == newItem.lineProduction);

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
                    DashboardCollection.Add(newItem);
                }
            }

            var itemsToRemove = DashboardCollection
                .Where(x => !newDataList.Any(n => n.lineProduction == x.lineProduction))
                .ToList();
            foreach (var item in itemsToRemove) DashboardCollection.Remove(item);

            int totalMachine = 0, totalActive = 0, totalInactive = 0;
            foreach (var s in DashboardCollection)
            {
                totalMachine += s.TotalMachine;
                totalActive += s.Active;
                totalInactive += s.Inactive;
            }

            Active = totalActive;
            Inactive = totalInactive;
            TotalMachine = totalMachine;
        }

        // --- Dependency Properties ---
        public int Active
        {
            get { return (int)GetValue(ActiveProperty); }
            set { SetValue(ActiveProperty, value); }
        }
        public static readonly DependencyProperty ActiveProperty = DependencyProperty.Register("Active", typeof(int), typeof(DashboardControl), new PropertyMetadata(0));

        public int Inactive
        {
            get { return (int)GetValue(InactiveProperty); }
            set { SetValue(InactiveProperty, value); }
        }
        public static readonly DependencyProperty InactiveProperty = DependencyProperty.Register("Inactive", typeof(int), typeof(DashboardControl), new PropertyMetadata(0));

        public int TotalMachine
        {
            get { return (int)GetValue(TotalMachineProperty); }
            set { SetValue(TotalMachineProperty, value); }
        }
        public static readonly DependencyProperty TotalMachineProperty = DependencyProperty.Register("TotalMachine", typeof(int), typeof(DashboardControl), new PropertyMetadata(0));

        // --- Event Handlers ---
        private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is LineSummary line)
            {
                // Panggil event OnLineSelected agar Admin.xaml.cs bisa merespon
                OnLineSelected?.Invoke(this, line.lineProduction);
            }
        }
    }
}