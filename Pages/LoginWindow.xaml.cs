using System.Windows;
using System.Windows.Input;
using MonitoringApp.Pages; // Pastikan ini sesuai dengan namespace MainWindow Anda

namespace MonitoringApp
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();

            // Logika agar window bisa digeser (drag) saat diklik kiri
            this.MouseLeftButtonDown += (sender, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            // Cek username dan password
            if (txtUsername.Text == "user" && txtPassword.Password == "123")
            {
                // Buka MainWindow (Dashboard)
                var mainWin = new MainWindow(); 
                mainWin.Show();
                
                // Tutup Login Window
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