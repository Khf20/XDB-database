namespace XDBDatabase_WinUI.Models;

public sealed class PortInfo
{
    public int Port { get; set; }
    public int Pid { get; set; }
    public string ProcessName { get; set; } = "";
    public string ProcessPath { get; set; } = "";
    public bool OwnedByXampp { get; set; }
}
