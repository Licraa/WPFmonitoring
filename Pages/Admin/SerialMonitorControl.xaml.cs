using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MonitoringApp.Services; // Menggunakan Service yang sudah kita refactor

namespace MonitoringApp.Pages
{
    public partial class SerialMonitorControl : UserControl
    {
        // --- Dependency Services (Readonly & Injected) ---
        private readonly SerialPortService _serialService;
        private readonly RealtimeDataService _realtimeService;
        private readonly DataProcessingService _dataProcessingService;
        private readonly CsvLogService _csvService;
        private readonly MachineService _machineService;

        // --- State & Timers ---
        private DispatcherTimer _clockTimer;
        private bool _subscribed = false;
        private string _lastShiftName = "";
        private DateTime _lastShiftDate = DateTime.MinValue;

        // Log Path
        private readonly string _logFile = Path.Combine("Logs", "realtime_debug.log");
        private volatile bool _isLiveLogEnabled = false;

        // --- CONSTRUCTOR INJECTION ---
        // Parameter ini diisi otomatis oleh App.ServiceProvider
        public SerialMonitorControl(
            SerialPortService serialService,
            RealtimeDataService realtimeService,
            DataProcessingService dataProcessingService,
            CsvLogService csvService,
            MachineService machineService)
        {
            InitializeComponent();
            EnsureLogDirectory();

            // Assign Services
            _serialService = serialService;
            _realtimeService = realtimeService;
            _dataProcessingService = dataProcessingService;
            _csvService = csvService;
            _machineService = machineService;

            // Init UI
            InitPorts();

            // Set Initial State
            var info = _csvService.GetCurrentShiftInfo();
            _lastShiftName = info.shiftName;
            _lastShiftDate = info.shiftDate;
            UpdateDbStatusUI(true); // Asumsi EF Core terhubung (karena throw error jika gagal start di App.xaml)

            // Init Timer (1 Detik)
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();

            UpdateShiftDisplay();

            // Event Loading/Unloading
            this.Loaded += SerialMonitorControl_Loaded;
            this.Unloaded += SerialMonitorControl_Unloaded;
        }

        // ==================================================================
        // 1. LOGIKA TIMER & SHIFT
        // ==================================================================
        private void ClockTimer_Tick(object? sender, EventArgs e)
        {
            UpdateShiftDisplay();

            var info = _csvService.GetCurrentShiftInfo();
            if (info.shiftName != _lastShiftName || info.shiftDate != _lastShiftDate)
            {
                LogToUI($"[SYSTEM] Shift Changed: {_lastShiftName} -> {info.shiftName}");

                string shiftToProcess = _lastShiftName;
                DateTime dateToProcess = _lastShiftDate;

                _lastShiftName = info.shiftName;
                _lastShiftDate = info.shiftDate;

                // Generate Excel Report di Background
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
                txtCurrentShift.Text = $"{info.shiftName} ({DateTime.Now:HH:mm:ss})";
        }

        // ==================================================================
        // 2. LOGIKA DATA MASUK (CORE)
        // ==================================================================
        private void SerialService_DataReceived(object? sender, SerialDataEventArgs e)
        {
            var lines = e.Text.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (_isLiveLogEnabled) LogToUI($"RAW: {line}");

                // 1. Parsing
                var result = _dataProcessingService.ProcessRawData(line);

                if (!string.IsNullOrEmpty(result.ErrorMessage) && _isLiveLogEnabled)
                    LogToUI($"PARSE ERROR: {result.ErrorMessage}", true);

                if (result.IsValid && !result.IsDuplicate)
                {
                    // 2. Simpan ke Database (Via Service)
                    SaveToDatabase(result.IdKey, result.ParsedData);

                    if (_isLiveLogEnabled)
                        LogToUI($"SAVED ID: {result.IdKey}");
                }
            }
        }

        private void SaveToDatabase(int idKey, object[] data)
        {
            try
            {
                if (data.Length < 10) return;

                // A. Simpan ke SQL Server (EF Core)
                _realtimeService.SaveToDatabase(
                    idKey,
                    (int)data[1],   // A0
                    (int)data[2],   // A2
                    (float)data[3], // A4
                    (float)data[4], // AvgA4
                    (int)data[5],   // PartHours
                    (float)data[6], // DataCh1 (Sec)
                    (float)data[7], // Uptime (Sec)
                    (int)data[8],   // P_Ch1
                    (int)data[9]    // P_Uptime
                );

                // B. Simpan ke CSV (Background Task)
                Task.Run(() =>
                {
                    try
                    {
                        // Ambil Cache Info Mesin (Cepat & Efisien)
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
                        if (_isLiveLogEnabled)
                            Application.Current.Dispatcher.Invoke(() => LogToUI($"CSV Error: {ex.Message}"));
                    }
                });
            }
            catch (Exception ex)
            {
                if (_isLiveLogEnabled) LogToUI($"DB Error: {ex.Message}");
            }
        }

        // ==================================================================
        // 3. UI CONTROLS
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
                LogToUI($"Started on {port} @ {baud}");
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
            LogToUI("Serial Stopped.");

            // Finalize Excel saat stop manual
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
            // Karena pakai EF Core dan DI, kita tidak perlu reconnect manual
            // Cukup cek apakah masih bisa connect
            MessageBox.Show("Database is managed automatically by System.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ChkShowLog_Checked(object sender, RoutedEventArgs e)
        {
            _isLiveLogEnabled = true;
            LogToUI("Debug Mode: ON");
        }

        private void ChkShowLog_Unchecked(object sender, RoutedEventArgs e)
        {
            _isLiveLogEnabled = false;
            LogToUI("Debug Mode: OFF");
        }

        // ==================================================================
        // 4. HELPERS
        // ==================================================================
        private void InitPorts()
        {
            try
            {
                var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
                comboPorts.ItemsSource = ports;
                if (ports.Length > 0) comboPorts.SelectedIndex = 0;
            }
            catch { /* Ignore */ }
        }

        private void EnsureSubscribed()
        {
            if (!_subscribed)
            {
                _serialService.DataReceived += SerialService_DataReceived;
                _subscribed = true;
            }
        }

        private void LogToUI(string message, bool isError = false)
        {
            string log = $"[{DateTime.Now:HH:mm:ss}] {message}";

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (listBoxLogs == null) return;
                listBoxLogs.Items.Insert(0, log);
                if (listBoxLogs.Items.Count > 200) listBoxLogs.Items.RemoveAt(listBoxLogs.Items.Count - 1);
            });

            // Log ke File (Fire and Forget)
            Task.Run(async () => {
                try { await File.AppendAllTextAsync(_logFile, log + Environment.NewLine); } catch { }
            });
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