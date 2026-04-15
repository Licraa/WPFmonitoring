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
                // Salin sandi ke kotak teks biasa
                txtPasswordVisible.Text = txtPassword.Password;
                txtPassword.Visibility = Visibility.Collapsed;
                txtPasswordVisible.Visibility = Visibility.Visible;
                txtEyeIcon.Text = "\uE8D4";
            }
            else
            {
                // Kembalikan isi dari teks biasa ke kotak sandi
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

            // ⚠️ RAWAN ERROR: Pastikan AuthService kamu menangani error dengan aman
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
            // Sembunyikan kembali pelayannya
            OverlayAlert.Visibility = Visibility.Collapsed;

            // BEST PRACTICE UX & SECURITY: Bersihkan password yang salah, lalu kembalikan kursor ke Username
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
                // Jika kosong, biarkan saja (jangan pindah)
                if (string.IsNullOrWhiteSpace(txtUsername.Text)) return;

                // LANGSUNG PINDAH KE PASSWORD APAPUN YANG TERJADI (Trik mengelabui Hacker)
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

        // Satpam Pos 2: Mencegat Enter di kotak Password
        private void TxtPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ProsesLogin(); // Tidak ada lagi pemaksaan (null, null) yang dibenci C#
            }
        }
        private void BtnOkAlert_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Jika Popup sedang terbuka dan user menekan Enter
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                // Panggil fungsi klik tombol OK secara manual!
                BtnCloseAlert_Click(sender, e);
            }
        }
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}