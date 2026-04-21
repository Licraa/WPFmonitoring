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

        private void BtnEye_Click(object sender, RoutedEventArgs e)
        {
            if (txtPassword.Visibility == Visibility.Visible)
            {
                txtPasswordVisible.Text = txtPassword.Password;
                txtPassword.Visibility = Visibility.Collapsed;
                txtPasswordVisible.Visibility = Visibility.Visible;
                txtEyeIcon.Text = "\uE8D4";
            }
            else
            {
                txtPassword.Password = txtPasswordVisible.Text;
                txtPasswordVisible.Visibility = Visibility.Collapsed;
                txtPassword.Visibility = Visibility.Visible;
                txtEyeIcon.Text = "\uE7B3";
            }
        }

        private void ProsesLogin()
        {
            string username = txtUsername.Text;
            string password = txtPassword.Visibility == Visibility.Visible ? txtPassword.Password : txtPasswordVisible.Text;

            string? role = _authService.Login(username, password);

            if (role != null)
            {
                var scopeFactory = App.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
                var csvService = App.ServiceProvider.GetRequiredService<CsvLogService>();
                var serialService = App.ServiceProvider.GetRequiredService<SerialPortService>();

                var mainWin = new MainWindow(role, scopeFactory, csvService, serialService);
                mainWin.Show();

                this.Close();
            }
            else
            {
                OverlayAlert.Visibility = Visibility.Visible;
                OverlayAlert.UpdateLayout();
                btnOkAlert.Focus();
            }
        }

        private void BtnCloseAlert_Click(object sender, RoutedEventArgs e)
        {
            OverlayAlert.Visibility = Visibility.Collapsed;

            txtPassword.Password = "";
            txtPasswordVisible.Text = "";
            txtUsername.Focus();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            ProsesLogin();
        }

        // Satpam Pos 1: Mencegat Enter di kotak Username
        private void TxtUsername_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (string.IsNullOrWhiteSpace(txtUsername.Text)) return;

                if (txtPassword.Visibility == Visibility.Visible)
                {
                    txtPassword.Focus();
                }
                else
                {
                    txtPasswordVisible.Focus();

                }
            }
        }

        private void TxtPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ProsesLogin();
            }
        }
        private void BtnOkAlert_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnCloseAlert_Click(sender, e);
            }
        }
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}