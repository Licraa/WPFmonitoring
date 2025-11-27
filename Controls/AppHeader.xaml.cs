using System;
using System.Windows.Controls;
using System.Windows.Threading; // Wajib ada untuk DispatcherTimer

namespace MonitoringApp.Controls
{
    public partial class AppHeader : UserControl
    {
        public AppHeader()
        {
            InitializeComponent();
            StartClock(); // Panggil fungsi untuk memulai jam
        }

        private void StartClock()
        {
            // 1. Setup Timer
            DispatcherTimer timer = new DispatcherTimer();
            
            // 2. Set interval update setiap 1 detik
            timer.Interval = TimeSpan.FromSeconds(1);
            
            // 3. Tentukan apa yang dilakukan setiap "tik" (setiap detik)
            timer.Tick += Timer_Tick;
            
            // 4. Jalankan update pertama kali agar tidak kosong saat baru dibuka
            UpdateDateTime();

            // 5. Mulai Timer
            timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateDateTime();
        }

        private void UpdateDateTime()
        {
            // Format Tanggal: Senin, 17 November 2025 (Format lengkap)
            TxtDate.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy");
            
            // Format Jam: 10:00:04 (24 jam)
            TxtTime.Text = DateTime.Now.ToString("HH:mm:ss");
        }
    }
}
