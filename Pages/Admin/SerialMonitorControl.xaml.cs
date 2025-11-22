using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MonitoringApp.Services;
using MonitoringApp.ViewModels;
using System.Windows.Media;

namespace MonitoringApp.Pages.Admin
{
    public partial class SerialMonitorControl : UserControl
    {
        private SerialPortService _serialService;
        private readonly SummaryService _summaryService;
        private string? _selectedLine;
        private bool _subscribed = false;
        private FileSystemWatcher? _logWatcher;
        private long _logPosition = 0;

        private DatabaseService? _dbService;
        private RealtimeDataService? _realtimeService;
        private DataProcessingService _dataProcessingService;

        // existing collections
        private Dictionary<int, object[]> _lastDataById = new();
        private Dictionary<int, object[]> _localCache = new();

        private object[]? _lastData;
        private int _dataId = 0;
        private string _serialBuffer = string.Empty;

        // logging
        private readonly string _logDirectory = "Logs";
        private readonly string _logFile = Path.Combine("Logs", "realtime_debug.log");

        public event EventHandler? RequestClose;

        // Default ctor (used by designer / normal creation)
        public SerialMonitorControl()
        {
            InitializeComponent();

            EnsureLogDirectory();

            _serialService = new SerialPortService();
            _dataProcessingService = new DataProcessingService();

            InitPorts();

            this.Loaded += SerialMonitorControl_Loaded;
            this.Unloaded += SerialMonitorControl_Unloaded;

            // Inisialisasi db dan services — aman: buat _dbService dulu
            try
            {
                _dbService = new DatabaseService();
                _summaryService = new SummaryService(_dbService);
                // Buat realtime service dengan connection string yang valid
                _realtimeService = new RealtimeDataService(_dbService.GetConnection().ConnectionString);
                LogDebug("Database and RealtimeDataService initialized (default ctor).");
            }
            catch (Exception ex)
            {
                // fallback: tetap buat SummaryService dengan db jika perlu; catat error
                try
                {
                    // jika _dbService gagal, buat SummaryService dengan null tidak mungkin, jadi tangani
                    LogDebug($"Database initialization failed: {ex.Message}");
                }
                catch { }
                // Jika membutuhkan, Anda bisa menampilkan pesan atau men-disable fitur DB di UI.
                _summaryService = new SummaryService(new DatabaseService()); // minimal agar field readonly punya nilai
            }
        }

        // Overload ctor — dipanggil dari Admin.xaml.cs (pastikan compatible)
        public SerialMonitorControl(SerialPortService existingService)
        {
            InitializeComponent();

            EnsureLogDirectory();

            _serialService = existingService ?? new SerialPortService();
            _dataProcessingService = new DataProcessingService();

            InitPorts();
            EnsureSubscribed();

            // Pastikan juga inisialisasi DB & realtime service agar Admin yang memanggil ctor(SerialPortService) tetap bekerja
            try
            {
                _dbService = new DatabaseService();
                _summaryService = new SummaryService(_dbService);
                _realtimeService = new RealtimeDataService(_dbService.GetConnection().ConnectionString);
                LogDebug("Database and RealtimeDataService initialized (overload ctor).");
            }
            catch (Exception ex)
            {
                LogDebug($"Database initialization failed in overload ctor: {ex.Message}");
                // still keep app running; DB operations will be skipped with a clear debug message when attempted
                _summaryService = new SummaryService(new DatabaseService());
            }
        }

        private void EnsureLogDirectory()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                    Directory.CreateDirectory(_logDirectory);
            }
            catch { /* jangan crash app hanya karena log */ }
        }

        private void LogDebug(string message)
        {
            string text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            try
            {
                File.AppendAllText(_logFile, text + Environment.NewLine);
            }
            catch { /* ignore logging IO errors */ }

            Console.WriteLine(text);

            try
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // pastikan listBoxFilteredData ada di XAML
                    if (this.FindName("listBoxFilteredData") is ListBox lb)
                    {
                        lb.Items.Insert(0, text);
                    }
                }));
            }
            catch { /* ignore UI logging errors */ }
        }

        private void EnsureSubscribed()
        {
            if (_subscribed) return;
            try
            {
                _serialService.DataReceived += SerialService_DataReceived;
                _subscribed = true;
            }
            catch (Exception ex)
            {
                LogDebug($"EnsureSubscribed exception: {ex.Message}");
            }

            if (_serialService.IsRunning)
            {
                try
                {
                    if (this.FindName("btnStart") is Button btnStart) btnStart.IsEnabled = false;
                    if (this.FindName("btnStop") is Button btnStop) btnStop.IsEnabled = true;
                    if (this.FindName("txtStatus") is TextBlock txtStatus) txtStatus.Text = "Running";
                }
                catch { }
            }
        }

        private void StopLogWatcher()
        {
            try
            {
                if (_logWatcher != null)
                {
                    _logWatcher.EnableRaisingEvents = false;
                    _logWatcher.Changed -= LogWatcher_Changed;
                    _logWatcher.Dispose();
                    _logWatcher = null;
                }
            }
            catch (Exception ex)
            {
                LogDebug($"StopLogWatcher error: {ex.Message}");
            }
        }

        private void LogWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            try
            {
                using var fs = new FileStream(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (_logPosition > fs.Length) _logPosition = 0; // rotated or truncated
                fs.Seek(_logPosition, SeekOrigin.Begin);
                using var sr = new StreamReader(fs, Encoding.UTF8);
                var added = sr.ReadToEnd();
                _logPosition = fs.Position;
                if (string.IsNullOrEmpty(added)) return;
                var lines = added.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (this.FindName("listBoxLogs") is ListBox listBoxLogs)
                    {
                        for (int i = lines.Length - 1; i >= 0; i--)
                        {
                            listBoxLogs.Items.Insert(0, lines[i]);
                        }
                        if (listBoxLogs.Items.Count > 1000)
                        {
                            while (listBoxLogs.Items.Count > 1000) listBoxLogs.Items.RemoveAt(listBoxLogs.Items.Count - 1);
                        }
                    }
                }));
            }
            catch (Exception ex)
            {
                LogDebug($"LogWatcher_Changed error: {ex.Message}");
            }
        }

        private void InitPorts()
        {
            try
            {
                var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (this.FindName("comboPorts") is ComboBox comboPorts)
                    {
                        comboPorts.ItemsSource = ports.Length == 0 ? new[] { "COM4" } : ports;
                        comboPorts.SelectedIndex = 0;
                    }
                }));
            }
            catch (Exception ex)
            {
                LogDebug($"InitPorts error: {ex.Message}");
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (this.FindName("comboPorts") is ComboBox comboPorts)
                    {
                        comboPorts.Items.Add("COM4");
                        comboPorts.SelectedIndex = 0;
                    }
                }));
            }
        }

        // --- Event handlers kept (tidak dihapus) ---
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            var portName = (this.FindName("comboPorts") as ComboBox)?.SelectedItem?.ToString()
                           ?? "COM4";
            int baud = 115200;
            try
            {
                var sel = this.FindName("comboBaud") as ComboBoxItem;
                if (sel != null) Int32.TryParse(sel.Content.ToString(), out baud);
            }
            catch { }

            try
            {
                _serialService.EnsureConsole();
                EnsureSubscribed();
                _serialService.Start(portName, baud);

                if (this.FindName("btnStart") is Button btnStart) btnStart.IsEnabled = false;
                if (this.FindName("btnStop") is Button btnStop) btnStop.IsEnabled = true;
                if (this.FindName("txtStatus") is TextBlock txtStatus) txtStatus.Text = $"Running ({portName}@{baud})";

                LogDebug($"Serial started on {portName}@{baud}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open port: {ex.Message}", "Serial Error", MessageBoxButton.OK, MessageBoxImage.Error);
                if (this.FindName("txtStatus") is TextBlock txtStatus) txtStatus.Text = "Error";
                LogDebug($"Serial start failed: {ex.Message}");
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            try { if (_subscribed) { _serialService.DataReceived -= SerialService_DataReceived; _subscribed = false; } } catch { }
            try { _serialService.Stop(); } catch { }

            if (this.FindName("btnStart") is Button btnStart) btnStart.IsEnabled = true;
            if (this.FindName("btnStop") is Button btnStop) btnStop.IsEnabled = false;
            if (this.FindName("txtStatus") is TextBlock txtStatus) txtStatus.Text = "Stopped";

            LogDebug("Serial stopped.");
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this.FindName("listBoxLogs") is ListBox listBoxLogs) listBoxLogs.Items.Clear();
            }
            catch { }
            LogDebug("Cleared listBoxLogs.");
        }

        private void SerialService_DataReceived(object? sender, SerialDataEventArgs e)
        {
            try
            {
                var lines = e.Text.Replace("\r\n", "\n").Replace("\r", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;
                    var tokens = trimmed.Split(',').Select(t => t.Trim()).ToArray();
                    if (tokens.Length < 2) continue;
                    if (!int.TryParse(tokens[0], out int idKey)) continue;
                    var rawData = tokens.Select(value =>
                    {
                        if (int.TryParse(value, out var intValue)) return (object)intValue;
                        if (float.TryParse(value, out var floatValue)) return (object)floatValue;
                        return value;
                    }).ToArray();
                    ProcessSerialData(idKey, rawData);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (this.FindName("listBoxLogs") is ListBox listBoxLogs)
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => listBoxLogs.Items.Add($"Serial error: {ex.Message}")));
                }
                catch { }
                LogDebug($"SerialService_DataReceived error: {ex.Message}");
            }
        }

        private void ProcessSerialData(int idKey, object[] rawData)
        {
            // Check if data is all zeros (excluding ID)
            if (rawData.Skip(1).All(value => value.Equals(0)))
            {
                var comment = $"ID {idKey}: Data semua 0, diabaikan.";
                try { Application.Current.Dispatcher.BeginInvoke(new Action(() => { if (this.FindName("listBoxLogs") is ListBox lb) lb.Items.Add(comment); })); } catch { }
                LogDebug(comment);
                Console.WriteLine(comment);
                return;
            }

            // Debug log data masuk
            LogDebug($"Received ID {idKey}: {string.Join(", ", rawData.Skip(1))}");

            if (_realtimeService == null)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (this.FindName("listBoxFilteredData") is ListBox listBoxFilteredData)
                    {
                        listBoxFilteredData.Items.Insert(0, $"[DB ERROR] ID {idKey}: RealtimeDataService belum diinisialisasi. Pastikan koneksi DB berhasil.");
                    }
                }));
                LogDebug($"RealtimeDataService null when attempting to save ID {idKey}.");
            }
            else
            {
                try
                {
                    if (rawData.Length >= 10)
                    {
                        _realtimeService.SaveToDatabase(
                            idKey,
                            Convert.ToInt32(rawData[1]),
                            Convert.ToInt32(rawData[2]),
                            Convert.ToSingle(rawData[3]),
                            Convert.ToSingle(rawData[4]),
                            Convert.ToInt32(rawData[5]),
                            TimeSpan.FromSeconds(Convert.ToSingle(rawData[6])),
                            TimeSpan.FromSeconds(Convert.ToSingle(rawData[7])),
                            Convert.ToInt32(rawData[8]),
                            Convert.ToInt32(rawData[9])
                        );

                        LogDebug($"[DB OK] Data ID {idKey} berhasil disimpan.");
                    }
                    else
                    {
                        LogDebug($"[DB WARNING] Data ID {idKey} format tidak lengkap (length {rawData.Length}).");
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"[DB ERROR] ID {idKey}: {ex.Message}");
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (this.FindName("listBoxFilteredData") is ListBox listBoxFilteredData)
                        {
                            listBoxFilteredData.Items.Insert(0, $"[DB ERROR] ID {idKey}: Gagal menyimpan ke database: {ex.Message}");
                        }
                    }));
                }
            }

            // Bandingkan dengan cache per ID
            if (_localCache.TryGetValue(idKey, out var cachedData))
            {
                if (AreArraysEqual(rawData, cachedData))
                {
                    var comment = $"ID {idKey}: Data sama, diabaikan.";
                    try { Application.Current.Dispatcher.BeginInvoke(new Action(() => { if (this.FindName("listBoxLogs") is ListBox lb) lb.Items.Add(comment); })); } catch { }
                    LogDebug(comment);
                    return;
                }
            }

            // Data baru, simpan dan tampilkan di UI & terminal
            _localCache[idKey] = rawData;

            var formattedData = $"ID {idKey}: {string.Join(", ", rawData.Skip(1))}";
            try { Application.Current.Dispatcher.BeginInvoke(new Action(() => { if (this.FindName("listBoxLogs") is ListBox lb) lb.Items.Add(formattedData); })); } catch { }
            Console.WriteLine(formattedData);
        }

        private static bool AreArraysEqual(object[] data1, object[] data2)
        {
            if (data1.Length != data2.Length) return false;
            for (int i = 0; i < data1.Length; i++)
            {
                if (!data1[i].Equals(data2[i])) return false;
            }
            return true;
        }

        private void SerialMonitorControl_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                EnsureSubscribed();
            }
            catch (Exception ex)
            {
                LogDebug($"Loaded handler error: {ex.Message}");
            }
        }

        private void SerialMonitorControl_Unloaded(object? sender, RoutedEventArgs e)
        {
            try { if (_subscribed) { _serialService.DataReceived -= SerialService_DataReceived; _subscribed = false; } } catch { }
            try { StopLogWatcher(); } catch { }
            LogDebug("Control unloaded.");
        }

        private async void BtnDbConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dbService == null) _dbService = new DatabaseService();
                bool isConnected = _dbService.TestConnection();

                if (this.FindName("txtDbStatus") is TextBlock txtDbStatus)
                    txtDbStatus.Text = isConnected ? "DB: Connected" : "DB: Disconnected";
                if (this.FindName("dbStatusIndicator") is System.Windows.Shapes.Rectangle rect)
                    rect.Fill = isConnected ? Brushes.Green : Brushes.Red;

                if (!isConnected)
                {
                    MessageBox.Show("Failed to connect to the database.", "Database Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                LogDebug($"DB test connection: {(isConnected ? "Connected" : "Disconnected")}");
            }
            catch (Exception ex)
            {
                if (this.FindName("txtDbStatus") is TextBlock txtDbStatus)
                    txtDbStatus.Text = "DB: Error";
                if (this.FindName("dbStatusIndicator") is System.Windows.Shapes.Rectangle rect)
                    rect.Fill = Brushes.Red;

                MessageBox.Show($"Error connecting to database: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogDebug($"BtnDbConnect_Click error: {ex.Message}");
            }
        }
    }
}
