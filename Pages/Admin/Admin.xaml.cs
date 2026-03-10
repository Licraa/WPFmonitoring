using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using MonitoringApp.Services;

namespace MonitoringApp.Pages
{
    public partial class Admin : Window
    {
        public Admin()
        {
            InitializeComponent();
            
            // Set halaman awal saat pertama kali dibuka
            NavigateToDashboard();

            // Memastikan pembersihan total saat jendela Admin ditutup
            this.Closed += (s, e) => 
            {
                CleanupMemory();
                this.DataContext = null;
                this.Content = null;
            };
        }

        // --- Logic Navigasi Utama ---

        private void NavigateToDashboard()
        {
            PrepareNavigation();
            // Selalu ambil instance baru dari ServiceProvider
            MainContentArea.Content = App.ServiceProvider.GetRequiredService<DashboardControl>();
            SetActiveButton(btnNavDashboard);
        }

        private void NavigateToSerial()
        {
            PrepareNavigation();
            MainContentArea.Content = App.ServiceProvider.GetRequiredService<SerialMonitorControl>();
            SetActiveButton(btnNavSerial);
        }

        private void NavigateToMachines()
        {
            PrepareNavigation();
            // Pastikan MachinesControl sudah terdaftar di App.xaml.cs sebagai Transient
            MainContentArea.Content = App.ServiceProvider.GetRequiredService<MachinesControl>();
            SetActiveButton(btnNavMachines);
        }

        private void NavigateToUsers()
        {
            PrepareNavigation();
            MainContentArea.Content = App.ServiceProvider.GetRequiredService<UserManagementControl>();
            SetActiveButton(btnNavUsers);
        }

        private void NavigateToSettings()
        {
            PrepareNavigation(); // Memanggil fungsi cleanup memori yang sudah ada
                                 // Memanggil instance SettingsControl dari ServiceProvider
            MainContentArea.Content = App.ServiceProvider.GetRequiredService<SettingsControl>();
            SetActiveButton(btnNavSettings);
        }



        // --- Core Memory Management Logic ---

        /// <summary>
        /// Membersihkan halaman lama dan memaksa Garbage Collector untuk mengosongkan RAM
        /// </summary>
        private void PrepareNavigation()
        {
            // 1. Lepas konten lama dari UI Tree agar bisa dideteksi GC sebagai 'unused'
            MainContentArea.Content = null;

            // 2. Jalankan pembersihan memori manual (Sangat penting untuk menurunkan Gen 2)
            CleanupMemory();
        }

        private void CleanupMemory()
        {
            try
            {
                // Paksa pembersihan semua generasi (0, 1, dan 2)
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cleanup Memory Error: {ex.Message}");
            }
        }

        // --- Event Handlers Sidebar ---

        private void BtnNavDashboard_Click(object sender, RoutedEventArgs e) => NavigateToDashboard();
        private void BtnNavSerial_Click(object sender, RoutedEventArgs e) => NavigateToSerial();
        private void BtnNavMachines_Click(object sender, RoutedEventArgs e) => NavigateToMachines();
        private void BtnNavUsers_Click(object sender, RoutedEventArgs e) => NavigateToUsers();
        private void BtnNavSettings_Click(object sender, RoutedEventArgs e) => NavigateToSettings();
        

 
        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to logout?", "Logout", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                this.Close();
            }
        }

        // --- Visual Sidebar Helpers ---

        private void SetActiveButton(Button activeButton)
        {
            ResetButtonStyle(btnNavDashboard);
            ResetButtonStyle(btnNavSerial);
            ResetButtonStyle(btnNavMachines);
            ResetButtonStyle(btnNavSettings);
            ResetButtonStyle(btnNavUsers);

            activeButton.Foreground = Brushes.White;
            activeButton.Background = new SolidColorBrush(Color.FromRgb(31, 41, 55)); // #1F2937
        }

        private void ResetButtonStyle(Button btn)
        {
            if (btn == null) return;
            btn.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)); // #9CA3AF
            btn.Background = Brushes.Transparent;
        }
    }
}