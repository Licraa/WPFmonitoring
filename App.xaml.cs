using System;
using System.IO; // WAJIB: Untuk menyimpan file log
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using MonitoringApp.Data;
using MonitoringApp.Pages;
using MonitoringApp.Services;

// Alias untuk menghindari bentrok nama
using AdminWindow = MonitoringApp.Pages.Admin;

namespace MonitoringApp
{
    public partial class App : Application
    {
        // Container Service Provider
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. SETUP EXCEPTION HANDLING (Paling Awal)
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            base.OnStartup(e); 

            var services = new ServiceCollection();

            // --- 2. DAFTARKAN SERVICES ---
            services.AddDbContext<AppDbContext>();
            services.AddSingleton<SerialPortService>();

            // Core Services
            services.AddTransient<MachineService>();
            services.AddTransient<SummaryService>();
            services.AddTransient<RealtimeDataService>();
            services.AddTransient<SecurityHelper>();
            services.AddTransient<UserService>();
            services.AddTransient<AuthService>();
            services.AddTransient<CsvLogService>();
            services.AddTransient<DataProcessingService>();
            services.AddTransient<DashboardControl>();

            // --- 3. DAFTARKAN UI ---
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<AdminWindow>();
            services.AddTransient<SerialMonitorControl>();

            // Build Provider
            ServiceProvider = services.BuildServiceProvider();

            

            // --- 4. JALANKAN APLIKASI ---
            try
            {
                var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
            }
            catch (Exception ex)
            {
                // Tangkap jika gagal saat start awal (misal database error parah)
                LogToCrashFile($"Startup Failed: {ex.Message}");
                MessageBox.Show($"Startup Error: {ex.Message}", "Critical", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        // --- HANDLER ERROR UI (WPF) ---
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = $"[UI ERROR] {DateTime.Now}: {e.Exception.Message}\nStack Trace: {e.Exception.StackTrace}";

            // 1. Simpan ke File
            LogToCrashFile(errorMessage);

            // 2. Info ke User
            MessageBox.Show($"Terjadi kesalahan aplikasi:\n{e.Exception.Message}", "Application Error", MessageBoxButton.OK, MessageBoxImage.Warning);

            // 3. Cegah Crash
            e.Handled = true;
        }

        // --- HANDLER ERROR SYSTEM/BACKGROUND ---
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            string errorMessage = $"[SYSTEM CRASH] {DateTime.Now}: {ex.Message}\nStack Trace: {ex.StackTrace}";

            // 1. Simpan ke File
            LogToCrashFile(errorMessage);

            // 2. Info ke User (Aplikasi akan tutup setelah ini)
            MessageBox.Show("Terjadi kesalahan fatal pada sistem. Aplikasi akan ditutup.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // --- HELPER: TULIS LOG KE FILE ---
        private void LogToCrashFile(string message)
        {
            try
            {
                // Simpan di folder "Logs" di sebelah file .exe
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string filePath = Path.Combine(folder, "crash_report.log");

                // Tambahkan pesan error ke baris baru (Append)
                File.AppendAllText(filePath, message + Environment.NewLine + "--------------------------------------------------" + Environment.NewLine);
            }
            catch
            {
                // Jika logger gagal, diam saja agar tidak infinite loop error
            }
        }
    }
}