using System.Windows;

namespace MonitoringApp.Pages
{
    public partial class AddMachineWindow : Window
    {
        public string MachineName { get; private set; } = string.Empty;
        public string Process { get; private set; } = string.Empty;
        public string Line { get; private set; } = string.Empty;
        public string Remark { get; private set; } = string.Empty;
        public bool IsSaved { get; private set; } = false;

        public AddMachineWindow()
        {
            InitializeComponent();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validasi sederhana
            if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtLine.Text))
            {
                MessageBox.Show("Name and Line are required!", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtMachineCode.Text, out int code))
            {
                MessageBox.Show("Arduino ID must be a number!");
                return;
            }

            MachineCode = code;
            MachineName = txtName.Text;
            Process = txtProcess.Text;
            Line = txtLine.Text;
            Remark = txtRemark.Text;
            IsSaved = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
