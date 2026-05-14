namespace XDBDatabase_WinUI.Models;

public sealed class AppSettings
{
    public string XamppRoot { get; set; } = "";
    public string ApacheServiceName { get; set; } = "Apache2.4";
    public string MysqlServiceNames { get; set; } = "mysql;MariaDB";
    public string Browser { get; set; } = "Default";
    public string ActivePhpName { get; set; } = "php";
}
