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
        // PENTING: Menggunakan IDbContextFactory agar tidak terjadi kebocoran koneksi
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public RealtimeDataService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            Debug.WriteLine("Realtime service STARTED with Factory Pattern");
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
            // Buka koneksi baru yang otomatis ditutup setelah block 'using' selesai
            using (var context = _contextFactory.CreateDbContext())
            {
                try
                {
                    TimeSpan ts_dataCh1 = TimeSpan.FromSeconds(dataCh1_Sec);
                    TimeSpan ts_uptime = TimeSpan.FromSeconds(uptime_Sec);

                    // 1. Simpan ke Tabel Utama
                    UpsertData(context.DataRealtimes, id, nilaiA0, nilaiTerakhirA2, durasiTerakhirA4, ratarataTerakhirA4, parthours, ts_dataCh1, ts_uptime, p_datach1, p_uptime);

                    // 2. Simpan ke Tabel Shift
                    TimeSpan now = DateTime.Now.TimeOfDay;
                    if (now >= new TimeSpan(6, 30, 0) && now < new TimeSpan(14, 30, 0))
                    {
                        UpsertData(context.Shift1s, id, nilaiA0, nilaiTerakhirA2, durasiTerakhirA4, ratarataTerakhirA4, parthours, ts_dataCh1, ts_uptime, p_datach1, p_uptime);
                    }
                    else if (now >= new TimeSpan(14, 30, 0) && now < new TimeSpan(22, 30, 0))
                    {
                        UpsertData(context.Shift2s, id, nilaiA0, nilaiTerakhirA2, durasiTerakhirA4, ratarataTerakhirA4, parthours, ts_dataCh1, ts_uptime, p_datach1, p_uptime);
                    }
                    else
                    {
                        UpsertData(context.Shift3s, id, nilaiA0, nilaiTerakhirA2, durasiTerakhirA4, ratarataTerakhirA4, parthours, ts_dataCh1, ts_uptime, p_datach1, p_uptime);
                    }

                    context.SaveChanges();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error Saving Realtime: {ex.Message}");
                }
            } // Koneksi dan memori langsung dibersihkan di sini
        }

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
    }
}