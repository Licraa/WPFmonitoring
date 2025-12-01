using System.Windows;
using MonitoringApp.ViewModels;

namespace MonitoringApp.Pages
{
    public partial class EditMachineWindow : Window
    {
        public int NewMachineCode { get; private set; } 
        public string NewName { get; private set; } = string.Empty;
        public string NewProcess { get; private set; } = string.Empty;
        public string NewLine { get; private set; } = string.Empty;
        public string NewRemark { get; private set; } = string.Empty; // Tambahan Remark

        public bool IsSaved { get; private set; } = false;

        public EditMachineWindow(MachineDetailViewModel machine)
        {
            InitializeComponent();
            this.DataContext = machine;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtMachineCode.Text, out int code))
            {
                MessageBox.Show("Arduino ID must be a number!");
                return;
            }
            NewMachineCode = code;
            NewName = txtName.Text;
            NewProcess = txtProcess.Text;
            NewLine = txtLine.Text;
            NewRemark = txtRemark.Text; // Ambil nilai Remark

            IsSaved = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsSaved = false;
            this.Close();
        }
    }
}
