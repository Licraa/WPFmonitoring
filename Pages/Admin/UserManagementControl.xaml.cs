using System.Windows;
using MonitoringApp.Services;
// using MonitoringApp.Models; // Uncomment jika error

namespace MonitoringApp.Pages
{
    public partial class AddMachineWindow : Window
    {
        private readonly MachineService _machineService;

        // Constructor Injection
        public AddMachineWindow(MachineService machineService)
        {
            InitializeComponent();
            _machineService = machineService;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string name = txtName.Text;
            string type = cmbType.Text; // Support input manual karena IsEditable="True"
            string location = txtLocation.Text;

            // 1. Validasi Input
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type))
            {
                MessageBox.Show("Nama Mesin dan Tipe harus diisi!", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Panggil Service untuk simpan ke Database
            // Pastikan method AddMachine ada di MachineService Anda
            bool isSuccess = _machineService.AddMachine(name, type, location);

            if (isSuccess)
            {
                MessageBox.Show("Data Mesin berhasil disimpan!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true; // Tutup window & beritahu parent sukses
                this.Close();
            }
            else
            {
                MessageBox.Show("Gagal menyimpan. Kemungkinan nama mesin sudah ada.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}