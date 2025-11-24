using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
        private readonly object _fileLock = new object();
        private readonly string _logPath;

        public event EventHandler<SerialDataEventArgs>? DataReceived;

        public bool IsRunning => _port != null && _port.IsOpen;

        public SerialPortService(string logFolder = "Logs")
        {
            try
            {
                Directory.CreateDirectory(logFolder);
            }
            catch
            {
                // ignore directory creation failures; will attempt to write anyway
            }
            _logPath = Path.Combine(logFolder, "serial.log");
        }

        /// <summary>
        /// Ensure a console window is available (useful when running a WPF app to see Console.WriteLine).
        /// This tries to attach to parent console first, otherwise allocates a new one.
        /// </summary>
        public void EnsureConsole()
        {
            try
            {
                // If attach fails, allocate a console
                if (!NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS))
                {
                    NativeMethods.AllocConsole();
                }
            }
            catch { }
        }

        /// <summary>
        /// Start the serial port. Defaults to COM4, 9600, 8N1.
        /// </summary>
        public void Start(string portName = "COM4", int baudRate = 115200, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
        {
            if (IsRunning) return;

            _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
            {
                Encoding = Encoding.ASCII,
                ReadTimeout = 500,
                WriteTimeout = 500,
                DtrEnable = true,  // PENTING: Untuk beberapa Arduino (Uno/Mega) agar reset saat connect
                RtsEnable = true,  // PENTING: Stabilkan koneksi
                NewLine = "\n"     // PENTING: Agar ReadLine() tahu batas baris (sesuaikan dengan Arduino Serial.println)
            };

            _port.DataReceived += Port_DataReceived;

            try
            {
                _port.Open();
                Log($"[Started] Port={portName} Baud={baudRate}");
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

            // Gunakan try-catch agar jika serial putus/error aplikasi tidak crash
            try
            {
                // Loop selagi ada data yang bisa dibaca
                // (Terkadang ReadLine bisa melempar TimeoutException, jadi kita handle)
                while (sp.BytesToRead > 0)
                {
                    try
                    {
                        // Baca satu baris penuh sampai ketemu enter (\n)
                        string line = sp.ReadLine();

                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // Fire event ke UI
                        var args = new SerialDataEventArgs
                        {
                            Data = Encoding.ASCII.GetBytes(line), // Opsional, jika butuh byte
                            Text = line.Trim(), // Bersihkan spasi/enter di ujung
                            Timestamp = DateTime.Now
                        };

                        // Panggil event secara aman
                        DataReceived?.Invoke(this, args);

                        // Log untuk debug (opsional)
                        // Log($"RX: {line.Trim()}"); 
                    }
                    catch (TimeoutException)
                    {
                        // Wajar terjadi jika data terpotong di tengah jalan, abaikan saja
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"{ex.Message}");
            }
        }

        protected virtual void Log(string message)
        {
            string line = $"{message}";
            try
            {
                Debug.WriteLine(line);
            }
            catch { }
            try
            {
                Console.WriteLine(line);
            }
            catch { }
            // Removed file logging to avoid storage burden
        }

        /// <summary>
        /// Stops and disposes the port.
        /// </summary>
        public void Stop()
        {
            if (_port == null) return;

            try
            {
                _port.DataReceived -= Port_DataReceived;
                if (_port.IsOpen) _port.Close();
                Log("[Stopped]");
            }
            catch (Exception ex)
            {
                Log($"[StopError] {ex}");
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
