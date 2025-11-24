using System.Windows;
using MonitoringApp.ViewModels;

namespace MonitoringApp.Pages
{
    public partial class EditMachineWindow : Window
    {
        public string NewName { get; private set; }
        public string NewProcess { get; private set; }
        public string NewLine { get; private set; }
        public string NewRemark { get; private set; } // Tambahan Remark

        public bool IsSaved { get; private set; } = false;

        public EditMachineWindow(MachineDetailViewModel machine)
        {
            InitializeComponent();
            this.DataContext = machine;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
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