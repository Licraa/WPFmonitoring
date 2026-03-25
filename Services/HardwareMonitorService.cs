using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonitoringApp.Services
{
    public class HardwareMonitorService : IDisposable
    {
        private readonly PerformanceCounter? _cpuCounter;
        private readonly PerformanceCounter? _ramCounter;

        public HardwareMonitorService()
        {
            try
            {
                // Inisialisasi hanya sekali seumur hidup aplikasi
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");

                // Panggil NextValue sekali untuk inisialisasi awal
                _cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Hardware Monitor Error: {ex.Message}");
            }
        }

        public string GetCpuUsage() => _cpuCounter != null ? $"{_cpuCounter.NextValue():0}%" : "N/A";
        public string GetRamUsage() => _ramCounter != null ? $"{_ramCounter.NextValue()} MB Free" : "N/A";

        public void Dispose()
        {
            _cpuCounter?.Dispose();
            _ramCounter?.Dispose();
        }
    }
}