using System;
using System.Collections.Generic;
using System.Linq; // Wajib untuk LINQ
using Microsoft.EntityFrameworkCore; // Wajib untuk EF Core
using MonitoringApp.Data;
using MonitoringApp.Models;
using MonitoringApp.ViewModels;

namespace MonitoringApp.Services
{
    public class MachineService
    {
        // 1. Deklarasi Context Database (Pengganti DatabaseService)
        private readonly AppDbContext _context;

        // 2. Deklarasi Cache Memori
        private readonly Dictionary<int, (string Name, string Line, string Process)> _machineCache = new();

        public MachineService()
        {
            // Karena belum menggunakan Dependency Injection penuh di App.xaml.cs,
            // kita inisialisasi Context secara manual menggunakan Factory yang sudah Anda buat.
            _context = new AppDbContextFactory().CreateDbContext(null);
        }

        // --- 1. GET ALL (READ) ---
        public List<MachineDetailViewModel> GetAllMachines()
        {
            // Menggunakan LINQ (Lebih bersih & aman daripada Raw SQL)
            var query = from l in _context.Lines
                        join dr in _context.DataRealtimes on l.Id equals dr.Id into joinedData
                        from dr in joinedData.DefaultIfEmpty() // LEFT JOIN
                        orderby l.LineProduction, l.Id
                        select new MachineDetailViewModel
                        {
                            Id = l.Id,
                            Line = l.LineProduction,
                            Name = l.Name,
                            Process = l.Process,
                            Remark = l.Remark ?? "", // Handle null

                            // Ambil data realtime jika ada
                            NilaiA0 = dr != null ? dr.NilaiA0 : 0,

                            // Format tanggal
                            LastUpdate = (dr != null) ? dr.Last_Update.ToString("yyyy-MM-dd HH:mm:ss") : "-"
                        };

            return query.ToList();
        }

        // --- 2. UPDATE ---
        public bool UpdateMachine(int id, string name, string process, string line, string remark)
        {
            // Hapus cache agar data fresh saat diambil lagi
            if (_machineCache.ContainsKey(id)) _machineCache.Remove(id);

            try
            {
                var entity = _context.Lines.FirstOrDefault(x => x.Id == id);
                if (entity == null) return false;

                entity.Name = name;
                entity.Process = process;
                entity.LineProduction = line;
                entity.Remark = remark;

                _context.SaveChanges(); // EF Core otomatis generate query UPDATE
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update Error: {ex.Message}");
                return false;
            }
        }

        // --- 3. ADD (CREATE) ---
        public bool AddMachine(string name, string process, string line, string remark)
        {
            try
            {
                var newMachine = new Line
                {
                    Name = name,
                    Process = process,
                    LineProduction = line,
                    Remark = remark,
                    // Line1 tidak diisi sesuai kode lama (atau default null)
                };

                _context.Lines.Add(newMachine);
                _context.SaveChanges(); // EF Core otomatis generate query INSERT
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Add Error: {ex.Message}");
                return false;
            }
        }

        // --- 4. DELETE ---
        public bool DeleteMachine(int id)
        {
            if (_machineCache.ContainsKey(id)) _machineCache.Remove(id);

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // Hapus Data Realtime Terkait
                var realtimeData = _context.DataRealtimes.FirstOrDefault(x => x.Id == id);
                if (realtimeData != null) _context.DataRealtimes.Remove(realtimeData);

                // Hapus Data Shift Terkait
                var shift1 = _context.Shift1s.FirstOrDefault(x => x.Id == id);
                if (shift1 != null) _context.Shift1s.Remove(shift1);

                var shift2 = _context.Shift2s.FirstOrDefault(x => x.Id == id);
                if (shift2 != null) _context.Shift2s.Remove(shift2);

                var shift3 = _context.Shift3s.FirstOrDefault(x => x.Id == id);
                if (shift3 != null) _context.Shift3s.Remove(shift3);

                // Hapus Mesin (Line)
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

        // --- 5. CACHING & INFO (Untuk Serial Monitor) ---
        public (string Name, string Line, string Process) GetMachineInfoCached(int id)
        {
            // A. Cek Cache Memory
            if (_machineCache.TryGetValue(id, out var cachedInfo))
            {
                return cachedInfo;
            }

            // B. Ambil dari DB via EF Core
            try
            {
                var machine = _context.Lines
                    .Where(l => l.Id == id)
                    .Select(l => new { l.Name, l.LineProduction, l.Process })
                    .FirstOrDefault();

                if (machine != null)
                {
                    var result = (machine.Name ?? "Unknown", machine.LineProduction ?? "-", machine.Process ?? "-");
                    _machineCache[id] = result; // Simpan ke cache
                    return result;
                }
            }
            catch { /* Ignore error, return default */ }

            return ("Unknown", "-", "-");
        }

        public void ClearCache()
        {
            _machineCache.Clear();
        }
    }
}