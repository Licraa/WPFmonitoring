using System.Windows;

namespace MonitoringApp.Pages
{
    public partial class AddMachineWindow : Window
    {
        public string MachineName { get; private set; }
        public string Process { get; private set; }
        public string Line { get; private set; }
        public string Remark { get; private set; }
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