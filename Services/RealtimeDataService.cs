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

        public void SaveToDatabase(
            int id,
            int nilaiA0,
            int nilaiTerakhirA2,
            float durasiTerakhirA4,
            float ratarataTerakhirA4,
            int parthours,
            TimeSpan dataCh1,
            TimeSpan uptime,
            int p_datach1,
            int p_uptime)
        {
            // Query MERGE asli Anda (menggunakan kolom 'id')
            string mergeQuery = @"
MERGE INTO data_realtime AS target
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
    -- Menyisipkan nilai ke kolom 'id' yang merupakan IDENTITY
    INSERT (id, nilaiA0, nilaiTerakhirA2, durasiTerakhirA4, ratarataTerakhirA4, parthours, dataCh1, uptime, p_datach1, p_uptime, last_update)
    VALUES (@id, @nilaiA0, @nilaiTerakhirA2, @durasiTerakhirA4, @ratarataTerakhirA4, @parthours, @dataCh1, @uptime, @p_datach1, @p_uptime, GETDATE());
";

            // Menggabungkan query dengan perintah SET IDENTITY_INSERT ON/OFF
            // Ini harus dilakukan dalam satu koneksi agar berhasil.
            string finalQuery = @$"
SET IDENTITY_INSERT data_realtime ON;
{mergeQuery}
SET IDENTITY_INSERT data_realtime OFF;
";
            
            using (var conn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(finalQuery, conn)) // Gunakan finalQuery
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
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}