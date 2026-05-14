using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using XDBDatabase_WinUI.Models;
using XDBDatabase_WinUI.ViewModels;

namespace XDBDatabase_WinUI;

public sealed partial class MainPage : Page
{
    private readonly string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XDB-database");
    private readonly string settingsFile;
    private readonly string activityLogFile;
    private readonly MainViewModel ViewModel = new();
    private AppSettings settings;
    private string root = "";
    private string apacheServiceName = "Apache2.4";
    private string[] mysqlServiceNames = { "mysql", "MariaDB" };
    private string apacheExe = "";
    private string apacheConf = "";
    private string xamppConf = "";
    private string mysqlExe = "";
    private string mysqlAdminExe = "";
    private string mysqlClientExe = "";
    private string mysqlIni = "";
    private string phpExe = "";
    private string apacheErrorLog = "";
    private string mysqlErrorLog = "";
    private string xamppControlLog = "";
    private string apacheAccessLog = "";
    private string httpdVhostsConf = "";
    private string stackRoot = "";
    private string apacheRoot = "";
    private string mariadbRoot = "";
    private string phpStackRoot = "";
    private string selectedPhpDir = "";
    private string wwwDir = "";
    private string dataDir = "";
    private string apacheTemplateFile = "";
    private string mariadbTemplateFile = "";
    private ServiceState? apacheTransientState;
    private ServiceState? mysqlTransientState;
    private bool apacheOperationActive;
    private bool mysqlOperationActive;
    private bool operationBusy;
    private string? previousPhpName;
    private bool xamppInstallValid;
    private readonly DispatcherTimer timer = new();
    private static readonly HttpClient localHttp = new() { Timeout = TimeSpan.FromMilliseconds(900) };

    public MainPage()
    {
        settingsFile = Path.Combine(appDataDir, "settings.json");
        activityLogFile = Path.Combine(appDataDir, "activity.log");
        settings = new AppSettings();
        ApplySettingsToPaths();

        InitializeComponent();
        DataContext = ViewModel;
        PhpList.ItemsSource = ViewModel.PhpInstalls;
        BindViewModelActions();

        Loaded += MainPage_Loaded;
        timer.Interval = TimeSpan.FromSeconds(5);
        timer.Tick += async (_, _) => await RefreshStatusAsync();
    }

    private void BindViewModelActions()
    {
        ViewModel.RefreshAction = RefreshStatusAsync;
        ViewModel.StartApacheAction = () => RunBusy("Start Apache", StartApacheAsync);
        ViewModel.StopApacheAction = () => RunBusy("Stop Apache", StopApacheAsync);
        ViewModel.RestartApacheAction = () => RunBusy("Restart Apache", async () => { await StopApacheAsync(); await StartApacheAsync(); });
        ViewModel.StartMysqlAction = () => RunBusy("Start MySQL/MariaDB", StartMysqlAsync);
        ViewModel.StopMysqlAction = () => RunBusy("Stop MySQL/MariaDB", StopMysqlAsync);
        ViewModel.RestartMysqlAction = () => RunBusy("Restart MySQL/MariaDB", async () => { await StopMysqlAsync(); await StartMysqlAsync(); });
        ViewModel.ReloadPhpAction = RefreshPhpListAsync;
        ViewModel.UsePhpAction = UseSelectedPhpAsync;
        ViewModel.RollbackPhpAction = RollbackLastPhpBackupAsync;
        ViewModel.RepairPhpMyAdminAction = () => RunBusy("Repair phpMyAdmin access", RepairPhpMyAdminAccessAsync);
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        settings = await LoadSettingsAsync();
        ApplySettingsToPaths();
        EnsurePortableDirectories();
        SyncSettingsView();
        ShowView(DashboardView, "Dashboard");
        UpdateValidationStatus();
        await RefreshPhpListAsync();
        await RefreshStatusAsync();
        await LoadLogAsync();
        await AppendActivityAsync("App", "Started", "XDB-database launched. Portable root: " + root);
        timer.Start();
    }

    private void DashboardNav_Click(object sender, RoutedEventArgs e) => ShowView(DashboardView, "Dashboard");
    private async void LogsNav_Click(object sender, RoutedEventArgs e) { ShowView(LogsView, "Logs"); await LoadLogAsync(); }
    private async void PhpNav_Click(object sender, RoutedEventArgs e) { ShowView(PhpView, "PHP Switcher"); await RefreshPhpListAsync(); }
    private void ToolsNav_Click(object sender, RoutedEventArgs e) => ShowView(ToolsView, "Tools");
    private void SettingsNav_Click(object sender, RoutedEventArgs e) => ShowView(SettingsView, "Settings");
    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    private void PhpMyAdminButton_Click(object sender, RoutedEventArgs e) => OpenUrl("http://localhost/phpmyadmin/");
    private void OpenDashboard_Click(object sender, RoutedEventArgs e) => OpenUrl("http://localhost/dashboard/");
    private void OpenHtdocs_Click(object sender, RoutedEventArgs e) => OpenPath(wwwDir);
    private void OpenApacheConfig_Click(object sender, RoutedEventArgs e) => OpenPath(Path.Combine(apacheRoot, "conf"));
    private void OpenMysqlData_Click(object sender, RoutedEventArgs e) => OpenPath(dataDir);
    private void OpenRoot_Click(object sender, RoutedEventArgs e) => OpenPath(root);
    private async void OpenPhpIni_Click(object sender, RoutedEventArgs e) => OpenPath(Path.Combine(await GetApachePhpDirAsync(), "php.ini"));
    private void OpenMysqlIni_Click(object sender, RoutedEventArgs e) => OpenPath(mysqlIni);
    private void OpenHttpdConf_Click(object sender, RoutedEventArgs e) => OpenPath(apacheConf);
    private void OpenVhostsConf_Click(object sender, RoutedEventArgs e) => OpenPath(httpdVhostsConf);
    private void OpenErrorLog_Click(object sender, RoutedEventArgs e) => OpenPath(apacheErrorLog);
    private void OpenWindowsServices_Click(object sender, RoutedEventArgs e) => OpenPortableTerminal();
    private async void ReloadLogButton_Click(object sender, RoutedEventArgs e) => await LoadLogAsync();
    private async void ReloadPhpButton_Click(object sender, RoutedEventArgs e) => await ViewModel.ReloadPhpCommand.ExecuteAsync(null);
    private async void LogCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => await LoadLogAsync();
    private void PhpList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PhpList.SelectedItem is PhpInstall selected)
        {
            ViewModel.SelectedPhpInstall = selected;
        }
        UpdatePhpPreview();
    }
    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e) => await SaveSettingsFromViewAsync();
    private async void ResetSettingsButton_Click(object sender, RoutedEventArgs e) => await ResetSettingsAsync();
    private async void RepairPhpMyAdminButton_Click(object sender, RoutedEventArgs e) => await ViewModel.RepairPhpMyAdminCommand.ExecuteAsync(null);
    private async void RollbackPhpButton_Click(object sender, RoutedEventArgs e) => await ViewModel.RollbackPhpCommand.ExecuteAsync(null);

    private async void StartApacheButton_Click(object sender, RoutedEventArgs e) => await ViewModel.StartApacheCommand.ExecuteAsync(null);
    private async void StopApacheButton_Click(object sender, RoutedEventArgs e) => await ViewModel.StopApacheCommand.ExecuteAsync(null);
    private async void RestartApacheButton_Click(object sender, RoutedEventArgs e) => await ViewModel.RestartApacheCommand.ExecuteAsync(null);
    private async void StartMysqlButton_Click(object sender, RoutedEventArgs e) => await ViewModel.StartMysqlCommand.ExecuteAsync(null);
    private async void StopMysqlButton_Click(object sender, RoutedEventArgs e) => await ViewModel.StopMysqlCommand.ExecuteAsync(null);
    private async void RestartMysqlButton_Click(object sender, RoutedEventArgs e) => await ViewModel.RestartMysqlCommand.ExecuteAsync(null);

    private void ShowView(FrameworkElement activeView, string title)
    {
        DashboardView.Visibility = Visibility.Collapsed;
        LogsView.Visibility = Visibility.Collapsed;
        PhpView.Visibility = Visibility.Collapsed;
        ToolsView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        activeView.Visibility = Visibility.Visible;
        ViewModel.SetPage(title, root);
    }

    private async Task RunBusy(string actionName, Func<Task> action)
    {
        SetBusy(false);
        operationBusy = true;
        SetOperationState(actionName, true, false);
        await AppendActivityAsync(actionName, "Started", "");
        try
        {
            await action();
            await AppendActivityAsync(actionName, "Succeeded", "");
            SetOperationState(actionName, false, false);
        }
        catch (Exception ex)
        {
            await AppendActivityAsync(actionName, "Failed", ex.Message);
            SetOperationState(actionName, false, true);
            await ShowMessageAsync("XDB-database", ex.Message);
        }
        finally
        {
            SetBusy(true);
            operationBusy = false;
            UpdateValidationStatus();
            await RefreshStatusAsync();
            await LoadLogAsync();
        }
    }

    private void SetOperationState(string actionName, bool active, bool failed)
    {
        if (actionName.Contains("Apache", StringComparison.OrdinalIgnoreCase))
        {
            apacheOperationActive = active;
            apacheTransientState = failed ? ServiceState.Error : active ? OperationState(actionName) : null;
        }

        if (actionName.Contains("MySQL", StringComparison.OrdinalIgnoreCase) ||
            actionName.Contains("MariaDB", StringComparison.OrdinalIgnoreCase))
        {
            mysqlOperationActive = active;
            mysqlTransientState = failed ? ServiceState.Error : active ? OperationState(actionName) : null;
        }
    }

    private static ServiceState OperationState(string actionName)
    {
        if (actionName.Contains("Start", StringComparison.OrdinalIgnoreCase)) return ServiceState.Starting;
        if (actionName.Contains("Stop", StringComparison.OrdinalIgnoreCase)) return ServiceState.Stopping;
        if (actionName.Contains("Restart", StringComparison.OrdinalIgnoreCase)) return ServiceState.Restarting;
        if (actionName.Contains("Repair", StringComparison.OrdinalIgnoreCase)) return ServiceState.Repairing;
        return ServiceState.Starting;
    }

    private void SetBusy(bool enabled)
    {
        foreach (var button in new[]
        {
            StartApacheButton, StopApacheButton, RestartApacheButton,
            StartMysqlButton, StopMysqlButton, RestartMysqlButton
        })
        {
            button.IsEnabled = enabled;
        }
    }

    private async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(settingsFile)) return new AppSettings();
            var json = await File.ReadAllTextAsync(settingsFile);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    private async Task SaveSettingsAsync()
    {
        Directory.CreateDirectory(appDataDir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(settingsFile, json);
    }

    private void ApplySettingsToPaths()
    {
        root = string.IsNullOrWhiteSpace(settings.XamppRoot) ? ResolveDefaultPortableRoot() : settings.XamppRoot.Trim().TrimEnd('\\');
        apacheServiceName = string.IsNullOrWhiteSpace(settings.ApacheServiceName) ? "Apache2.4" : settings.ApacheServiceName.Trim();
        mysqlServiceNames = ParseServiceNames(settings.MysqlServiceNames);

        stackRoot = Path.Combine(root, "stack");
        apacheRoot = Path.Combine(stackRoot, "apache");
        mariadbRoot = Path.Combine(stackRoot, "mariadb");
        phpStackRoot = stackRoot;
        wwwDir = Path.Combine(root, "www");
        dataDir = Path.Combine(root, "data");
        selectedPhpDir = ResolveSelectedPhpDirectory();

        apacheExe = Path.Combine(apacheRoot, @"bin\httpd.exe");
        apacheConf = Path.Combine(apacheRoot, @"conf\httpd.conf");
        xamppConf = Path.Combine(apacheRoot, @"conf\extra\httpd-xampp.conf");
        mysqlExe = Path.Combine(mariadbRoot, @"bin\mysqld.exe");
        mysqlAdminExe = Path.Combine(mariadbRoot, @"bin\mysqladmin.exe");
        mysqlClientExe = Path.Combine(mariadbRoot, @"bin\mysql.exe");
        mysqlIni = Path.Combine(mariadbRoot, "my.ini");
        phpExe = Path.Combine(selectedPhpDir, "php.exe");
        apacheErrorLog = Path.Combine(apacheRoot, @"logs\error.log");
        mysqlErrorLog = Path.Combine(dataDir, "mysql_error.log");
        xamppControlLog = activityLogFile;
        apacheAccessLog = Path.Combine(apacheRoot, @"logs\access.log");
        httpdVhostsConf = Path.Combine(apacheRoot, @"conf\extra\httpd-vhosts.conf");
        apacheTemplateFile = Path.Combine(root, @"config\httpd.template.conf");
        mariadbTemplateFile = Path.Combine(root, @"config\my.template.ini");
    }

    private static string ResolveDefaultPortableRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "stack")) ||
                Directory.Exists(Path.Combine(directory.FullName, "src")) && File.Exists(Path.Combine(directory.FullName, "build-winui.bat")))
            {
                return directory.FullName.TrimEnd('\\');
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory.TrimEnd('\\');
    }

    private string ResolveSelectedPhpDirectory()
    {
        var name = string.IsNullOrWhiteSpace(settings.ActivePhpName) ? "php" : settings.ActivePhpName.Trim();
        var preferred = Path.Combine(stackRoot, name);
        if (Directory.Exists(preferred) || name.Equals("php", StringComparison.OrdinalIgnoreCase))
        {
            return preferred;
        }

        return Directory.Exists(stackRoot)
            ? Directory.GetDirectories(stackRoot, "php*").OrderBy(path => path).FirstOrDefault() ?? preferred
            : preferred;
    }

    private void EnsurePortableDirectories()
    {
        foreach (var directory in new[]
        {
            root,
            stackRoot,
            wwwDir,
            dataDir,
            Path.Combine(root, "config"),
            Path.Combine(apacheRoot, "conf"),
            Path.Combine(apacheRoot, "logs"),
            Path.Combine(mariadbRoot, "logs")
        })
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string[] ParseServiceNames(string value)
    {
        var names = value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return names.Length == 0 ? new[] { "mysql", "MariaDB" } : names;
    }

    private void SyncSettingsView()
    {
        XamppRootBox.Text = root;
        ApacheServiceBox.Text = settings.ApacheServiceName;
        MysqlServiceBox.Text = settings.MysqlServiceNames;
        BrowserCombo.SelectedIndex = settings.Browser switch
        {
            "Edge" => 1,
            "Chrome" => 2,
            "Firefox" => 3,
            _ => 0
        };
        ViewModel.SetSettings(root, settings.ApacheServiceName, settings.MysqlServiceNames, settings.Browser, settingsFile, activityLogFile);
    }

    private async Task SaveSettingsFromViewAsync()
    {
        settings.XamppRoot = string.IsNullOrWhiteSpace(XamppRootBox.Text) ? ResolveDefaultPortableRoot() : XamppRootBox.Text.Trim().TrimEnd('\\');
        settings.ApacheServiceName = string.IsNullOrWhiteSpace(ApacheServiceBox.Text) ? "Apache2.4" : ApacheServiceBox.Text.Trim();
        settings.MysqlServiceNames = string.IsNullOrWhiteSpace(MysqlServiceBox.Text) ? "mysql;MariaDB" : MysqlServiceBox.Text.Trim();
        settings.Browser = (BrowserCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Default";
        ApplySettingsToPaths();
        EnsurePortableDirectories();
        await SaveSettingsAsync();
        await AppendActivityAsync("Settings", "Saved", "Portable root: " + root);
        UpdateValidationStatus();
        await RefreshPhpListAsync();
        await RefreshStatusAsync();
        await LoadLogAsync();
        ViewModel.SetSettings(root, settings.ApacheServiceName, settings.MysqlServiceNames, settings.Browser, settingsFile, activityLogFile);
        await ShowMessageAsync("Settings saved", "Pengaturan sudah disimpan.");
    }

    private async Task ResetSettingsAsync()
    {
        settings = new AppSettings();
        ApplySettingsToPaths();
        EnsurePortableDirectories();
        await SaveSettingsAsync();
        SyncSettingsView();
        await AppendActivityAsync("Settings", "Reset", "Settings restored to defaults.");
        UpdateValidationStatus();
        await RefreshStatusAsync();
        await ShowMessageAsync("Settings reset", "Pengaturan dikembalikan ke default.");
    }

    private async Task AppendActivityAsync(string action, string status, string details)
    {
        try
        {
            Directory.CreateDirectory(appDataDir);
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + status + " | " + action;
            if (!string.IsNullOrWhiteSpace(details)) line += " | " + details.Replace(Environment.NewLine, " ");
            await File.AppendAllTextAsync(activityLogFile, line + Environment.NewLine);
        }
        catch
        {
            // Activity logging must never break service control.
        }
    }

    private void UpdateValidationStatus()
    {
        var missing = GetMissingXamppItems().ToList();
        xamppInstallValid = missing.Count == 0;
        XamppValidationBorder.Visibility = xamppInstallValid ? Visibility.Collapsed : Visibility.Visible;
        ViewModel.XamppValidationText = xamppInstallValid
            ? "Portable stack looks good."
            : "Portable stack belum lengkap di " + root + ". Missing: " + string.Join(", ", missing) + ". Isi folder stack/apache, stack/mariadb, stack/php, www, dan data.";
    }

    private IEnumerable<string> GetMissingXamppItems()
    {
        if (!Directory.Exists(root)) yield return root;
        foreach (var item in new[]
        {
            stackRoot,
            apacheRoot,
            mariadbRoot,
            selectedPhpDir,
            wwwDir,
            dataDir
        })
        {
            if (!Directory.Exists(item)) yield return item;
        }

        foreach (var file in new[] { apacheExe, mysqlExe, phpExe })
        {
            if (!File.Exists(file)) yield return file;
        }
    }

    private async Task RefreshStatusAsync()
    {
        UpdateValidationStatus();
        var state = await GetStateAsync();
        ApacheVersionText.Text = state.ApacheVersion;
        ApachePhpText.Text = state.ApachePhpVersion;
        PhpCliText.Text = state.PhpCliVersion;
        MariaDbText.Text = state.MysqlVersion;

        SetStateText(ApacheStateText, apacheTransientState ?? (state.ApacheRunning ? ServiceState.Running : ServiceState.Stopped));
        SetStateText(MysqlStateText, mysqlTransientState ?? (state.MysqlRunning ? ServiceState.Running : ServiceState.Stopped));
        ApacheProgress.IsActive = apacheOperationActive;
        ApacheProgress.Visibility = apacheOperationActive ? Visibility.Visible : Visibility.Collapsed;
        MysqlProgress.IsActive = mysqlOperationActive;
        MysqlProgress.Visibility = mysqlOperationActive ? Visibility.Visible : Visibility.Collapsed;

        ApachePidText.Text = state.ApachePids.Any() ? "PID " + string.Join(", ", state.ApachePids) : "PID -";
        MysqlPidText.Text = state.MysqlPids.Any() ? "PID " + string.Join(", ", state.MysqlPids) : "PID -";
        ApachePortsText.Text = "Port 80: " + (state.Port80 ?? "free") + Environment.NewLine +
                               "Port 443: " + (state.Port443 ?? "free");
        MysqlPortsText.Text = "Port 3306: " + (state.Port3306 ?? "free");

        StartApacheButton.IsEnabled = !state.ApacheRunning;
        StopApacheButton.IsEnabled = state.ApacheRunning;
        RestartApacheButton.IsEnabled = state.ApacheRunning;
        StartMysqlButton.IsEnabled = !state.MysqlRunning;
        StopMysqlButton.IsEnabled = state.MysqlRunning;
        RestartMysqlButton.IsEnabled = state.MysqlRunning;
        if (!xamppInstallValid)
        {
            SetBusy(false);
        }
        if (operationBusy)
        {
            SetBusy(false);
        }

        var runningCount = (state.ApacheRunning ? 1 : 0) + (state.MysqlRunning ? 1 : 0);
        ViewModel.SetServiceSummary(runningCount, DateTime.Now);
        await UpdateHealthSummaryAsync(state);
    }

    private void SetStateText(TextBlock label, ServiceState state)
    {
        label.Text = state.ToString();

        var bgResource = "BadgeStoppedBg";
        var textResource = "BadgeStoppedText";

        if (state == ServiceState.Running)
        {
            bgResource = "BadgeRunningBg";
            textResource = "BadgeRunningText";
        }
        else if (state is ServiceState.Starting or ServiceState.Stopping or ServiceState.Restarting or ServiceState.Repairing)
        {
            bgResource = "BadgeWorkingBg";
            textResource = "BadgeWorkingText";
        }

        var bgBrush = ResolveBrush(bgResource);
        var textBrush = ResolveBrush(textResource);
        label.Foreground = textBrush;

        if (label.Parent is StackPanel stack && stack.Parent is Border badgeBorder)
        {
            badgeBorder.Background = bgBrush;
            var dot = stack.Children.OfType<Microsoft.UI.Xaml.Shapes.Ellipse>().FirstOrDefault();
            if (dot != null)
            {
                dot.Fill = textBrush;
            }
        }
    }

    private SolidColorBrush ResolveBrush(string resourceKey)
    {
        if (Resources.TryGetValue(resourceKey, out var pageValue) && pageValue is SolidColorBrush pageBrush)
        {
            return pageBrush;
        }

        if (Application.Current.Resources.TryGetValue(resourceKey, out var appValue) && appValue is SolidColorBrush appBrush)
        {
            return appBrush;
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    private async Task UpdateHealthSummaryAsync(XamppState state)
    {
        var items = new List<string>
        {
            "Apache " + (state.ApacheRunning ? "OK" : "Stopped"),
            "MySQL " + (state.MysqlRunning ? "OK" : "Stopped")
        };

        var phpApache = ExtractPhpVersion(state.ApachePhpVersion);
        var phpCli = ExtractPhpVersion(state.PhpCliVersion);
        items.Add(!string.IsNullOrWhiteSpace(phpApache) && phpApache == phpCli ? "PHP Apache = PHP CLI" : "PHP Apache != PHP CLI");

        var phpMyAdminStatus = await GetPhpMyAdminHealthAsync(state.ApacheRunning);
        items.Add(phpMyAdminStatus);

        HealthSummaryText.Text = string.Join("   |   ", items);

        if (state.PortConflicts.Count > 0)
        {
            PortConflictBorder.Visibility = Visibility.Visible;
            PortConflictText.Text = string.Join(Environment.NewLine, state.PortConflicts.Select(port =>
                "Port " + port.Port + " dipakai PID " + port.Pid + " - " + port.ProcessName));
        }
        else
        {
            PortConflictBorder.Visibility = Visibility.Collapsed;
            PortConflictText.Text = "";
        }
    }

    private static string ExtractPhpVersion(string value)
    {
        var match = Regex.Match(value, "PHP\\s+([0-9]+\\.[0-9]+\\.[0-9]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "";
    }

    private async Task<string> GetPhpMyAdminHealthAsync(bool apacheRunning)
    {
        if (!apacheRunning) return "phpMyAdmin offline";
        try
        {
            using var response = await localHttp.GetAsync("http://localhost/phpmyadmin/");
            return response.StatusCode == HttpStatusCode.Forbidden ? "phpMyAdmin Forbidden" : "phpMyAdmin OK";
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            return ex.StatusCode == HttpStatusCode.Forbidden
                ? "phpMyAdmin Forbidden"
                : "phpMyAdmin " + (int)ex.StatusCode.Value;
        }
        catch
        {
            return "phpMyAdmin unreachable";
        }
    }

    private async Task<XamppState> GetStateAsync()
    {
        var ports = GetListeningPorts();
        var httpd = GetProcesses("httpd");
        var mysqld = GetProcesses("mysqld");
        var phpDir = await GetApachePhpDirAsync();
        var apachePhpExe = Path.Combine(phpDir, "php.exe");
        return new XamppState
        {
            ApacheRunning = httpd.Count > 0,
            MysqlRunning = mysqld.Count > 0,
            ApachePids = httpd.Select(p => p.Id).ToList(),
            MysqlPids = mysqld.Select(p => p.Id).ToList(),
            Port80 = FormatPort(ports, 80),
            Port443 = FormatPort(ports, 443),
            Port3306 = FormatPort(ports, 3306),
            PortConflicts = ports.Values.Where(port => !port.OwnedByXampp).ToList(),
            ApacheVersion = FirstLine(apacheExe, "-v"),
            ApachePhpVersion = FirstLine(apachePhpExe, "-v"),
            PhpCliVersion = FirstLine(phpExe, "-v"),
            MysqlVersion = FirstLine(mysqlClientExe, "--version")
        };
    }

    private List<Process> GetProcesses(string name)
    {
        var list = new List<Process>();
        foreach (var process in Process.GetProcessesByName(name))
        {
            try
            {
                var file = process.MainModule?.FileName;
                if (file != null && file.StartsWith(root, StringComparison.OrdinalIgnoreCase)) list.Add(process);
            }
            catch
            {
                // If the path cannot be read, do not assume it belongs to this XAMPP install.
            }
        }
        return list;
    }

    private Dictionary<int, PortInfo> GetListeningPorts()
    {
        var result = new Dictionary<int, PortInfo>();
        var text = RunCapture("netstat.exe", "-ano -p tcp");
        foreach (var lineText in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var lineTrim = lineText.Trim();
            if (!lineTrim.Contains("LISTENING")) continue;
            var parts = Regex.Split(lineTrim, "\\s+");
            if (parts.Length < 5) continue;
            foreach (var port in new[] { 80, 443, 3306 })
            {
                if (parts[1].EndsWith(":" + port, StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(parts[4], out var pid))
                    {
                        result[port] = GetPortInfo(port, pid);
                    }
                }
            }
        }
        return result;
    }

    private PortInfo GetPortInfo(int port, int pid)
    {
        var name = "PID " + pid;
        var path = "";
        try
        {
            using var process = Process.GetProcessById(pid);
            name = process.ProcessName + ".exe";
            path = process.MainModule?.FileName ?? "";
        }
        catch
        {
            // Some system-owned processes hide their executable path.
        }

        return new PortInfo
        {
            Port = port,
            Pid = pid,
            ProcessName = name,
            ProcessPath = path,
            OwnedByXampp = !string.IsNullOrWhiteSpace(path) && path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string? FormatPort(Dictionary<int, PortInfo> ports, int port)
    {
        if (!ports.TryGetValue(port, out var info)) return null;
        return info.OwnedByXampp
            ? "PID " + info.Pid + " - " + info.ProcessName
            : "Conflict PID " + info.Pid + " - " + info.ProcessName;
    }

    private string FirstLine(string exe, string args)
    {
        if (!File.Exists(exe)) return "Not found";
        var output = RunCapture(exe, args);
        var lineText = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(lineText) ? "Unknown" : lineText.Trim();
    }

    private string RunCapture(string exe, string args)
    {
        return RunCaptureResult(exe, args).Output;
    }

    private (int ExitCode, string Output) RunCaptureResult(string exe, string args, int timeoutMilliseconds = 10000)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return (-1, "");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(timeoutMilliseconds))
            {
                try { process.Kill(); } catch { }
                return (-1, output + error + Environment.NewLine + "Process timed out.");
            }
            return (process.ExitCode, output + error);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    private async Task<string> GetApachePhpDirAsync()
    {
        if (!File.Exists(apacheConf)) return selectedPhpDir;
        var content = await File.ReadAllTextAsync(apacheConf);
        var match = Regex.Match(content, "LoadModule\\s+php_module\\s+\"([^\"]+php8apache2_4\\.dll)\"", RegexOptions.IgnoreCase);
        if (match.Success) return Path.GetDirectoryName(match.Groups[1].Value.Replace('/', '\\')) ?? selectedPhpDir;
        return selectedPhpDir;
    }

    private async Task PreparePortableConfigurationAsync()
    {
        EnsurePortableDirectories();
        EnsurePortableBinaryExists(apacheExe, "Apache binary belum ada. Ekstrak Apache Lounge ke stack\\apache.");
        EnsurePortableBinaryExists(mysqlExe, "MariaDB binary belum ada. Ekstrak MariaDB portable ke stack\\mariadb.");
        EnsurePortableBinaryExists(phpExe, "PHP binary belum ada. Ekstrak PHP for Windows ke " + selectedPhpDir + ".");

        if (!File.Exists(apacheTemplateFile))
        {
            await File.WriteAllTextAsync(apacheTemplateFile, DefaultApacheTemplate);
        }

        if (!File.Exists(mariadbTemplateFile))
        {
            await File.WriteAllTextAsync(mariadbTemplateFile, DefaultMariaDbTemplate);
        }

        var replacements = BuildTemplateReplacements();
        var apacheTemplate = await File.ReadAllTextAsync(apacheTemplateFile);
        var mariadbTemplate = await File.ReadAllTextAsync(mariadbTemplateFile);

        await File.WriteAllTextAsync(apacheConf, RenderTemplate(apacheTemplate, replacements));
        await File.WriteAllTextAsync(mysqlIni, RenderTemplate(mariadbTemplate, replacements));
    }

    private void EnsurePortableBinaryExists(string path, string message)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(message + Environment.NewLine + path);
        }
    }

    private Dictionary<string, string> BuildTemplateReplacements()
    {
        var phpApacheDll = Path.Combine(selectedPhpDir, "php8apache2_4.dll");
        return new Dictionary<string, string>
        {
            ["APP_ROOT"] = PortablePath(root),
            ["STACK_ROOT"] = PortablePath(stackRoot),
            ["APACHE_ROOT"] = PortablePath(apacheRoot),
            ["MARIADB_ROOT"] = PortablePath(mariadbRoot),
            ["PHP_ROOT"] = PortablePath(selectedPhpDir),
            ["PHP_APACHE_DLL"] = PortablePath(phpApacheDll),
            ["WWW_ROOT"] = PortablePath(wwwDir),
            ["DATA_DIR"] = PortablePath(dataDir),
            ["APACHE_LOGS"] = PortablePath(Path.Combine(apacheRoot, "logs"))
        };
    }

    private static string RenderTemplate(string template, Dictionary<string, string> replacements)
    {
        foreach (var item in replacements)
        {
            template = template.Replace("{{" + item.Key + "}}", item.Value, StringComparison.OrdinalIgnoreCase);
        }

        return template;
    }

    private static string PortablePath(string path) => path.Replace('\\', '/');

    private string BuildPortablePath(string phpDir)
    {
        var parts = new[]
        {
            phpDir,
            Path.Combine(apacheRoot, "bin"),
            Path.Combine(mariadbRoot, "bin"),
            Environment.GetEnvironmentVariable("PATH") ?? ""
        };
        return string.Join(Path.PathSeparator, parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private async Task StartApacheAsync()
    {
        await PreparePortableConfigurationAsync();
        var phpDir = await GetApachePhpDirAsync();
        var psi = new ProcessStartInfo(apacheExe, "-f \"" + apacheConf + "\" -d \"" + apacheRoot + "\"")
        {
            WorkingDirectory = apacheRoot,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.EnvironmentVariables["PATH"] = BuildPortablePath(phpDir);
        psi.EnvironmentVariables["OPENSSL_CONF"] = Path.Combine(apacheRoot, @"conf\openssl.cnf");
        await Task.Run(() => Process.Start(psi));
        await Task.Delay(1800);
    }

    private async Task StopApacheAsync()
    {
        await Task.Run(() => RunCapture(apacheExe, "-k shutdown -f \"" + apacheConf + "\""));
        await Task.Delay(700);
        foreach (var p in GetProcesses("httpd"))
        {
            try { p.Kill(); } catch { }
        }
        await Task.Delay(400);
    }

    private static string? ResolveExecutablePath(string executable)
    {
        var systemPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), executable);
        if (File.Exists(systemPath)) return systemPath;

        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var path in paths)
        {
            var candidate = Path.Combine(path.Trim(), executable);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private async Task StartMysqlAsync()
    {
        await PreparePortableConfigurationAsync();
        var psi = new ProcessStartInfo(mysqlExe, "--defaults-file=\"" + mysqlIni + "\" --standalone")
        {
            WorkingDirectory = mariadbRoot,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.EnvironmentVariables["PATH"] = BuildPortablePath(selectedPhpDir);
        await Task.Run(() => Process.Start(psi));
        await Task.Delay(1800);
    }

    private async Task StopMysqlAsync()
    {
        var result = await Task.Run(() => RunCaptureResult(mysqlAdminExe, "--user=root --protocol=tcp --port=3306 shutdown", 15000));
        if (await WaitUntilAsync(() => GetProcesses("mysqld").Count == 0, TimeSpan.FromSeconds(12)))
        {
            return;
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("MySQL tidak bisa dihentikan secara aman. Aplikasi tidak memaksa kill proses database. Detail: " + Shorten(result.Output));
        }

        throw new InvalidOperationException("MySQL masih berjalan setelah shutdown aman. Cek mysql_error.log sebelum memaksa stop manual.");
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(300);
        }
        return condition();
    }

    private static string Shorten(string value)
    {
        value = string.IsNullOrWhiteSpace(value) ? "No output." : value.Trim();
        return value.Length <= 800 ? value : value.Substring(0, 800) + "...";
    }

    private const string DefaultApacheTemplate = """
ServerRoot "{{APACHE_ROOT}}"
Listen 80

LoadModule php_module "{{PHP_APACHE_DLL}}"
PHPINIDir "{{PHP_ROOT}}"

ServerName localhost:80
DocumentRoot "{{WWW_ROOT}}"
DirectoryIndex index.php index.html

<Directory "{{WWW_ROOT}}">
    Options Indexes FollowSymLinks Includes ExecCGI
    AllowOverride All
    Require all granted
</Directory>

ErrorLog "{{APACHE_LOGS}}/error.log"
CustomLog "{{APACHE_LOGS}}/access.log" common
TypesConfig conf/mime.types
AddType application/x-httpd-php .php
""";

    private const string DefaultMariaDbTemplate = """
[mysqld]
basedir={{MARIADB_ROOT}}
datadir={{DATA_DIR}}
port=3306
bind-address=127.0.0.1
character-set-server=utf8mb4
collation-server=utf8mb4_general_ci

[client]
port=3306
host=127.0.0.1
""";

    private async Task LoadLogAsync()
    {
        if (LogPathText == null || LogBox == null) return;
        var file = apacheErrorLog;
        if (LogCombo != null)
        {
            if (LogCombo.SelectedIndex == 1) file = mysqlErrorLog;
            if (LogCombo.SelectedIndex == 2) file = xamppControlLog;
            if (LogCombo.SelectedIndex == 3) file = apacheAccessLog;
            if (LogCombo.SelectedIndex == 4) file = activityLogFile;
        }
        LogPathText.Text = file;
        if (!File.Exists(file))
        {
            LogBox.Text = "Log file not found.";
            return;
        }
        var lines = await ReadAllLinesSharedAsync(file);
        LogBox.Text = string.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - 250)));
    }

    private static async Task<string[]> ReadAllLinesSharedAsync(string file)
    {
        try
        {
            await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            return content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }
        catch (Exception ex)
        {
            return new[] { "Could not read log file:", ex.Message };
        }
    }

    private async Task RefreshPhpListAsync()
    {
        ViewModel.PhpInstalls.Clear();
        PhpInstall? activeInstall = null;
        foreach (var install in await GetPhpInstallsAsync())
        {
            ViewModel.PhpInstalls.Add(install);
            if (install.Active) activeInstall = install;
        }

        ViewModel.SelectedPhpInstall = activeInstall ?? ViewModel.PhpInstalls.FirstOrDefault();
        UpdatePhpPreview();
    }

    private async Task<IEnumerable<PhpInstall>> GetPhpInstallsAsync()
    {
        var active = await GetApachePhpDirAsync();
        if (!Directory.Exists(stackRoot)) return Array.Empty<PhpInstall>();
        return Directory.GetDirectories(stackRoot, "php*")
            .Where(dir => File.Exists(Path.Combine(dir, "php.exe")))
            .Select(dir => new PhpInstall
            {
                Name = Path.GetFileName(dir),
                Path = dir,
                Version = FirstLine(Path.Combine(dir, "php.exe"), "-v"),
                Active = string.Equals(dir, active, StringComparison.OrdinalIgnoreCase),
                HasApacheModule = File.Exists(Path.Combine(dir, "php8apache2_4.dll"))
            })
            .OrderBy(item => item.Name)
            .ToList();
    }

    private void UpdatePhpPreview()
    {
        if (PhpPreviewBox == null || PhpValidationText == null) return;
        if (ViewModel.SelectedPhpInstall is not PhpInstall selected)
        {
            PhpPreviewBox.Text = "Pilih versi PHP untuk melihat preview.";
            PhpValidationText.Text = "Pilih versi PHP untuk melihat validasi.";
            return;
        }

        var validation = ValidatePhpInstall(selected).ToList();
        PhpValidationText.Text = validation.Count == 0
            ? "Validasi OK. PHP ini siap dipakai Apache."
            : "Perlu diperbaiki: " + string.Join(", ", validation);

        var phpForward = selected.Path.Replace('\\', '/');
        PhpPreviewBox.Text =
            "Target: " + selected.Name + Environment.NewLine +
            "Path: " + selected.Path + Environment.NewLine +
            "Apache module: " + Path.Combine(selected.Path, "php8apache2_4.dll") + Environment.NewLine +
            "Thread-safe DLL: " + Path.Combine(selected.Path, "php8ts.dll") + Environment.NewLine +
            "php.ini: " + Path.Combine(selected.Path, "php.ini") + Environment.NewLine + Environment.NewLine +
            "Config preview:" + Environment.NewLine +
            "LoadFile \"" + phpForward + "/php8ts.dll\"" + Environment.NewLine +
            "LoadModule php_module \"" + phpForward + "/php8apache2_4.dll\"" + Environment.NewLine +
            "PHPINIDir \"" + phpForward + "\"";
    }

    private static IEnumerable<string> ValidatePhpInstall(PhpInstall install)
    {
        if (!File.Exists(Path.Combine(install.Path, "php8apache2_4.dll"))) yield return "php8apache2_4.dll missing";
        if (!File.Exists(Path.Combine(install.Path, "php8ts.dll"))) yield return "php8ts.dll missing";
        if (!File.Exists(Path.Combine(install.Path, "php.ini"))) yield return "php.ini missing";
    }

    private async void UsePhpButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.UsePhpCommand.ExecuteAsync(null);
    }

    private async Task UseSelectedPhpAsync()
    {
        if (ViewModel.SelectedPhpInstall is not PhpInstall selected) return;
        var validation = ValidatePhpInstall(selected).ToList();
        if (validation.Count > 0)
        {
            await ShowMessageAsync("Tidak bisa switch", string.Join(Environment.NewLine, validation));
            return;
        }

        var confirm = await ShowConfirmAsync("Apply PHP switch?", PhpPreviewBox.Text + Environment.NewLine + Environment.NewLine + "Apache config akan dirender ulang dari template. Jika Apache sedang berjalan, proses akan restart otomatis.");
        if (!confirm) return;

        var wasRunning = GetProcesses("httpd").Count > 0;
        previousPhpName = settings.ActivePhpName;
        settings.ActivePhpName = selected.Name;
        ApplySettingsToPaths();
        await SaveSettingsAsync();
        await PreparePortableConfigurationAsync();

        var test = File.Exists(apacheExe) ? RunCapture(apacheExe, "-t -f \"" + apacheConf + "\"") : "Apache binary not found.";
        if (!test.Contains("Syntax OK", StringComparison.OrdinalIgnoreCase) && File.Exists(apacheExe))
        {
            await ShowMessageAsync("Apache config warning", test);
            return;
        }

        if (wasRunning)
        {
            await StopApacheAsync();
            await StartApacheAsync();
        }

        await ShowMessageAsync("PHP switched", "Apache diarahkan ke " + selected.Name + ".");
        await AppendActivityAsync("PHP Switcher", "Succeeded", "Apache PHP set to " + selected.Name);
        await RefreshPhpListAsync();
        await RefreshStatusAsync();
    }

    private async Task RollbackLastPhpBackupAsync()
    {
        if (string.IsNullOrWhiteSpace(previousPhpName))
        {
            await ShowMessageAsync("Rollback PHP", "Belum ada versi PHP sebelumnya di sesi aplikasi ini.");
            return;
        }

        var confirm = await ShowConfirmAsync("Rollback PHP config?", "Kembalikan PHP aktif ke " + previousPhpName + "?");
        if (!confirm) return;

        var current = settings.ActivePhpName;
        settings.ActivePhpName = previousPhpName;
        previousPhpName = current;
        ApplySettingsToPaths();
        await SaveSettingsAsync();
        await PreparePortableConfigurationAsync();

        if (GetProcesses("httpd").Count > 0)
        {
            await StopApacheAsync();
            await StartApacheAsync();
        }

        await AppendActivityAsync("PHP Switcher", "Rolled back", "Restored PHP " + settings.ActivePhpName);
        await RefreshPhpListAsync();
        await RefreshStatusAsync();
        await ShowMessageAsync("Rollback complete", "PHP aktif dikembalikan ke " + settings.ActivePhpName + ".");
    }

    private async Task RepairPhpMyAdminAccessAsync()
    {
        if (!File.Exists(xamppConf))
        {
            throw new InvalidOperationException("File httpd-xampp.conf tidak ditemukan: " + xamppConf);
        }

        var backup = xamppConf + ".app-bak-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        File.Copy(xamppConf, backup, true);
        var content = await File.ReadAllTextAsync(xamppConf);
        var directoryPattern = "(<Directory\\s+\"[^\"]*phpMyAdmin[^\"]*\"\\s*>)(.*?)(</Directory>)";
        var matched = false;
        var repaired = Regex.Replace(content, directoryPattern, match =>
        {
            matched = true;
            var body = match.Groups[2].Value;
            body = Regex.Replace(body, "^\\s*Require\\s+all\\s+denied\\s*$", "    Require local", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            body = Regex.Replace(body, "^\\s*Deny\\s+from\\s+all\\s*$", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            body = Regex.Replace(body, "^\\s*Order\\s+deny,allow\\s*$", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (!Regex.IsMatch(body, "^\\s*Require\\s+local\\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            {
                body = body.TrimEnd() + Environment.NewLine + "    Require local" + Environment.NewLine;
            }
            return match.Groups[1].Value + body + match.Groups[3].Value;
        }, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!matched)
        {
            File.Copy(backup, xamppConf, true);
            throw new InvalidOperationException("Blok Directory phpMyAdmin tidak ditemukan di httpd-xampp.conf. Backup dibuat: " + backup);
        }

        await File.WriteAllTextAsync(xamppConf, repaired);
        var test = RunCapture(apacheExe, "-t -f \"" + apacheConf + "\"");
        if (!test.Contains("Syntax OK", StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(backup, xamppConf, true);
            throw new InvalidOperationException("Repair phpMyAdmin dibatalkan karena Apache config tidak valid. Detail: " + Shorten(test));
        }

        await AppendActivityAsync("Repair phpMyAdmin access", "Config updated", "Backup: " + backup);
    }

    private Task ShowMessageAsync(string title, string message)
    {
        var combined = title + " " + message;
        var severity =
            combined.Contains("warning", StringComparison.OrdinalIgnoreCase) ? InfoBarSeverity.Warning :
            combined.Contains("gagal", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("tidak bisa", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("tidak ditemukan", StringComparison.OrdinalIgnoreCase) ? InfoBarSeverity.Error :
            combined.Contains("saved", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("reset", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("switched", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("complete", StringComparison.OrdinalIgnoreCase) ? InfoBarSeverity.Success :
            InfoBarSeverity.Informational;

        ShowInfo(title, message, severity);
        return Task.CompletedTask;
    }

    private void ShowInfo(string title, string message, InfoBarSeverity severity)
    {
        AppInfoBar.Title = title;
        AppInfoBar.Message = message;
        AppInfoBar.Severity = severity;
        AppInfoBar.IsOpen = true;
    }

    private async Task<bool> ShowConfirmAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static void OpenPath(string path)
    {
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return;
        }

        Process.Start("explorer.exe", "\"" + path + "\"");
    }

    private void OpenUrl(string url)
    {
        var browser = settings.Browser switch
        {
            "Edge" => "msedge.exe",
            "Chrome" => "chrome.exe",
            "Firefox" => "firefox.exe",
            _ => null
        };

        try
        {
            if (browser == null)
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo(browser, url) { UseShellExecute = true });
            }
        }
        catch
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void OpenPortableTerminal()
    {
        var terminal = ResolveExecutablePath("wt.exe");
        var psi = terminal != null
            ? new ProcessStartInfo(terminal, "-d \"" + root + "\" cmd /k")
            : new ProcessStartInfo("cmd.exe", "/k cd /d \"" + root + "\"");

        psi.WorkingDirectory = root;
        psi.UseShellExecute = false;
        psi.EnvironmentVariables["PATH"] = BuildPortablePath(selectedPhpDir);
        Process.Start(psi);
    }
}
