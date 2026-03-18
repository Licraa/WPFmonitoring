using System;

namespace MonitoringApp.ViewModels
{
    /// <summary>
    /// Satu titik data harian untuk chart tren uptime/downtime
    /// </summary>
    public class DailyUptimePoint
    {
        public DateTime Date { get; set; }
        public int UptimePercent { get; set; }
        public int DowntimePercent => 100 - UptimePercent;
        public int Count { get; set; }

        /// <summary>Label singkat untuk sumbu X chart, misal "17/03"</summary>
        public string DateLabel => Date.ToString("dd/MM");

        /// <summary>Label lengkap untuk tooltip, misal "Sen 17/03"</summary>
        public string DateLabelFull => Date.ToString("ddd dd/MM");
    }
}