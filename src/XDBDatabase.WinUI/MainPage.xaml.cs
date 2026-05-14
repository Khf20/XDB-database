using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace XDBDatabase_WinUI;

public sealed partial class MainPage : Page
{
    private readonly string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XDB-database");
    private readonly string settingsFile;
    private readonly string activityLogFile;
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
        settings = LoadSettings();
        ApplySettingsToPaths();

        InitializeComponent();

        Loaded += MainPage_Loaded;
        SizeChanged += (_, _) => UpdateResponsiveLayout();
        timer.Interval = TimeSpan.FromSeconds(5);
        timer.Tick += (_, _) => RefreshStatus();
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        SyncSettingsView();
        ShowView(DashboardView, "Dashboard");
        UpdateValidationStatus();
        RefreshPhpList();
        RefreshStatus();
        LoadLog();
        AppendActivity("App", "Started", "XDB-database launched. XAMPP root: " + root);
        UpdateResponsiveLayout();
        timer.Start();
    }

    private void DashboardNav_Click(object sender, RoutedEventArgs e) => ShowView(DashboardView, "Dashboard");
    private void LogsNav_Click(object sender, RoutedEventArgs e) { ShowView(LogsView, "Logs"); LoadLog(); }
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
    private void ReloadLogButton_Click(object sender, RoutedEventArgs e) => LoadLog();
    private void ReloadPhpButton_Click(object sender, RoutedEventArgs e) => RefreshPhpList();
    private void LogCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => LoadLog();
    private void PhpList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePhpPreview();
    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e) => await SaveSettingsFromViewAsync();
    private async void ResetSettingsButton_Click(object sender, RoutedEventArgs e) => await ResetSettingsAsync();
    private async void RepairPhpMyAdminButton_Click(object sender, RoutedEventArgs e) => await RunBusy("Repair phpMyAdmin access", RepairPhpMyAdminAccess);
    private async void RollbackPhpButton_Click(object sender, RoutedEventArgs e) => await RollbackLastPhpBackupAsync();

    private async void StartApacheButton_Click(object sender, RoutedEventArgs e) => await RunBusy("Start Apache", StartApache);
    private async void StopApacheButton_Click(object sender, RoutedEventArgs e) => await RunBusy("Stop Apache", StopApache);
    private async void RestartApacheButton_Click(object sender, RoutedEventArgs e) => await RunBusy("Restart Apache", () => { StopApache(); StartApache(); });
    private async void StartMysqlButton_Click(object sender, RoutedEventArgs e) => await RunBusy("Start MySQL/MariaDB", StartMysql);
    private async void StopMysqlButton_Click(object sender, RoutedEventArgs e) => await RunBusy("Stop MySQL/MariaDB", StopMysql);
    private async void RestartMysqlButton_Click(object sender, RoutedEventArgs e) => await RunBusy("Restart MySQL/MariaDB", () => { StopMysql(); StartMysql(); });

    private void ShowView(FrameworkElement activeView, string title)
    {
        DashboardView.Visibility = Visibility.Collapsed;
        LogsView.Visibility = Visibility.Collapsed;
        PhpView.Visibility = Visibility.Collapsed;
        ToolsView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        activeView.Visibility = Visibility.Visible;
        PageTitle.Text = title;
        PageSubtitle.Text = root;
    }

    private async Task RunBusy(string actionName, Action action)
    {
        SetBusy(false);
        operationBusy = true;
        SetOperationState(actionName, true, false);
        AppendActivity(actionName, "Started", "");
        try
        {
            await Task.Run(action);
            AppendActivity(actionName, "Succeeded", "");
            SetOperationState(actionName, false, false);
        }
        catch (Exception ex)
        {
            AppendActivity(actionName, "Failed", ex.Message);
            SetOperationState(actionName, false, true);
            await ShowMessageAsync("XDB-database", ex.Message);
        }
        finally
        {
            SetBusy(true);
            operationBusy = false;
            UpdateValidationStatus();
            RefreshStatus();
            LoadLog();
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

    private AppSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(settingsFile)) return new AppSettings();
            var json = File.ReadAllText(settingsFile);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    private void SaveSettings()
    {
        Directory.CreateDirectory(appDataDir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsFile, json);
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
        SettingsPathText.Text = settingsFile;
        ActivityLogPathText.Text = activityLogFile;
    }

    private async Task SaveSettingsFromViewAsync()
    {
        settings.XamppRoot = string.IsNullOrWhiteSpace(XamppRootBox.Text) ? @"C:\xampp" : XamppRootBox.Text.Trim().TrimEnd('\\');
        settings.ApacheServiceName = string.IsNullOrWhiteSpace(ApacheServiceBox.Text) ? "Apache2.4" : ApacheServiceBox.Text.Trim();
        settings.MysqlServiceNames = string.IsNullOrWhiteSpace(MysqlServiceBox.Text) ? "mysql;MariaDB" : MysqlServiceBox.Text.Trim();
        settings.Browser = (BrowserCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Default";
        ApplySettingsToPaths();
        SaveSettings();
        AppendActivity("Settings", "Saved", "XAMPP root: " + root);
        UpdateValidationStatus();
        RefreshPhpList();
        RefreshStatus();
        LoadLog();
        PageSubtitle.Text = root;
        await ShowMessageAsync("Settings saved", "Pengaturan sudah disimpan.");
    }

    private async Task ResetSettingsAsync()
    {
        settings = new AppSettings();
        ApplySettingsToPaths();
        SaveSettings();
        SyncSettingsView();
        AppendActivity("Settings", "Reset", "Settings restored to defaults.");
        UpdateValidationStatus();
        RefreshStatus();
        await ShowMessageAsync("Settings reset", "Pengaturan dikembalikan ke default.");
    }

    private void AppendActivity(string action, string status, string details)
    {
        try
        {
            Directory.CreateDirectory(appDataDir);
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + status + " | " + action;
            if (!string.IsNullOrWhiteSpace(details)) line += " | " + details.Replace(Environment.NewLine, " ");
            File.AppendAllText(activityLogFile, line + Environment.NewLine);
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
        XamppValidationText.Text = xamppInstallValid
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
        ActiveServicesText.Text = runningCount.ToString();
        DashboardClockText.Text = DateTime.Now.ToString("HH:mm:ss");
        SidebarStatusText.Text = runningCount + " service aktif";
        SidebarTimeText.Text = DateTime.Now.ToString("HH:mm:ss");
        UpdateHealthSummary(state);
    }

    private static void SetStateText(TextBlock label, string state)
    {
        label.Text = state;
        var color = state switch
        {
            "Running" => Color.FromArgb(255, 18, 130, 76),
            "Starting" or "Stopping" or "Restarting" or "Repairing" => Color.FromArgb(255, 37, 99, 235),
            "Error" => Color.FromArgb(255, 248, 113, 113),
            _ => Color.FromArgb(255, 248, 113, 113)
        };
        label.Foreground = new SolidColorBrush(color);
        if (label.Parent is Border badge)
        {
            var running = state == "Running";
            var working = state is "Starting" or "Stopping" or "Restarting" or "Repairing";
            badge.Background = new SolidColorBrush(running ? Color.FromArgb(255, 234, 248, 239) : working ? Color.FromArgb(255, 29, 45, 80) : Color.FromArgb(255, 56, 38, 43));
            badge.BorderBrush = new SolidColorBrush(running ? Color.FromArgb(255, 134, 239, 172) : working ? Color.FromArgb(255, 96, 165, 250) : Color.FromArgb(255, 127, 29, 29));
        }
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
        var content = File.ReadAllText(xamppConf);
        var match = Regex.Match(content, "LoadModule\\s+php_module\\s+\"([^\"]+php8apache2_4\\.dll)\"", RegexOptions.IgnoreCase);
        if (match.Success) return Path.GetDirectoryName(match.Groups[1].Value.Replace('/', '\\')) ?? Path.Combine(root, "php");
        return Path.Combine(root, "php");
    }

    private void StartApache()
    {
        if (IsXamppServiceInstalled(apacheServiceName))
        {
            RunElevatedScAndWait("start " + apacheServiceName, "Start Apache service");
            Thread.Sleep(1800);
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
        Process.Start(psi);
        Thread.Sleep(1800);
    }

    private void StopApache()
    {
        if (IsXamppServiceInstalled(apacheServiceName))
        {
            RunElevatedScAndWait("stop " + apacheServiceName, "Stop Apache service");
            if (!WaitUntil(() => !IsServiceRunning(apacheServiceName), TimeSpan.FromSeconds(12)))
            {
                throw new InvalidOperationException("Apache service belum berhenti setelah timeout. Buka Services Windows dan cek Apache2.4.");
            }
            return;
        }

        RunCapture(apacheExe, "-k shutdown -f \"" + apacheConf + "\"");
        Thread.Sleep(700);
        foreach (var p in GetProcesses("httpd"))
        {
            try { p.Kill(); } catch { }
        }
        Thread.Sleep(400);
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

    private void RunElevatedScAndWait(string scArguments, string actionName)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"& { sc.exe " + scArguments + "; exit $LASTEXITCODE }\"")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });
            if (process == null) throw new InvalidOperationException(actionName + " gagal dijalankan.");
            if (!process.WaitForExit(30000))
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

    private void StartMysql()
    {
        var service = GetInstalledService(mysqlServiceNames);
        if (service != null)
        {
            RunElevatedScAndWait("start " + service, "Start MySQL/MariaDB service");
            Thread.Sleep(1800);
            return;
        }

        var psi = new ProcessStartInfo(mysqlExe, "--defaults-file=\"" + mysqlIni + "\" --standalone")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Process.Start(psi);
        Thread.Sleep(1800);
    }

    private void StopMysql()
    {
        var service = GetInstalledService(mysqlServiceNames);
        if (service != null && IsServiceRunning(service))
        {
            RunElevatedScAndWait("stop " + service, "Stop MySQL/MariaDB service");
            if (!WaitUntil(() => !IsServiceRunning(service), TimeSpan.FromSeconds(12)))
            {
                throw new InvalidOperationException("MySQL/MariaDB service belum berhenti setelah timeout.");
            }
            return;
        }

        var result = RunCaptureResult(mysqlAdminExe, "--user=root shutdown", 15000);
        if (WaitUntil(() => GetProcesses("mysqld").Count == 0, TimeSpan.FromSeconds(12)))
        {
            return;
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("MySQL tidak bisa dihentikan secara aman. Aplikasi tidak memaksa kill proses database. Detail: " + Shorten(result.Output));
        }

        throw new InvalidOperationException("MySQL masih berjalan setelah shutdown aman. Cek mysql_error.log sebelum memaksa stop manual.");
    }

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            Thread.Sleep(300);
        }
        return condition();
    }

    private static string Shorten(string value)
    {
        value = string.IsNullOrWhiteSpace(value) ? "No output." : value.Trim();
        return value.Length <= 800 ? value : value.Substring(0, 800) + "...";
    }

    private void UpdateResponsiveLayout()
    {
        if (ServiceCardsGrid == null || ToolsGrid == null) return;

        var compactServices = ActualWidth < 1120;
        ServiceCardColumn2.Width = compactServices ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(MysqlServiceCard, compactServices ? 0 : 1);
        Grid.SetRow(MysqlServiceCard, compactServices ? 1 : 0);

        var toolColumns = ActualWidth < 900 ? 1 : ActualWidth < 1260 ? 2 : 3;
        ToolsColumn2.Width = toolColumns >= 2 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        ToolsColumn3.Width = toolColumns >= 3 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        var tools = new[]
        {
            ToolOpenHtdocs, ToolApacheConfig, ToolMysqlData,
            ToolRoot, ToolPhpMyAdmin, ToolServerStatus,
            ToolRepairPhpMyAdmin, ToolPhpIni, ToolMysqlIni,
            ToolHttpdConf, ToolVhostsConf, ToolErrorLog,
            ToolWindowsServices
        };

        for (var index = 0; index < tools.Length; index++)
        {
            Grid.SetColumn(tools[index], index % toolColumns);
            Grid.SetRow(tools[index], index / toolColumns);
        }
    }

    private void LoadLog()
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
        var lines = ReadAllLinesShared(file);
        LogBox.Text = string.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - 250)));
    }

    private static string[] ReadAllLinesShared(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
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
        var content = File.ReadAllText(xamppConf);

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

        File.WriteAllText(xamppConf, content);
        var test = RunCapture(apacheExe, "-t -f \"" + apacheConf + "\"");
        if (!test.Contains("Syntax OK", StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(backup, xamppConf, true);
            await ShowMessageAsync("Apache config gagal, rollback", test);
            return;
        }

        await ShowMessageAsync("PHP switched", "Apache diarahkan ke " + selected.Name + ". Restart Apache agar aktif.");
        AppendActivity("PHP Switcher", "Succeeded", "Apache PHP set to " + selected.Name);
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

        AppendActivity("PHP Switcher", "Rolled back", "Restored " + backup);
        RefreshPhpList();
        RefreshStatus();
        await ShowMessageAsync("Rollback complete", "Config PHP sudah dikembalikan dari backup terakhir.");
    }

    private void RepairPhpMyAdminAccess()
    {
        if (!File.Exists(xamppConf))
        {
            throw new InvalidOperationException("File httpd-xampp.conf tidak ditemukan: " + xamppConf);
        }

        var backup = xamppConf + ".app-bak-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        File.Copy(xamppConf, backup, true);
        var content = File.ReadAllText(xamppConf);
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

        File.WriteAllText(xamppConf, repaired);
        var test = RunCapture(apacheExe, "-t -f \"" + apacheConf + "\"");
        if (!test.Contains("Syntax OK", StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(backup, xamppConf, true);
            throw new InvalidOperationException("Repair phpMyAdmin dibatalkan karena Apache config tidak valid. Detail: " + Shorten(test));
        }

        AppendActivity("Repair phpMyAdmin access", "Config updated", "Backup: " + backup);
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

    private sealed class XamppState
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

    private sealed class PortInfo
    {
        public int Port { get; set; }
        public int Pid { get; set; }
        public string ProcessName { get; set; } = "";
        public string ProcessPath { get; set; } = "";
        public bool OwnedByXampp { get; set; }
    }

    private sealed class PhpInstall
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Version { get; set; } = "";
        public bool Active { get; set; }
        public bool HasApacheModule { get; set; }

        public override string ToString()
        {
            return Name + " | " + Version + (Active ? " | ACTIVE" : "") + (HasApacheModule ? "" : " | CLI only");
        }
    }

    private sealed class AppSettings
    {
        public string XamppRoot { get; set; } = @"C:\xampp";
        public string ApacheServiceName { get; set; } = "Apache2.4";
        public string MysqlServiceNames { get; set; } = "mysql;MariaDB";
        public string Browser { get; set; } = "Default";
    }
}
