using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MonitoringApp.Models
{
    [Table("TrendChart_log")]
    public class DailyUptimeLog
    {
        [Key]
        public int Id { get; set; }

        public int MachineId { get; set; }

        // 👇 TAMBAHAN 1: Kolom ShiftName yang kita diskusikan tadi
        [MaxLength(20)]
        public string ShiftName { get; set; } = string.Empty;

        public DateTime LogDate { get; set; }

        public int UptimePct { get; set; }

        public int DowntimePct { get; set; }

        public int TotalCount { get; set; }

        [ForeignKey("MachineId")]
        public virtual Line Line { get; set; } = null!;
    }
}