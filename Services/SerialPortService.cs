using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Timers; 

using Timer = System.Timers.Timer;

namespace MonitoringApp.Services
{
    /// <summary>
    /// Event args for received serial data.
    /// </summary>
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

        // --- WATCHDOG VARIABLES ---
        private Timer _watchdogTimer;
        private DateTime _lastDataTime;
        private bool _isIntentionalStop = false; // Flag: User klik tombol Stop atau bukan?
        private bool _isReconnecting = false;    // Flag: Sedang proses reconnect?

        // Simpan konfigurasi terakhir untuk auto-reconnect
        private string _lastPortName = "COM4";
        private int _lastBaudRate = 115200;
        private Parity _lastParity = Parity.None;
        private int _lastDataBits = 8;
        private StopBits _lastStopBits = StopBits.One;

        // Config: Berapa lama tidak ada data sebelum dianggap putus? (Detik)
        private const int WATCHDOG_TIMEOUT_SECONDS = 10;

        public event EventHandler<SerialDataEventArgs>? DataReceived;

        // Event tambahan untuk memberi tahu UI status koneksi berubah (Opsional tapi berguna)
        public event EventHandler<string>? ConnectionStatusChanged;

        public bool IsRunning => _port != null && _port.IsOpen;

        public SerialPortService(string logFolder = "Logs")
        {
            try
            {
                Directory.CreateDirectory(logFolder);
            }
            catch
            {
                // ignore directory creation failures
            }
            _logPath = Path.Combine(logFolder, "serial.log");

            // Setup Watchdog Timer (Cek setiap 2 detik)
            _watchdogTimer = new Timer(2000);
            _watchdogTimer.Elapsed += OnWatchdogCheck;
            _watchdogTimer.AutoReset = true;
        }

        /// <summary>
        /// Logika Watchdog: Dijalankan setiap 2 detik oleh Timer
        /// </summary>
        private void OnWatchdogCheck(object? sender, ElapsedEventArgs e)
        {
            // Jika user sengaja stop atau sedang reconnecting, jangan lakukan apa-apa
            if (_isIntentionalStop || _isReconnecting) return;

            // Hitung selisih waktu dari data terakhir
            TimeSpan timeSinceLastData = DateTime.Now - _lastDataTime;

            // Jika melebihi batas waktu (misal 10 detik)
            if (timeSinceLastData.TotalSeconds > WATCHDOG_TIMEOUT_SECONDS)
            {
                Log($"[Watchdog] No data for {timeSinceLastData.TotalSeconds:F1}s. Triggering Auto-Reconnect...");
                PerformReconnect();
            }
        }

        private void PerformReconnect()
        {
            _isReconnecting = true;
            NotifyStatus("Reconnecting...");

            try
            {
                // 1. Matikan Port Lama (Force Close)
                StopInternal(isWatchdogTrigger: true);

                // 2. Tunggu sebentar agar resource OS bersih
                System.Threading.Thread.Sleep(1000);

                // 3. Coba Start Ulang
                // Kita panggil Start() biasa, tapi settings pakai yang tersimpan
                Start(_lastPortName, _lastBaudRate, _lastParity, _lastDataBits, _lastStopBits);

                Log("[Watchdog] Reconnect Success!");
                NotifyStatus($"Recovered ({_lastPortName})");
            }
            catch (Exception ex)
            {
                Log($"[Watchdog] Reconnect Failed: {ex.Message}. Will try again next cycle.");
                // Jangan throw error agar aplikasi tidak crash.
                // Timer akan tetap jalan dan mencoba lagi di siklus berikutnya (2 detik lagi).
            }
            finally
            {
                _isReconnecting = false;
                // Reset waktu agar tidak langsung trigger lagi instan
                _lastDataTime = DateTime.Now;
            }
        }

        public void EnsureConsole()
        {
            try
            {
                if (!NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS))
                {
                    NativeMethods.AllocConsole();
                }
            }
            catch { }
        }

        public void Start(string portName = "COM4", int baudRate = 115200, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
        {
            if (IsRunning) return;

            // Simpan Config untuk Reconnect nanti
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

                // RESET STATE WATCHDOG
                _isIntentionalStop = false;
                _lastDataTime = DateTime.Now; // Anggap baru connect adalah data masuk
                _watchdogTimer.Start();       // Mulai Timer Penjaga

                Log($"[Started] Port={portName} Baud={baudRate}");
                NotifyStatus("Running");
            }
            catch (Exception ex)
            {
                Log($"[StartError] {ex.Message}");
                throw; // Lempar ke UI agar tau start awal gagal
            }
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var sp = (SerialPort)sender;

            // UPDATE WATCHDOG: Data masuk! Reset timer "kematian"
            _lastDataTime = DateTime.Now;

            try
            {
                while (sp.BytesToRead > 0)
                {
                    try
                    {
                        string line = sp.ReadLine();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var args = new SerialDataEventArgs
                        {
                            Data = Encoding.ASCII.GetBytes(line),
                            Text = line.Trim(),
                            Timestamp = DateTime.Now
                        };

                        DataReceived?.Invoke(this, args);
                    }
                    catch (TimeoutException)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[ReadError] {ex.Message}");

                // PENTING: Jika terjadi IOException (misal kabel dicabut paksa), 
                // Port biasanya langsung error fatal. Kita bisa trigger reconnect dini.
                if (ex is IOException && !_isReconnecting)
                {
                    Log("[Critical] IOException detected (Cable unplugged?). Triggering reconnect...");
                    // Jalankan di Task terpisah agar tidak memblokir thread event handler ini
                    Task.Run(() => PerformReconnect());
                }
            }
        }

        protected virtual void Log(string message)
        {
            string line = $"{DateTime.Now:HH:mm:ss} {message}";
            try
            {
                Debug.WriteLine(line);
                Console.WriteLine(line);
            }
            catch { }
        }

        private void NotifyStatus(string status)
        {
            ConnectionStatusChanged?.Invoke(this, status);
        }

        /// <summary>
        /// Stops the port explicitly (User Action).
        /// </summary>
        public void Stop()
        {
            // Set flag INTENTIONAL agar Watchdog tidak mencoba menghidupkan lagi
            _isIntentionalStop = true;
            _watchdogTimer.Stop();

            StopInternal(isWatchdogTrigger: false);
            NotifyStatus("Stopped");
        }

        /// <summary>
        /// Internal stop logic used by both User Stop and Watchdog Restart.
        /// </summary>
        private void StopInternal(bool isWatchdogTrigger)
        {
            if (_port == null) return;

            try
            {
                // Lepas event handler dulu
                _port.DataReceived -= Port_DataReceived;

                if (_port.IsOpen)
                {
                    // Discard buffer agar bersih saat start ulang
                    try { _port.DiscardInBuffer(); } catch { }
                    try { _port.DiscardOutBuffer(); } catch { }
                    _port.Close();
                }

                if (!isWatchdogTrigger) Log("[Stopped by User]");
            }
            catch (Exception ex)
            {
                Log($"[StopError] {ex.Message}");
            }
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

    internal static class NativeMethods
    {
        public const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole(uint dwProcessId);
    }
}