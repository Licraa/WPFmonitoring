using System.ComponentModel.DataAnnotations;

namespace MonitoringApp.Models
{
    public class Line
    {
        [Key]
        public int Id { get; set; } // Ini akan jadi FK untuk tabel lain

        public int MachineCode { get; set; }
        /*PENTING: Setelah ini, buka Package Manager Console di Visual Studio dan jalankan:
        Add-Migration AddMachineCode
        Update-Database
        */

        public string? Line1 { get; set; } // Line fisik (L1, L2)
        public string LineProduction { get; set; } = string.Empty; // Nama Line (Line A, Line B)
        public string Process { get; set; } = string.Empty; // Winding, Cutting
        public string Name { get; set; } = string.Empty; // Nama Mesin
        public string? Remark { get; set; } // Catatan
    }
}
