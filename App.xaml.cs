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

// Alias untuk menghindari bentrok nama
using AdminWindow = MonitoringApp.Pages.Admin;

namespace MonitoringApp
{
    public partial class App : Application
    {
        // Container Service Provider untuk akses global
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. SETUP EXCEPTION HANDLING (Paling Awal)
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            base.OnStartup(e);

            var services = new ServiceCollection();

            // --- 2. BACA KONFIGURASI appsettings.json (Hanya Sekali) ---
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            var configuration = configBuilder.Build();
            string connectionString = configuration.GetConnectionString("DefaultConnection");

            // --- 3. DAFTARKAN SERVICES ---
            // SOLUSI MEMORY LEAK: Gunakan AddDbContextFactory
            services.AddDbContextFactory<AppDbContext>(options => {
                options.UseSqlServer(connectionString, sqlOptions => {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });
            });

            // Services bersifat Singleton
            services.AddSingleton<SerialPortService>();
            services.AddSingleton<HardwareMonitorService>();

            // Services bersifat Transient
            services.AddTransient<MachineService>();
            services.AddTransient<SummaryService>();
            services.AddTransient<RealtimeDataService>();
            services.AddTransient<SecurityHelper>();
            services.AddTransient<UserService>();
            services.AddTransient<AuthService>();
            services.AddTransient<CsvLogService>();
            services.AddTransient<DataProcessingService>();
            services.AddTransient<DashboardControl>();

            // --- 4. DAFTARKAN UI ---
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<AdminWindow>();
            services.AddTransient<SerialMonitorControl>();

            // Build Provider
            ServiceProvider = services.BuildServiceProvider();

            // --- 5. JALANKAN APLIKASI ---
            try
            {
                var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
            }
            catch (Exception ex)
            {
                LogToCrashFile($"Startup Failed: {ex.Message}");
                MessageBox.Show($"Startup Error: {ex.Message}", "Critical", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

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