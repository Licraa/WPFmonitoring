using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks; // [Wajib ada untuk Task.Delay]

namespace MonitoringApp.ViewModels
{
    // Pastikan ViewModelBase sudah mengimplementasikan INotifyPropertyChanged
    public class MachineDetailViewModel : ViewModelBase
    {
        // Data Statis (Jarang berubah) - Pakai auto-property tidak apa-apa
        public int Id { get; set; }
        public string Line { get; set; }
        public string Name { get; set; }
        public string Process { get; set; }

        // Data Dinamis (Sering berubah) - Pakai Backing Field + OnPropertyChanged
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

        // --- LOGIKA FLASH / BLINK ---
        private bool _isJustUpdated;
        public bool IsJustUpdated
        {
            get => _isJustUpdated;
            set
            {
                // Kita tidak perlu cek if (_isJustUpdated != value) disini
                // Agar animasi bisa dipicu berulang kali meskipun nilainya diset true lagi
                _isJustUpdated = value;
                OnPropertyChanged();
            }
        }

        public async void TriggerFlash()
        {
            IsJustUpdated = true;       // Nyalakan Overlay Kuning
            await Task.Delay(500);      // Tunggu 500ms (Sama seperti JS setTimeout)
            IsJustUpdated = false;      // Matikan Overlay
        }

        public bool IsDifferentFrom(MachineDetailViewModel newData)
        {
            if (newData == null) return false;

            // --- LOGIKA ALA JAVASCRIPT ---
            // Karena di SummaryService kita sudah pakai format ".fff" (milidetik),
            // String LastUpdate akan SELALU BEDA setiap kali ada data baru dari Arduino.

            string timeOld = this.LastUpdate ?? "";
            string timeNew = newData.LastUpdate ?? "";

            // Cukup bandingkan stringnya saja. 
            // "10:00:05.100" != "10:00:05.600" -> Pasti TRUE -> Pasti BLINK.
            if (timeOld != timeNew)
            {
                return true;
            }

            // Opsional: Cek status jika jam kebetulan sama (sangat jarang terjadi sekarang)
            if (this.NilaiA0 != newData.NilaiA0) return true;

            return false;
        }
    }

    // Nested Class untuk Shift
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
        public string Downtime
        {
            get => _downtime;
            set { if (_downtime != value) { _downtime = value; OnPropertyChanged(); } }
        }

        private string _uptime;
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