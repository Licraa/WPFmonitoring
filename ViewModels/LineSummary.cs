using MonitoringApp.Models;

namespace MonitoringApp.ViewModels
{
    // 1. Warisi ViewModelBase
    public class LineSummary : ViewModelBase
    {
        public string lineProduction { get; set; } = string.Empty;// Identifier (biasanya statis)

        // 2. Ubah properti angka menjadi Full Property dengan OnPropertyChanged
        private int _active;
        public int Active
        {
            get => _active;
            set { if (_active != value) { _active = value; OnPropertyChanged(); } }
        }

        private int _inactive;
        public int Inactive
        {
            get => _inactive;
            set { if (_inactive != value) { _inactive = value; OnPropertyChanged(); } }
        }

        private int _totalMachine;
        public int TotalMachine
        {
            get => _totalMachine;
            set { if (_totalMachine != value) { _totalMachine = value; OnPropertyChanged(); } }
        }

        private int _machineCount;
        public int MachineCount
        {
            get => _machineCount;
            set { if (_machineCount != value) { _machineCount = value; OnPropertyChanged(); } }
        }

        private int _count;
        public int Count
        {
            get => _count;
            set { if (_count != value) { _count = value; OnPropertyChanged(); } }
        }

        private double _partHours;
        public double PartHours
        {
            get => _partHours;
            set { if (_partHours != value) { _partHours = value; OnPropertyChanged(); } }
        }

        private double _cycle;
        public double Cycle
        {
            get => _cycle;
            set { if (_cycle != value) { _cycle = value; OnPropertyChanged(); } }
        }

        private double _avgCycle;
        public double AvgCycle
        {
            get => _avgCycle;
            set { if (_avgCycle != value) { _avgCycle = value; OnPropertyChanged(); } }
        }

        private double _downtime;
        public double Downtime
        {
            get => _downtime;
            set { if (Math.Abs(_downtime - value) > 0.001) { _downtime = value; OnPropertyChanged(); } }
        }

        private double _uptime;
        public double Uptime
        {
            get => _uptime;
            set { if (Math.Abs(_uptime - value) > 0.001) { _uptime = value; OnPropertyChanged(); } }
        }

        private double _downtimePercent;
        public double DowntimePercent
        {
            get => _downtimePercent;
            set { if (Math.Abs(_downtimePercent - value) > 0.001) { _downtimePercent = value; OnPropertyChanged(); } }
        }

        private double _uptimePercent;
        public double UptimePercent
        {
            get => _uptimePercent;
            set { if (Math.Abs(_uptimePercent - value) > 0.001) { _uptimePercent = value; OnPropertyChanged(); } }
        }
    }
}