using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MonitoringApp.Models
{
    // Class ini tidak jadi tabel, cuma template
    public abstract class MachineDataBase
    {
        [Key]
        public int Id { get; set; } // Kita matikan Auto Increment karena ID ikut tabel Line

        public int NilaiA0 { get; set; }
        public int NilaiTerakhirA2 { get; set; }
        public float DurasiTerakhirA4 { get; set; }
        public float RataRataTerakhirA4 { get; set; }
        public int PartHours { get; set; }

        public TimeSpan DataCh1 { get; set; } // Downtime
        public TimeSpan Uptime { get; set; }  // Uptime

        public int P_DataCh1 { get; set; }
        public int P_Uptime { get; set; }

        public DateTime Last_Update { get; set; }
    }

    // Tabel-tabel asli
    [Table("data_realtime")]
    public class DataRealtime : MachineDataBase { }

    [Table("shift_1")]
    public class Shift1 : MachineDataBase { }

    [Table("shift_2")]
    public class Shift2 : MachineDataBase { }

    [Table("shift_3")]
    public class Shift3 : MachineDataBase { }
}