using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Timers;
using Timer = System.Timers.Timer;

namespace MonitoringApp.Services
{
    public class SerialDataEventArgs : EventArgs
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string Text { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class SerialPortService : IDisposable
    {
        private SerialPort? _port;
        private readonly string _logPath;
        private Timer _watchdogTimer;
        private DateTime _lastDataTime;
        private bool _isIntentionalStop = false;
        private bool _isReconnecting = false;

        private string _lastPortName = "COM4";
        private int _lastBaudRate = 115200;
        private Parity _lastParity = Parity.None;
        private int _lastDataBits = 8;
        private StopBits _lastStopBits = StopBits.One;

        private const int WATCHDOG_TIMEOUT_SECONDS = 10;

        public event EventHandler<SerialDataEventArgs>? DataReceived;
        public event EventHandler<string>? ConnectionStatusChanged;

        public bool IsRunning => _port != null && _port.IsOpen;

        public SerialPortService(string logFolder = "Logs")
        {
            try { Directory.CreateDirectory(logFolder); } catch { }
            _logPath = Path.Combine(logFolder, "serial.log");

            _watchdogTimer = new Timer(2000);
            _watchdogTimer.Elapsed += OnWatchdogCheck;
            _watchdogTimer.AutoReset = true;
        }

        private void OnWatchdogCheck(object? sender, ElapsedEventArgs e)
        {
            if (_isIntentionalStop || _isReconnecting) return;

            TimeSpan timeSinceLastData = DateTime.Now - _lastDataTime;
            if (timeSinceLastData.TotalSeconds > WATCHDOG_TIMEOUT_SECONDS)
            {
                Log($"[Watchdog] No data for {timeSinceLastData.TotalSeconds:F1}s. Reconnecting...");
                // ?? JALANKAN SECARA ASYNC AGAR UI TIDAK MACET
                Task.Run(async () => await PerformReconnectAsync());
            }
        }

        // ?? PERBAIKAN: Menggunakan Async agar tidak ada Thread.Sleep yang memblokir aplikasi
        private async Task PerformReconnectAsync()
        {
            _isReconnecting = true;
            NotifyStatus("Reconnecting...");

            try
            {
                StopInternal(isWatchdogTrigger: true);

                // Menunggu dengan pintar, CPU tetap bisa mengerjakan tugas lain
                await Task.Delay(1000);

                Start(_lastPortName, _lastBaudRate, _lastParity, _lastDataBits, _lastStopBits);
                Log("[Watchdog] Reconnect Success!");
                NotifyStatus($"Recovered ({_lastPortName})");
            }
            catch (Exception ex)
            {
                Log($"[Watchdog] Reconnect Failed: {ex.Message}");
            }
            finally
            {
                _isReconnecting = false;
                _lastDataTime = DateTime.Now;
            }
        }

        public void Start(string portName = "COM4", int baudRate = 115200, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
        {
            if (IsRunning) return;

            _lastPortName = portName;
            _lastBaudRate = baudRate;
            _lastParity = parity;
            _lastDataBits = dataBits;
            _lastStopBits = stopBits;

            _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
            {
                Encoding = Encoding.ASCII,
                ReadTimeout = 500,
                WriteTimeout = 500,
                DtrEnable = true,
                RtsEnable = true,
                NewLine = "\n"
            };

            _port.DataReceived += Port_DataReceived;

            try
            {
                _port.Open();
                _isIntentionalStop = false;
                _lastDataTime = DateTime.Now;
                _watchdogTimer.Start();
                Log($"[Started] Port={portName}");
                NotifyStatus("Running");
            }
            catch (Exception ex)
            {
                Log($"[StartError] {ex.Message}");
                throw;
            }
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var sp = (SerialPort)sender;
            _lastDataTime = DateTime.Now;

            try
            {
                while (sp.BytesToRead > 0)
                {
                    try
                    {
                        string line = sp.ReadLine();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        DataReceived?.Invoke(this, new SerialDataEventArgs
                        {
                            Data = Encoding.ASCII.GetBytes(line),
                            Text = line.Trim(),
                            Timestamp = DateTime.Now
                        });
                    }
                    catch (TimeoutException) { break; }
                }
            }
            catch (Exception ex)
            {
                Log($"[ReadError] {ex.Message}");
                if (ex is IOException && !_isReconnecting)
                {
                    Log("[Critical] IOException. Triggering reconnect...");
                    Task.Run(async () => await PerformReconnectAsync());
                }
            }
        }

        protected virtual void Log(string message)
        {
            Debug.WriteLine($"{DateTime.Now:HH:mm:ss} {message}");
        }

        private void NotifyStatus(string status) => ConnectionStatusChanged?.Invoke(this, status);

        public void Stop()
        {
            _isIntentionalStop = true;
            _watchdogTimer.Stop();
            StopInternal(isWatchdogTrigger: false);
            NotifyStatus("Stopped");
        }

        private void StopInternal(bool isWatchdogTrigger)
        {
            if (_port == null) return;
            try
            {
                _port.DataReceived -= Port_DataReceived;
                if (_port.IsOpen)
                {
                    try { _port.DiscardInBuffer(); } catch { }
                    try { _port.DiscardOutBuffer(); } catch { }
                    _port.Close();
                }
            }
            catch (Exception ex) { Log($"[StopError] {ex.Message}"); }
            finally
            {
                try { _port.Dispose(); } catch { }
                _port = null;
            }
        }

        public void Dispose()
        {
            Stop();
            _watchdogTimer?.Dispose();
        }
    }
}