using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MonitoringApp.Services;

namespace MonitoringApp.Pages
{
    public partial class LoginWindow : Window
    {
        private readonly AuthService _authService;

        public LoginWindow(AuthService authService)
        {
            InitializeComponent();
            _authService = authService;
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text;
            string password = txtPassword.Password;

            string? role = _authService.Login(username, password);

            if (role != null)
            {
                // AMBIL SERVICE FACTORY DARI DI CONTAINER
                var scopeFactory = App.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
                var csvService = App.ServiceProvider.GetRequiredService<CsvLogService>();

                // PASSING FACTORY KE MAIN WINDOW
                var mainWin = new MainWindow(role, scopeFactory, csvService);
                mainWin.Show();

                this.Close();
            }
            else
            {
                MessageBox.Show("Login Failed", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}