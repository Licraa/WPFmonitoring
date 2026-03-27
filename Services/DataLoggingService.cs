using Microsoft.Extensions.DependencyInjection;
using MonitoringApp.Data;
using MonitoringApp.Models;
using MonitoringApp.Services;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MonitoringApp.Services
{
    public class DataLoggingService
    {
        private readonly SerialPortService _serialService;
        private readonly DataProcessingService _dataProcessingService;
        private readonly CsvLogService _csvService;
        private readonly IServiceScopeFactory _scopeFactory;

        // Antrean data yang tetap hidup meskipun UI ditutup
        private readonly ConcurrentQueue<(int id, object[] data, (int MachineCode, string Name, string Line, string Process) meta)> _csvBuffer = new();
        private bool _isCsvWorkerRunning = false;

        private string _activeShiftName;
        private DateTime _activeShiftDate;
        private readonly System.Timers.Timer _shiftCheckTimer;

        // Event untuk mengirim notifikasi ke UI (SerialMonitorControl)
        public event Action<string>? OnStatusMessage;

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

            // Inisialisasi shift saat ini dari config dinamis
            var info = _csvService.GetCurrentShiftInfo();
            _activeShiftName = info.shiftName;
            _activeShiftDate = info.shiftDate;

            // Cek perpindahan shift setiap 1 menit
            _shiftCheckTimer = new System.Timers.Timer(60000);
            _shiftCheckTimer.Elapsed += (s, e) => CheckShiftTransition();
            _shiftCheckTimer.Start();

            // Berlangganan data secara GLOBAL
            _serialService.DataReceived += OnDataReceived;
        }

        /// <summary>
        /// Method untuk memicu konversi Excel secara manual dari UI
        /// </summary>
        public async Task ManualFinalize()
        {
            OnStatusMessage?.Invoke("[PROCESS] Menguras antrean data dan menyiapkan Excel...");

            await Task.Run(async () => {
                // 1. Kuras sisa antrean ke CSV agar data terakhir tidak hilang
                await FlushBufferToCsv();

                // 2. Ambil info shift terbaru untuk penamaan file
                var info = _csvService.GetCurrentShiftInfo();

                OnStatusMessage?.Invoke($"[PROCESS] Mengonversi ke Excel: {info.shiftName} ({info.shiftDate:yyyy-MM-dd})...");

                // 3. Jalankan konversi
                _csvService.FinalizeExcel(info.shiftName, info.shiftDate);
            });

            OnStatusMessage?.Invoke("[SUCCESS] File Excel berhasil diperbarui di folder log.");
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
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] ArduinoCode={result.IdKey} → dbId={dbId}");
                        if (dbId != -1)
                        {
                            // 1. Simpan ke Database (SQL Server)
                            realtimeService.SaveToDatabase(dbId, (int)result.ParsedData[1], (int)result.ParsedData[2],
                                (float)result.ParsedData[3], (float)result.ParsedData[4], (int)result.ParsedData[5],
                                (float)result.ParsedData[6], (float)result.ParsedData[7], (int)result.ParsedData[8], (int)result.ParsedData[9]);

                            // 2. Masukkan ke antrean CSV
                            var info = machineService.GetMachineInfoCached(dbId);
                            _csvBuffer.Enqueue((result.IdKey, result.ParsedData, (info.MachineCode, info.Name, info.Line, info.Process)));

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

                OnStatusMessage?.Invoke($"[SHIFT] Pergantian ke {current.shiftName}. Mengonversi data shift sebelumnya...");

                // Auto-finalize shift sebelumnya ke Excel setelah delay singkat agar disk I/O selesai
                Task.Run(async () => {
                    await FlushBufferToCsv();
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        // Ambil semua data dari MachineShiftData untuk shift yang baru saja berakhir
                        var lastShiftData = context.MachineShiftDatas
                            .Where(x => x.ShiftNumber == GetShiftNumber(oldName))
                            .ToList();

                        foreach (var data in lastShiftData)
                        {
                            // Simpan ke tabel DailyUptimeLogs (Trend)
                            context.DailyUptimeLogs.Add(new DailyUptimeLog
                            {
                                MachineId = data.Id,
                                LogDate = oldDate,
                                ShiftName = oldName,
                                UptimePct = data.P_Uptime,
                                TotalCount = data.NilaiTerakhirA2
                            });
                        }
                        await context.SaveChangesAsync();
                    }
                    await Task.Delay(3000);
                    _csvService.FinalizeExcel(oldName, oldDate);
                });
            }
        }

        private int GetShiftNumber(string name)
        {
            // Logika sederhana: jika nama shift mengandung angka 1, maka return 1, dst.
            if (name.Contains("1")) return 1;
            if (name.Contains("2")) return 2;
            if (name.Contains("3")) return 3;

            return 1; // Default jika tidak ditemukan
        }

        private async void StartCsvWorker()
        {
            if (_isCsvWorkerRunning) return;
            _isCsvWorkerRunning = true;

            await Task.Run(async () => {
                await FlushBufferToCsv();
                _isCsvWorkerRunning = false;
            });
        }

        /// <summary>
        /// Fungsi helper untuk menguras antrean ConcurrentQueue ke file CSV
        /// </summary>
        private async Task FlushBufferToCsv()
        {
            while (_csvBuffer.TryDequeue(out var item))
            {
                try
                {
                    _csvService.LogDataToCsv(item.meta.MachineCode, item.meta.Name, item.meta.Line, item.meta.Process,
                        ((int)item.data[1] == 1 ? "Active" : "Inactive"), (int)item.data[2], (float)item.data[3],
                        (float)item.data[4], (int)item.data[5],
                        TimeSpan.FromSeconds((float)item.data[6]).ToString(@"hh\:mm\:ss"),
                        TimeSpan.FromSeconds((float)item.data[7]).ToString(@"hh\:mm\:ss"));
                }
                catch { }
                await Task.Delay(5); // Memberi nafas pada CPU agar tidak 100% saat antrean panjang
            }
        }
    }
}