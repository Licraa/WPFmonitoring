using System.Windows;
using MonitoringApp.Services;

namespace MonitoringApp.Pages
{
    public partial class AddUserWindow : Window
    {
        private readonly UserService _userService;

        public AddUserWindow(UserService userService)
        {
            InitializeComponent();
            _userService = userService;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text;
            string password = txtPassword.Password;

            // KITA PAKSA ROLE MENJADI "User"
            // Walaupun di UI sudah dilock, kita hardcode di sini biar aman 100%
            string role = "User";

            // Validasi
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Username dan Password wajib diisi!", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Simpan
            bool isSuccess = _userService.AddUser(username, password, role);

            if (isSuccess)
            {
                MessageBox.Show("Akun User berhasil dibuat!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("Username sudah terpakai. Gunakan nama lain.", "Gagal", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}