using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MonitoringApp.Data;
using MonitoringApp.Models;
using MonitoringApp.ViewModels;

namespace MonitoringApp.Services
{
    public class SummaryService
    {
        private readonly AppDbContext _context;

       
        public SummaryService(AppDbContext context)
        {
            _context = context;
        }

        // --- 1. DASHBOARD SUMMARY (Group by Line) ---
        public List<LineSummary> GetLineSummary()
        {
            
            var rawData = (from l in _context.Lines.AsNoTracking()
                           join dr in _context.DataRealtimes.AsNoTracking() on l.Id equals dr.Id into joined
                           from dr in joined.DefaultIfEmpty()
                           select new { l.LineProduction, dr }).ToList();

            
            var result = rawData
                .GroupBy(x => x.LineProduction)
                .Select(g => new LineSummary
                {
                    lineProduction = g.Key,
                    TotalMachine = g.Count(),

                    // Logika Status: Active jika NilaiA0 == 1
                    Active = g.Count(x => x.dr != null && x.dr.NilaiA0 == 1),
                    Inactive = g.Count(x => x.dr == null || x.dr.NilaiA0 != 1),

                    // Agregasi Data Lainnya (Handle null dengan '?? 0')
                    Count = g.Sum(x => x.dr?.NilaiTerakhirA2 ?? 0),
                    Cycle = g.Sum(x => (double)(x.dr?.DurasiTerakhirA4 ?? 0)),
                    AvgCycle = g.Sum(x => (double)(x.dr?.RataRataTerakhirA4 ?? 0)),
                    PartHours = g.Sum(x => (double)(x.dr?.PartHours ?? 0)),
                    DowntimePercent = g.Sum(x => (double)(x.dr?.P_DataCh1 ?? 0)),
                    UptimePercent = g.Sum(x => (double)(x.dr?.P_Uptime ?? 0))
                })
                .OrderBy(x => x.lineProduction)
                .ToList();

            return result;
        }

        // --- 2. MACHINE DETAIL (Per Line) ---
        public List<MachineDetailViewModel> GetMachineDetailByLine(string lineProduction)
        {
            var result = new List<MachineDetailViewModel>();

            // Query Data Utama (Line + Realtime)
            
            var machines = (from l in _context.Lines.AsNoTracking()
                            join dr in _context.DataRealtimes.AsNoTracking() on l.Id equals dr.Id into joined
                            from dr in joined.DefaultIfEmpty()
                            where l.LineProduction == lineProduction
                            select new
                            {
                                l.Id,
                                l.LineProduction,
                                l.Name,
                                l.Process,
                                l.Remark,
                                
                                Realtime = dr
                            }).ToList();

           
            foreach (var item in machines)
            {
                var vm = new MachineDetailViewModel
                {
                    Id = item.Id,
                    Line = item.LineProduction,
                    Name = item.Name,
                    Process = item.Process,
                    Remark = item.Remark ?? "-", 

                    
                    NilaiA0 = item.Realtime?.NilaiA0 ?? 0,
                    PartHours = item.Realtime?.PartHours ?? 0,
                    Cycle = item.Realtime?.DurasiTerakhirA4 ?? 0,
                    AvgCycle = item.Realtime?.RataRataTerakhirA4 ?? 0,
                    LastUpdate = item.Realtime != null ? item.Realtime.Last_Update.ToString("yyyy-MM-dd HH:mm:ss") : "-"
                };

                
                vm.Shift1 = GetShiftSummaryLINQ(_context.Shift1s, item.Id);
                vm.Shift2 = GetShiftSummaryLINQ(_context.Shift2s, item.Id);
                vm.Shift3 = GetShiftSummaryLINQ(_context.Shift3s, item.Id);

                result.Add(vm);
            }

            return result;
        }

        // --- HELPER: Generic Shift Fetcher ---
        private ShiftSummaryViewModel GetShiftSummaryLINQ<T>(DbSet<T> dbSet, int mesinId) where T : MachineDataBase
        {
            
            var data = dbSet.AsNoTracking()
                            .FirstOrDefault(x => x.Id == mesinId);

            if (data == null)
            {
                return new ShiftSummaryViewModel
                {
                    Count = 0,
                    Downtime = "00:00:00",
                    Uptime = "00:00:00",
                    DowntimePercent = 0,
                    UptimePercent = 0
                };
            }

            
            return new ShiftSummaryViewModel
            {
                Count = data.NilaiTerakhirA2,
                Downtime = data.DataCh1.ToString(@"hh\:mm\:ss"),
                Uptime = data.Uptime.ToString(@"hh\:mm\:ss"),
                DowntimePercent = data.P_DataCh1,
                UptimePercent = data.P_Uptime
            };
        }
    }
}