using MonitoringApp.ViewModels;       // Pastikan LineSummary ada di sini
using MonitoringApp.Models;       // Pastikan LineSummary ada di sini
using System.Collections.Generic;
using System.Data.SqlClient;

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
            List<LineSummary> result = new();

            using var conn = _db.GetConnection();
            conn.Open();

            // Ambil semua line_production
            string lineQuery = "SELECT DISTINCT line_production FROM line";
            var lineCmd = new SqlCommand(lineQuery, conn);
            var reader = lineCmd.ExecuteReader();

            List<string> lineProductions = new();
            while (reader.Read())
                lineProductions.Add(reader.GetString(0));

            reader.Close();

            foreach (string lp in lineProductions)
            {
                var summary = new LineSummary() { lineProduction = lp };

                var cmd = new SqlCommand(@"
                    SELECT r.*
                    FROM data_realtime r
                    JOIN line l ON l.id = r.id
                    WHERE l.line_production = @lp
                ", conn);

                cmd.Parameters.AddWithValue("@lp", lp);

                var r = cmd.ExecuteReader();

                int machineCount = 0;
                int countA2 = 0;
                while (r.Read())
                {
                    machineCount++;
                    // nilaiTerakhirA2 untuk Count
                    int a2 = r["nilaiTerakhirA2"] != DBNull.Value ? Convert.ToInt32(r["nilaiTerakhirA2"]) : 0;
                    countA2 += a2;
                    // nilaiA0 untuk status aktif/tidak aktif
                    int a0 = r["nilaiA0"] != DBNull.Value ? Convert.ToInt32(r["nilaiA0"]) : 0;
                    summary.Active += a0 == 1 ? 1 : 0;
                    summary.Inactive += a0 != 1 ? 1 : 0;

                    summary.Cycle += r["durasiTerakhirA4"] != DBNull.Value ? Convert.ToDouble(r["durasiTerakhirA4"]) : 0;
                    summary.AvgCycle += r["ratarataTerakhirA4"] != DBNull.Value ? Convert.ToDouble(r["ratarataTerakhirA4"]) : 0;
                    summary.PartHours += r["parthours"] != DBNull.Value ? Convert.ToDouble(r["parthours"]) : 0;

                    summary.DowntimePercent += r["p_datach1"] != DBNull.Value ? Convert.ToDouble(r["p_datach1"]) : 0;
                    summary.UptimePercent += r["p_uptime"] != DBNull.Value ? Convert.ToDouble(r["p_uptime"]) : 0;
                }

                r.Close();

                // Simpan total machine
                summary.TotalMachine = machineCount;
                summary.Count = countA2;

                result.Add(summary);
            }

            return result;
        }

        public List<MachineDetailViewModel> GetMachineDetailByLine(string lineProduction)
        {
            var result = new List<MachineDetailViewModel>();
            var tempList = new List<(int Id, string lineName, string name, string process, string lastUpdate, double partHours, double cycle, double avgCycle, int nilaiA0)>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                // Query utama: join data_realtime dan line
                var cmd = new SqlCommand(@"
                    SELECT dr.*, l.line_production, l.process, l.name, l.id
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
                        string lastUpdate = "";
                        try {
                            int idx = reader.GetOrdinal("last_update");
                            if (!reader.IsDBNull(idx))
                                lastUpdate = ((DateTime)reader.GetValue(idx)).ToString("yyyy-MM-dd HH:mm:ss");
                        } catch {}
                        var partHours = reader["Parthours"] != DBNull.Value ? Convert.ToDouble(reader["Parthours"]) : 0;
                        var cycle = reader["DurasiTerakhirA4"] != DBNull.Value ? Convert.ToDouble(reader["DurasiTerakhirA4"]) : 0;
                        var avgCycle = reader["RatarataTerakhirA4"] != DBNull.Value ? Convert.ToDouble(reader["RatarataTerakhirA4"]) : 0;
                        var nilaiA0 = reader["NilaiA0"] != DBNull.Value ? Convert.ToInt32(reader["NilaiA0"]) : 0;
                        
                        tempList.Add((Id, lineName, name, process, lastUpdate, partHours, cycle, avgCycle, nilaiA0));
                    }
                }
                // Setelah reader utama ditutup, baru ambil data shift
                foreach (var item in tempList)
                {
                    var shift1 = GetShiftSummary(conn, "shift_1", item.Id);
                    var shift2 = GetShiftSummary(conn, "shift_2", item.Id);
                    var shift3 = GetShiftSummary(conn, "shift_3", item.Id);
                    result.Add(new MachineDetailViewModel
                    {
                        Line = item.lineName,
                        Name = item.name,
                        Process = item.process,
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
