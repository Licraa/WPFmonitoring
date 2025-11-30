using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MonitoringApp.Data;
using MonitoringApp.Services;

namespace MonitoringApp.Pages
{
    public partial class DashboardControl : UserControl
    {
        // Dependency Services
        private readonly AppDbContext _context;
        private readonly SerialPortService _serialService;
        private readonly CsvLogService _csvService;

        // Hardware Counters
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _ramCounter;

        // Timer
        private DispatcherTimer _timer;

        // CONSTRUCTOR INJECTION (Profesional Standard)
        // Kita meminta Service langsung di parameter, bukan mengambil sendiri
        public DashboardControl(
            AppDbContext context,
            SerialPortService serialService,
            CsvLogService csvService)
        {
            InitializeComponent();

            _context = context;
            _serialService = serialService;
            _csvService = csvService;

            // Setup ViewModel
            this.DataContext = new DashboardViewModel();

            // Init Hardware Counters (Bungkus try-catch agar tidak crash di PC yang diproteksi)
            InitializePerformanceCounters();

            // Setup Timer
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += UpdateSystemStatus;

            // Lifetime Management
            this.Loaded += (s, e) => { UpdateSystemStatus(null, null); _timer.Start(); };
            this.Unloaded += OnUnloaded;
        }

        private void InitializePerformanceCounters()
        {
            try
            {
                // Note: PerformanceCounter hanya jalan di Windows
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                // Jika gagal (misal permission denied), biarkan null agar aplikasi tidak crash
                System.Diagnostics.Debug.WriteLine($"Counter Error: {ex.Message}");
            }
        }

        private void UpdateSystemStatus(object? sender, EventArgs? e)
        {
            if (this.DataContext is not DashboardViewModel vm) return;

            // 1. Cek Database
            bool dbConnected = false;
            try { dbConnected = _context.Database.CanConnect(); } catch { }

            vm.DbStatusText = dbConnected ? "Connected" : "Disconnected";
            vm.DbStatusColor = dbConnected ? "#10B981" : "#EF4444";
            vm.DbName = "MonitoringDB";

            // 2. Cek Serial Port
            bool serialActive = _serialService.IsRunning;
            vm.SerialStatusText = serialActive ? "Running" : "Stopped";
            vm.SerialStatusColor = serialActive ? "#10B981" : "#F59E0B";
            vm.ActivePort = serialActive ? "Port Active" : "No Port";
            vm.LastDataTime = serialActive ? "Listening..." : "Service Idle";

            // 3. Shift Info
            var shiftInfo = _csvService.GetCurrentShiftInfo();
            vm.CurrentShift = shiftInfo.shiftName.Replace("_", " ").ToUpper();
            vm.SystemTime = DateTime.Now.ToString("dd MMM yyyy, HH:mm:ss");

            // 4. Log Size
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data_log_monitoring");
            if (Directory.Exists(logPath))
            {
                // Hitung total size semua file dalam folder log
                long size = new DirectoryInfo(logPath).GetFiles("*.*", SearchOption.AllDirectories).Sum(fi => fi.Length);
                vm.LogSize = FormatBytes(size);
            }
            else
            {
                vm.LogSize = "0 MB";
            }

            // 5. Hardware Stats (Hanya jika counter berhasil di-init)
            if (_cpuCounter != null && _ramCounter != null)
            {
                // NextValue() pertama kali seringkali return 0, jadi pemanggilan berulang timer akan memperbaikinya
                vm.CpuUsage = $"{_cpuCounter.NextValue():0}%";
                vm.RamUsage = $"{_ramCounter.NextValue()} MB Free";
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            // Bersihkan memori counter agar tidak leak
            _cpuCounter?.Dispose();
            _ramCounter?.Dispose();
        }

        private string FormatBytes(long bytes)
        {
            if (bytes >= 1048576) return $"{bytes / 1024f / 1024f:0.0} MB";
            if (bytes >= 1024) return $"{bytes / 1024f:0.0} KB";
            return $"{bytes} B";
        }

        private void BtnOpenLogs_Click(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data_log_monitoring");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
    }

    // ViewModel Sederhana
    public class DashboardViewModel : ViewModels.ViewModelBase
    {
        private string _dbStatusText = "Checking...";
        public string DbStatusText { get => _dbStatusText; set => SetProperty(ref _dbStatusText, value); }

        private string _dbStatusColor = "#9CA3AF";
        public string DbStatusColor { get => _dbStatusColor; set => SetProperty(ref _dbStatusColor, value); }

        private string _dbName = "-";
        public string DbName { get => _dbName; set => SetProperty(ref _dbName, value); }

        private string _serialStatusText = "Checking...";
        public string SerialStatusText { get => _serialStatusText; set => SetProperty(ref _serialStatusText, value); }

        private string _serialStatusColor = "#9CA3AF";
        public string SerialStatusColor { get => _serialStatusColor; set => SetProperty(ref _serialStatusColor, value); }

        private string _activePort = "-";
        public string ActivePort { get => _activePort; set => SetProperty(ref _activePort, value); }

        private string _lastDataTime = "-";
        public string LastDataTime { get => _lastDataTime; set => SetProperty(ref _lastDataTime, value); }

        private string _currentShift = "-";
        public string CurrentShift { get => _currentShift; set => SetProperty(ref _currentShift, value); }

        private string _systemTime = "-";
        public string SystemTime { get => _systemTime; set => SetProperty(ref _systemTime, value); }

        private string _logSize = "0 MB";
        public string LogSize { get => _logSize; set => SetProperty(ref _logSize, value); }

        private string _cpuUsage = "0%";
        public string CpuUsage { get => _cpuUsage; set => SetProperty(ref _cpuUsage, value); }

        private string _ramUsage = "0 MB";
        public string RamUsage { get => _ramUsage; set => SetProperty(ref _ramUsage, value); }
    }
}