using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XDBDatabase_WinUI.Models;

namespace XDBDatabase_WinUI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty] public partial string PageTitle { get; set; } = "Dashboard";
    [ObservableProperty] public partial string PageSubtitle { get; set; } = "Portable root";
    [ObservableProperty] public partial string ActiveServicesText { get; set; } = "0";
    [ObservableProperty] public partial string DashboardClockText { get; set; } = "00:00:00";
    [ObservableProperty] public partial string SidebarStatusText { get; set; } = "Checking services";
    [ObservableProperty] public partial string SidebarTimeText { get; set; } = "-";
    [ObservableProperty] public partial string XamppValidationText { get; set; } = "";
    [ObservableProperty] public partial string SettingsPathText { get; set; } = "";
    [ObservableProperty] public partial string ActivityLogPathText { get; set; } = "";

    [ObservableProperty] public partial string XamppRoot { get; set; } = "";
    [ObservableProperty] public partial string ApacheServiceName { get; set; } = "Apache2.4";
    [ObservableProperty] public partial string MysqlServiceNames { get; set; } = "mysql;MariaDB";
    [ObservableProperty] public partial string Browser { get; set; } = "Default";
    [ObservableProperty] public partial PhpInstall? SelectedPhpInstall { get; set; }

    public ObservableCollection<PhpInstall> PhpInstalls { get; } = new();

    internal Func<Task>? RefreshAction { get; set; }
    internal Func<Task>? StartApacheAction { get; set; }
    internal Func<Task>? StopApacheAction { get; set; }
    internal Func<Task>? RestartApacheAction { get; set; }
    internal Func<Task>? StartMysqlAction { get; set; }
    internal Func<Task>? StopMysqlAction { get; set; }
    internal Func<Task>? RestartMysqlAction { get; set; }
    internal Func<Task>? ReloadPhpAction { get; set; }
    internal Func<Task>? UsePhpAction { get; set; }
    internal Func<Task>? RollbackPhpAction { get; set; }
    internal Func<Task>? RepairPhpMyAdminAction { get; set; }

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

    [RelayCommand]
    private Task RefreshAsync() => InvokeAsync(RefreshAction);

    [RelayCommand]
    private Task StartApacheAsync() => InvokeAsync(StartApacheAction);

    [RelayCommand]
    private Task StopApacheAsync() => InvokeAsync(StopApacheAction);

    [RelayCommand]
    private Task RestartApacheAsync() => InvokeAsync(RestartApacheAction);

    [RelayCommand]
    private Task StartMysqlAsync() => InvokeAsync(StartMysqlAction);

    [RelayCommand]
    private Task StopMysqlAsync() => InvokeAsync(StopMysqlAction);

    [RelayCommand]
    private Task RestartMysqlAsync() => InvokeAsync(RestartMysqlAction);

    [RelayCommand]
    private Task ReloadPhpAsync() => InvokeAsync(ReloadPhpAction);

    [RelayCommand]
    private Task UsePhpAsync() => InvokeAsync(UsePhpAction);

    [RelayCommand]
    private Task RollbackPhpAsync() => InvokeAsync(RollbackPhpAction);

    [RelayCommand]
    private Task RepairPhpMyAdminAsync() => InvokeAsync(RepairPhpMyAdminAction);

    private static Task InvokeAsync(Func<Task>? action)
    {
        return action?.Invoke() ?? Task.CompletedTask;
    }
}
