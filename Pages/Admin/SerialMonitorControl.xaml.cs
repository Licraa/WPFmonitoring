using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading; // Wajib untuk Timer
using Microsoft.Data.SqlClient; // Wajib untuk koneksi database
using MonitoringApp.Services;

namespace MonitoringApp.Pages
{
    public partial class SerialMonitorControl : UserControl
    {
        // --- Services ---
        private SerialPortService _serialService;
        private DatabaseService? _dbService;
        private RealtimeDataService? _realtimeService;
        private DataProcessingService _dataProcessingService;
        private CsvLogService _csvService;

        // --- State & Timers ---
        private DispatcherTimer _clockTimer;
        private bool _subscribed = false;

        // Variabel untuk melacak pergantian shift
        private string _lastShiftName = "";
        private DateTime _lastShiftDate = DateTime.MinValue;

        // --- Logging Paths ---
        private readonly string _logDirectory = "Logs";
        private readonly string _logFile = Path.Combine("Logs", "realtime_debug.log");

        public event EventHandler? RequestClose;

        // Constructor Utama
        public SerialMonitorControl(SerialPortService? existingService = null)
        {
            InitializeComponent();
            EnsureLogDirectory();

            // 1. Inisialisasi Services
            _serialService = existingService ?? new SerialPortService();
            _dataProcessingService = new DataProcessingService();
            _csvService = new CsvLogService(); // Service baru untuk CSV/Excel

            InitPorts();
            InitializeDatabaseServices();

            // 2. Set State Awal Shift
            var info = _csvService.GetCurrentShiftInfo();
            _lastShiftName = info.shiftName;
            _lastShiftDate = info.shiftDate;

            // 3. Inisialisasi Timer (Detik) untuk Shift & Jam
            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();

            // Update Tampilan Awal
            UpdateShiftDisplay();

            this.Loaded += SerialMonitorControl_Loaded;
            this.Unloaded += SerialMonitorControl_Unloaded;
        }

        // Konstruktor Default (untuk Designer)
        public SerialMonitorControl() : this(null) { }

        // ==================================================================
        // 1. LOGIKA TIMER & SHIFT CHANGE
        // ==================================================================
        private void ClockTimer_Tick(object? sender, EventArgs e)
        {
            // A. Update Tampilan Jam & Shift di UI
            UpdateShiftDisplay();

            // B. Cek Pergantian Shift Otomatis
            var info = _csvService.GetCurrentShiftInfo();

            // Jika nama shift berubah ATAU tanggal shift berubah (pergantian hari)
            if (info.shiftName != _lastShiftName || info.shiftDate != _lastShiftDate)
            {
                LogToUI($"[SYSTEM] Shift Changed: {_lastShiftName} -> {info.shiftName}");
                LogToUI($"[SYSTEM] Generating Excel Report for {_lastShiftName}...");

                // Simpan state lama untuk diproses
                string shiftToProcess = _lastShiftName;
                DateTime dateToProcess = _lastShiftDate;

                // Update tracker ke shift baru
                _lastShiftName = info.shiftName;
                _lastShiftDate = info.shiftDate;

                // Jalankan Convert Excel di Background (agar UI tidak macet)
                Task.Run(() =>
                {
                    _csvService.FinalizeExcel(shiftToProcess, dateToProcess);
                    Application.Current.Dispatcher.Invoke(() => LogToUI($"[SUCCESS] Excel Report Ready: {shiftToProcess}"));
                });
            }
        }

        private void UpdateShiftDisplay()
        {
            var info = _csvService.GetCurrentShiftInfo();
            if (txtCurrentShift != null)
            {
                txtCurrentShift.Text = $"{info.shiftName} ({DateTime.Now:HH:mm:ss})";
            }
        }

        // ==================================================================
        // 2. LOGIKA PENERIMAAN DATA & SIMPAN
        // ==================================================================
        private void SerialService_DataReceived(object? sender, SerialDataEventArgs e)
        {
            // Pisahkan baris jika data masuk bertumpuk
            var lines = e.Text.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // 1. Parsing Data (Strict Mode)
                var result = _dataProcessingService.ProcessRawData(line);

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    // Opsional: Log error parsing jika perlu debugging
                    // LogToUI($"Parse Error: {result.ErrorMessage}");
                    continue;
                }

                if (result.IsValid && !result.IsDuplicate)
                {
                    // 2. Simpan Data (SQL & CSV)
                    SaveToDatabase(result.IdKey, result.ParsedData);

                    // 3. Update Log UI
                    LogToUI($"Data Saved - ID {result.IdKey}: {string.Join(",", result.ParsedData.Skip(1))}");
                }
            }
        }

        private void SaveToDatabase(int idKey, object[] data)
        {
            if (_realtimeService == null)
            {
                LogFilteredData($"DB Error: Service not init. ID {idKey} skipped.");
                return;
            }

            try
            {
                if (data.Length >= 10)
                {
                    // Safety Check: Pastikan tidak ada data NULL
                    for (int i = 0; i < 10; i++)
                    {
                        if (data[i] == null) { LogFilteredData($"Data ID {idKey} Index {i} is NULL. Skipped."); return; }
                    }

                    // A. SIMPAN KE SQL SERVER
                    _realtimeService.SaveToDatabase(
                        idKey,
                        (int)data[1],   // nilaiA0
                        (int)data[2],   // nilaiTerakhirA2
                        (float)data[3], // durasiTerakhirA4
                        (float)data[4], // ratarataTerakhirA4
                        (int)data[5],   // parthours
                        (float)data[6], // dataCh1 (Service convert ke Time)
                        (float)data[7], // uptime (Service convert ke Time)
                        (int)data[8],   // p_datach1
                        (int)data[9]    // p_uptime
                    );

                    // B. SIMPAN KE CSV (Ringan)
                    // Ambil Metadata Mesin (Nama, Line, Process)
                    var meta = GetMachineInfoById(idKey);

                    // Hitung status & waktu string
                    string status = ((int)data[1]) == 1 ? "Active" : "Inactive";
                    string tsDown = TimeSpan.FromSeconds((float)data[6]).ToString(@"hh\:mm\:ss");
                    string tsUp = TimeSpan.FromSeconds((float)data[7]).ToString(@"hh\:mm\:ss");

                    _csvService.LogDataToCsv(
                        id: idKey,
                        name: meta.Name,
                        line: meta.Line,
                        process: meta.Process,
                        status: status,
                        count: (int)data[2],
                        cycle: (float)data[3],
                        avgCycle: (float)data[4],
                        partHours: (int)data[5],
                        downtime: tsDown,
                        uptime: tsUp
                    );

                    LogFilteredData($"ID {idKey} saved (SQL+CSV).");
                }
                else
                {
                    LogFilteredData($"ID {idKey} incomplete data.");
                }
            }
            catch (Exception ex)
            {
                LogFilteredData($"Save Error ID {idKey}: {ex.Message}");
            }
        }

        // Helper: Ambil Nama Mesin dari Database untuk CSV
        private (string Name, string Line, string Process) GetMachineInfoById(int id)
        {
            string name = "Unknown", line = "-", process = "-";
            try
            {
                if (_dbService == null) return (name, line, process);
                using var conn = _dbService.GetConnection();
                conn.Open();
                var cmd = new SqlCommand("SELECT name, line_production, process FROM line WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    name = r["name"] != DBNull.Value ? r["name"].ToString() : "Unknown";
                    line = r["line_production"] != DBNull.Value ? r["line_production"].ToString() : "-";
                    process = r["process"] != DBNull.Value ? r["process"].ToString() : "-";
                }
            }
            catch { /* Ignore, gunakan default */ }
            return (name, line, process);
        }

        // ==================================================================
        // 3. UI EVENTS (Start, Stop, Connect, Clear)
        // ==================================================================
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            var portName = comboPorts.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(portName)) { MessageBox.Show("Select Port."); return; }

            int baud = 115200;
            if (comboBaud.SelectedItem is ComboBoxItem item && int.TryParse(item.Content.ToString(), out int b)) baud = b;

            try
            {
                _serialService.Start(portName, baud);
                EnsureSubscribed();
                btnStart.IsEnabled = false;
                btnStop.IsEnabled = true;
                txtStatus.Text = $"Running ({portName})";
                LogToUI($"Started on {portName} @ {baud}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed: {ex.Message}");
                LogToUI($"Start Error: {ex.Message}");
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Hentikan Serial
                _serialService.Stop();
                if (_subscribed) { _serialService.DataReceived -= SerialService_DataReceived; _subscribed = false; }

                btnStart.IsEnabled = true;
                btnStop.IsEnabled = false;
                txtStatus.Text = "Stopped";
                LogToUI("Serial Stopped.");

                // 2. GENERATE EXCEL SAAT STOP (Finalisasi sesi)
                LogToUI("[SYSTEM] Finalizing Excel Report...");
                var info = _csvService.GetCurrentShiftInfo();

                Task.Run(() =>
                {
                    _csvService.FinalizeExcel(info.shiftName, info.shiftDate);
                    Application.Current.Dispatcher.Invoke(() => LogToUI("[SUCCESS] Excel Report Generated."));
                });
            }
            catch (Exception ex) { LogToUI($"Stop Error: {ex.Message}"); }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            listBoxLogs.Items.Clear();
            listBoxFilteredData.Items.Clear();
        }

        private void BtnDbConnect_Click(object sender, RoutedEventArgs e)
        {
            InitializeDatabaseServices();
        }

        // ==================================================================
        // 4. SYSTEM HELPERS (Init, Log, Config)
        // ==================================================================
        private void InitializeDatabaseServices()
        {
            try
            {
                _dbService = new DatabaseService();
                if (_dbService.TestConnection())
                {
                    _realtimeService = new RealtimeDataService(_dbService.GetConnection().ConnectionString);
                    UpdateDbStatusUI(true);
                    LogToUI("Database Connected & Service Ready.");
                }
                else
                {
                    UpdateDbStatusUI(false);
                    LogToUI("Database Connected but Test Failed.");
                }
            }
            catch (Exception ex)
            {
                UpdateDbStatusUI(false);
                LogToUI($"Database Init Error: {ex.Message}");
            }
        }

        private void InitPorts()
        {
            try
            {
                var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
                comboPorts.ItemsSource = ports;
                if (ports.Length > 0) comboPorts.SelectedIndex = 0;
                else LogToUI("No Serial Ports found.");
            }
            catch (Exception ex) { LogToUI($"Error getting ports: {ex.Message}"); }
        }

        private void LogToUI(string message, bool isError = false)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string fullLog = $"[{timestamp}] {message}";

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (listBoxLogs == null) return;
                listBoxLogs.Items.Insert(0, fullLog);
                if (listBoxLogs.Items.Count > 500) listBoxLogs.Items.RemoveAt(listBoxLogs.Items.Count - 1);
            });

            Task.Run(async () =>
            {
                try { await File.AppendAllTextAsync(_logFile, fullLog + Environment.NewLine); } catch { }
            });
            Console.WriteLine(fullLog);
        }

        private void LogFilteredData(string message)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (listBoxFilteredData == null) return;
                listBoxFilteredData.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
                if (listBoxFilteredData.Items.Count > 100) listBoxFilteredData.Items.RemoveAt(listBoxFilteredData.Items.Count - 1);
            });
        }

        private void EnsureSubscribed()
        {
            if (!_subscribed) { _serialService.DataReceived += SerialService_DataReceived; _subscribed = true; }
        }

        private void UpdateDbStatusUI(bool connected)
        {
            txtDbStatus.Text = connected ? "Connected" : "Disconnected";
            dbStatusIndicator.Fill = connected ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        private void EnsureLogDirectory()
        {
            if (!Directory.Exists(_logDirectory)) Directory.CreateDirectory(_logDirectory);
        }

        private void SerialMonitorControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_serialService.IsRunning)
            {
                EnsureSubscribed();
                btnStart.IsEnabled = false;
                btnStop.IsEnabled = true;
                txtStatus.Text = "Running (Resumed)";
            }
        }

        private void SerialMonitorControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_subscribed) { _serialService.DataReceived -= SerialService_DataReceived; _subscribed = false; }
            _clockTimer?.Stop();
        }
    }
}