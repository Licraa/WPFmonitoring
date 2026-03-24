using System;

namespace MonitoringApp.Models
{
    public class MachineShiftData : MachineDataBase
    {
        // Kolom tambahan khusus untuk tabel gabungan
        public int ShiftNumber { get; set; }
    }
}