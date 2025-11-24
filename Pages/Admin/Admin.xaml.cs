using MonitoringApp.Services;
using MonitoringApp.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;



namespace MonitoringApp.Pages
{
    public partial class Admin : Window
    {
        // Service tunggal agar koneksi port konsisten
        private readonly SerialPortService _sharedSerialService;

        // Cache untuk halaman agar tidak dibuat ulang (Memory Efficiency)
        private DashboardControl? _dashboardView;
        private SerialMonitorControl? _serialView;
        private MachinesControl? _machinesView; // <--- Harus tipe spesifik ini

        public Admin()
        {
            InitializeComponent();

            // 1. Init Service Utama
            _sharedSerialService = new SerialPortService();

            // 2. Load Dashboard pertama kali
            NavigateToDashboard();

        }

        // --- Logic Navigasi Utama ---

        private void NavigateToDashboard()
        {
            if (_dashboardView == null)
            {
                _dashboardView = new DashboardControl();

                // --- TAMBAHKAN INI: MENDENGARKAN SINYAL KLIK ---
                _dashboardView.OnLineSelected += (sender, lineName) =>
                {
                    // 1. Pindah ke Halaman Machines
                    NavigateToMachines();

                    // 2. Suruh Halaman Machines membuka Line yang diklik tadi
                    _machinesView?.OpenSpecificLine(lineName);
                };
            }

            MainContentArea.Content = _dashboardView;
            SetActiveButton(btnNavDashboard);
        }

        private void NavigateToSerial()
        {
            // Kita pass service yang sama ke SerialView
            if (_serialView == null)
            {
                _serialView = new SerialMonitorControl(_sharedSerialService);
            }

            MainContentArea.Content = _serialView;
            SetActiveButton(btnNavSerial);
        }

        private void NavigateToMachines()
        {
            // Placeholder: Jika belum ada halaman Machines, kita tampilkan pesan sementara
            if (_machinesView == null)
            {
                // Anda bisa membuat MachinesControl.xaml nanti
                 _machinesView = new MachinesControl();
            }

            MainContentArea.Content = _machinesView;

            SetActiveButton(btnNavMachines);
        }

        // --- Event Handlers Sidebar ---

        private void BtnNavDashboard_Click(object sender, RoutedEventArgs e) => NavigateToDashboard();
        private void BtnNavSerial_Click(object sender, RoutedEventArgs e) => NavigateToSerial();
        private void BtnNavMachines_Click(object sender, RoutedEventArgs e) => NavigateToMachines();

        private void BtnNavSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Settings feature coming soon!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            SetActiveButton(btnNavSettings);
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to logout?", "Logout", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                // PENTING: Matikan serial port sebelum keluar agar tidak 'nyangkut'
                try { _sharedSerialService?.Stop(); } catch { }

                this.Close();
                // Opsional: Buka kembali LoginWindow jika ada
                // new LoginWindow().Show();
            }
        }

        // --- Helper: Visual Sidebar Effect ---

        private void SetActiveButton(Button activeButton)
        {
            // Reset semua tombol ke style default (Transparent/Abu-abu)
            ResetButtonStyle(btnNavDashboard);
            ResetButtonStyle(btnNavSerial);
            ResetButtonStyle(btnNavMachines);
            ResetButtonStyle(btnNavSettings);

            // Set tombol aktif menjadi lebih terang
            activeButton.Foreground = Brushes.White;
            activeButton.Background = new SolidColorBrush(Color.FromRgb(31, 41, 55)); // #1F2937

            // (Opsional) Tampilkan border kiri/indikator jika di-support template
        }

        private void ResetButtonStyle(Button btn)
        {
            btn.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)); // #9CA3AF
            btn.Background = Brushes.Transparent;
        }

        // Tambahkan Event Handler di bagian atas class
        public event EventHandler<string> OnLineSelected;

        private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 1. Cek data kartu yang diklik
            if (sender is Border border && border.DataContext is LineSummary line)
            {
                // 2. Kirim sinyal ke Admin.xaml "Hei, user klik Line A!"
                OnLineSelected?.Invoke(this, line.lineProduction);
            }
        }
    }
}