using MonitoringApp.Models;
using MonitoringApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MonitoringApp.Pages
{
    public partial class SettingsControl : UserControl
    {
        private readonly SettingService _settingService;
        // Inisialisasi list agar tidak null
        private ObservableCollection<ShiftItem> _shiftList = new ObservableCollection<ShiftItem>();

        public SettingsControl(SettingService settingService)
        {
            InitializeComponent();
            _settingService = settingService;
            LoadDataFromConfig();
        }

        private void LoadDataFromConfig()
        {
            var settings = _settingService.GetSettings();
            txtConnectionString.Text = settings.ConnectionStrings?.DefaultConnection ?? "";

            var savedShifts = settings.ShiftSettings ?? new List<ShiftItem>();
            _shiftList.Clear();

            if (savedShifts.Count == 0)
            {
                // Default 3 shift jika file json kosong
                _shiftList.Add(new ShiftItem { Name = "Shift 1", StartTime = "06:30" });
                _shiftList.Add(new ShiftItem { Name = "Shift 2", StartTime = "14:30" });
                _shiftList.Add(new ShiftItem { Name = "Shift 3", StartTime = "22:30" });
            }
            else
            {
                foreach (var s in savedShifts) _shiftList.Add(s);
            }

            icShiftList.ItemsSource = _shiftList;
        }

        private void btnAddShift_Click(object sender, RoutedEventArgs e)
        {
            _shiftList.Add(new ShiftItem { Name = $"Shift {_shiftList.Count + 1}", StartTime = "00:00" });
        }

        private void btnTestDb_Click(object sender, RoutedEventArgs e)
        {
            // Panggil logika test dari service
            bool ok = _settingService.TestDatabaseConnection(txtConnectionString.Text);
            MessageBox.Show(ok ? "Koneksi Berhasil!" : "Koneksi Gagal!", "Database Test");
        }

        private void btnSaveAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Ambil data dari UI dan masukkan ke model
                var newSettings = new AppSettingsModel
                {
                    // Bagian Database
                    ConnectionStrings = new ConnectionStrings
                    {
                        DefaultConnection = txtConnectionString.Text
                    },

                    // Bagian Path Export (Jika ada)
                    // ExportPath = txtExportPath.Text,

                    // Bagian Shift (Otomatis mengambil dari ObservableCollection _shiftList)
                    ShiftSettings = _shiftList.ToList()
                };

                // 2. Panggil Service untuk menyimpan ke file fisik (appsettings.json)
                _settingService.SaveSettings(newSettings);

                // 3. Beri feedback ke user
                MessageBox.Show("Semua pengaturan berhasil diperbarui dan disimpan ke appsettings.json!",
                                "Simpan Berhasil",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal menyimpan pengaturan: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
    }
}