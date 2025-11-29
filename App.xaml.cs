using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MonitoringApp.Data;
using MonitoringApp.Pages;
using MonitoringApp.Services;
// Tambahkan alias agar tidak bingung
using AdminWindow = MonitoringApp.Pages.Admin;

namespace MonitoringApp
{
    public partial class App : Application
    {
        // Container untuk menyimpan semua service kita
        public static IServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();

            // --- 1. DAFTARKAN DATABASE & CORE SERVICES ---
            // DbContext (EF Core)
            services.AddDbContext<AppDbContext>();

            // SerialPortService (Singleton: Agar port tetap terbuka meski pindah halaman)
            services.AddSingleton<SerialPortService>();

            // Services Lain (Transient: Dibuat baru setiap kali diminta)
            services.AddTransient<MachineService>();
            services.AddTransient<SummaryService>();
            services.AddTransient<RealtimeDataService>();
            services.AddTransient<AuthService>();
            services.AddTransient<CsvLogService>();
            services.AddTransient<DataProcessingService>();

            // --- 2. DAFTARKAN UI (WINDOW & PAGES) ---
            services.AddTransient<LoginWindow>();

            // MainWindow butuh parameter userRole, kita daftarkan factory-nya nanti manual di LoginWindow 
            // atau biarkan Transient biasa tapi resolve manual.
            services.AddTransient<MainWindow>();

            services.AddTransient<AdminWindow>();
            services.AddTransient<SerialMonitorControl>(); // Daftarkan UserControl juga

            // Build Provider
            ServiceProvider = services.BuildServiceProvider();

            // --- 3. JALANKAN APLIKASI ---
            // Buka LoginWindow pertama kali
            var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Show();
        }
    }
}