using System;
using System.Collections.Concurrent;
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
        private readonly SerialPortService _serialService;
        private readonly DataProcessingService _dataProcessingService;
        private readonly CsvLogService _csvService;
        private readonly IServiceScopeFactory _scopeFactory;

        // Antrean untuk Log UI dan File Log Sistem
        private readonly ConcurrentQueue<string> _logQueue = new();
        private bool _isProcessingQueue = false;

        // Antrean khusus untuk data Arduino agar tidak menghambat thread utama (Gen 2 optimization)
        private readonly ConcurrentQueue<(int id, object[] data, (string Name, string Line, string Process) meta)> _csvBuffer = new();
        private bool _isCsvWorkerRunning = false;

        // Melacak shift yang sedang aktif untuk deteksi perubahan (Auto Convert Excel)
        private string _activeShiftName;
        private DateTime _activeShiftDate;

        private readonly ConcurrentDictionary<int, object> _processingLocks = new();
        private DispatcherTimer _clockTimer;
        private bool _subscribed = false;
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

            // Inisialisasi info shift saat pertama kali dijalankan
            var initialInfo = _csvService.GetCurrentShiftInfo();
            _activeShiftName = initialInfo.shiftName;
            _activeShiftDate = initialInfo.shiftDate;

            InitPorts();
            UpdateDbStatusUI(true);

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => UpdateShiftDisplay();
            _clockTimer.Start();

            this.Loaded += SerialMonitorControl_Loaded;
            this.Unloaded += SerialMonitorControl_Unloaded;
        }

        private void EnsureSubscribed()
        {
            if (!_subscribed)
            {
                WeakEventManager<SerialPortService, SerialDataEventArgs>.AddHandler(
                    _serialService, "DataReceived", SerialService_DataReceived);
                _subscribed = true;
            }
        }

        private void SerialService_DataReceived(object? sender, SerialDataEventArgs e)
        {
            // 1. Deteksi Perubahan Shift secara otomatis untuk konversi Excel
            var currentInfo = _csvService.GetCurrentShiftInfo();
            if (currentInfo.shiftName != _activeShiftName || currentInfo.shiftDate != _activeShiftDate)
            {
                string oldShiftName = _activeShiftName;
                DateTime oldShiftDate = _activeShiftDate;

                _activeShiftName = currentInfo.shiftName;
                _activeShiftDate = currentInfo.shiftDate;

                // Konversi file shift sebelumnya ke Excel di latar belakang
                Task.Run(async () => {
                    await Task.Delay(2000); // Tunggu sisa tulis CSV terakhir selesai
                    _csvService.FinalizeExcel(oldShiftName, oldShiftDate);
                });
            }

            // 2. Proses data Arduino (Masuk per 1 detik)
            var lines = e.Text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (_showRawData) LogToRawTerminal(line);

                var result = _dataProcessingService.ProcessRawData(line);
                if (result.IsValid && !result.IsDuplicate)
                {
                    var lockObj = _processingLocks.GetOrAdd(result.IdKey, _ => new object());
                    lock (lockObj)
                    {
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var machineService = scope.ServiceProvider.GetRequiredService<MachineService>();
                            var realtimeService = scope.ServiceProvider.GetRequiredService<RealtimeDataService>();

                            int dbId = machineService.GetDbIdByArduinoCode(result.IdKey);
                            if (dbId != -1)
                            {
                                // Simpan SQL (Instan)
                                realtimeService.SaveToDatabase(dbId, (int)result.ParsedData[1], (int)result.ParsedData[2],
                                    (float)result.ParsedData[3], (float)result.ParsedData[4], (int)result.ParsedData[5],
                                    (float)result.ParsedData[6], (float)result.ParsedData[7], (int)result.ParsedData[8], (int)result.ParsedData[9]);

                                // Masukkan ke antrean CSV (Background)
                                var meta = machineService.GetMachineInfoCached(dbId);
                                _csvBuffer.Enqueue((dbId, result.ParsedData, meta));
                                if (!_isCsvWorkerRunning) StartCsvWorker();

                                Dispatcher.InvokeAsync(() => LogToEventHistory($"ID {dbId} Saved"), DispatcherPriority.Background);
                            }
                        }
                    }
                }
            }
        }

        private async Task FlushCsvQueue()
        {
            // 1. Beritahu worker untuk berhenti
            _isCsvWorkerRunning = false;
            await Task.Delay(500); // Tunggu worker benar-benar idle

            if (_csvBuffer.IsEmpty)
            {
                // Jika antrean kosong, tetap coba convert file yang ada sekarang
                await Task.Run(() => _csvService.FinalizeExcel(_activeShiftName, _activeShiftDate));
                return;
            }

            // 2. Kuras sisa antrean secara manual
            await Task.Run(() => {
                while (_csvBuffer.TryDequeue(out var item))
                {
                    try
                    {
                        _csvService.LogDataToCsv(item.id, item.meta.Name, item.meta.Line, item.meta.Process,
                            ((int)item.data[1] == 1 ? "Active" : "Inactive"), (int)item.data[2], (float)item.data[3],
                            (float)item.data[4], (int)item.data[5], TimeSpan.FromSeconds((float)item.data[6]).ToString(@"hh\:mm\:ss"),
                            TimeSpan.FromSeconds((float)item.data[7]).ToString(@"hh\:mm\:ss"));
                    }
                    catch { }
                }

                // 3. Beri jeda sedikit agar Stream closed sempurna
                Thread.Sleep(1000);

                // 4. Baru panggil konversi
                _csvService.FinalizeExcel(_activeShiftName, _activeShiftDate);
            });
        }

        private async void StartCsvWorker()
        {
            if (_isCsvWorkerRunning) return;
            _isCsvWorkerRunning = true;

            await Task.Run(async () => {
                while (_csvBuffer.TryDequeue(out var item))
                {
                    try
                    {
                        _csvService.LogDataToCsv(item.id, item.meta.Name, item.meta.Line, item.meta.Process,
                            ((int)item.data[1] == 1 ? "Active" : "Inactive"), (int)item.data[2], (float)item.data[3],
                            (float)item.data[4], (int)item.data[5], TimeSpan.FromSeconds((float)item.data[6]).ToString(@"hh\:mm\:ss"),
                            TimeSpan.FromSeconds((float)item.data[7]).ToString(@"hh\:mm\:ss"));
                    }
                    catch { }
                    await Task.Delay(10);
                }
                _isCsvWorkerRunning = false;
            });
        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _serialService.Stop();
            _subscribed = false;

            txtStatus.Text = "Saving data to Excel...";
            await FlushCsvQueue(); // Paksa tulis sisa data dan konversi ke Excel

            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
            txtStatus.Text = "Stopped - Excel Generated";
        }

        private void LogToEventHistory(string message)
        {
            string log = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Dispatcher.InvokeAsync(() => {
                if (listBoxFilteredData == null) return;
                listBoxFilteredData.Items.Insert(0, log);
                if (listBoxFilteredData.Items.Count > 20) listBoxFilteredData.Items.RemoveAt(20);
            }, DispatcherPriority.Background);

            if (_logQueue.Count > 100) _logQueue.TryDequeue(out _);
            _logQueue.Enqueue(log);
            if (!_isProcessingQueue) ProcessLogQueue();
        }

        private async void ProcessLogQueue()
        {
            if (_isProcessingQueue) return;
            _isProcessingQueue = true;
            try
            {
                while (_logQueue.TryDequeue(out var log))
                {
                    if (log != null) await File.AppendAllTextAsync(_logFile, log + Environment.NewLine);
                    await Task.Delay(50);
                }
            }
            catch { }
            finally { _isProcessingQueue = false; }
        }

        // --- UI Event Handlers (Mencegah Build Error) ---

        private void SerialMonitorControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_serialService.IsRunning)
            {
                EnsureSubscribed();
                btnStart.IsEnabled = false; btnStop.IsEnabled = true;
                txtStatus.Text = "Running (Resumed)";
            }
        }

        private async void SerialMonitorControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _clockTimer?.Stop();
            _subscribed = false;
            await FlushCsvQueue(); // Penting: Agar data tidak hilang saat pindah menu
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            listBoxLogs?.Items.Clear();
            listBoxFilteredData?.Items.Clear();
        }

        private void ChkShowLog_Checked(object sender, RoutedEventArgs e) => _showRawData = true;

        private void ChkShowLog_Unchecked(object sender, RoutedEventArgs e) => _showRawData = false;

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            var port = comboPorts.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(port)) return;
            _serialService.Start(port, 115200);
            EnsureSubscribed();
            btnStart.IsEnabled = false; btnStop.IsEnabled = true;
            txtStatus.Text = $"Running ({port})";
        }

        // --- Helpers ---
        private void InitPorts() { try { comboPorts.ItemsSource = SerialPort.GetPortNames().OrderBy(p => p).ToArray(); if (comboPorts.Items.Count > 0) comboPorts.SelectedIndex = 0; } catch { } }
        private void UpdateShiftDisplay() { var info = _csvService.GetCurrentShiftInfo(); if (txtCurrentShift != null) txtCurrentShift.Text = $"{info.shiftName} ({DateTime.Now:HH:mm:ss})"; }
        private void UpdateDbStatusUI(bool connected) { if (txtDbStatus == null) return; txtDbStatus.Text = connected ? "Connected" : "Disconnected"; dbStatusIndicator.Fill = connected ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68)); }
        private void EnsureLogDirectory() { if (!Directory.Exists("Logs")) Directory.CreateDirectory("Logs"); }
        private void LogToRawTerminal(string rawText)
        {
            Dispatcher.InvokeAsync(() => {
                if (listBoxLogs == null) return;
                listBoxLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {rawText}");
                if (listBoxLogs.Items.Count > 20) listBoxLogs.Items.RemoveAt(20);
            }, DispatcherPriority.Background);
        }
    }
}