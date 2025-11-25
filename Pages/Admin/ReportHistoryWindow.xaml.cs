using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32; // WAJIB: Untuk SaveFileDialog

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

        // 1. LOAD FOLDER (TANGGAL)
        private void LoadFolders()
        {
            if (!Directory.Exists(_rootPath)) Directory.CreateDirectory(_rootPath);

            var directories = Directory.GetDirectories(_rootPath)
                                       .Select(d => new DirectoryInfo(d))
                                       .OrderByDescending(d => d.Name)
                                       .ToList();

            ListFolders.ItemsSource = directories;
            if (directories.Count > 0) ListFolders.SelectedIndex = 0;
        }

        // 2. PILIH FOLDER -> TAMPIL FILE
        private void ListFolders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListFolders.SelectedItem is DirectoryInfo selectedDir)
            {
                LoadFiles(selectedDir.FullName);
            }
        }

        private void LoadFiles(string folderPath)
        {
            var files = Directory.GetFiles(folderPath)
                         .Where(f => f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                         .Select(f => new FileDisplayItem(f))
                         .OrderByDescending(f => f.Name)
                         .ToList();

            ListFiles.ItemsSource = files;
        }

        // 3. DOUBLE CLICK -> LANGSUNG BUKA (Preview)
        private void ListFiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListFiles.SelectedItem is FileDisplayItem fileItem)
            {
                try
                {
                    var p = new Process();
                    p.StartInfo = new ProcessStartInfo(fileItem.FullPath) { UseShellExecute = true };
                    p.Start();
                }
                catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
            }
        }

        // 4. LOGIKA TOMBOL DOWNLOAD (SAVE AS)
        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (ListFiles.SelectedItem is FileDisplayItem fileItem)
            {
                try
                {
                    // Siapkan Dialog Simpan File
                    SaveFileDialog saveDialog = new SaveFileDialog();
                    saveDialog.FileName = fileItem.Name; // Nama default

                    // Filter sesuai ekstensi file asli
                    if (fileItem.Name.EndsWith(".xlsx"))
                        saveDialog.Filter = "Excel File|*.xlsx";
                    else if (fileItem.Name.EndsWith(".csv"))
                        saveDialog.Filter = "CSV File|*.csv";
                    else
                        saveDialog.Filter = "All Files|*.*";

                    // Tampilkan Dialog
                    if (saveDialog.ShowDialog() == true)
                    {
                        // Copy file dari sistem ke lokasi yang dipilih user
                        File.Copy(fileItem.FullPath, saveDialog.FileName, true);

                        MessageBox.Show("File successfully downloaded/saved!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                        // Opsional: Buka folder tempat file disimpan
                        // string folder = Path.GetDirectoryName(saveDialog.FileName);
                        // Process.Start("explorer.exe", folder);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Download failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a file from the list first.", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    // Helper Class untuk Tampilan
    public class FileDisplayItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string Size { get; set; }
        public string Icon { get; set; }

        public FileDisplayItem(string path)
        {
            var info = new FileInfo(path);
            Name = info.Name;
            FullPath = info.FullName;
            Size = $"{Math.Ceiling(info.Length / 1024.0)} KB";

            if (info.Extension.ToLower().Contains("xlsx")) Icon = "📊";
            else if (info.Extension.ToLower().Contains("csv")) Icon = "📝";
            else Icon = "📄";
        }
    }
}