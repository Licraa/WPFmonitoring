using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MonitoringApp.Services;

namespace MonitoringApp.Pages
{
    public partial class SerialMonitorControl : UserControl
    {
        // --- Dependency Services ---
        private readonly SerialPortService _serialService;
        private readonly RealtimeDataService _realtimeService;
        private readonly DataProcessingService _dataProcessingService;
        private readonly CsvLogService _csvService;
        private readonly MachineService _machineService;

        // --- State ---
        private DispatcherTimer _clockTimer;
        private bool _subscribed = false;
        private string _lastShiftName = "";
        private DateTime _lastShiftDate = DateTime.MinValue;

        // --- Logs Config ---
        private readonly string _logFile = Path.Combine("Logs", "system_events.log");

        // Flag untuk mengontrol apakah RAW data ditampilkan di layar
        private volatile bool _showRawData = false;

        public SerialMonitorControl(
            SerialPortService serialService,
            RealtimeDataService realtimeService,
            DataProcessingService dataProcessingService,
            CsvLogService csvService,
            MachineService machineService)
        {
            InitializeComponent();
            EnsureLogDirectory();

            _serialService = serialService;
            _realtimeService = realtimeService;
            _dataProcessingService = dataProcessingService;
            _csvService = csvService;
            _machineService = machineService;

            InitPorts();

            // Set Initial State
            var info = _csvService.GetCurrentShiftInfo();
            _lastShiftName = info.shiftName;
            _lastShiftDate = info.shiftDate;
            UpdateDbStatusUI(true);

            // Timer Jam & Shift
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();

            UpdateShiftDisplay();

            this.Loaded += SerialMonitorControl_Loaded;
            this.Unloaded += SerialMonitorControl_Unloaded;
        }

        // ==================================================================
        // 1. LOGIKA INTI (PEMISAHAN RAW vs EVENT & MAPPING ID)
        // ==================================================================
        private void SerialService_DataReceived(object? sender, SerialDataEventArgs e)
        {
            // 1. Terima Data Mentah
            // Pisahkan baris jika ada multiple lines dalam satu paket
            var lines = e.Text.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // [AREA 1] RAW SERIAL LOG
                if (_showRawData)
                {
                    LogToRawTerminal(line);
                }

                // 2. Proses / Parsing Data Awal
                var result = _dataProcessingService.ProcessRawData(line);

                if (!string.IsNullOrEmpty(result.ErrorMessage) && _showRawData)
                {
                    LogToRawTerminal($"[PARSE FAIL] {result.ErrorMessage}");
                }

                // 3. Jika Valid -> Lakukan Mapping ID & Simpan
                if (result.IsValid && !result.IsDuplicate)
                {
                    // --- LOGIKA BARU: MAPPING ARDUINO ID KE DATABASE ID ---
                    
                    // Angka pertama yang dikirim Arduino (result.IdKey) kita anggap sebagai "Machine Code"
                    int arduinoCode = result.IdKey;
                    int realDbId = -1;

                    // Kita harus mencari ID Database asli berdasarkan Arduino Code ini.
                    // Karena event ini berjalan di background thread, kita gunakan Dispatcher 
                    // atau try-catch sederhana untuk akses EF Core (yang tidak thread-safe).
                    // Untuk keamanan maksimal, kita lakukan lookup ini secara synchronous/hati-hati.
                    try
                    {
                        // Pastikan Anda sudah menambahkan method 'GetDbIdByArduinoCode' di MachineService.cs!
                        realDbId = _machineService.GetDbIdByArduinoCode(arduinoCode);
                    }
                    catch (Exception ex)
                    {
                        LogToEventHistory($"[DB LOOKUP ERROR] {ex.Message}");
                        continue; // Skip data ini jika DB error
                    }

                    // Cek apakah mesin terdaftar
                    if (realDbId != -1)
                    {
                        // UPDATE DATA ARRAY: Ganti ID Arduino (index 0) dengan ID Database Asli
                        // Agar fungsi SaveToDatabase menyimpan ke baris yang benar.
                        result.ParsedData[0] = realDbId;

                        // Simpan ke Database & CSV menggunakan ID ASLI (realDbId)
                        SaveToDatabase(realDbId, result.ParsedData);

                        // Log Sukses (Tampilkan Mapping)
                        var info = _csvService.GetCurrentShiftInfo();
                        string successMsg = $"Arduino {arduinoCode} -> ID {realDbId} | Saved ({info.shiftName})";
                        LogToEventHistory(successMsg);
                    }
                    else
                    {
                        // JIKA TIDAK KETEMU: Tolak Data
                        // Ini terjadi jika Arduino mengirim angka, tapi belum didaftarkan di Menu Admin "Add Machine"
                        LogToEventHistory($"[WARNING] Unknown Machine Code: {arduinoCode}. Data Ignored.");
                    }
                }
            }
        }

        private void SaveToDatabase(int idKey, object[] data)
        {
            try
            {
                if (data.Length < 10) return;

                // A. Simpan SQL (EF Core) - Gunakan ID yang sudah di-mapping (idKey)
                _realtimeService.SaveToDatabase(
                    idKey,
                    (int)data[1], (int)data[2], (float)data[3], (float)data[4],
                    (int)data[5], (float)data[6], (float)data[7], (int)data[8], (int)data[9]
                );

                // B. Simpan CSV (Background)
                Task.Run(() =>
                {
                    try
                    {
                        // Ambil Info Mesin (Nama, Line, dll) berdasarkan ID Database
                        var meta = _machineService.GetMachineInfoCached(idKey);
                        
                        string status = ((int)data[1]) == 1 ? "Active" : "Inactive";
                        string tsDown = TimeSpan.FromSeconds((float)data[6]).ToString(@"hh\:mm\:ss");
                        string tsUp = TimeSpan.FromSeconds((float)data[7]).ToString(@"hh\:mm\:ss");

                        _csvService.LogDataToCsv(
                            idKey, meta.Name, meta.Line, meta.Process, status,
                            (int)data[2], (float)data[3], (float)data[4],
                            (int)data[5], tsDown, tsUp
                        );
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            LogToEventHistory($"[CSV ERROR] {ex.Message}"));
                    }
                });
            }
            catch (Exception ex)
            {
                LogToEventHistory($"[DB ERROR] {ex.Message}");
            }
        }

        // ==================================================================
        // 2. HELPER UI LOGGING
        // ==================================================================

        private void LogToRawTerminal(string rawText)
        {
            string log = $"[{DateTime.Now:HH:mm:ss}] {rawText}";

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (listBoxLogs == null) return;
                listBoxLogs.Items.Insert(0, log);

                if (listBoxLogs.Items.Count > 100)
                    listBoxLogs.Items.RemoveAt(listBoxLogs.Items.Count - 1);
            });
        }

        private void LogToEventHistory(string message)
        {
            string log = $"[{DateTime.Now:HH:mm:ss}] {message}";

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (listBoxFilteredData == null) return;
                listBoxFilteredData.Items.Insert(0, log);

                if (listBoxFilteredData.Items.Count > 200)
                    listBoxFilteredData.Items.RemoveAt(listBoxFilteredData.Items.Count - 1);
            });

            // Opsional: Simpan log file
            Task.Run(async () => {
                try { await File.AppendAllTextAsync(_logFile, log + Environment.NewLine); } catch { }
            });
        }

        // ==================================================================
        // 3. TIMER & SYSTEM EVENTS
        // ==================================================================
        private void ClockTimer_Tick(object? sender, EventArgs e)
        {
            UpdateShiftDisplay();

            var info = _csvService.GetCurrentShiftInfo();
            if (info.shiftName != _lastShiftName || info.shiftDate != _lastShiftDate)
            {
                LogToEventHistory($"[SYSTEM] Shift Change: {_lastShiftName} -> {info.shiftName}");

                string shiftToProcess = _lastShiftName;
                DateTime dateToProcess = _lastShiftDate;

                _lastShiftName = info.shiftName;
                _lastShiftDate = info.shiftDate;

                Task.Run(() =>
                {
                    _csvService.FinalizeExcel(shiftToProcess, dateToProcess);
                    Application.Current.Dispatcher.Invoke(() =>
                        LogToEventHistory($"[REPORT] Excel Generated for {shiftToProcess}"));
                });
            }
        }

        // ==================================================================
        // 4. TOMBOL CONTROLS
        // ==================================================================
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            var port = comboPorts.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(port)) { MessageBox.Show("Select Port."); return; }

            int baud = 115200;
            if (comboBaud.SelectedItem is ComboBoxItem item && int.TryParse(item.Content.ToString(), out int b)) baud = b;

            try
            {
                _serialService.Start(port, baud);
                EnsureSubscribed();

                btnStart.IsEnabled = false;
                btnStop.IsEnabled = true;
                txtStatus.Text = $"Running ({port})";

                LogToEventHistory($"[SYSTEM] Serial Started on {port} @ {baud}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed: {ex.Message}");
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _serialService.Stop();
            if (_subscribed) { _serialService.DataReceived -= SerialService_DataReceived; _subscribed = false; }

            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
            txtStatus.Text = "Stopped";

            LogToEventHistory("[SYSTEM] Serial Stopped.");

            var info = _csvService.GetCurrentShiftInfo();
            Task.Run(() => _csvService.FinalizeExcel(info.shiftName, info.shiftDate));
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            listBoxLogs.Items.Clear();
            listBoxFilteredData.Items.Clear();
        }

        private void BtnDbConnect_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Database managed automatically.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ChkShowLog_Checked(object sender, RoutedEventArgs e)
        {
            _showRawData = true;
            LogToEventHistory("Raw Monitor: ENABLED");
        }

        private void ChkShowLog_Unchecked(object sender, RoutedEventArgs e)
        {
            _showRawData = false;
            LogToEventHistory("Raw Monitor: DISABLED");
        }

        // ==================================================================
        // 5. BOILERPLATE SETUP
        // ==================================================================
        private void InitPorts()
        {
            try
            {
                var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
                comboPorts.ItemsSource = ports;
                if (ports.Length > 0) comboPorts.SelectedIndex = 0;
            }
            catch { }
        }

        private void UpdateShiftDisplay()
        {
            var info = _csvService.GetCurrentShiftInfo();
            if (txtCurrentShift != null)
                txtCurrentShift.Text = $"{info.shiftName} ({DateTime.Now:HH:mm:ss})";
        }

        private void UpdateDbStatusUI(bool connected)
        {
            txtDbStatus.Text = connected ? "Connected" : "Disconnected";
            dbStatusIndicator.Fill = connected ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        private void EnsureLogDirectory()
        {
            if (!Directory.Exists("Logs")) Directory.CreateDirectory("Logs");
        }

        private void EnsureSubscribed()
        {
            if (!_subscribed)
            {
                _serialService.DataReceived += SerialService_DataReceived;
                _subscribed = true;
            }
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
            if (_subscribed)
            {
                _serialService.DataReceived -= SerialService_DataReceived;
                _subscribed = false;
            }
            _clockTimer?.Stop();
        }
    }
}
