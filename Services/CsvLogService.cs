using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;

namespace MonitoringApp.Services
{
    public class CsvLogService
    {
        private readonly string _baseFolder = "data_log_monitoring";

        // Mendapatkan path CSV berdasarkan shift dan tanggal
        public string GetCsvPath(DateTime date, string shiftName)
        {
            string tanggalFolder = date.ToString("yyyy-MM-dd");
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _baseFolder, tanggalFolder);

            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string fileName = $"data_log_{shiftName}_{date:yyyy-MM-dd}.csv";
            return Path.Combine(folderPath, fileName);
        }

        // Mendapatkan info shift saat ini
        public (string shiftName, DateTime shiftDate) GetCurrentShiftInfo()
        {
            DateTime now = DateTime.Now;
            TimeSpan time = now.TimeOfDay;
            DateTime shiftDate = now.Date;
            string shift = "shift_3";

            TimeSpan startShift1 = new TimeSpan(6, 30, 0);
            TimeSpan startShift2 = new TimeSpan(14, 30, 0);
            TimeSpan startShift3 = new TimeSpan(22, 30, 0);

            if (time >= startShift1 && time < startShift2) shift = "shift_1";
            else if (time >= startShift2 && time < startShift3) shift = "shift_2";
            else
            {
                shift = "shift_3";
                if (time < startShift1) shiftDate = now.Date.AddDays(-1);
            }
            return (shift, shiftDate);
        }

        // 1. Simpan ke CSV (Dipanggil setiap detik)
        public void LogDataToCsv(int id, string name, string line, string process, string status,
            int count, float cycle, float avgCycle, int partHours, string downtime, string uptime)
        {
            try
            {
                var info = GetCurrentShiftInfo();
                string csvPath = GetCsvPath(info.shiftDate, info.shiftName);
                bool fileExists = File.Exists(csvPath);

                // Gunakan FileShare.ReadWrite agar tidak bentrok dengan proses konversi
                using (var fs = new FileStream(csvPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    if (!fileExists)
                        sw.WriteLine("ID`NAME`LINE`PROCESS`STATUS`COUNT`CYCLE`AVG CYCLE`PART-HOURS`DOWNTIME`UPTIME`LOG TIME");

                    string logTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    // Menggunakan backtick ` sebagai separator
                    string row = $"{id}`{name}`{line}`{process}`{status}`{count}`{cycle:F2}`{avgCycle:F2}`{partHours}`{downtime}`{uptime}`{logTime}";
                    sw.WriteLine(row);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"CSV Error: {ex.Message}"); }
        }

        // 2. Konversi CSV ke Excel (xlsx)
        public void FinalizeExcel(string shiftName, DateTime shiftDate)
        {
            try
            {
                string csvPath = GetCsvPath(shiftDate, shiftName);
                string excelPath = csvPath.Replace(".csv", ".xlsx");

                if (!File.Exists(csvPath)) return;

                // Tunggu jika file masih dikunci proses lain
                int retry = 0;
                while (IsFileLocked(new FileInfo(csvPath)) && retry < 5)
                {
                    System.Threading.Thread.Sleep(1000);
                    retry++;
                }

                string[] lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1) return;

                using (var workbook = new XLWorkbook())
                {
                    // SHEET 1: LOG DATA (Semua baris)
                    var wsLog = workbook.Worksheets.Add("Log Data");
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var cols = lines[i].Split('`');
                        for (int j = 0; j < cols.Length; j++)
                        {
                            // Coba parse ke angka agar di Excel bisa dihitung (SUM/AVG)
                            if (i > 0 && double.TryParse(cols[j], out double num))
                                wsLog.Cell(i + 1, j + 1).Value = num;
                            else
                                wsLog.Cell(i + 1, j + 1).Value = cols[j];
                        }
                    }
                    wsLog.Columns().AdjustToContents();
                    wsLog.FirstRow().Style.Font.Bold = true;
                    wsLog.FirstRow().Style.Fill.BackgroundColor = XLColor.LightGray;

                    // SHEET 2: SUMMARY (Hanya status terakhir per Mesin)
                    GenerateSummarySheet(workbook, lines);

                    workbook.SaveAs(excelPath);
                }
                System.Diagnostics.Debug.WriteLine($"[SUCCESS] Excel Created: {excelPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] FinalizeExcel: {ex.Message}");
            }
        }

        private void GenerateSummarySheet(XLWorkbook workbook, string[] lines)
        {
            var wsSummary = workbook.Worksheets.Add("Summary");
            var lastDataPerMachine = new Dictionary<string, string[]>();

            // Header
            var header = lines[0].Split('`');
            for (int j = 0; j < header.Length; j++) wsSummary.Cell(1, j + 1).Value = header[j];

            // Ambil data terbaru berdasarkan ID mesin (Kolom pertama)
            for (int i = 1; i < lines.Length; i++)
            {
                var cols = lines[i].Split('`');
                if (cols.Length > 0) lastDataPerMachine[cols[0]] = cols;
            }

            int rowIdx = 2;
            foreach (var kvp in lastDataPerMachine)
            {
                var cols = kvp.Value;
                for (int j = 0; j < cols.Length; j++)
                {
                    if (double.TryParse(cols[j], out double num))
                        wsSummary.Cell(rowIdx, j + 1).Value = num;
                    else
                        wsSummary.Cell(rowIdx, j + 1).Value = cols[j];
                }
                rowIdx++;
            }

            wsSummary.Columns().AdjustToContents();
            var headerRange = wsSummary.Range(1, 1, 1, header.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.SkyBlue;
        }

        private bool IsFileLocked(FileInfo file)
        {
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException) { return true; }
            return false;
        }
    }
}