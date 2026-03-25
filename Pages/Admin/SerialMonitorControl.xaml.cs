using System;
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
        private readonly SerialPortService _serialService;
        private readonly CsvLogService _csvService;
        private readonly DataLoggingService _dataLoggingService;

        private bool _subscribed = false;
        private volatile bool _showRawData = false;

        public SerialMonitorControl(
            SerialPortService serialService,
            CsvLogService csvService,
            DataLoggingService dataLoggingService)
        {
            InitializeComponent();

            _serialService = serialService;
            _csvService = csvService;
            _dataLoggingService = dataLoggingService;

            InitPorts();
            UpdateDbStatusUI(true);

            this.Loaded += SerialMonitorControl_Loaded;
            this.Unloaded += SerialMonitorControl_Unloaded;
        }

        private void SerialMonitorControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Update info shift sekali saja saat halaman dibuka (tanpa timer detik)
            UpdateShiftDisplay();

            if (_serialService.IsRunning)
            {
                EnsureSubscribed();
                btnStart.IsEnabled = false;
                btnStop.IsEnabled = true;
                txtStatus.Text = "Running (Logging in background)";
            }
        }

        private void SerialMonitorControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Lepas handler untuk mencegah memory leak
            WeakEventManager<SerialPortService, SerialDataEventArgs>.RemoveHandler(
                _serialService, "DataReceived", SerialService_DataReceived);
            _subscribed = false;
        }

        private void EnsureSubscribed()
        {
            if (!_subscribed)
            {
                WeakEventManager<SerialPortService, SerialDataEventArgs>.RemoveHandler(
                    _serialService, "DataReceived", SerialService_DataReceived);

                WeakEventManager<SerialPortService, SerialDataEventArgs>.AddHandler(
                    _serialService, "DataReceived", SerialService_DataReceived);
                _subscribed = true;
            }
        }

        private void UpdateShiftDisplay()
        {
            if (txtCurrentShift == null) return;
            var info = _csvService.GetCurrentShiftInfo();
            // Menampilkan shift tanpa detik yang terus berjalan untuk hemat CPU/RAM
            txtCurrentShift.Text = $"Active: {info.shiftName}";
        }

        private void LogErrorToUI(string message)
        {
            string log = $"[{DateTime.Now:HH:mm:ss}] ERROR: {message}";
            if (listBoxFilteredData == null) return;

            Dispatcher.Invoke(() => {
                listBoxFilteredData.Items.Insert(0, log);
                if (listBoxFilteredData.Items.Count > 30)
                    listBoxFilteredData.Items.RemoveAt(30);
            });
        }

        private void SerialService_DataReceived(object? sender, SerialDataEventArgs e)
        {
            // Tampilkan data hanya jika Checkbox "Show Raw Data" dicentang
            if (_showRawData)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    LogToRawTerminal(e.Text);
                }, DispatcherPriority.Background);
            }
        }

        private void LogToRawTerminal(string rawText)
        {
            if (listBoxLogs == null) return;
            listBoxLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {rawText}");
            if (listBoxLogs.Items.Count > 15)
                listBoxLogs.Items.RemoveAt(15);
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            var port = comboPorts.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(port)) return;

            _serialService.Start(port, 115200);
            EnsureSubscribed();

            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
            txtStatus.Text = "Running";
        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _serialService.Stop();
            _subscribed = false;
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;

            try
            {
                // Finalisasi tetap dilakukan di background service
                await Task.Run(() => _dataLoggingService.ManualFinalize());
                txtStatus.Text = "Stopped - Excel Generated";
            }
            catch (Exception ex)
            {
                LogErrorToUI($"Export Failed: {ex.Message}");
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            listBoxLogs?.Items.Clear();
            listBoxFilteredData?.Items.Clear();
        }

        private void ChkShowLog_Checked(object sender, RoutedEventArgs e) => _showRawData = true;
        private void ChkShowLog_Unchecked(object sender, RoutedEventArgs e) => _showRawData = false;
        private void InitPorts() { try { comboPorts.ItemsSource = SerialPort.GetPortNames().OrderBy(p => p).ToArray(); if (comboPorts.Items.Count > 0) comboPorts.SelectedIndex = 0; } catch { } }
        private void UpdateDbStatusUI(bool connected) { if (txtDbStatus == null) return; txtDbStatus.Text = connected ? "Connected" : "Disconnected"; dbStatusIndicator.Fill = connected ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68)); }
    }
}