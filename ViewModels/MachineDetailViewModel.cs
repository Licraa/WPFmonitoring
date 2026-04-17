using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MonitoringApp.ViewModels
{
    public class MachineDetailViewModel : ViewModelBase
    {
        public int Id { get; set; }
        public int MachineCode { get; set; }
        public string Line { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Process { get; set; } = string.Empty;
        private string _remark = string.Empty;
        public string Remark
        {
            get => _remark;
            set { if (_remark != value) { _remark = value; OnPropertyChanged(); } }
        }

        private string _lastUpdate = string.Empty;
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

        private int _nilaiA0;
        public int NilaiA0
        {
            get => _nilaiA0;
            set
            {
                if (_nilaiA0 != value)
                {
                    _nilaiA0 = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        private string _selectedTrendShift = "Shift 1";

        public string SelectedTrendShift
        {
            get => _selectedTrendShift;
            set
            {
                if (_selectedTrendShift != value)
                {
                    _selectedTrendShift = value;
                    OnPropertyChanged();
                    TrendData.Clear();
                }
            }
        }


        public string Status => NilaiA0 == 1 ? "Active" : "Inactive";
        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    if (!value) ShowTrend = false;
                }
            }
        }
        private bool _showTrend = false;
        public bool ShowTrend
        {
            get => _showTrend;
            set
            {
                if (_showTrend != value)
                {
                    _showTrend = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowShift));
                }
            }
        }
        public bool ShowShift => !_showTrend;
        public ICommand ShowShiftCommand { get; }
        public ICommand ShowTrendCommand { get; }

        private ObservableCollection<DailyUptimePoint> _trendData = new();
        public ObservableCollection<DailyUptimePoint> TrendData
        {
            get => _trendData;
            set { _trendData = value; OnPropertyChanged(); }
        }
        private ShiftSummaryViewModel _shift1 = new();
        public ShiftSummaryViewModel Shift1
        {
            get => _shift1;
            set { _shift1 = value; OnPropertyChanged(); }
        }

        private ShiftSummaryViewModel _shift2 = new();
        public ShiftSummaryViewModel Shift2
        {
            get => _shift2;
            set { _shift2 = value; OnPropertyChanged(); }
        }

        private ShiftSummaryViewModel _shift3 = new();
        public ShiftSummaryViewModel Shift3
        {
            get => _shift3;
            set { _shift3 = value; OnPropertyChanged(); }
        }
        private bool _isJustUpdated;
        public bool IsJustUpdated
        {
            get => _isJustUpdated;
            set { _isJustUpdated = value; OnPropertyChanged(); }
        }

        public async void TriggerFlash()
        {
            IsJustUpdated = true;
            await Task.Delay(500);
            IsJustUpdated = false;
        }

        public bool IsDifferentFrom(MachineDetailViewModel newData)
        {
            if (newData == null) return false;
            if ((this.LastUpdate ?? "") != (newData.LastUpdate ?? "")) return true;
            if (this.NilaiA0 != newData.NilaiA0) return true;
            return false;
        }
        public MachineDetailViewModel()
        {
            ShowShiftCommand = new RelayCommand(_ => ShowTrend = false);
            ShowTrendCommand = new RelayCommand(_ => ShowTrend = true);
        }

        private bool _allowAnimation;
        public bool AllowAnimation
        {
            get { return _allowAnimation; }
            set
            {
                if (_allowAnimation != value)
                {
                    _allowAnimation = value;
                    OnPropertyChanged(nameof(AllowAnimation));
                }
            }
        }
    }
    public class ShiftSummaryViewModel : ViewModelBase
    {
        public int Id { get; set; }

        private int _count;
        public int Count
        {
            get => _count;
            set { if (_count != value) { _count = value; OnPropertyChanged(); } }
        }

        private string _downtime = "00:00:00";
        public string Downtime
        {
            get => _downtime;
            set { if (_downtime != value) { _downtime = value; OnPropertyChanged(); } }
        }

        private string _uptime = "00:00:00";
        public string Uptime
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