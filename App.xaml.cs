using System;
using System.Windows;
using MonitoringApp.Pages; // Sesuaikan jika LoginWindow ada di namespace lain

namespace MonitoringApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Coba jalankan LoginWindow manual
                var loginWindow = new LoginWindow();
                loginWindow.Show();
            }
            catch (Exception ex)
            {
                // Jika error, tampilkan pesan aslinya!
                // Ini akan memberitahu kita jika DatabaseService gagal load atau connection string null
                string msg = $"Startup Error: {ex.Message}";
                if (ex.InnerException != null)
                {
                    msg += $"\n\nDetail: {ex.InnerException.Message}";
                }

                MessageBox.Show(msg, "CRITICAL ERROR", MessageBoxButton.OK, MessageBoxImage.Error);

                // Matikan aplikasi
                Shutdown();
            }
        }
    }
}