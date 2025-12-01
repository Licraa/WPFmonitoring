using System.Windows;
using MonitoringApp.Services;

namespace MonitoringApp.Pages
{
    public partial class AddMachineWindow : Window
    {
        private readonly MachineService _machineService;

        // Constructor
        public AddMachineWindow(MachineService machineService)
        {
            InitializeComponent();
            _machineService = machineService;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // 1. Ambil Input dari UI
            string name = txtName.Text;
            string process = cmbType.Text;   // UI: Type -> masuk ke DB: Process
            string line = txtLocation.Text;  // UI: Location -> masuk ke DB: Line
            string remark = "";              // Default kosong

            // 2. Validasi Input
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(process))
            {
                MessageBox.Show("Nama Mesin dan Tipe harus diisi!", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3. AUTO-ID: Minta ID kosong secara otomatis
            // (Pastikan method GetNextAvailableId sudah Anda copy ke MachineService.cs)
            int newMachineCode = _machineService.GetNextAvailableId();

            // 4. Konfirmasi
            var confirmResult = MessageBox.Show(
                $"Mesin akan didaftarkan dengan ID #{newMachineCode}.\nLanjutkan?",
                "Konfirmasi",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult == MessageBoxResult.No) return;

            // 5. Simpan ke Database (Kirim 5 PARAMETER lengkap)
            bool isSuccess = _machineService.AddMachine(name, process, line, remark, newMachineCode);

            if (isSuccess)
            {
                MessageBox.Show($"Sukses! Mesin '{name}' berhasil didaftarkan.", "Berhasil", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("Gagal menyimpan data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}