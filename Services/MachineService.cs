using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MonitoringApp.Data;
using MonitoringApp.Models;
using MonitoringApp.ViewModels;

namespace MonitoringApp.Services
{
    public class MachineService
    {
        private readonly AppDbContext _context;
        private readonly Dictionary<int, (string Name, string Line, string Process)> _machineCache = new();

        public MachineService(AppDbContext context)
        {
            _context = context;
        }

        // --- 1. GET ALL (READ) ---
        public List<MachineDetailViewModel> GetAllMachines()
        {
            var query = from l in _context.Lines.AsNoTracking()

                            // Join Data Realtime
                        join dr in _context.DataRealtimes.AsNoTracking() on l.Id equals dr.Id into joinedDr
                        from dr in joinedDr.DefaultIfEmpty()

                            // Join Shift 1
                        join s1 in _context.Shift1s.AsNoTracking() on l.Id equals s1.Id into joinedS1
                        from s1 in joinedS1.DefaultIfEmpty()

                            // Join Shift 2
                        join s2 in _context.Shift2s.AsNoTracking() on l.Id equals s2.Id into joinedS2
                        from s2 in joinedS2.DefaultIfEmpty()

                            // Join Shift 3
                        join s3 in _context.Shift3s.AsNoTracking() on l.Id equals s3.Id into joinedS3
                        from s3 in joinedS3.DefaultIfEmpty()

                        orderby l.LineProduction, l.Id
                        select new MachineDetailViewModel
                        {
                            Id = l.Id,
                            // PERBAIKAN: Langsung ambil l.MachineCode (karena tipe datanya int)
                            MachineCode = l.MachineCode,
                            Line = l.LineProduction,
                            Name = l.Name,
                            Process = l.Process,
                            Remark = l.Remark ?? "",

                            NilaiA0 = dr != null ? dr.NilaiA0 : 0,
                            LastUpdate = dr != null ? dr.Last_Update.ToString("yyyy-MM-dd HH:mm:ss") : "-",
                            PartHours = dr != null ? dr.PartHours : 0,
                            Cycle = dr != null ? dr.DurasiTerakhirA4 : 0,
                            AvgCycle = dr != null ? dr.RataRataTerakhirA4 : 0,

                            Shift1 = s1 == null ? new ShiftSummaryViewModel() : new ShiftSummaryViewModel
                            {
                                Count = s1.NilaiTerakhirA2,
                                Downtime = s1.DataCh1.ToString(@"hh\:mm\:ss"),
                                Uptime = s1.Uptime.ToString(@"hh\:mm\:ss"),
                                DowntimePercent = s1.P_DataCh1,
                                UptimePercent = s1.P_Uptime
                            },
                            Shift2 = s2 == null ? new ShiftSummaryViewModel() : new ShiftSummaryViewModel
                            {
                                Count = s2.NilaiTerakhirA2,
                                Downtime = s2.DataCh1.ToString(@"hh\:mm\:ss"),
                                Uptime = s2.Uptime.ToString(@"hh\:mm\:ss"),
                                DowntimePercent = s2.P_DataCh1,
                                UptimePercent = s2.P_Uptime
                            },
                            Shift3 = s3 == null ? new ShiftSummaryViewModel() : new ShiftSummaryViewModel
                            {
                                Count = s3.NilaiTerakhirA2,
                                Downtime = s3.DataCh1.ToString(@"hh\:mm\:ss"),
                                Uptime = s3.Uptime.ToString(@"hh\:mm\:ss"),
                                DowntimePercent = s3.P_DataCh1,
                                UptimePercent = s3.P_Uptime
                            }
                        };
            return query.ToList();
        }

        // --- 2. UPDATE ---
        public bool UpdateMachine(int id, int machineCode, string name, string process, string line, string remark)
        {
            if (_machineCache.ContainsKey(id)) _machineCache.Remove(id);
            try
            {
                // VALIDASI DUPLIKAT
                bool isDuplicate = _context.Lines.Any(x => x.MachineCode == machineCode && x.Id != id);
                if (isDuplicate)
                {
                    System.Windows.MessageBox.Show($"Machine Code {machineCode} is already used by another machine!", "Duplicate Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return false;
                }

                var entity = _context.Lines.FirstOrDefault(x => x.Id == id);
                if (entity == null) return false;

                entity.MachineCode = machineCode;
                entity.Name = name;
                entity.Process = process;
                entity.LineProduction = line;
                entity.Remark = remark;

                _context.SaveChanges();
                return true;
            }
            catch { return false; }
        }

        // --- 3. ADD (CREATE) ---
        public bool AddMachine(string name, string process, string line, string remark, int machineCode)
        {
            try
            {
                // VALIDASI DUPLIKAT
                bool isDuplicate = _context.Lines.Any(x => x.MachineCode == machineCode);
                if (isDuplicate)
                {
                    System.Windows.MessageBox.Show($"Machine Code {machineCode} already exists!", "Duplicate Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return false;
                }

                _context.Lines.Add(new Line
                {
                    Name = name,
                    Process = process,
                    LineProduction = line,
                    Remark = remark,
                    MachineCode = machineCode
                });

                _context.SaveChanges();
                return true;
            }
            catch { return false; }
        }

        // --- 4. DELETE ---
        public bool DeleteMachine(int id)
        {
            if (_machineCache.ContainsKey(id)) _machineCache.Remove(id);

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                var realtimeData = _context.DataRealtimes.FirstOrDefault(x => x.Id == id);
                if (realtimeData != null) _context.DataRealtimes.Remove(realtimeData);

                var shift1 = _context.Shift1s.FirstOrDefault(x => x.Id == id);
                if (shift1 != null) _context.Shift1s.Remove(shift1);

                var shift2 = _context.Shift2s.FirstOrDefault(x => x.Id == id);
                if (shift2 != null) _context.Shift2s.Remove(shift2);

                var shift3 = _context.Shift3s.FirstOrDefault(x => x.Id == id);
                if (shift3 != null) _context.Shift3s.Remove(shift3);

                var line = _context.Lines.FirstOrDefault(x => x.Id == id);
                if (line != null)
                {
                    _context.Lines.Remove(line);
                    _context.SaveChanges();
                    transaction.Commit();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Delete Error: {ex.Message}");
                return false;
            }
        }

        // --- 5. INFO & HELPERS ---
        public (string Name, string Line, string Process) GetMachineInfoCached(int id)
        {
            if (_machineCache.TryGetValue(id, out var info)) return info;

            var machine = _context.Lines.Where(l => l.Id == id).Select(l => new { l.Name, l.LineProduction, l.Process }).FirstOrDefault();
            if (machine != null)
            {
                var res = (machine.Name ?? "Unknown", machine.LineProduction ?? "-", machine.Process ?? "-");
                _machineCache[id] = res;
                return res;
            }
            return ("Unknown", "-", "-");
        }

        public int GetDbIdByArduinoCode(int arduinoCode)
        {
            var machine = _context.Lines.AsNoTracking().FirstOrDefault(x => x.MachineCode == arduinoCode);
            return machine != null ? machine.Id : -1;
        }

        public int GetNextAvailableId()
        {
            // 1. Cek apakah ada ID yang bolong/hilang (menggunakan method GetMissingIds yg sudah Anda buat)
            var missingIds = GetMissingIds();

            if (missingIds.Count > 0)
            {
                // Jika ada ID bolong (misal 1, 3, 4 -> bolong 2), pakai yg terkecil (2)
                return missingIds.First();
            }

            // 2. Jika tidak ada yang bolong, ambil ID terbesar + 1
            // Gunakan (int?) agar aman jika tabel kosong
            int maxId = _context.Lines.Max(x => (int?)x.MachineCode) ?? 0;

            return maxId + 1;
        }

        // --- 6. CHECK MISSING MACHINE CODES (PERBAIKAN ERROR == METHOD GROUP) ---
        public List<int> GetMissingIds()
        {
            // Ambil semua MachineCode
            // PERBAIKAN:
            // 1. Hapus 'x.MachineCode != null' (karena int pasti ada isinya)
            // 2. Hapus 'x.MachineCode.Value' (karena int tidak punya .Value)
            // 3. Pastikan '.ToList()' ada di akhir agar jadi List<int> bukan Query

            var existingCodes = _context.Lines
                                      .Where(x => x.MachineCode > 0) // Ambil yang kodenya valid
                                      .Select(x => x.MachineCode)    // Select int langsung
                                      .OrderBy(x => x)
                                      .ToList();                     // WAJIB: Eksekusi query ke List

            var missingCodes = new List<int>();

            // Sekarang 'existingCodes' adalah List, jadi properti .Count (angka) tersedia.
            // Tidak akan error "method group" lagi.
            if (existingCodes.Count == 0) return missingCodes;

            int maxCode = existingCodes.Last();
            int currentCheck = 1;

            foreach (var code in existingCodes)
            {
                while (currentCheck < code)
                {
                    missingCodes.Add(currentCheck);
                    currentCheck++;
                }
                currentCheck = code + 1;
            }

            return missingCodes;
        }

        public void ClearCache()
        {
            _machineCache.Clear();
        }
    }
}