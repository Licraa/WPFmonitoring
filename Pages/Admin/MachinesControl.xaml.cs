using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection; // WAJIB: Untuk GetRequiredService
using MonitoringApp.Services;
using MonitoringApp.ViewModels;

namespace MonitoringApp.Pages
{
    public partial class MachinesControl : UserControl
    {
        private readonly MachineService _service;

        // Cache data
        private List<MachineDetailViewModel> _allDataCache;

        public MachinesControl()
        {
            InitializeComponent();


            _service = App.ServiceProvider.GetRequiredService<MachineService>();

            _allDataCache = new List<MachineDetailViewModel>();

            LoadData();
        }

        public void OpenSpecificLine(string lineName)
        {
            var lineSummary = _allDataCache
                .GroupBy(m => m.Line)
                .Select(g => new LineSummaryItem
                {
                    LineName = g.Key
                })
                .FirstOrDefault(x => x.LineName == lineName);

            if (lineSummary != null)
            {
                var machinesInLine = _allDataCache
                    .Where(m => m.Line == lineName)
                    .OrderBy(m => m.Id)
                    .ToList();

                ListMachines.ItemsSource = machinesInLine;
                txtSelectedLine.Text = lineName;
                txtTotalMachinesDetail.Text = $"{machinesInLine.Count} Machines";

                GridLines.Visibility = Visibility.Collapsed;
                GridMachines.Visibility = Visibility.Visible;
            }
        }

        private void LoadData()
        {
            // Ambil Data Mentah dari Service (EF Core)
            _allDataCache = _service.GetAllMachines();

            // Olah Data untuk Tampilan Depan (Card Line)
            var summaryList = _allDataCache
                .GroupBy(m => m.Line)
                .Select(g => new LineSummaryItem
                {
                    LineName = g.Key,
                    TotalCount = g.Count(),
                    ActiveCount = g.Count(x => x.NilaiA0 == 1), // Sesuaikan dengan properti int NilaiA0
                    InactiveCount = g.Count(x => x.NilaiA0 != 1)
                })
                .OrderBy(x => x.LineName)
                .ToList();

            ListLines.ItemsSource = summaryList;
        }

        private void LineCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is LineSummaryItem selectedLine)
            {
                var machinesInLine = _allDataCache
                    .Where(m => m.Line == selectedLine.LineName)
                    .OrderBy(m => m.Id)
                    .ToList();

                ListMachines.ItemsSource = machinesInLine;
                txtSelectedLine.Text = selectedLine.LineName;
                txtTotalMachinesDetail.Text = $"{machinesInLine.Count} Machines";

                GridLines.Visibility = Visibility.Collapsed;
                GridMachines.Visibility = Visibility.Visible;
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            GridMachines.Visibility = Visibility.Collapsed;
            GridLines.Visibility = Visibility.Visible;
            LoadData();
        }

        private void BtnEditMachine_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is MachineDetailViewModel selectedMachine)
            {
                var editWindow = new EditMachineWindow(selectedMachine);
                editWindow.Owner = Window.GetWindow(this);
                editWindow.ShowDialog();

                if (editWindow.IsSaved)
                {
                    bool success = _service.UpdateMachine(
                        selectedMachine.Id,
                        editWindow.NewMachineCode,
                        editWindow.NewName,
                        editWindow.NewProcess,
                        editWindow.NewLine,
                        editWindow.NewRemark
                    );

                    if (success)
                    {
                        MessageBox.Show("Machine updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadData();

                        // Refresh tampilan detail jika sedang terbuka
                        if (!string.IsNullOrEmpty(txtSelectedLine.Text))
                        {
                            var updatedMachines = _allDataCache
                                 .Where(m => m.Line == txtSelectedLine.Text)
                                 .OrderBy(m => m.Id)
                                 .ToList();
                            ListMachines.ItemsSource = updatedMachines;
                        }
                    }
                    else
                    {
                        MessageBox.Show("Failed to update machine.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnAddMachine_Click(object sender, RoutedEventArgs e)
        {
            var addWindow = new AddMachineWindow(_service);
            addWindow.Owner = Window.GetWindow(this);

            
            if (addWindow.ShowDialog() == true)
            {
                
                LoadData();
            }
        }

        private void BtnDeleteMachine_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is MachineDetailViewModel selectedMachine)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to DELETE machine:\nName: {selectedMachine.Name}\nID: {selectedMachine.Id}",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    bool success = _service.DeleteMachine(selectedMachine.Id);

                    if (success)
                    {
                        MessageBox.Show("Machine deleted successfully.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadData();

                        if (!string.IsNullOrEmpty(txtSelectedLine.Text))
                        {
                            var machinesInLine = _allDataCache
                                .Where(m => m.Line == txtSelectedLine.Text)
                                .OrderBy(m => m.Id)
                                .ToList();

                            ListMachines.ItemsSource = machinesInLine;
                            txtTotalMachinesDetail.Text = $"{machinesInLine.Count} Machines";
                        }
                    }
                    else
                    {
                        MessageBox.Show("Failed to delete machine.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnCheckIds_Click(object sender, RoutedEventArgs e)
        {
            // Panggil fungsi pencari ID kosong
            var missingIds = _service.GetMissingIds();

            if (missingIds.Count > 0)
            {
                // Format tampilan agar rapi (dipisah koma)
                string idsString = string.Join(", ", missingIds);

                MessageBox.Show($"Found {missingIds.Count} Empty/Deleted IDs:\n\n{idsString}\n\n" +
                                "You can configure your Arduino to match these IDs if needed.",
                                "Available IDs",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("No empty IDs found within the current sequence.\nAll IDs from 1 to Max are currently in use.",
                                "Sequence Full",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
        }
    }

    // Class Helper Sederhana untuk Tampilan Kartu Depan
    public class LineSummaryItem
    {
        public string LineName { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int InactiveCount { get; set; }
    }
}