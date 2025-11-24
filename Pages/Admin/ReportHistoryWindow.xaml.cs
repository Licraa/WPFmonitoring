using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MonitoringApp.Pages
{
    public partial class ReportHistoryWindow : Window
    {
        private readonly string _rootPath;

        public ReportHistoryWindow()
        {
            InitializeComponent();

            // Path folder log
            _rootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data_log_monitoring");
            
            LoadFolders();
        }

        // 1. SCAN FOLDER (Tanggal)
        private void LoadFolders()
        {
            if (!Directory.Exists(_rootPath))
            {
                Directory.CreateDirectory(_rootPath);
            }

            // Ambil semua sub-folder
            var directories = Directory.GetDirectories(_rootPath)
                                       .Select(d => new DirectoryInfo(d))
                                       .OrderByDescending(d => d.Name) // Tanggal terbaru di atas
                                       .ToList();

            ListFolders.ItemsSource = directories;
            
            // Pilih yang pertama (terbaru) otomatis
            if (directories.Count > 0) ListFolders.SelectedIndex = 0;
        }

        // 2. SAAT KLIK FOLDER -> TAMPILKAN FILE
        private void ListFolders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListFolders.SelectedItem is DirectoryInfo selectedDir)
            {
                LoadFiles(selectedDir.FullName);
            }
        }

        private void LoadFiles(string folderPath)
            {
                if (!Directory.Exists(folderPath)) return;

                var files = Directory.GetFiles(folderPath)
                                    .Where(f => f.EndsWith(".xlsx")) // HANYA AMBIL YANG .xlsx
                                    .Select(f => new FileDisplayItem(f))
                                    .OrderByDescending(f => f.Name)
                                    .ToList();

                ListFiles.ItemsSource = files;
            }

        // 3. DOUBLE KLIK FILE -> BUKA EXCEL/CSV
        private void ListFiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListFiles.SelectedItem is FileDisplayItem fileItem)
            {
                try
                {
                    var p = new Process();
                    p.StartInfo = new ProcessStartInfo(fileItem.FullPath)
                    {
                        UseShellExecute = true // Penting agar Windows membuka aplikasi default (Excel)
                    };
                    p.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Cannot open file: {ex.Message}");
                }
            }
        }

        // 4. BUKA EXPLORER MANUAL
        private void BtnOpenExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (ListFolders.SelectedItem is DirectoryInfo selectedDir)
            {
                Process.Start("explorer.exe", selectedDir.FullName);
            }
            else
            {
                Process.Start("explorer.exe", _rootPath);
            }
        }
    }

    // Class Helper untuk Tampilan File agar cantik
    public class FileDisplayItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string Size { get; set; }
        public string Icon { get; set; } // Emoji sebagai icon

        public FileDisplayItem(string path)
        {
            var info = new FileInfo(path);
            Name = info.Name;
            FullPath = info.FullName;
            
            // Hitung Size KB
            Size = $"{info.Length / 1024} KB";

            // Tentukan Icon
            if (info.Extension.ToLower().Contains("xlsx")) Icon = "📊"; // Excel
            else if (info.Extension.ToLower().Contains("csv")) Icon = "📝"; // CSV
            else Icon = "📄";
        }
    }
}