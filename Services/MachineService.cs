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

        // Constructor Injection
        public MachineService(AppDbContext context)
        {
            _context = context;
        }

        // --- 1. GET ALL (READ) ---
        public List<MachineDetailViewModel> GetAllMachines()
        {
            var query = from l in _context.Lines
                        join dr in _context.DataRealtimes on l.Id equals dr.Id into joinedData
                        from dr in joinedData.DefaultIfEmpty()
                        orderby l.LineProduction, l.Id
                        select new MachineDetailViewModel
                        {
                            Id = l.Id,
                            Line = l.LineProduction,
                            Name = l.Name,
                            Process = l.Process,
                            Remark = l.Remark ?? "",
                            NilaiA0 = dr != null ? dr.NilaiA0 : 0,
                            LastUpdate = dr != null ? dr.Last_Update.ToString("yyyy-MM-dd HH:mm:ss") : "-"
                        };
            return query.ToList();
        }

        // --- 2. UPDATE ---
        public bool UpdateMachine(int id, string name, string process, string line, string remark)
        {
            if (_machineCache.ContainsKey(id)) _machineCache.Remove(id);
            try
            {
                var entity = _context.Lines.FirstOrDefault(x => x.Id == id);
                if (entity == null) return false;
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
        public bool AddMachine(string name, string process, string line, string remark)
        {
            try
            {
                _context.Lines.Add(new Line { Name = name, Process = process, LineProduction = line, Remark = remark });
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

        public void ClearCache()
        {
            _machineCache.Clear();
        }
    }
}