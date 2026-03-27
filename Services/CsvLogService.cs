using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
// PENTING 1: Tambahkan using ini agar ShiftSetting dikenali
using MonitoringApp.Models;

namespace MonitoringApp.Services
{
    public class CsvLogService
    {
        private readonly string _baseFolder = "data_log_monitoring";
        private readonly SettingService _settingService;
        public CsvLogService(SettingService settingService)
        {
            _settingService = settingService;
        }

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
            TimeSpan currentTime = now.TimeOfDay;

            // PENTING 2: Logika pemisahan hari biasa dan Sabtu
            var allSettings = _settingService.GetSettings();
            List<ShiftItem> activeSettings;

            if (now.DayOfWeek == DayOfWeek.Saturday)
            {
                // Jika Sabtu, ambil jadwal khusus Sabtu
                activeSettings = allSettings.SaturdayShiftSettings
                                    .OrderBy(s => TimeSpan.Parse(s.StartTime)).ToList();
            }
            else
            {
                // Jika bukan Sabtu, ambil jadwal biasa
                activeSettings = allSettings.ShiftSettings
                                    .OrderBy(s => TimeSpan.Parse(s.StartTime)).ToList();
            }

            // Jika kosong, kembalikan nilai default
            if (activeSettings == null || activeSettings.Count == 0) return ("Shift 1", now.Date);

            TimeSpan firstShiftStart = TimeSpan.Parse(activeSettings.First().StartTime);

            // LOGIKA CROSS-DAY:
            if (currentTime < firstShiftStart)
            {
                return (activeSettings.Last().Name, now.AddDays(-1).Date);
            }

            var currentShift = activeSettings.LastOrDefault(s => currentTime >= TimeSpan.Parse(s.StartTime));

            return (currentShift?.Name ?? activeSettings.First().Name, now.Date);
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

                using (var fs = new FileStream(csvPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    if (!fileExists)
                        sw.WriteLine("ID`NAME`LINE`PROCESS`STATUS`COUNT`CYCLE`AVG CYCLE`PART-HOURS`DOWNTIME`UPTIME`LOG TIME");

                    string logTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                    var wsLog = workbook.Worksheets.Add("Log Data");
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var cols = lines[i].Split('`');
                        for (int j = 0; j < cols.Length; j++)
                        {
                            if (i > 0 && double.TryParse(cols[j], out double num))
                                wsLog.Cell(i + 1, j + 1).Value = num;
                            else
                                wsLog.Cell(i + 1, j + 1).Value = cols[j];
                        }
                    }
                    wsLog.Columns().AdjustToContents();
                    wsLog.FirstRow().Style.Font.Bold = true;
                    wsLog.FirstRow().Style.Fill.BackgroundColor = XLColor.LightGray;

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

            var header = lines[0].Split('`');
            for (int j = 0; j < header.Length; j++) wsSummary.Cell(1, j + 1).Value = header[j];

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