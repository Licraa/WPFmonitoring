using System.Collections.Generic;
using System.Linq; // Wajib untuk LINQ (GroupBy, Select)
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MonitoringApp.Services;
using MonitoringApp.ViewModels;

namespace MonitoringApp.Pages
{
    public partial class MachinesControl : UserControl
    {
        private readonly MachineService _service;

        // Simpan SEMUA data mentah di sini
        private List<MachineDetailViewModel> _allDataCache;

        public MachinesControl()
        {
            InitializeComponent();
            _service = new MachineService();
            _allDataCache = new List<MachineDetailViewModel>();

            LoadData();
        }

        // Method Public agar bisa dipanggil dari Admin.xaml
        public void OpenSpecificLine(string lineName)
        {
            // 1. Cari data summary untuk line tersebut
            var lineSummary = _allDataCache
                .GroupBy(m => m.Line)
                .Select(g => new LineSummaryItem
                {
                    LineName = g.Key
                    // ... properti lain tidak penting untuk navigasi ini
                })
                .FirstOrDefault(x => x.LineName == lineName);

            if (lineSummary != null)
            {
                // 2. Filter data mesin
                var machinesInLine = _allDataCache
                    .Where(m => m.Line == lineName)
                    .OrderBy(m => m.Id)
                    .ToList();

                // 3. Update UI
                ListMachines.ItemsSource = machinesInLine;
                txtSelectedLine.Text = lineName;
                txtTotalMachinesDetail.Text = $"{machinesInLine.Count} Machines";

                // 4. Paksa Ganti Tampilan ke View 2 (Detail)
                GridLines.Visibility = Visibility.Collapsed;
                GridMachines.Visibility = Visibility.Visible;
            }
        }

        private void LoadData()
        {
            // 1. Ambil Data Mentah dari DB
            _allDataCache = _service.GetAllMachines();

            // 2. Olah Data untuk Tampilan Depan (Card Line)
            // Kita Grouping data berdasarkan Nama Line
            var summaryList = _allDataCache
                .GroupBy(m => m.Line)
                .Select(g => new LineSummaryItem
                {
                    LineName = g.Key,
                    TotalCount = g.Count(),
                    ActiveCount = g.Count(x => x.Status == "Active"), // Asumsi Status di VM sudah benar
                    InactiveCount = g.Count(x => x.Status != "Active")
                })
                .OrderBy(x => x.LineName)
                .ToList();

            // 3. Masukkan ke GridLines
            ListLines.ItemsSource = summaryList;
        }

        // --- Event: Klik Kartu Line ---
        private void LineCard_Click(object sender, MouseButtonEventArgs e)
        {
            // Ambil data dari kartu yang diklik
            if (sender is Border border && border.DataContext is LineSummaryItem selectedLine)
            {
                // 1. Filter data mesin khusus line ini
                var machinesInLine = _allDataCache
                    .Where(m => m.Line == selectedLine.LineName)
                    .OrderBy(m => m.Id)
                    .ToList();

                // 2. Tampilkan ke UI GridMachines
                ListMachines.ItemsSource = machinesInLine;
                txtSelectedLine.Text = selectedLine.LineName;
                txtTotalMachinesDetail.Text = $"{machinesInLine.Count} Machines";

                // 3. Switch View
                GridLines.Visibility = Visibility.Collapsed;
                GridMachines.Visibility = Visibility.Visible;
            }
        }

        // --- Event: Tombol Back ---
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            // Kembalikan ke tampilan awal
            GridMachines.Visibility = Visibility.Collapsed;
            GridLines.Visibility = Visibility.Visible;

            // Opsional: Refresh data lagi biar update
            LoadData();
        }

        private void BtnEditMachine_Click(object sender, RoutedEventArgs e)
        {
            // 1. Ambil data dari tombol yang diklik
            if (sender is Button btn && btn.DataContext is MachineDetailViewModel selectedMachine)
            {
                // 2. Buka Window Edit (sebagai Dialog/Popup)
                var editWindow = new EditMachineWindow(selectedMachine);
                editWindow.Owner = Window.GetWindow(this); // Agar muncul di tengah window induk
        
                editWindow.ShowDialog(); // Tunggu sampai user tutup window
        
                // 3. Cek apakah user menekan Save?
                if (editWindow.IsSaved)
                {
                    // 4. Update Database
                    bool success = _service.UpdateMachine(
                        selectedMachine.Id,
                        editWindow.NewName,
                        editWindow.NewProcess,
                        editWindow.NewLine,
                        editWindow.NewRemark
                    );
        
                    if (success)
                    {
                        // =========================================================
                        // PENTING: Bersihkan Cache Service agar SerialMonitor
                        // segera mengenali nama mesin yang baru.
                        // =========================================================
                        _service.ClearCache(); 
        
                        MessageBox.Show("Machine updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        
                        // 5. Refresh Data UI
                        LoadData();
        
                        // 6. Refresh Tampilan Detail (agar tidak kembali ke menu utama)
                        // Jika user sedang melihat list mesin di line tertentu, update list itu
                        if (!string.IsNullOrEmpty(txtSelectedLine.Text))
                        {
                            var updatedMachines = _allDataCache
                                 .Where(m => m.Line == txtSelectedLine.Text) // Filter berdasarkan Line yang sedang dibuka
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
            var addWindow = new AddMachineWindow();
            addWindow.Owner = Window.GetWindow(this);
            
            addWindow.ShowDialog();

            if (addWindow.IsSaved)
            {
                bool success = _service.AddMachine(
                    addWindow.MachineName,
                    addWindow.Process,
                    addWindow.Line,
                    addWindow.Remark
                );

                if (success)
                {
                    MessageBox.Show("New machine added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData(); // Refresh tampilan
                }
                else
                {
                    MessageBox.Show("Failed to add machine.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnDeleteMachine_Click(object sender, RoutedEventArgs e)
        {
            // 1. Ambil data mesin dari tombol yang diklik
            if (sender is Button btn && btn.DataContext is MachineDetailViewModel selectedMachine)
            {
                // 2. Konfirmasi User (PENTING!)
                var result = MessageBox.Show(
                    $"Are you sure you want to DELETE machine:\n\n" +
                    $"Name: {selectedMachine.Name}\n" +
                    $"ID: {selectedMachine.Id}\n\n" +
                    "This action cannot be undone and will delete all history logs for this machine.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
        
                if (result == MessageBoxResult.Yes)
                {
                    // 3. Panggil Service Delete
                    bool success = _service.DeleteMachine(selectedMachine.Id);
        
                    if (success)
                    {
                        // =========================================================
                        // PENTING: Bersihkan Cache Service agar SerialMonitor
                        // tidak lagi mengenali ID mesin yang sudah dihapus.
                        // =========================================================
                        _service.ClearCache();
        
                        MessageBox.Show("Machine deleted successfully.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        // 4. Refresh Data UI (Ambil ulang dari DB)
                        LoadData();
        
                        // 5. Refresh Tampilan Detail (jika sedang terbuka)
                        if (!string.IsNullOrEmpty(txtSelectedLine.Text))
                        {
                            // Ambil data terbaru dari cache lokal (_allDataCache) yang baru di-LoadData()
                            var machinesInLine = _allDataCache
                                .Where(m => m.Line == txtSelectedLine.Text)
                                .OrderBy(m => m.Id)
                                .ToList();
                                
                            ListMachines.ItemsSource = machinesInLine;
                            txtTotalMachinesDetail.Text = $"{machinesInLine.Count} Machines";
                            
                            // Opsional: Jika mesin habis, bisa kembali ke tampilan line
                            if (machinesInLine.Count == 0)
                            {
                                BtnBack_Click(null, null); // Kembali ke menu utama
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Failed to delete machine. Please check database connection.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        
    }

    // Class Helper Sederhana untuk Tampilan Kartu Depan
    public class LineSummaryItem
    {
        public string LineName { get; set; }
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int InactiveCount { get; set; }
    }
}
