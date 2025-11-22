// 📁 File: MonitoringApp.Models/RealtimeDataModel.cs (Model Pemrosesan)
using System;

namespace MonitoringApp.Models
{
    public class RealtimeDataModel
    {
        public int Id { get; set; }
        public int NilaiA0 { get; set; }
        public int NilaiTerakhirA2 { get; set; }
        public double DurasiTerakhirA4 { get; set; } 
        public double RatarataTerakhirA4 { get; set; } 
        public int PartHours { get; set; }
        public TimeSpan DataCh1 { get; set; } // Downtime (dikonversi dari detik)
        public TimeSpan Uptime { get; set; }   // Uptime (dikonversi dari detik)
        public int PDatach1 { get; set; }
        public int PUptime { get; set; }

        /// <summary>
        /// Logika Caching: Membandingkan semua metrik inti termasuk Uptime/Downtime.
        /// Mengabaikan PDatach1 dan PUptime.
        /// </summary>
        public bool IsCoreDataEqual(RealtimeDataModel other)
        {
            if (other == null) return false;
            
            return Id == other.Id &&
                   NilaiA0 == other.NilaiA0 &&
                   NilaiTerakhirA2 == other.NilaiTerakhirA2 &&
                   DurasiTerakhirA4 == other.DurasiTerakhirA4 &&
                   RatarataTerakhirA4 == other.RatarataTerakhirA4 &&
                   PartHours == other.PartHours &&
                   // Membandingkan TotalSeconds untuk TimeSpan
                   DataCh1.TotalSeconds == other.DataCh1.TotalSeconds &&
                   Uptime.TotalSeconds == other.Uptime.TotalSeconds;
        }
    }
}