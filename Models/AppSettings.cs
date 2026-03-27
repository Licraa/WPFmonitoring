namespace MonitoringApp.Models
{
    public class AppSettingsModel
    {
        // Pastikan kelas ConnectionStrings didefinisikan di bawah atau di file yang sama
        public ConnectionStrings ConnectionStrings { get; set; } = new();
        public List<ShiftItem> ShiftSettings { get; set; } = new();
        public List<ShiftItem> SaturdayShiftSettings { get; set; } = new();
    }

    public class ConnectionStrings
    {
        public string DefaultConnection { get; set; } = "";
    }

    public class ShiftItem
    {
        public string Name { get; set; } = "";
        public string StartTime { get; set; } = "";
    }
}