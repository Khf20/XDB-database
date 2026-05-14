namespace XDBDatabase_WinUI.Models;

public sealed class XamppState
{
    public bool ApacheRunning { get; set; }
    public bool MysqlRunning { get; set; }
    public List<int> ApachePids { get; set; } = new();
    public List<int> MysqlPids { get; set; } = new();
    public string? Port80 { get; set; }
    public string? Port443 { get; set; }
    public string? Port3306 { get; set; }
    public List<PortInfo> PortConflicts { get; set; } = new();
    public string ApacheVersion { get; set; } = "-";
    public string ApachePhpVersion { get; set; } = "-";
    public string PhpCliVersion { get; set; } = "-";
    public string MysqlVersion { get; set; } = "-";
}
