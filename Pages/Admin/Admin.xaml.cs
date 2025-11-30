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

                _dashboardView = App.ServiceProvider.GetRequiredService<DashboardControl>();

            }

            MainContentArea.Content = _dashboardView;
            SetActiveButton(btnNavDashboard);
        }

        private void NavigateToSerial()
        {
            if (_serialView == null)
            {
                
                _serialView = App.ServiceProvider.GetRequiredService<SerialMonitorControl>();
            }

            MainContentArea.Content = _serialView;
            SetActiveButton(btnNavSerial);
        }

        private void NavigateToMachines()
        {
            if (_machinesView == null)
            {
                
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
                

                this.Close(); 
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