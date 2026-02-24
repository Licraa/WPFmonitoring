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
        // PENTING: Gunakan Factory agar setiap request query membuat context baru yang bersih
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SummaryService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // --- 1. DASHBOARD SUMMARY (Group by Line) ---
        public List<LineSummary> GetLineSummary()
        {
            using (var context = _contextFactory.CreateDbContext())
            {
                // Ambil data mentah dengan AsNoTracking untuk kecepatan dan akurasi data
                var rawData = context.Lines
                    .AsNoTracking()
                    .Select(l => new {
                        l.LineProduction,
                        Data = context.DataRealtimes.AsNoTracking().FirstOrDefault(dr => dr.Id == l.Id)
                    })
                    .ToList(); // Eksekusi query ke SQL

                return rawData
                    .GroupBy(x => x.LineProduction)
                    .Select(g => new LineSummary
                    {
                        lineProduction = g.Key,
                        TotalMachine = g.Count(),
                        Active = g.Count(x => x.Data != null && x.Data.NilaiA0 == 1),
                        Inactive = g.Count(x => x.Data == null || x.Data.NilaiA0 != 1),
                        // Hitung total Count dan PartHours untuk dashboard utama jika perlu
                        Count = g.Sum(x => x.Data?.NilaiTerakhirA2 ?? 0),
                        PartHours = g.Sum(x => x.Data?.PartHours ?? 0)
                    })
                    .ToList();
            }
        }

        // --- 2. MACHINE DETAIL (Per Line) ---
        public List<MachineDetailViewModel> GetMachineDetailByLine(string lineProduction)
        {
            using (var context = _contextFactory.CreateDbContext())
            {
                var machines = (from l in context.Lines.AsNoTracking()
                                join dr in context.DataRealtimes.AsNoTracking() on l.Id equals dr.Id into joined
                                from dr in joined.DefaultIfEmpty()
                                where l.LineProduction == lineProduction
                                select new
                                {
                                    l.Id,
                                    l.MachineCode,
                                    l.LineProduction,
                                    l.Name,
                                    l.Process,
                                    l.Remark,
                                    Realtime = dr
                                }).ToList();

                if (!machines.Any()) return new List<MachineDetailViewModel>();

                var machineIds = machines.Select(m => m.Id).ToList();

                // Ambil Data Shift secara Bulk dengan Context baru
                var shift1Dict = context.Shift1s.AsNoTracking()
                    .Where(x => machineIds.Contains(x.Id))
                    .ToDictionary(k => k.Id, v => v);

                var shift2Dict = context.Shift2s.AsNoTracking()
                    .Where(x => machineIds.Contains(x.Id))
                    .ToDictionary(k => k.Id, v => v);

                var shift3Dict = context.Shift3s.AsNoTracking()
                    .Where(x => machineIds.Contains(x.Id))
                    .ToDictionary(k => k.Id, v => v);

                var result = new List<MachineDetailViewModel>();

                foreach (var item in machines)
                {
                    var vm = new MachineDetailViewModel
                    {
                        Id = item.Id,
                        MachineCode = item.MachineCode,
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

                    vm.Shift1 = MapShiftToViewModel(shift1Dict.ContainsKey(item.Id) ? shift1Dict[item.Id] : null);
                    vm.Shift2 = MapShiftToViewModel(shift2Dict.ContainsKey(item.Id) ? shift2Dict[item.Id] : null);
                    vm.Shift3 = MapShiftToViewModel(shift3Dict.ContainsKey(item.Id) ? shift3Dict[item.Id] : null);

                    result.Add(vm);
                }

                return result;
            }
        }

        private ShiftSummaryViewModel MapShiftToViewModel(MachineDataBase? data)
        {
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