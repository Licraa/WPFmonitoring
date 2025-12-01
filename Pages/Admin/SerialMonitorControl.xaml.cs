using System;
using System.Collections.Concurrent; // WAJIB: Untuk Locking
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using MonitoringApp.Services;

namespace MonitoringApp.Pages
{
    public partial class SerialMonitorControl : UserControl
    {
        // --- Dependency Services ---
        private readonly SerialPortService _serialService;
        private readonly DataProcessingService _dataProcessingService;
        private readonly CsvLogService _csvService;
        private readonly IServiceScopeFactory _scopeFactory;

        // --- Concurrency Control (Solusi DbUpdateException) ---
        // Ini memastikan data dari mesin yang sama diproses antre (satu per satu)
        private static readonly ConcurrentDictionary<int, object> _processingLocks = new();

        // --- State ---
        private DispatcherTimer _clockTimer;
        private bool _subscribed = false;
        private string _lastShiftName = "";
        private DateTime _lastShiftDate = DateTime.MinValue;

        // --- Logs Config ---
        private readonly string _logFile = Path.Combine("Logs", "system_events.log");
        private volatile bool _showRawData = false;

        public SerialMonitorControl(
            SerialPortService serialService,
            DataProcessingService dataProcessingService,
            CsvLogService csvService,
            IServiceScopeFactory scopeFactory)
        {
            InitializeComponent();
            EnsureLogDirectory();

            _serialService = serialService;
            _dataProcessingService = dataProcessingService;
            _csvService = csvService;
            _scopeFactory = scopeFactory;

            InitPorts();

            var info = _csvService.GetCurrentShiftInfo();
            _lastShiftName = info.shiftName;
            _lastShiftDate = info.shiftDate;
            UpdateDbStatusUI(true);

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();

            UpdateShiftDisplay();

            this.Loaded += SerialMonitorControl_Loaded;
            this.Unloaded += SerialMonitorControl_Unloaded;
        }

        private void SerialService_DataReceived(object? sender, SerialDataEventArgs e)
        {
            var lines = e.Text.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (_showRawData) LogToRawTerminal(line);

                var result = _dataProcessingService.ProcessRawData(line);

                if (!string.IsNullOrEmpty(result.ErrorMessage) && _showRawData)
                {
                    LogToRawTerminal($"[PARSE FAIL] {result.ErrorMessage}");
                }

                if (result.IsValid && !result.IsDuplicate)
                {
                    // === SOLUSI 1: LOCKING (Mencegah Tabrakan Data / DbUpdateException) ===
                    // Kita kunci proses berdasarkan ID Arduino.
                    // Jika ID 101 mengirim 5 data sekaligus, mereka akan diproses antre, bukan barengan.
                    var lockObj = _processingLocks.GetOrAdd(result.IdKey, _ => new object());

                    lock (lockObj)
                    {
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var scopedMachineService = scope.ServiceProvider.GetRequiredService<MachineService>();
                            var scopedRealtimeService = scope.ServiceProvider.GetRequiredService<RealtimeDataService>();

                            int arduinoCode = result.IdKey;
                            int realDbId = -1;

                            try
                            {
                                realDbId = scopedMachineService.GetDbIdByArduinoCode(arduinoCode);
                            }
                            catch (Exception ex)
                            {
                                LogToEventHistory($"[DB LOOKUP ERROR] {ex.Message}");
                                continue;
                            }

                            if (realDbId != -1)
                            {
                                result.ParsedData[0] = realDbId;
                                SaveToDatabaseScoped(scopedRealtimeService, scopedMachineService, realDbId, result.ParsedData, arduinoCode);
                            }
                            else
                            {
                                LogToEventHistory($"[WARNING] Unknown Machine Code: {arduinoCode}. Data Ignored.");
                            }
                        }
                    }
                }
            }
        }

        private void SaveToDatabaseScoped(RealtimeDataService realtimeService, MachineService machineService, int idKey, object[] data, int arduinoCode)
        {
            try
            {
                if (data.Length < 10) return;

                // 1. Simpan SQL (Synchronous - di dalam Lock & Scope)
                realtimeService.SaveToDatabase(
                    idKey,
                    (int)data[1], (int)data[2], (float)data[3], (float)data[4],
                    (int)data[5], (float)data[6], (float)data[7], (int)data[8], (int)data[9]
                );

                // Log UI
                Application.Current.Dispatcher.Invoke(() =>
                {
                    LogToEventHistory($"Code {arduinoCode} -> DB ID {idKey} | Saved");
                });

                // === SOLUSI 2: OBJECT DISPOSED EXCEPTION ===
                // Ambil data mesin SEKARANG, saat Scope Database masih hidup.
                // Jangan panggil ini di dalam Task.Run!
                var metaInfo = machineService.GetMachineInfoCached(idKey);

                // 2. Simpan CSV (Background - Fire & Forget)
                Task.Run(() =>
                {
                    try
                    {
                        string status = ((int)data[1]) == 1 ? "Active" : "Inactive";
                        string tsDown = TimeSpan.FromSeconds((float)data[6]).ToString(@"hh\:mm\:ss");
                        string tsUp = TimeSpan.FromSeconds((float)data[7]).ToString(@"hh\:mm\:ss");

                        // Gunakan 'metaInfo' yang sudah diamankan tadi
                        _csvService.LogDataToCsv(
                            idKey, metaInfo.Name, metaInfo.Line, metaInfo.Process, status,
                            (int)data[2], (float)data[3], (float)data[4],
                            (int)data[5], tsDown, tsUp
                        );
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() => LogToEventHistory($"[CSV ERROR] {ex.Message}"));
                    }
                });
            }
            catch (Exception ex)
            {
                // Tangkap error detail agar tahu kenapa gagal
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                LogToEventHistory($"[DB ERROR] {innerMsg}");
            }
        }

        // ... (Sisa kode ke bawah sama persis: LogToRawTerminal, ClockTimer_Tick, InitPorts, dll) ...

        private void LogToRawTerminal(string rawText)
        {
            string log = $"[{DateTime.Now:HH:mm:ss}] {rawText}";
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (listBoxLogs == null) return;
                listBoxLogs.Items.Insert(0, log);
                if (listBoxLogs.Items.Count > 100) listBoxLogs.Items.RemoveAt(listBoxLogs.Items.Count - 1);
            });
        }

        private void LogToEventHistory(string message)
        {
            string log = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (listBoxFilteredData == null) return;
                listBoxFilteredData.Items.Insert(0, log);
                if (listBoxFilteredData.Items.Count > 200) listBoxFilteredData.Items.RemoveAt(listBoxFilteredData.Items.Count - 1);
            });
            Task.Run(async () => { try { await File.AppendAllTextAsync(_logFile, log + Environment.NewLine); } catch { } });
        }

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
                    Application.Current.Dispatcher.Invoke(() => LogToEventHistory($"[REPORT] Excel Generated for {shiftToProcess}"));
                });
            }
        }

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
            catch (Exception ex) { MessageBox.Show($"Failed: {ex.Message}"); }
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

        private void BtnDbConnect_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Database managed automatically.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        private void ChkShowLog_Checked(object sender, RoutedEventArgs e) { _showRawData = true; LogToEventHistory("Raw Monitor: ENABLED"); }
        private void ChkShowLog_Unchecked(object sender, RoutedEventArgs e) { _showRawData = false; LogToEventHistory("Raw Monitor: DISABLED"); }

        private void InitPorts()
        {
            try { comboPorts.ItemsSource = SerialPort.GetPortNames().OrderBy(p => p).ToArray(); if (comboPorts.Items.Count > 0) comboPorts.SelectedIndex = 0; } catch { }
        }

        private void UpdateShiftDisplay()
        {
            var info = _csvService.GetCurrentShiftInfo();
            if (txtCurrentShift != null) txtCurrentShift.Text = $"{info.shiftName} ({DateTime.Now:HH:mm:ss})";
        }

        private void UpdateDbStatusUI(bool connected)
        {
            txtDbStatus.Text = connected ? "Connected" : "Disconnected";
            dbStatusIndicator.Fill = connected ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        private void EnsureLogDirectory() { if (!Directory.Exists("Logs")) Directory.CreateDirectory("Logs"); }
        private void EnsureSubscribed() { if (!_subscribed) { _serialService.DataReceived += SerialService_DataReceived; _subscribed = true; } }
        private void SerialMonitorControl_Loaded(object sender, RoutedEventArgs e) { if (_serialService.IsRunning) { EnsureSubscribed(); btnStart.IsEnabled = false; btnStop.IsEnabled = true; txtStatus.Text = "Running (Resumed)"; } }
        private void SerialMonitorControl_Unloaded(object sender, RoutedEventArgs e) { if (_subscribed) { _serialService.DataReceived -= SerialService_DataReceived; _subscribed = false; } _clockTimer?.Stop(); }
    }
}