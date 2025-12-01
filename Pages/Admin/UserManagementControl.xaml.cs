using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection; // Wajib untuk DI
using MonitoringApp.Services;

namespace MonitoringApp.Pages
{
    public partial class UserManagementControl : UserControl
    {
        public UserManagementControl()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this)) return;

            // Ambil service dari container
            var userService = App.ServiceProvider.GetService<UserService>();
            if (userService != null)
            {
                UserDataGrid.ItemsSource = userService.GetAllUsers();
            }
        }

        private void BtnAddUser_Click(object sender, RoutedEventArgs e)
        {
            var userService = App.ServiceProvider.GetRequiredService<UserService>();

            // Buka AddUserWindow
            AddUserWindow addWin = new AddUserWindow(userService);
            if (addWin.ShowDialog() == true)
            {
                LoadData(); // Refresh tabel setelah tambah
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int userId)
            {
                var result = MessageBox.Show($"Are you sure you want to delete User ID: {userId}?",
                                             "Confirm Delete",
                                             MessageBoxButton.YesNo,
                                             MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    var userService = App.ServiceProvider.GetRequiredService<UserService>();
                    userService.DeleteUser(userId);
                    LoadData(); // Refresh tabel
                }
            }
        }
    }
}