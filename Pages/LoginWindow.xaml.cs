using System.Windows;
using System.Windows.Input;
using MonitoringApp.Pages;
using MonitoringApp.Services; // Tambahkan ini

namespace MonitoringApp.Pages
{
    public partial class LoginWindow : Window
    {
        private readonly AuthService _authService;

        public LoginWindow()
        {
            InitializeComponent();
            _authService = new AuthService();

            // Logika drag window
            this.MouseLeftButtonDown += (sender, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) this.DragMove();
            };
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text;
            string password = txtPassword.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Username dan Password harus diisi!", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. Cek ke Database
            string? role = _authService.Login(username, password);

            if (role != null)
            {
                // 2. Jika Sukses, Buka MainWindow sambil membawa Role
                var mainWin = new MainWindow(role); // Kita akan modifikasi MainWindow sebentar lagi
                mainWin.Show();

                this.Close();
            }
            else
            {
                MessageBox.Show("Username atau Password salah!", "Login Gagal", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}