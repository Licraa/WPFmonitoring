using Microsoft.EntityFrameworkCore;
using MonitoringApp.Data;
using MonitoringApp.Models;
using System;
using System.Diagnostics;
using System.Linq;

namespace MonitoringApp.Services
{
    public class RealtimeDataService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly CsvLogService _csvService;

        public RealtimeDataService(IDbContextFactory<AppDbContext> contextFactory, CsvLogService csvService)
        {
            _contextFactory = contextFactory;
            _csvService = csvService;
            Debug.WriteLine("Realtime service STARTED with Factory Pattern");
        }

        // ─── 1. SIMPAN DATA DARI ARDUINO ─────────────────────────────────────────
        public void SaveToDatabase(int id, int nilaiA0, int nilaiTerakhirA2, float durasiTerakhirA4,
                               float ratarataTerakhirA4, int parthours, float dataCh1_Sec,
                               float uptime_Sec, int p_datach1, int p_uptime)
        {
            using (var context = _contextFactory.CreateDbContext())
            {
                try
                {
                    TimeSpan ts_dataCh1 = TimeSpan.FromSeconds(dataCh1_Sec);
                    TimeSpan ts_uptime = TimeSpan.FromSeconds(uptime_Sec);

                    UpsertData(context.DataRealtimes, id, nilaiA0, nilaiTerakhirA2, durasiTerakhirA4,
                               ratarataTerakhirA4, parthours, ts_dataCh1, ts_uptime, p_datach1, p_uptime);

                    // SEKARANG SUDAH SINGKRON: Mengambil jam dari Setting Service via CsvLogService
                    var shiftInfo = _csvService.GetCurrentShiftInfo();
                    int currentShift = GetShiftNumber(shiftInfo.shiftName);

                    UpsertShiftData(context, id, currentShift, nilaiA0, nilaiTerakhirA2, durasiTerakhirA4,
                                    ratarataTerakhirA4, parthours, ts_dataCh1, ts_uptime, p_datach1, p_uptime);

                    context.SaveChanges();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error Saving Realtime: {ex.Message}");
                }
            }
        }
        // Gunakan helper ini agar seragam
        private int GetShiftNumber(string name)
        {
            if (name.Contains("1")) return 1;
            if (name.Contains("2")) return 2;
            return 3;
        }

        // ─── HELPER A: SIMPAN DATA REALTIME (TETAP SAMA) ─────────────────────────
        private void UpsertData<T>(DbSet<T> dbSet, int id, int a0, int a2, float a4, float avgA4, int ph, TimeSpan ch1, TimeSpan up, int pCh1, int pUp)
            where T : MachineDataBase, new()
        {
            var data = dbSet.FirstOrDefault(x => x.Id == id);
            if (data == null)
            {
                data = new T { Id = id };
                dbSet.Add(data);
            }
            data.NilaiA0 = a0;
            data.NilaiTerakhirA2 = a2;
            data.DurasiTerakhirA4 = a4;
            data.RataRataTerakhirA4 = avgA4;
            data.PartHours = ph;
            data.DataCh1 = ch1;
            data.Uptime = up;
            data.P_DataCh1 = pCh1;
            data.P_Uptime = pUp;
            data.Last_Update = DateTime.Now;
        }

        // ─── HELPER B: SIMPAN DATA SHIFT (TABEL BARU) ────────────────────────────
        private void UpsertShiftData(AppDbContext context, int id, int shiftNum, int a0, int a2, float a4, float avgA4, int ph, TimeSpan ch1, TimeSpan up, int pCh1, int pUp)
        {
            // Cari data berdasarkan ID Mesin DAN Nomor Shift-nya
            var data = context.MachineShiftDatas.FirstOrDefault(x => x.Id == id && x.ShiftNumber == shiftNum);

            if (data == null)
            {
                data = new MachineShiftData { Id = id, ShiftNumber = shiftNum };
                context.MachineShiftDatas.Add(data);
            }

            data.NilaiA0 = a0;
            data.NilaiTerakhirA2 = a2;
            data.DurasiTerakhirA4 = a4;
            data.RataRataTerakhirA4 = avgA4;
            data.PartHours = ph;
            data.DataCh1 = ch1;
            data.Uptime = up;
            data.P_DataCh1 = pCh1;
            data.P_Uptime = pUp;
            data.Last_Update = DateTime.Now;
        }

        // ─── 2. FUNGSI REKAP HARIAN (SUMMARIZE) ──────────────────────────────────
        public void SummarizeAllMachines(DateTime summaryDate)
        {
            using (var context = _contextFactory.CreateDbContext())
            {
                // 🌟 PERBAIKAN 1: Ambil SEMUA data di awal (Bawa seluruh rak telur ke meja)
                var machineList = context.Lines.AsNoTracking().ToList();
                var allShiftData = context.MachineShiftDatas.AsNoTracking().ToList(); // Ambil semua dari Panci Baru

                foreach (var machine in machineList)
                {
                    // 🌟 Pencarian ini sekarang SANGAT CEPAT karena dicarinya di RAM, bukan di Database
                    var s1 = allShiftData.FirstOrDefault(m => m.Id == machine.Id && m.ShiftNumber == 1);
                    var s2 = allShiftData.FirstOrDefault(m => m.Id == machine.Id && m.ShiftNumber == 2);
                    var s3 = allShiftData.FirstOrDefault(m => m.Id == machine.Id && m.ShiftNumber == 3);

                    // 1. Process individual shifts
                    CalculateShiftPercentAndSave(context, machine.Id, "Shift 1", summaryDate, s1);
                    CalculateShiftPercentAndSave(context, machine.Id, "Shift 2", summaryDate, s2);
                    CalculateShiftPercentAndSave(context, machine.Id, "Shift 3", summaryDate, s3);

                    // 2. PROCESS ALL DAY
                    //CalculateDailyTotalAndSave(context, machine.Id, summaryDate, s1, s2, s3);
                }

                // Simpan semua perubahan sekaligus ke Database
                context.SaveChanges();
            }
        }

        // ─── HELPER 1: CALCULATE INDIVIDUAL SHIFT (DENGAN UPSERT) ───────────────────
        private void CalculateShiftPercentAndSave(AppDbContext context, int machineId, string shiftName, DateTime date, MachineDataBase? shiftData)
        {
            if (shiftData == null) return;

            double upSec = shiftData.Uptime.TotalSeconds;
            double downSec = shiftData.DataCh1.TotalSeconds;
            double totalSec = upSec + downSec;

            int upPct = 0;
            if (totalSec > 0)
            {
                upPct = (int)Math.Round((upSec / totalSec) * 100);
            }

            // 🌟 PERBAIKAN 2: LOGIKA UPSERT (Cek buku catatan dulu)
            var existingLog = context.DailyUptimeLogs.FirstOrDefault(x =>
                x.MachineId == machineId &&
                x.ShiftName == shiftName &&
                x.LogDate.Date == date.Date);

            if (existingLog != null)
            {
                // JIKA SUDAH ADA -> UPDATE Tulisannya
                existingLog.UptimePct = upPct;
                existingLog.DowntimePct = 100 - upPct;
                existingLog.TotalCount = (int)shiftData.PartHours;
            }
            else
            {
                // JIKA BELUM ADA -> INSERT / Buat baru
                context.DailyUptimeLogs.Add(new DailyUptimeLog
                {
                    MachineId = machineId,
                    ShiftName = shiftName,
                    LogDate = date.Date,
                    UptimePct = upPct,
                    DowntimePct = 100 - upPct,
                    TotalCount = (int)shiftData.PartHours
                });
            }
        }
    }
}