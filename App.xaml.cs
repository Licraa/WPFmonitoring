using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using MonitoringApp.Data;
using MonitoringApp.Pages;
using MonitoringApp.Services;
//using MonitoringApp.Helpers;
using MonitoringApp.Controls;

// Alias untuk menghindari bentrok nama jika ada kelas Admin di namespace lain
using AdminWindow = MonitoringApp.Pages.Admin;

namespace MonitoringApp
{
    public partial class App : Application
    {
        // Container Service Provider untuk akses global di seluruh aplikasi

        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. SETUP EXCEPTION HANDLING (Agar aplikasi tidak langsung close jika ada error)
            var args = Environment.GetCommandLineArgs();
            bool isEfDesignTime = Array.Exists(args, a =>
                a.Contains("Microsoft.EntityFrameworkCore.Design") ||
                a.Contains("ef.dll") ||
                a.Contains("efcore"));
            if (isEfDesignTime)
            {
                Shutdown();
                return;
            }

            // 1. SETUP EXCEPTION HANDLING (tidak berubah)
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            base.OnStartup(e);

            try
            {
                // 2. BACA KONFIGURASI DARI appsettings.json
                var configBuilder = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                var configuration = configBuilder.Build();
                string connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";

                // 3. DAFTARKAN SEMUA SERVICES (Dependency Injection)
                var services = new ServiceCollection();

                // Database Context
                services.AddDbContextFactory<AppDbContext>(options => {
                    options.UseSqlServer(connectionString, sqlOptions => {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorNumbersToAdd: null);
                    });
                });

                // Singleton Services (Hanya ada 1 instance selama aplikasi jalan)
                services.AddSingleton<SerialPortService>();
                services.AddSingleton<HardwareMonitorService>();
                services.AddSingleton<DataLoggingService>();
                services.AddSingleton<SettingService>();

                services.AddSingleton<RealtimeDataService>();
                services.AddSingleton<CsvLogService>();
                services.AddSingleton<DataProcessingService>();

                // Transient Services (Dibuat baru setiap kali dipanggil)
                services.AddTransient<MachineService>();
                services.AddTransient<SummaryService>();
                //services.AddTransient<RealtimeDataService>();
                services.AddTransient<SecurityHelper>();
                services.AddTransient<UserService>();
                services.AddTransient<AuthService>();
                //services.AddTransient<CsvLogService>();
                //services.AddTransient<DataProcessingService>();

                // UI Windows & Controls
                services.AddTransient<LoginWindow>();
                services.AddTransient<MainWindow>();
                services.AddTransient<AdminWindow>();
                services.AddTransient<DashboardControl>();
                services.AddTransient<SerialMonitorControl>();
                services.AddTransient<MachinesControl>();
                services.AddTransient<UserManagementControl>();
                services.AddTransient<SettingsControl>();

                // 4. BUILD SERVICE PROVIDER (Sangat Penting: Harus sebelum digunakan)
                ServiceProvider = services.BuildServiceProvider();

                // 5. INISIALISASI DATABASE (Gunakan Dispatcher untuk menghindari STA Error)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        using (var scope = ServiceProvider.CreateScope())
                        {
                            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        }
                    }
                    catch (Exception dbEx)
                    {
                        LogToCrashFile($"Database Init Error: {dbEx.Message}");
                        MessageBox.Show($"Gagal inisialisasi database: {dbEx.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });

                // 6. JALANKAN BACKGROUND SERVICES
                // Memanggil DataLoggingService agar constructor-nya jalan dan mulai monitoring
                ServiceProvider.GetRequiredService<DataLoggingService>();

                // 7. TAMPILKAN WINDOW LOGIN
                var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
            }
            catch (Exception ex)
            {
                LogToCrashFile($"Startup Critical Error: {ex.Message}");
                MessageBox.Show($"Terjadi kesalahan saat memulai aplikasi: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        // --- ERROR HANDLING HELPERS ---

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = $"[UI ERROR] {DateTime.Now}: {e.Exception.Message}\nStack Trace: {e.Exception.StackTrace}";
            LogToCrashFile(errorMessage);
            MessageBox.Show($"Terjadi kesalahan aplikasi:\n{e.Exception.Message}", "Application Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            string errorMessage = $"[SYSTEM CRASH] {DateTime.Now}: {ex.Message}\nStack Trace: {ex.StackTrace}";
            LogToCrashFile(errorMessage);
            MessageBox.Show("Terjadi kesalahan fatal pada sistem. Aplikasi akan ditutup.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void LogToCrashFile(string message)
        {
            try
            {
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string filePath = Path.Combine(folder, "crash_report.log");
                File.AppendAllText(filePath, message + Environment.NewLine + "--------------------------------------------------" + Environment.NewLine);
            }
            catch { }
        }
    }
}