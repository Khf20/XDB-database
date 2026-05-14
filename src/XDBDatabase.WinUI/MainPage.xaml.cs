using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
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
    private string root = @"C:\xampp";
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
    private string? apacheTransientState;
    private string? mysqlTransientState;
    private bool apacheOperationActive;
    private bool mysqlOperationActive;
    private bool operationBusy;
    private string? lastPhpBackup;
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

        Loaded += MainPage_Loaded;
        timer.Interval = TimeSpan.FromSeconds(5);
        timer.Tick += (_, _) => RefreshStatus();
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        settings = await LoadSettingsAsync();
        ApplySettingsToPaths();
        SyncSettingsView();
        ShowView(DashboardView, "Dashboard");
        UpdateValidationStatus();
        RefreshPhpList();
        RefreshStatus();
        await LoadLogAsync();
        await AppendActivityAsync("App", "Started", "XDB-database launched. XAMPP root: " + root);
        timer.Start();
    }

    private void DashboardNav_Click(object sender, RoutedEventArgs e) => ShowView(DashboardView, "Dashboard");
    private async void LogsNav_Click(object sender, RoutedEventArgs e) { ShowView(LogsView, "Logs"); await LoadLogAsync(); }
    private void PhpNav_Click(object sender, RoutedEventArgs e) { ShowView(PhpView, "PHP Switcher"); RefreshPhpList(); }
    private void ToolsNav_Click(object sender, RoutedEventArgs e) => ShowView(ToolsView, "Tools");
    private void SettingsNav_Click(object sender, RoutedEventArgs e) => ShowView(SettingsView, "Settings");
    private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshStatus();
    private void PhpMyAdminButton_Click(object sender, RoutedEventArgs e) => OpenUrl("http://localhost/phpmyadmin/");
    private void OpenDashboard_Click(object sender, RoutedEventArgs e) => OpenUrl("http://localhost/dashboard/");
    private void OpenHtdocs_Click(object sender, RoutedEventArgs e) => OpenPath(Path.Combine(root, "htdocs"));
    private void OpenApacheConfig_Click(object sender, RoutedEventArgs e) => OpenPath(Path.Combine(root, @"apache\conf"));
    private void OpenMysqlData_Click(object sender, RoutedEventArgs e) => OpenPath(Path.Combine(root, @"mysql\data"));
    private void OpenRoot_Click(object sender, RoutedEventArgs e) => OpenPath(root);
    private void OpenPhpIni_Click(object sender, RoutedEventArgs e) => OpenPath(Path.Combine(GetApachePhpDir(), "php.ini"));
    private void OpenMysqlIni_Click(object sender, RoutedEventArgs e) => OpenPath(mysqlIni);
    private void OpenHttpdConf_Click(object sender, RoutedEventArgs e) => OpenPath(apacheConf);
    private void OpenVhostsConf_Click(object sender, RoutedEventArgs e) => OpenPath(httpdVhostsConf);
    private void OpenErrorLog_Click(object sender, RoutedEventArgs e) => OpenPath(apacheErrorLog);
    private void OpenWindowsServices_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("services.msc") { UseShellExecute = true });
    private async void ReloadLogButton_Click(object sender, RoutedEventArgs e) => await LoadLogAsync();
    private void ReloadPhpButton_Click(object sender, RoutedEventArgs e) => RefreshPhpList();
    private async void LogCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => await LoadLogAsync();
    private void PhpList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePhpPreview();
    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e) => await SaveSettingsFromViewAsync();
    private async void ResetSettingsButton_Click(object sender, RoutedEventArgs e) => await ResetSettingsAsync();
    private async void RepairPhpMyAdminButton_Click(object sender, RoutedEventArgs e) => await RunBusy("Repair phpMyAdmin access", RepairPhpMyAdminAccessAsync);
    private async void RollbackPhpButton_Click(object sender, RoutedEventArgs e) => await RollbackLastPhpBackupAsync();

    private async void StartApacheButton_Click(object sender, RoutedEventArgs e) => await RunBusy("Start Apache", StartApacheAsync);
    private async void StopApacheButton_Click(object sender, RoutedEventArgs e) => await RunBusy("Stop Apache", StopApacheAsync);
    private async void RestartApacheButton_Click(object sender, RoutedEventArgs e) => await RunBusy("Restart Apache", async () => { await StopApacheAsync(); await StartApacheAsync(); });
    private async void StartMysqlButton_Click(object sender, RoutedEventArgs e) => await RunBusy("Start MySQL/MariaDB", StartMysqlAsync);
    private async void StopMysqlButton_Click(object sender, RoutedEventArgs e) => await RunBusy("Stop MySQL/MariaDB", StopMysqlAsync);
    private async void RestartMysqlButton_Click(object sender, RoutedEventArgs e) => await RunBusy("Restart MySQL/MariaDB", async () => { await StopMysqlAsync(); await StartMysqlAsync(); });

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
            RefreshStatus();
            await LoadLogAsync();
        }
    }

    private void SetOperationState(string actionName, bool active, bool failed)
    {
        if (actionName.Contains("Apache", StringComparison.OrdinalIgnoreCase))
        {
            apacheOperationActive = active;
            apacheTransientState = failed ? "Error" : active ? OperationLabel(actionName) : null;
        }

        if (actionName.Contains("MySQL", StringComparison.OrdinalIgnoreCase) ||
            actionName.Contains("MariaDB", StringComparison.OrdinalIgnoreCase))
        {
            mysqlOperationActive = active;
            mysqlTransientState = failed ? "Error" : active ? OperationLabel(actionName) : null;
        }
    }

    private static string OperationLabel(string actionName)
    {
        if (actionName.Contains("Start", StringComparison.OrdinalIgnoreCase)) return "Starting";
        if (actionName.Contains("Stop", StringComparison.OrdinalIgnoreCase)) return "Stopping";
        if (actionName.Contains("Restart", StringComparison.OrdinalIgnoreCase)) return "Restarting";
        if (actionName.Contains("Repair", StringComparison.OrdinalIgnoreCase)) return "Repairing";
        return "Working";
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
        root = string.IsNullOrWhiteSpace(settings.XamppRoot) ? @"C:\xampp" : settings.XamppRoot.Trim().TrimEnd('\\');
        apacheServiceName = string.IsNullOrWhiteSpace(settings.ApacheServiceName) ? "Apache2.4" : settings.ApacheServiceName.Trim();
        mysqlServiceNames = ParseServiceNames(settings.MysqlServiceNames);

        apacheExe = Path.Combine(root, @"apache\bin\httpd.exe");
        apacheConf = Path.Combine(root, @"apache\conf\httpd.conf");
        xamppConf = Path.Combine(root, @"apache\conf\extra\httpd-xampp.conf");
        mysqlExe = Path.Combine(root, @"mysql\bin\mysqld.exe");
        mysqlAdminExe = Path.Combine(root, @"mysql\bin\mysqladmin.exe");
        mysqlClientExe = Path.Combine(root, @"mysql\bin\mysql.exe");
        mysqlIni = Path.Combine(root, @"mysql\bin\my.ini");
        phpExe = Path.Combine(root, @"php\php.exe");
        apacheErrorLog = Path.Combine(root, @"apache\logs\error.log");
        mysqlErrorLog = Path.Combine(root, @"mysql\data\mysql_error.log");
        xamppControlLog = Path.Combine(root, "xampp-control.log");
        apacheAccessLog = Path.Combine(root, @"apache\logs\access.log");
        httpdVhostsConf = Path.Combine(root, @"apache\conf\extra\httpd-vhosts.conf");
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
        XamppRootBox.Text = settings.XamppRoot;
        ApacheServiceBox.Text = settings.ApacheServiceName;
        MysqlServiceBox.Text = settings.MysqlServiceNames;
        BrowserCombo.SelectedIndex = settings.Browser switch
        {
            "Edge" => 1,
            "Chrome" => 2,
            "Firefox" => 3,
            _ => 0
        };
        ViewModel.SetSettings(settings.XamppRoot, settings.ApacheServiceName, settings.MysqlServiceNames, settings.Browser, settingsFile, activityLogFile);
    }

    private async Task SaveSettingsFromViewAsync()
    {
        settings.XamppRoot = string.IsNullOrWhiteSpace(XamppRootBox.Text) ? @"C:\xampp" : XamppRootBox.Text.Trim().TrimEnd('\\');
        settings.ApacheServiceName = string.IsNullOrWhiteSpace(ApacheServiceBox.Text) ? "Apache2.4" : ApacheServiceBox.Text.Trim();
        settings.MysqlServiceNames = string.IsNullOrWhiteSpace(MysqlServiceBox.Text) ? "mysql;MariaDB" : MysqlServiceBox.Text.Trim();
        settings.Browser = (BrowserCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Default";
        ApplySettingsToPaths();
        await SaveSettingsAsync();
        await AppendActivityAsync("Settings", "Saved", "XAMPP root: " + root);
        UpdateValidationStatus();
        RefreshPhpList();
        RefreshStatus();
        await LoadLogAsync();
        ViewModel.SetSettings(settings.XamppRoot, settings.ApacheServiceName, settings.MysqlServiceNames, settings.Browser, settingsFile, activityLogFile);
        await ShowMessageAsync("Settings saved", "Pengaturan sudah disimpan.");
    }

    private async Task ResetSettingsAsync()
    {
        settings = new AppSettings();
        ApplySettingsToPaths();
        await SaveSettingsAsync();
        SyncSettingsView();
        await AppendActivityAsync("Settings", "Reset", "Settings restored to defaults.");
        UpdateValidationStatus();
        RefreshStatus();
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
            ? "XAMPP installation looks good."
            : "XAMPP tidak lengkap di " + root + ". Missing: " + string.Join(", ", missing) + ". Buka Settings untuk mengubah lokasi XAMPP.";
    }

    private IEnumerable<string> GetMissingXamppItems()
    {
        if (!Directory.Exists(root)) yield return root;
        foreach (var item in new[]
        {
            Path.Combine(root, "apache"),
            Path.Combine(root, "mysql"),
            Path.Combine(root, "php"),
            Path.Combine(root, "htdocs")
        })
        {
            if (!Directory.Exists(item)) yield return item;
        }

        foreach (var file in new[] { apacheExe, apacheConf, xamppConf, mysqlExe, phpExe })
        {
            if (!File.Exists(file)) yield return file;
        }
    }

    private void RefreshStatus()
    {
        UpdateValidationStatus();
        var state = GetState();
        ApacheVersionText.Text = state.ApacheVersion;
        ApachePhpText.Text = state.ApachePhpVersion;
        PhpCliText.Text = state.PhpCliVersion;
        MariaDbText.Text = state.MysqlVersion;

        SetStateText(ApacheStateText, apacheTransientState ?? (state.ApacheRunning ? "Running" : "Stopped"));
        SetStateText(MysqlStateText, mysqlTransientState ?? (state.MysqlRunning ? "Running" : "Stopped"));
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
        UpdateHealthSummary(state);
    }

    private void SetStateText(TextBlock label, string state)
    {
        label.Text = state;

        var bgResource = "BadgeStoppedBg";
        var textResource = "BadgeStoppedText";

        if (state == "Running")
        {
            bgResource = "BadgeRunningBg";
            textResource = "BadgeRunningText";
        }
        else if (state is "Starting" or "Stopping" or "Restarting" or "Repairing")
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

    private void UpdateHealthSummary(XamppState state)
    {
        var items = new List<string>
        {
            "Apache " + (state.ApacheRunning ? "OK" : "Stopped"),
            "MySQL " + (state.MysqlRunning ? "OK" : "Stopped")
        };

        var phpApache = ExtractPhpVersion(state.ApachePhpVersion);
        var phpCli = ExtractPhpVersion(state.PhpCliVersion);
        items.Add(!string.IsNullOrWhiteSpace(phpApache) && phpApache == phpCli ? "PHP Apache = PHP CLI" : "PHP Apache != PHP CLI");

        var phpMyAdminStatus = GetPhpMyAdminHealth(state.ApacheRunning);
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

    private string GetPhpMyAdminHealth(bool apacheRunning)
    {
        if (!apacheRunning) return "phpMyAdmin offline";
        try
        {
            using var response = localHttp.GetAsync("http://localhost/phpmyadmin/").GetAwaiter().GetResult();
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

    private XamppState GetState()
    {
        var ports = GetListeningPorts();
        var httpd = GetProcesses("httpd");
        var mysqld = GetProcesses("mysqld");
        var apacheServiceRunning = IsServiceRunning(apacheServiceName);
        var mysqlServiceRunning = mysqlServiceNames.Any(IsServiceRunning);
        var phpDir = GetApachePhpDir();
        var apachePhpExe = Path.Combine(phpDir, "php.exe");
        return new XamppState
        {
            ApacheRunning = httpd.Count > 0 || apacheServiceRunning,
            MysqlRunning = mysqld.Count > 0 || mysqlServiceRunning,
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

    private string GetApachePhpDir()
    {
        if (!File.Exists(xamppConf)) return Path.Combine(root, "php");
        var content = Task.Run(() => File.ReadAllTextAsync(xamppConf)).GetAwaiter().GetResult();
        var match = Regex.Match(content, "LoadModule\\s+php_module\\s+\"([^\"]+php8apache2_4\\.dll)\"", RegexOptions.IgnoreCase);
        if (match.Success) return Path.GetDirectoryName(match.Groups[1].Value.Replace('/', '\\')) ?? Path.Combine(root, "php");
        return Path.Combine(root, "php");
    }

    private async Task StartApacheAsync()
    {
        if (IsXamppServiceInstalled(apacheServiceName))
        {
            await RunElevatedScAndWaitAsync("start " + apacheServiceName, "Start Apache service");
            await Task.Delay(1800);
            return;
        }

        var phpDir = GetApachePhpDir();
        var psi = new ProcessStartInfo(apacheExe, "-f \"" + apacheConf + "\"")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.EnvironmentVariables["PATH"] = phpDir + ";" + Path.Combine(root, @"apache\bin") + ";" + Environment.GetEnvironmentVariable("PATH");
        psi.EnvironmentVariables["OPENSSL_CONF"] = Path.Combine(root, @"apache\conf\openssl.cnf");
        await Task.Run(() => Process.Start(psi));
        await Task.Delay(1800);
    }

    private async Task StopApacheAsync()
    {
        if (IsXamppServiceInstalled(apacheServiceName))
        {
            await RunElevatedScAndWaitAsync("stop " + apacheServiceName, "Stop Apache service");
            if (!await WaitUntilAsync(() => !IsServiceRunning(apacheServiceName), TimeSpan.FromSeconds(12)))
            {
                throw new InvalidOperationException("Apache service belum berhenti setelah timeout. Buka Services Windows dan cek Apache2.4.");
            }
            return;
        }

        await Task.Run(() => RunCapture(apacheExe, "-k shutdown -f \"" + apacheConf + "\""));
        await Task.Delay(700);
        foreach (var p in GetProcesses("httpd"))
        {
            try { p.Kill(); } catch { }
        }
        await Task.Delay(400);
    }

    private bool IsServiceInstalled(string serviceName)
    {
        var output = RunCapture("sc.exe", "query " + serviceName);
        return output.Contains("SERVICE_NAME:", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsXamppServiceInstalled(string serviceName)
    {
        if (!IsServiceInstalled(serviceName)) return false;
        var config = RunCapture("sc.exe", "qc " + serviceName).Replace('/', '\\');
        return config.Contains(root + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsServiceRunning(string serviceName)
    {
        if (!IsXamppServiceInstalled(serviceName)) return false;
        var output = RunCapture("sc.exe", "query " + serviceName);
        return output.Contains("STATE", StringComparison.OrdinalIgnoreCase) &&
               output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
    }

    private string? GetInstalledService(params string[] serviceNames)
    {
        return serviceNames.FirstOrDefault(IsXamppServiceInstalled);
    }

    private async Task RunElevatedScAndWaitAsync(string scArguments, string actionName)
    {
        var powershell = ResolvePowerShellPath();
        if (powershell == null)
        {
            throw new InvalidOperationException("Windows PowerShell tidak ditemukan. Sistem Windows ini mungkin dimodifikasi atau komponen PowerShell dihapus, jadi service tidak bisa dikontrol otomatis.");
        }

        var scExe = ResolveExecutablePath("sc.exe");
        if (scExe == null)
        {
            throw new InvalidOperationException("sc.exe tidak ditemukan di Windows. Aplikasi tidak bisa mengontrol service Apache/MySQL tanpa komponen Service Control Windows.");
        }

        try
        {
            var command = "& '" + scExe.Replace("'", "''") + "' " + scArguments + "; exit $LASTEXITCODE";
            var process = Process.Start(new ProcessStartInfo(powershell, "-NoProfile -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "`\"") + "\"")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });
            if (process == null) throw new InvalidOperationException(actionName + " gagal dijalankan.");
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                throw new TimeoutException(actionName + " timeout setelah 30 detik.");
            }
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(actionName + " gagal. Exit code: " + process.ExitCode + ".");
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException("Aksi ini membutuhkan izin Administrator. Setujui prompt UAC Windows, lalu coba lagi. Detail: " + ex.Message, ex);
        }
    }

    private static string? ResolvePowerShellPath()
    {
        var systemPowerShell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
        return File.Exists(systemPowerShell) ? systemPowerShell : ResolveExecutablePath("powershell.exe");
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
        var service = GetInstalledService(mysqlServiceNames);
        if (service != null)
        {
            await RunElevatedScAndWaitAsync("start " + service, "Start MySQL/MariaDB service");
            await Task.Delay(1800);
            return;
        }

        var psi = new ProcessStartInfo(mysqlExe, "--defaults-file=\"" + mysqlIni + "\" --standalone")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        await Task.Run(() => Process.Start(psi));
        await Task.Delay(1800);
    }

    private async Task StopMysqlAsync()
    {
        var service = GetInstalledService(mysqlServiceNames);
        if (service != null && IsServiceRunning(service))
        {
            await RunElevatedScAndWaitAsync("stop " + service, "Stop MySQL/MariaDB service");
            if (!await WaitUntilAsync(() => !IsServiceRunning(service), TimeSpan.FromSeconds(12)))
            {
                throw new InvalidOperationException("MySQL/MariaDB service belum berhenti setelah timeout.");
            }
            return;
        }

        var result = await Task.Run(() => RunCaptureResult(mysqlAdminExe, "--user=root shutdown", 15000));
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

    private void RefreshPhpList()
    {
        PhpList.Items.Clear();
        foreach (var install in GetPhpInstalls())
        {
            PhpList.Items.Add(install);
            if (install.Active) PhpList.SelectedItem = install;
        }
        UpdatePhpPreview();
    }

    private IEnumerable<PhpInstall> GetPhpInstalls()
    {
        var active = GetApachePhpDir();
        if (!Directory.Exists(root)) return Array.Empty<PhpInstall>();
        return Directory.GetDirectories(root, "php*")
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
        if (PhpList.SelectedItem is not PhpInstall selected)
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
        if (PhpList.SelectedItem is not PhpInstall selected) return;
        var validation = ValidatePhpInstall(selected).ToList();
        if (validation.Count > 0)
        {
            await ShowMessageAsync("Tidak bisa switch", string.Join(Environment.NewLine, validation));
            return;
        }

        var confirm = await ShowConfirmAsync("Apply PHP switch?", PhpPreviewBox.Text + Environment.NewLine + Environment.NewLine + "Backup config akan dibuat sebelum perubahan.");
        if (!confirm) return;

        var phpForward = selected.Path.Replace('\\', '/');
        var phpPathPattern = "[A-Za-z]:/[^\"\r\n]*?/php(?:[-_][^/\"]+)?";
        var phpEnvPattern = "\\\\\\\\[^\"]*?php(?:[-_][^\"]+)?";
        var phpEnv = @"\\" + Path.GetFileName(root.TrimEnd('\\')) + @"\\" + selected.Name;
        var backup = xamppConf + ".app-bak-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        File.Copy(xamppConf, backup, true);
        lastPhpBackup = backup;
        var content = await File.ReadAllTextAsync(xamppConf);

        content = Regex.Replace(content, "SetEnv MIBDIRS \"" + phpPathPattern + "/extras/mibs\"", "SetEnv MIBDIRS \"" + phpForward + "/extras/mibs\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "SetEnv PHP_PEAR_SYSCONF_DIR \"" + phpEnvPattern + "\"", "SetEnv PHP_PEAR_SYSCONF_DIR \"" + phpEnv + "\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "SetEnv PHPRC \"" + phpEnvPattern + "\"", "SetEnv PHPRC \"" + phpEnv + "\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "LoadFile \"" + phpPathPattern + "/php8ts\\.dll\"", "LoadFile \"" + phpForward + "/php8ts.dll\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "LoadFile \"" + phpPathPattern + "/libpq\\.dll\"", "LoadFile \"" + phpForward + "/libpq.dll\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "LoadFile \"" + phpPathPattern + "/libsqlite3\\.dll\"", "LoadFile \"" + phpForward + "/libsqlite3.dll\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "LoadModule php_module \"" + phpPathPattern + "/php8apache2_4\\.dll\"", "LoadModule php_module \"" + phpForward + "/php8apache2_4.dll\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "PHPINIDir \"" + phpPathPattern + "\"", "PHPINIDir \"" + phpForward + "\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "ScriptAlias /php-cgi/ \"" + phpPathPattern + "/\"", "ScriptAlias /php-cgi/ \"" + phpForward + "/\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "<Directory \"" + phpPathPattern + "\">", "<Directory \"" + phpForward + "\">", RegexOptions.IgnoreCase);

        await File.WriteAllTextAsync(xamppConf, content);
        var test = RunCapture(apacheExe, "-t -f \"" + apacheConf + "\"");
        if (!test.Contains("Syntax OK", StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(backup, xamppConf, true);
            await ShowMessageAsync("Apache config gagal, rollback", test);
            return;
        }

        await ShowMessageAsync("PHP switched", "Apache diarahkan ke " + selected.Name + ". Restart Apache agar aktif.");
        await AppendActivityAsync("PHP Switcher", "Succeeded", "Apache PHP set to " + selected.Name);
        RefreshPhpList();
        RefreshStatus();
    }

    private async Task RollbackLastPhpBackupAsync()
    {
        var backup = lastPhpBackup ?? Directory.GetFiles(Path.GetDirectoryName(xamppConf) ?? root, Path.GetFileName(xamppConf) + ".app-bak-*")
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();
        if (backup == null || !File.Exists(backup))
        {
            await ShowMessageAsync("Rollback PHP", "Backup PHP switch belum ditemukan.");
            return;
        }

        var confirm = await ShowConfirmAsync("Rollback PHP config?", "Restore backup ini ke httpd-xampp.conf?" + Environment.NewLine + backup);
        if (!confirm) return;

        File.Copy(backup, xamppConf, true);
        var test = RunCapture(apacheExe, "-t -f \"" + apacheConf + "\"");
        if (!test.Contains("Syntax OK", StringComparison.OrdinalIgnoreCase))
        {
            await ShowMessageAsync("Rollback warning", "Backup sudah direstore, tapi Apache config masih bermasalah:" + Environment.NewLine + test);
            return;
        }

        await AppendActivityAsync("PHP Switcher", "Rolled back", "Restored " + backup);
        RefreshPhpList();
        RefreshStatus();
        await ShowMessageAsync("Rollback complete", "Config PHP sudah dikembalikan dari backup terakhir.");
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

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
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

}
