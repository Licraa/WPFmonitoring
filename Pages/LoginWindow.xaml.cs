using System.Windows;
using MonitoringApp.Pages;

namespace MonitoringApp
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            // Sederhana: cek username dan password
            if (txtUsername.Text == "user" && txtPassword.Password == "123")
            {
                // Jika login berhasil
                var mainWin = new MainWindow(); // masuk ke dashboard
                mainWin.Show();
                
                this.Close(); // Tutup login window
            }
            else
            {
                MessageBox.Show("Invalid username or password!");
            }
        }
    }
}
