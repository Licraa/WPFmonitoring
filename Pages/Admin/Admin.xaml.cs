using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection; // Wajib untuk ServiceProvider
using MonitoringApp.ViewModels;

namespace MonitoringApp.Pages
{
    public partial class Admin : Window
    {
        // Cache untuk halaman agar state tidak hilang saat pindah tab
        private DashboardControl? _dashboardView;
        private SerialMonitorControl? _serialView;
        private MachinesControl? _machinesView;

        public Admin()
        {
            InitializeComponent();

            // Load Dashboard pertama kali
            NavigateToDashboard();
        }

        // --- Logic Navigasi Utama ---

        private void NavigateToDashboard()
        {
            if (_dashboardView == null)
            {
                // MENGGUNAKAN DI: Ambil DashboardControl dari ServiceProvider
                // Pastikan DashboardControl sudah didaftarkan di App.xaml.cs (AddTransient)
                // Jika belum, Anda bisa menambahkannya, atau jika DashboardControl constructor-nya kosong, 'new' biasa masih oke.
                // Tapi untuk konsistensi, kita asumsi DashboardControl kelak butuh Service.

                // Note: Jika DashboardControl belum didaftarkan di DI, gunakan 'new DashboardControl()' 
                // Tapi karena kita mau standar pro, kita pakai DI.
                // Jika error "No service for type DashboardControl", tambahkan services.AddTransient<DashboardControl>() di App.xaml.cs
                // Untuk amannya di sini saya pakai pendekatan manual injection jika DashboardControl blm didaftarkan:

                _dashboardView = new DashboardControl(); // (Atau pakai DI jika sudah didaftarkan)

                // Event Listener: Jika user klik kartu di Dashboard, pindah ke Machines
                _dashboardView.OnLineSelected += (sender, lineName) =>
                {
                    NavigateToMachines();
                    _machinesView?.OpenSpecificLine(lineName);
                };
            }

            MainContentArea.Content = _dashboardView;
            SetActiveButton(btnNavDashboard);
        }

        private void NavigateToSerial()
        {
            if (_serialView == null)
            {
                // MENGGUNAKAN DI: PENTING!
                // SerialMonitorControl punya BANYAK dependency di constructornya.
                // Kita TIDAK BISA pakai 'new SerialMonitorControl()'. Harus minta ke Provider.
                _serialView = App.ServiceProvider.GetRequiredService<SerialMonitorControl>();
            }

            MainContentArea.Content = _serialView;
            SetActiveButton(btnNavSerial);
        }

        private void NavigateToMachines()
        {
            if (_machinesView == null)
            {
                // MENGGUNAKAN DI (Opsional tapi disarankan jika MachinesControl butuh MachineService)
                // _machinesView = App.ServiceProvider.GetRequiredService<MachinesControl>();
                // Jika belum didaftarkan di App.xaml.cs, pakai new biasa dulu:
                _machinesView = new MachinesControl();
            }

            MainContentArea.Content = _machinesView;
            SetActiveButton(btnNavMachines);
        }

        private void BtnNavSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Settings feature coming soon!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            SetActiveButton(btnNavSettings);
        }

        // --- Event Handlers Sidebar ---

        private void BtnNavDashboard_Click(object sender, RoutedEventArgs e) => NavigateToDashboard();
        private void BtnNavSerial_Click(object sender, RoutedEventArgs e) => NavigateToSerial();
        private void BtnNavMachines_Click(object sender, RoutedEventArgs e) => NavigateToMachines();

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to logout?", "Logout", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                // Tidak perlu stop serial manual di sini, karena SerialPortService adalah Singleton.
                // Koneksi akan tetap hidup di background (sesuai standar monitoring), 
                // atau bisa dipanggil _serialService.Stop() jika ingin benar-benar mati.

                this.Close();
                // Opsional: Buka login lagi
                // App.ServiceProvider.GetRequiredService<LoginWindow>().Show();
            }
        }

        // --- Helper: Visual Sidebar Effect ---

        private void SetActiveButton(Button activeButton)
        {
            ResetButtonStyle(btnNavDashboard);
            ResetButtonStyle(btnNavSerial);
            ResetButtonStyle(btnNavMachines);
            ResetButtonStyle(btnNavSettings);

            activeButton.Foreground = Brushes.White;
            activeButton.Background = new SolidColorBrush(Color.FromRgb(31, 41, 55)); // #1F2937
        }

        private void ResetButtonStyle(Button btn)
        {
            btn.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)); // #9CA3AF
            btn.Background = Brushes.Transparent;
        }
    }
}