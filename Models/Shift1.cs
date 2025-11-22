using System;
using System.Collections.Generic;

namespace MonitoringApp.Models;

public partial class Shift1
{
    public int Id { get; set; }

    public int? NilaiA0 { get; set; }

    public int? NilaiTerakhirA2 { get; set; }

    public double? DurasiTerakhirA4 { get; set; }

    public double? RatarataTerakhirA4 { get; set; }

    public int? Parthours { get; set; }

    public TimeOnly? DataCh1 { get; set; }

    public TimeOnly? Uptime { get; set; }

    public int PDatach1 { get; set; }

    public int PUptime { get; set; }

    public DateTime LastUpdate { get; set; }
}
