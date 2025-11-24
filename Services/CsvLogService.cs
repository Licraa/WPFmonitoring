using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ClosedXML.Excel;

namespace MonitoringApp.Services
{
    public class CsvLogService
    {
        private readonly string _baseFolder = "data_log_monitoring";

        // Helper: Mendapatkan Path File berdasarkan Tanggal & Shift spesifik
        public string GetCsvPath(DateTime date, string shiftName)
        {
            string tanggalFolder = date.ToString("yyyy-MM-dd");
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _baseFolder, tanggalFolder);

            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string fileName = $"data_log_{shiftName}_{date:yyyy-MM-dd}.csv";
            return Path.Combine(folderPath, fileName);
        }

        // Helper: Menentukan Shift saat ini (Realtime)
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

        // 1. HANYA SIMPAN KE CSV (Ringan & Cepat)
        public void LogDataToCsv(int id, string name, string line, string process, string status,
            int count, float cycle, float avgCycle, int partHours, string downtime, string uptime)
        {
            try
            {
                // Ambil info shift saat ini
                var info = GetCurrentShiftInfo();
                string csvPath = GetCsvPath(info.shiftDate, info.shiftName);

                bool fileExists = File.Exists(csvPath);

                using (var sw = new StreamWriter(csvPath, true, Encoding.UTF8))
                {
                    if (!fileExists)
                        sw.WriteLine("ID`NAME`LINE`PROCESS`STATUS`COUNT`CYCLE`AVG CYCLE`PART - HOURS`DOWNTIME`UPTIME`LOG TIME");

                    string logTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string row = $"{id}`{EscapeCsv(name)}`{EscapeCsv(line)}`{EscapeCsv(process)}`{status}`{count}`{cycle:F2}`{avgCycle:F2}`{partHours}`{downtime}`{uptime}`{logTime}";
                    sw.WriteLine(row);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"CSV Error: {ex.Message}"); }
        }

        // 2. FUNGSI MANUAL UNTUK CONVERT (Dipanggil saat Stop / Ganti Shift)
        public void FinalizeExcel(string shiftName, DateTime shiftDate)
        {
            try
            {
                string csvPath = GetCsvPath(shiftDate, shiftName);
                string excelPath = csvPath.Replace(".csv", ".xlsx");

                if (!File.Exists(csvPath)) return; // Tidak ada data untuk diexport

                var lines = File.ReadAllLines(csvPath);
                if (lines.Length == 0) return;

                using (var workbook = new XLWorkbook())
                {
                    var wsLog = workbook.Worksheets.Add("Log");

                    // Parse CSV ke Excel
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var cols = ParseCsvLine(lines[i]);
                        for (int j = 0; j < cols.Count; j++)
                        {
                            if (i > 0 && double.TryParse(cols[j], out double num))
                                wsLog.Cell(i + 1, j + 1).Value = num;
                            else
                                wsLog.Cell(i + 1, j + 1).Value = cols[j];
                        }
                    }

                    // Formatting
                    var headerRange = wsLog.Range(1, 1, 1, 12);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                    wsLog.Columns().AdjustToContents();

                    // Create Summary Sheet
                    GenerateSummarySheet(workbook, lines);

                    workbook.SaveAs(excelPath);
                    System.Diagnostics.Debug.WriteLine($"[SUCCESS] Excel Created: {excelPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Excel Export Error: {ex.Message}");
            }
        }

        private void GenerateSummarySheet(XLWorkbook workbook, string[] lines)
        {
            var wsSummary = workbook.Worksheets.Add("Summary");
            var summaryDict = new Dictionary<string, List<string>>();
            List<string> header = ParseCsvLine(lines[0]);

            for (int i = 1; i < lines.Length; i++)
            {
                var cols = ParseCsvLine(lines[i]);
                if (cols.Count > 0) summaryDict[cols[0]] = cols;
            }

            for (int j = 0; j < header.Count; j++) wsSummary.Cell(1, j + 1).Value = header[j];

            int rowIdx = 2;
            foreach (var kvp in summaryDict)
            {
                var cols = kvp.Value;
                for (int j = 0; j < cols.Count; j++)
                {
                    if (double.TryParse(cols[j], out double num))
                        wsSummary.Cell(rowIdx, j + 1).Value = num;
                    else
                        wsSummary.Cell(rowIdx, j + 1).Value = cols[j];
                }
                rowIdx++;
            }

            var headerRange = wsSummary.Range(1, 1, 1, 12);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.SkyBlue;
            wsSummary.Columns().AdjustToContents();
        }

        private string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            return field.Contains(",") ? $"\"{field}\"" : field;
        }

        private List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            StringBuilder val = new StringBuilder();
            foreach (char c in line)
            {
                if (c == '\"') inQuotes = !inQuotes;
                else if (c == '`' && !inQuotes) { result.Add(val.ToString()); val.Clear(); }
                else val.Append(c);
            }
            result.Add(val.ToString());
            return result;
        }
    }
}