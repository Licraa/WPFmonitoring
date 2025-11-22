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

    /// <summary>
    /// Simple SerialPort reader service.
    /// - Starts a SerialPort on the given port (default COM4).
    /// - Raises DataReceived events.
    /// - Logs to Debug, Console (if present) and to a file under Logs/serial.log.
    /// Usage sample:
    /// var svc = new SerialPortService();
    /// svc.EnsureConsole(); // optional: creates a console window for WPF
    /// svc.DataReceived += (s,e) => Console.WriteLine($"RX: {e.Text}");
    /// svc.Start("COM4", 9600);
    /// ... svc.Stop(); svc.Dispose();
    /// </summary>
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
                WriteTimeout = 500
            };

            _port.DataReceived += Port_DataReceived;

            try
            {
                _port.Open();
                Log($"[Started] Port={portName} Baud={baudRate}");
            }
            catch (Exception ex)
            {
                Log($"[StartError] {ex}");
                throw;
            }
        }

        private StringBuilder _lineBuffer = new StringBuilder();

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var sp = (SerialPort)sender;
                int available = sp.BytesToRead;
                if (available <= 0) return;

                byte[] buffer = new byte[available];
                int read = sp.Read(buffer, 0, available);
                string text = Encoding.ASCII.GetString(buffer, 0, read);

                // Buffering per line
                _lineBuffer.Append(text);
                string allText = _lineBuffer.ToString();
                string[] lines = allText.Replace("\r\n", "\n").Replace("\r", "\n").Split(new[] { '\n' }, StringSplitOptions.None);

                for (int i = 0; i < lines.Length - 1; i++) // process all complete lines
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var args = new SerialDataEventArgs
                    {
                        Data = Encoding.ASCII.GetBytes(line),
                        Text = line,
                        Timestamp = DateTime.Now
                    };
                    try { DataReceived?.Invoke(this, args); } catch { }
                    Log($"{line.TrimEnd()}".TrimEnd());
                }

                // Save incomplete line back to buffer
                _lineBuffer.Clear();
                _lineBuffer.Append(lines[^1]);
            }
            catch (Exception ex)
            {
                Log($"[RXError] {ex}");
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
