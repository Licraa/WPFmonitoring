using System;
using System.Collections.Generic;

namespace MonitoringApp.Models;

public partial class user
{
    public int? Id { get; set; }
    public string? username { get; set; }
    public string? password { get; set; }
    public string? role { get; set; }
}
