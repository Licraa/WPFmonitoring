using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MonitoringApp.Data;
using MonitoringApp.Models;

namespace MonitoringApp.Services
{
    public class RealtimeDataService
    {
        private readonly AppDbContext _context;

        public RealtimeDataService(string connectionString)
        {
            // Kita abaikan connectionString string karena kita pakai Factory EF Core
            _context = new AppDbContextFactory().CreateDbContext(null);
        }

        public void SaveToDatabase(
            int id,
            int nilaiA0,
            int nilaiTerakhirA2,
            float durasiTerakhirA4,
            float ratarataTerakhirA4,
            int parthours,
            float dataCh1_Sec,
            float uptime_Sec,
            int p_datach1,
            int p_uptime)
        {
            try
            {
                // Konversi Detik ke TimeSpan
                TimeSpan ts_dataCh1 = TimeSpan.FromSeconds(dataCh1_Sec);
                TimeSpan ts_uptime = TimeSpan.FromSeconds(uptime_Sec);

                // 1. Simpan ke Tabel Utama (DataRealtime)
                UpsertData(_context.DataRealtimes, id, nilaiA0, nilaiTerakhirA2, durasiTerakhirA4, ratarataTerakhirA4, parthours, ts_dataCh1, ts_uptime, p_datach1, p_uptime);

                // 2. Simpan ke Tabel Shift yang Sesuai
                SaveToShiftTable(id, nilaiA0, nilaiTerakhirA2, durasiTerakhirA4, ratarataTerakhirA4, parthours, ts_dataCh1, ts_uptime, p_datach1, p_uptime);

                _context.SaveChanges(); // Eksekusi semua perubahan ke DB sekaligus
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error Saving Realtime: {ex.Message}");
            }
        }

        // Helper untuk memilih tabel Shift berdasarkan jam
        private void SaveToShiftTable(int id, int a0, int a2, float a4, float avgA4, int ph, TimeSpan ch1, TimeSpan up, int pCh1, int pUp)
        {
            TimeSpan now = DateTime.Now.TimeOfDay;

            // Logika jam shift
            if (now >= new TimeSpan(6, 30, 0) && now < new TimeSpan(14, 30, 0))
            {
                UpsertData(_context.Shift1s, id, a0, a2, a4, avgA4, ph, ch1, up, pCh1, pUp);
            }
            else if (now >= new TimeSpan(14, 30, 0) && now < new TimeSpan(22, 30, 0))
            {
                UpsertData(_context.Shift2s, id, a0, a2, a4, avgA4, ph, ch1, up, pCh1, pUp);
            }
            else
            {
                UpsertData(_context.Shift3s, id, a0, a2, a4, avgA4, ph, ch1, up, pCh1, pUp);
            }
        }

        // Generic Method untuk melakukan UPSERT (Update or Insert) ke tabel manapun (Realtime/Shift)
        private void UpsertData<T>(DbSet<T> dbSet, int id, int a0, int a2, float a4, float avgA4, int ph, TimeSpan ch1, TimeSpan up, int pCh1, int pUp)
            where T : MachineDataBase, new()
        {
            // Cek apakah data sudah ada?
            var existingData = dbSet.FirstOrDefault(x => x.Id == id);

            if (existingData != null)
            {
                // UPDATE
                existingData.NilaiA0 = a0;
                existingData.NilaiTerakhirA2 = a2;
                existingData.DurasiTerakhirA4 = a4;
                existingData.RataRataTerakhirA4 = avgA4;
                existingData.PartHours = ph;
                existingData.DataCh1 = ch1;
                existingData.Uptime = up;
                existingData.P_DataCh1 = pCh1;
                existingData.P_Uptime = pUp;
                existingData.Last_Update = DateTime.Now;

                _context.Entry(existingData).State = EntityState.Modified;
            }
            else
            {
                // INSERT
                var newData = new T
                {
                    Id = id, // ID Manual, tidak auto increment
                    NilaiA0 = a0,
                    NilaiTerakhirA2 = a2,
                    DurasiTerakhirA4 = a4,
                    RataRataTerakhirA4 = avgA4,
                    PartHours = ph,
                    DataCh1 = ch1,
                    Uptime = up,
                    P_DataCh1 = pCh1,
                    P_Uptime = pUp,
                    Last_Update = DateTime.Now
                };
                dbSet.Add(newData);
            }
        }
    }
}