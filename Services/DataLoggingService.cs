using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MonitoringApp.Services;

namespace MonitoringApp.Services
{
    public class DataLoggingService
    {
        private readonly SerialPortService _serialService;
        private readonly DataProcessingService _dataProcessingService;
        private readonly CsvLogService _csvService;
        private readonly IServiceScopeFactory _scopeFactory;

        // Antrean data yang tetap hidup meskipun UI ditutup
        private readonly ConcurrentQueue<(int id, object[] data, (string Name, string Line, string Process) meta)> _csvBuffer = new();
        private bool _isCsvWorkerRunning = false;

        private string _activeShiftName;
        private DateTime _activeShiftDate;
        private readonly System.Timers.Timer _shiftCheckTimer;

        public DataLoggingService(
            SerialPortService serialService,
            DataProcessingService dataProcessingService,
            CsvLogService csvService,
            IServiceScopeFactory scopeFactory)
        {
            _serialService = serialService;
            _dataProcessingService = dataProcessingService;
            _csvService = csvService;
            _scopeFactory = scopeFactory;

            var info = _csvService.GetCurrentShiftInfo();
            _activeShiftName = info.shiftName;
            _activeShiftDate = info.shiftDate;

            // Cek perpindahan shift setiap 1 menit
            _shiftCheckTimer = new System.Timers.Timer(60000);
            _shiftCheckTimer.Elapsed += (s, e) => CheckShiftTransition();
            _shiftCheckTimer.Start();

            // Berlangganan data secara GLOBAL sejak aplikasi start
            _serialService.DataReceived += OnDataReceived;
        }

        private void OnDataReceived(object? sender, SerialDataEventArgs e)
        {
            var lines = e.Text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var result = _dataProcessingService.ProcessRawData(line);
                if (result.IsValid && !result.IsDuplicate)
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var machineService = scope.ServiceProvider.GetRequiredService<MachineService>();
                        var realtimeService = scope.ServiceProvider.GetRequiredService<RealtimeDataService>();

                        int dbId = machineService.GetDbIdByArduinoCode(result.IdKey);
                        if (dbId != -1)
                        {
                            // Simpan ke SQL Server
                            realtimeService.SaveToDatabase(dbId, (int)result.ParsedData[1], (int)result.ParsedData[2],
                                (float)result.ParsedData[3], (float)result.ParsedData[4], (int)result.ParsedData[5],
                                (float)result.ParsedData[6], (float)result.ParsedData[7], (int)result.ParsedData[8], (int)result.ParsedData[9]);

                            // Masukkan ke antrean CSV global
                            var meta = machineService.GetMachineInfoCached(dbId);
                            _csvBuffer.Enqueue((dbId, result.ParsedData, meta));
                            if (!_isCsvWorkerRunning) StartCsvWorker();
                        }
                    }
                }
            }
        }

        private void CheckShiftTransition()
        {
            var current = _csvService.GetCurrentShiftInfo();
            if (current.shiftName != _activeShiftName || current.shiftDate != _activeShiftDate)
            {
                string oldName = _activeShiftName;
                DateTime oldDate = _activeShiftDate;

                _activeShiftName = current.shiftName;
                _activeShiftDate = current.shiftDate;

                // Auto-finalize shift sebelumnya ke Excel
                Task.Run(async () => {
                    await Task.Delay(3000);
                    _csvService.FinalizeExcel(oldName, oldDate);
                });
            }
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

        // --- FIX: Method yang dicari oleh SerialMonitorControl ---
        public void ManualFinalize()
        {
            Task.Run(() => {
                // Kuras sisa antrean ke CSV dulu
                while (_csvBuffer.TryDequeue(out var item))
                {
                    _csvService.LogDataToCsv(item.id, item.meta.Name, item.meta.Line, item.meta.Process,
                        ((int)item.data[1] == 1 ? "Active" : "Inactive"), (int)item.data[2], (float)item.data[3],
                        (float)item.data[4], (int)item.data[5], TimeSpan.FromSeconds((float)item.data[6]).ToString(@"hh\:mm\:ss"),
                        TimeSpan.FromSeconds((float)item.data[7]).ToString(@"hh\:mm\:ss"));
                }
                // Baru buat Excelnya
                _csvService.FinalizeExcel(_activeShiftName, _activeShiftDate);
            });
        }
    }
}