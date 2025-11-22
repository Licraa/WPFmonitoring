using System;
using System.Collections.Generic;

namespace MonitoringApp.Models;

public partial class Line
{
    public int Id { get; set; }

    public string Line1 { get; set; } = null!;

    public string LineProduction { get; set; } = null!;

    public string Process { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Remark { get; set; } = null!;
}
