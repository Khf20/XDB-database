using CommunityToolkit.Mvvm.ComponentModel;

namespace XDBDatabase_WinUI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty] public partial string PageTitle { get; set; } = "Dashboard";
    [ObservableProperty] public partial string PageSubtitle { get; set; } = @"C:\xampp";
    [ObservableProperty] public partial string ActiveServicesText { get; set; } = "0";
    [ObservableProperty] public partial string DashboardClockText { get; set; } = "00:00:00";
    [ObservableProperty] public partial string SidebarStatusText { get; set; } = "Checking services";
    [ObservableProperty] public partial string SidebarTimeText { get; set; } = "-";
    [ObservableProperty] public partial string XamppValidationText { get; set; } = "";
    [ObservableProperty] public partial string SettingsPathText { get; set; } = "";
    [ObservableProperty] public partial string ActivityLogPathText { get; set; } = "";

    [ObservableProperty] public partial string XamppRoot { get; set; } = @"C:\xampp";
    [ObservableProperty] public partial string ApacheServiceName { get; set; } = "Apache2.4";
    [ObservableProperty] public partial string MysqlServiceNames { get; set; } = "mysql;MariaDB";
    [ObservableProperty] public partial string Browser { get; set; } = "Default";

    public void SetPage(string title, string subtitle)
    {
        PageTitle = title;
        PageSubtitle = subtitle;
    }

    public void SetSettings(string root, string apacheService, string mysqlServices, string selectedBrowser, string settingsPath, string activityLogPath)
    {
        XamppRoot = root;
        ApacheServiceName = apacheService;
        MysqlServiceNames = mysqlServices;
        Browser = selectedBrowser;
        SettingsPathText = settingsPath;
        ActivityLogPathText = activityLogPath;
        PageSubtitle = root;
    }

    public void SetServiceSummary(int runningCount, DateTime now)
    {
        ActiveServicesText = runningCount.ToString();
        DashboardClockText = now.ToString("HH:mm:ss");
        SidebarStatusText = runningCount + " service aktif";
        SidebarTimeText = DashboardClockText;
    }
}
