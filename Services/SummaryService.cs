using MonitoringApp.ViewModels;       // Pastikan LineSummary ada di sini
using MonitoringApp.Models;       // Pastikan LineSummary ada di sini
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace MonitoringApp.Services
{
    public class SummaryService
    {
        private readonly DatabaseService _db;

        public SummaryService(DatabaseService dbService)
        {
            _db = dbService;
        }

        public List<LineSummary> GetLineSummary()
{
    var result = new List<LineSummary>();

    using var conn = _db.GetConnection();
    conn.Open();

    // OPTIMASI: Ambil SEMUA data sekaligus dengan JOIN dalam 1 Query.
    // Tidak ada lagi looping query ke database.
    string query = @"
        SELECT 
            l.line_production,
            dr.nilaiA0,
            dr.nilaiTerakhirA2,
            dr.durasiTerakhirA4,
            dr.ratarataTerakhirA4,
            dr.parthours,
            dr.p_datach1,
            dr.p_uptime
        FROM line l
        LEFT JOIN data_realtime dr ON l.id = dr.id";

    var cmd = new SqlCommand(query, conn);
    var reader = cmd.ExecuteReader();

    // Kita tampung dulu datanya di memori lokal agar mudah di-grouping
    var rawData = new List<dynamic>();

    while (reader.Read())
    {
        rawData.Add(new
        {
            Line = reader["line_production"]?.ToString() ?? "Unknown",
            NilaiA0 = reader["nilaiA0"] != DBNull.Value ? Convert.ToInt32(reader["nilaiA0"]) : 0,
            NilaiTerakhirA2 = reader["nilaiTerakhirA2"] != DBNull.Value ? Convert.ToInt32(reader["nilaiTerakhirA2"]) : 0,
            Durasi = reader["durasiTerakhirA4"] != DBNull.Value ? Convert.ToDouble(reader["durasiTerakhirA4"]) : 0.0,
            RataRata = reader["ratarataTerakhirA4"] != DBNull.Value ? Convert.ToDouble(reader["ratarataTerakhirA4"]) : 0.0,
            PartHours = reader["parthours"] != DBNull.Value ? Convert.ToDouble(reader["parthours"]) : 0.0,
            DowntimeP = reader["p_datach1"] != DBNull.Value ? Convert.ToDouble(reader["p_datach1"]) : 0.0,
            UptimeP = reader["p_uptime"] != DBNull.Value ? Convert.ToDouble(reader["p_uptime"]) : 0.0
        });
    }
    reader.Close();

    // Lakukan Grouping di C# (Jauh lebih ringan daripada bolak-balik ke DB)
    var groupedData = rawData.GroupBy(x => x.Line);

    foreach (var group in groupedData)
    {
        var summary = new LineSummary
        {
            lineProduction = group.Key
        };

        // Hitung aggregasi
        foreach (var item in group)
        {
            // Status Mesin (1 = Active)
            if (item.NilaiA0 == 1) summary.Active++;
            else summary.Inactive++;

            // Total Count
            summary.Count += item.NilaiTerakhirA2;

            // Penjumlahan lainnya (sesuaikan logika jika butuh rata-rata atau total)
            summary.Cycle += item.Durasi;
            summary.AvgCycle += item.RataRata;
            summary.PartHours += item.PartHours;
            summary.DowntimePercent += item.DowntimeP;
            summary.UptimePercent += item.UptimeP;
        }

        summary.TotalMachine = summary.Active + summary.Inactive;
        
        // Opsional: Jika Cycle/AvgCycle harusnya dirata-rata per mesin, bagi disini:
        // if (summary.TotalMachine > 0) summary.AvgCycle /= summary.TotalMachine;

        result.Add(summary);
    }

    return result;
}

        public List<MachineDetailViewModel> GetMachineDetailByLine(string lineProduction)
        {
            var result = new List<MachineDetailViewModel>();

            // 1. PERBAIKAN DISINI: Tambahkan 'string remark' ke dalam definisi Tuple
            var tempList = new List<(int Id, string lineName, string name, string process, string lastUpdate, double partHours, double cycle, double avgCycle, int nilaiA0, string remark)>();

            using (var conn = _db.GetConnection())
            {
                conn.Open();
                // 2. Query mengambil kolom 'remark'
                var cmd = new SqlCommand(@"
            SELECT dr.*, l.line_production, l.process, l.name, l.id, l.remark
            FROM data_realtime dr
            INNER JOIN line l ON l.id = dr.id
            WHERE l.line_production = @line", conn);

                cmd.Parameters.AddWithValue("@line", lineProduction);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int Id = Convert.ToInt32(reader["id"]);
                        var lineName = reader["line_production"].ToString();
                        var process = reader["process"].ToString();
                        var name = reader["name"].ToString();

                        // 3. Ambil data Remark dari database
                        var remark = reader["remark"] != DBNull.Value ? reader["remark"].ToString() : "-";

                        string lastUpdate = "";
                        try
                        {
                            int idx = reader.GetOrdinal("last_update");
                            if (!reader.IsDBNull(idx))
                                lastUpdate = ((DateTime)reader.GetValue(idx)).ToString("yyyy-MM-dd HH:mm:ss");
                        }
                        catch { }

                        var partHours = reader["Parthours"] != DBNull.Value ? Convert.ToDouble(reader["Parthours"]) : 0;
                        var cycle = reader["DurasiTerakhirA4"] != DBNull.Value ? Convert.ToDouble(reader["DurasiTerakhirA4"]) : 0;
                        var avgCycle = reader["RatarataTerakhirA4"] != DBNull.Value ? Convert.ToDouble(reader["RatarataTerakhirA4"]) : 0;
                        var nilaiA0 = reader["NilaiA0"] != DBNull.Value ? Convert.ToInt32(reader["NilaiA0"]) : 0;

                        // 4. Masukkan 'remark' ke dalam list sementara
                        tempList.Add((Id, lineName, name, process, lastUpdate, partHours, cycle, avgCycle, nilaiA0, remark));
                    }
                }

                // Setelah reader utama ditutup, ambil data shift
                foreach (var item in tempList)
                {
                    var shift1 = GetShiftSummary(conn, "shift_1", item.Id);
                    var shift2 = GetShiftSummary(conn, "shift_2", item.Id);
                    var shift3 = GetShiftSummary(conn, "shift_3", item.Id);

                    result.Add(new MachineDetailViewModel
                    {
                        Id = item.Id, // Pastikan ID juga dipapping
                        Line = item.lineName,
                        Name = item.name,
                        Process = item.process,

                        // 5. Masukkan ke ViewModel
                        Remark = item.remark,

                        LastUpdate = item.lastUpdate,
                        PartHours = item.partHours,
                        Cycle = item.cycle,
                        AvgCycle = item.avgCycle,
                        Shift1 = shift1,
                        Shift2 = shift2,
                        Shift3 = shift3,
                        NilaiA0 = item.nilaiA0
                    });
                }
            }
            return result;
        }

        // Helper untuk ambil data shift
        private ShiftSummaryViewModel GetShiftSummary(SqlConnection conn, string shiftTable, int MesinId)
        {
            // Ambil semua data shift untuk line terkait
            var cmd = new SqlCommand($@"
                SELECT s.*
                FROM {shiftTable} s
                JOIN line l ON l.id = s.id
                WHERE l.id = @id
            ", conn);
            cmd.Parameters.AddWithValue("@id", MesinId);
            int countA2 = 0;
            int active = 0;
            int inactive = 0;
            int totalSecondsDowntime = 0;
            int totalSecondsUptime = 0;
            int totalPDatach1 = 0;
            int totalPUptime = 0;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    int a2 = reader["nilaiTerakhirA2"] != DBNull.Value ? Convert.ToInt32(reader["nilaiTerakhirA2"]) : 0;
                    countA2 += a2;
                    int a0 = reader["nilaiA0"] != DBNull.Value ? Convert.ToInt32(reader["nilaiA0"]) : 0;
                    active += a0 == 1 ? 1 : 0;
                    inactive += a0 != 1 ? 1 : 0;
                    // DataCh1 = jam downtime, PDatach1 = persen downtime
                    if (reader["dataCh1"] != DBNull.Value)
                    {
                        var t = reader["dataCh1"];
                        if (t is TimeSpan ts)
                            totalSecondsDowntime += (int)ts.TotalSeconds;
                        else if (t is TimeOnly to)
                            totalSecondsDowntime += (int)to.ToTimeSpan().TotalSeconds;
                    }
                    if (reader["p_datach1"] != DBNull.Value)
                        totalPDatach1 += Convert.ToInt32(reader["p_datach1"]);
                    // Uptime = jam uptime, PUptime = persen uptime
                    if (reader["uptime"] != DBNull.Value)
                    {
                        var t = reader["uptime"];
                        if (t is TimeSpan ts)
                            totalSecondsUptime += (int)ts.TotalSeconds;
                        else if (t is TimeOnly to)
                            totalSecondsUptime += (int)to.ToTimeSpan().TotalSeconds;
                    }
                    if (reader["p_uptime"] != DBNull.Value)
                        totalPUptime += Convert.ToInt32(reader["p_uptime"]);
                }
            }
            TimeSpan downtimeSpan = TimeSpan.FromSeconds(totalSecondsDowntime);
            TimeSpan uptimeSpan = TimeSpan.FromSeconds(totalSecondsUptime);
            return new ShiftSummaryViewModel
            {
                Count = countA2,
                Downtime = downtimeSpan.ToString(@"hh\:mm\:ss"),
                Uptime = uptimeSpan.ToString(@"hh\:mm\:ss"),
                DowntimePercent = totalPDatach1,
                UptimePercent = totalPUptime
            };
        }
    }
}
