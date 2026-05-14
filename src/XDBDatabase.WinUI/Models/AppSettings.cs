namespace XDBDatabase_WinUI.Models;

public sealed class AppSettings
{
    public string XamppRoot { get; set; } = @"C:\xampp";
    public string ApacheServiceName { get; set; } = "Apache2.4";
    public string MysqlServiceNames { get; set; } = "mysql;MariaDB";
    public string Browser { get; set; } = "Default";
}
