using System;
using Microsoft.Data.SqlClient;

namespace MonitoringApp.Services
{
    public class RealtimeDataService
    {
        private readonly string _connectionString;

        public RealtimeDataService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // PERUBAHAN: dataCh1 dan uptime sekarang menerima float (detik)
        public void SaveToDatabase(
            int id,
            int nilaiA0,
            int nilaiTerakhirA2,
            float durasiTerakhirA4,
            float ratarataTerakhirA4,
            int parthours,
            float dataCh1_Sec,   // Input Float (Detik)
            float uptime_Sec,    // Input Float (Detik)
            int p_datach1,
            int p_uptime)
        {
            // Konversi Float (Detik) ke TimeSpan (Jam:Menit:Detik)
            TimeSpan ts_dataCh1 = TimeSpan.FromSeconds(dataCh1_Sec);
            TimeSpan ts_uptime = TimeSpan.FromSeconds(uptime_Sec);

            // Simpan ke tabel utama
            SaveToTable("data_realtime", id, nilaiA0, nilaiTerakhirA2, durasiTerakhirA4, ratarataTerakhirA4, parthours, ts_dataCh1, ts_uptime, p_datach1, p_uptime);

            // Simpan ke tabel Shift
            string shiftTable = GetShiftTableName();
            SaveToTable(shiftTable, id, nilaiA0, nilaiTerakhirA2, durasiTerakhirA4, ratarataTerakhirA4, parthours, ts_dataCh1, ts_uptime, p_datach1, p_uptime);
        }

        private string GetShiftTableName()
        {
            TimeSpan now = DateTime.Now.TimeOfDay;
            if (now >= new TimeSpan(6, 30, 0) && now < new TimeSpan(14, 30, 0)) return "shift_1";
            if (now >= new TimeSpan(14, 30, 0) && now < new TimeSpan(22, 30, 0)) return "shift_2";
            return "shift_3";
        }

        private void SaveToTable(
            string tableName,
            int id, int nilaiA0, int nilaiTerakhirA2, float durasiTerakhirA4, float ratarataTerakhirA4,
            int parthours, TimeSpan dataCh1, TimeSpan uptime, int p_datach1, int p_uptime)
        {
            string query = $@"
                SET IDENTITY_INSERT {tableName} ON;
                MERGE INTO {tableName} AS target
                USING (SELECT @id AS id) AS source
                ON target.id = source.id
                WHEN MATCHED THEN
                    UPDATE SET
                        nilaiA0 = @nilaiA0,
                        nilaiTerakhirA2 = @nilaiTerakhirA2,
                        durasiTerakhirA4 = @durasiTerakhirA4,
                        ratarataTerakhirA4 = @ratarataTerakhirA4,
                        parthours = @parthours,
                        dataCh1 = @dataCh1,
                        uptime = @uptime,
                        p_datach1 = @p_datach1,
                        p_uptime = @p_uptime,
                        last_update = GETDATE()
                WHEN NOT MATCHED THEN
                    INSERT (id, nilaiA0, nilaiTerakhirA2, durasiTerakhirA4, ratarataTerakhirA4, parthours, dataCh1, uptime, p_datach1, p_uptime, last_update)
                    VALUES (@id, @nilaiA0, @nilaiTerakhirA2, @durasiTerakhirA4, @ratarataTerakhirA4, @parthours, @dataCh1, @uptime, @p_datach1, @p_uptime, GETDATE());
                SET IDENTITY_INSERT {tableName} OFF;
            ";

            using (var conn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@nilaiA0", nilaiA0);
                cmd.Parameters.AddWithValue("@nilaiTerakhirA2", nilaiTerakhirA2);
                cmd.Parameters.AddWithValue("@durasiTerakhirA4", durasiTerakhirA4);
                cmd.Parameters.AddWithValue("@ratarataTerakhirA4", ratarataTerakhirA4);
                cmd.Parameters.AddWithValue("@parthours", parthours);
                cmd.Parameters.AddWithValue("@dataCh1", dataCh1);
                cmd.Parameters.AddWithValue("@uptime", uptime);
                cmd.Parameters.AddWithValue("@p_datach1", p_datach1);
                cmd.Parameters.AddWithValue("@p_uptime", p_uptime);

                try { conn.Open(); cmd.ExecuteNonQuery(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error saving to {tableName}: {ex.Message}"); throw; }
            }
        }
    }
}