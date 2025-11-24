using System;

namespace MonitoringApp.ViewModels
{
    // Warisi ViewModelBase
    public class MachineDetailViewModel : ViewModelBase
    {
        public int Id { get; set; }
        public string Line { get; set; }
        public string Name { get; set; }
        public string Process { get; set; }

        private string _remark;
        public string Remark
        {
            get => _remark;
            set { if (_remark != value) { _remark = value; OnPropertyChanged(); } }
        }

        private string _lastUpdate;
        public string LastUpdate
        {
            get => _lastUpdate;
            set { if (_lastUpdate != value) { _lastUpdate = value; OnPropertyChanged(); } }
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

        // --- SPECIAL CASE: NilaiA0 mempengaruhi Status ---
        private int _nilaiA0;
        public int NilaiA0
        {
            get => _nilaiA0;
            set
            {
                if (_nilaiA0 != value)
                {
                    _nilaiA0 = value;
                    OnPropertyChanged();           // Update nilai integer
                    OnPropertyChanged(nameof(Status)); // Update string "Active"/"Inactive"
                }
            }
        }

        // Property ini readonly, nilainya bergantung pada NilaiA0
        public string Status => NilaiA0 == 1 ? "Active" : "Inactive";

        // Objek Shift (Nested ViewModels)
        private ShiftSummaryViewModel _shift1;
        public ShiftSummaryViewModel Shift1
        {
            get => _shift1;
            set { _shift1 = value; OnPropertyChanged(); }
        }

        private ShiftSummaryViewModel _shift2;
        public ShiftSummaryViewModel Shift2
        {
            get => _shift2;
            set { _shift2 = value; OnPropertyChanged(); }
        }

        private ShiftSummaryViewModel _shift3;
        public ShiftSummaryViewModel Shift3
        {
            get => _shift3;
            set { _shift3 = value; OnPropertyChanged(); }
        }
    }

    // Nested Class juga perlu mewarisi ViewModelBase agar properti di dalamnya bisa update realtime
    public class ShiftSummaryViewModel : ViewModelBase
    {
        public int Id { get; set; }

        private int _count;
        public int Count
        {
            get => _count;
            set { if (_count != value) { _count = value; OnPropertyChanged(); } }
        }

        private string _downtime;
        public string Downtime // format jam:menit:detik
        {
            get => _downtime;
            set { if (_downtime != value) { _downtime = value; OnPropertyChanged(); } }
        }

        private string _uptime;
        public string Uptime // format jam:menit:detik
        {
            get => _uptime;
            set { if (_uptime != value) { _uptime = value; OnPropertyChanged(); } }
        }

        private int _downtimePercent;
        public int DowntimePercent
        {
            get => _downtimePercent;
            set { if (_downtimePercent != value) { _downtimePercent = value; OnPropertyChanged(); } }
        }

        private int _uptimePercent;
        public int UptimePercent
        {
            get => _uptimePercent;
            set { if (_uptimePercent != value) { _uptimePercent = value; OnPropertyChanged(); } }
        }
    }
}