using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace XDBDatabase_WinUI;

public sealed partial class MainPage : Page
{
    private readonly string root = @"C:\xampp";
    private readonly string apacheExe;
    private readonly string apacheConf;
    private readonly string xamppConf;
    private readonly string mysqlExe;
    private readonly string mysqlAdminExe;
    private readonly string mysqlClientExe;
    private readonly string mysqlIni;
    private readonly string phpExe;
    private readonly string apacheErrorLog;
    private readonly string mysqlErrorLog;
    private readonly string xamppControlLog;
    private readonly string apacheAccessLog;
    private readonly DispatcherTimer timer = new();

    public MainPage()
    {
        InitializeComponent();

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

        Loaded += MainPage_Loaded;
        timer.Interval = TimeSpan.FromSeconds(5);
        timer.Tick += (_, _) => RefreshStatus();
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        ShowView(DashboardView, "Dashboard");
        RefreshPhpList();
        RefreshStatus();
        LoadLog();
        timer.Start();
    }

    private void DashboardNav_Click(object sender, RoutedEventArgs e) => ShowView(DashboardView, "Dashboard");
    private void LogsNav_Click(object sender, RoutedEventArgs e) { ShowView(LogsView, "Logs"); LoadLog(); }
    private void PhpNav_Click(object sender, RoutedEventArgs e) { ShowView(PhpView, "PHP Switcher"); RefreshPhpList(); }
    private void ToolsNav_Click(object sender, RoutedEventArgs e) => ShowView(ToolsView, "Tools");
    private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshStatus();
    private void PhpMyAdminButton_Click(object sender, RoutedEventArgs e) => OpenUrl("http://localhost/phpmyadmin/");
    private void OpenDashboard_Click(object sender, RoutedEventArgs e) => OpenUrl("http://localhost/dashboard/");
    private void OpenHtdocs_Click(object sender, RoutedEventArgs e) => OpenPath(Path.Combine(root, "htdocs"));
    private void OpenApacheConfig_Click(object sender, RoutedEventArgs e) => OpenPath(Path.Combine(root, @"apache\conf"));
    private void OpenMysqlData_Click(object sender, RoutedEventArgs e) => OpenPath(Path.Combine(root, @"mysql\data"));
    private void OpenRoot_Click(object sender, RoutedEventArgs e) => OpenPath(root);
    private void ReloadLogButton_Click(object sender, RoutedEventArgs e) => LoadLog();
    private void ReloadPhpButton_Click(object sender, RoutedEventArgs e) => RefreshPhpList();
    private void LogCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => LoadLog();

    private async void StartApacheButton_Click(object sender, RoutedEventArgs e) => await RunBusy(StartApache);
    private async void StopApacheButton_Click(object sender, RoutedEventArgs e) => await RunBusy(StopApache);
    private async void RestartApacheButton_Click(object sender, RoutedEventArgs e) => await RunBusy(() => { StopApache(); StartApache(); });
    private async void StartMysqlButton_Click(object sender, RoutedEventArgs e) => await RunBusy(StartMysql);
    private async void StopMysqlButton_Click(object sender, RoutedEventArgs e) => await RunBusy(StopMysql);
    private async void RestartMysqlButton_Click(object sender, RoutedEventArgs e) => await RunBusy(() => { StopMysql(); StartMysql(); });

    private void ShowView(FrameworkElement activeView, string title)
    {
        DashboardView.Visibility = Visibility.Collapsed;
        LogsView.Visibility = Visibility.Collapsed;
        PhpView.Visibility = Visibility.Collapsed;
        ToolsView.Visibility = Visibility.Collapsed;
        activeView.Visibility = Visibility.Visible;
        PageTitle.Text = title;
    }

    private async Task RunBusy(Action action)
    {
        SetBusy(false);
        try
        {
            await Task.Run(action);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("XDB-database", ex.Message);
        }
        finally
        {
            SetBusy(true);
            RefreshStatus();
        }
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

    private void RefreshStatus()
    {
        var state = GetState();
        ApacheVersionText.Text = state.ApacheVersion;
        ApachePhpText.Text = state.ApachePhpVersion;
        PhpCliText.Text = state.PhpCliVersion;
        MariaDbText.Text = state.MysqlVersion;

        SetStateText(ApacheStateText, state.ApacheRunning);
        SetStateText(MysqlStateText, state.MysqlRunning);

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

        var runningCount = (state.ApacheRunning ? 1 : 0) + (state.MysqlRunning ? 1 : 0);
        SidebarStatusText.Text = runningCount + " service aktif";
        SidebarTimeText.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    private static void SetStateText(TextBlock label, bool running)
    {
        label.Text = running ? "Running" : "Stopped";
        label.Foreground = new SolidColorBrush(running ? Color.FromArgb(255, 18, 130, 76) : Color.FromArgb(255, 102, 112, 133));
    }

    private XamppState GetState()
    {
        var ports = GetListeningPorts();
        var httpd = GetProcesses("httpd");
        var mysqld = GetProcesses("mysqld");
        var phpDir = GetApachePhpDir();
        var apachePhpExe = Path.Combine(phpDir, "php.exe");
        return new XamppState
        {
            ApacheRunning = httpd.Count > 0 || ports.ContainsKey(80) || ports.ContainsKey(443),
            MysqlRunning = mysqld.Count > 0 || ports.ContainsKey(3306),
            ApachePids = httpd.Select(p => p.Id).ToList(),
            MysqlPids = mysqld.Select(p => p.Id).ToList(),
            Port80 = ports.TryGetValue(80, out var port80) ? port80 : null,
            Port443 = ports.TryGetValue(443, out var port443) ? port443 : null,
            Port3306 = ports.TryGetValue(3306, out var port3306) ? port3306 : null,
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
                list.Add(process);
            }
        }
        return list;
    }

    private Dictionary<int, string> GetListeningPorts()
    {
        var result = new Dictionary<int, string>();
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
                    result[port] = "#" + parts[4];
                }
            }
        }
        return result;
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
            if (process == null) return "";
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(10000);
            return output + error;
        }
        catch (Exception ex)
        {
            return ex.Message;
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
        RunCapture(apacheExe, "-k shutdown -f \"" + apacheConf + "\"");
        Thread.Sleep(700);
        foreach (var p in GetProcesses("httpd"))
        {
            try { p.Kill(); } catch { }
        }
        Thread.Sleep(400);
    }

    private void StartMysql()
    {
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
        RunCapture(mysqlAdminExe, "--user=root shutdown");
        Thread.Sleep(900);
        foreach (var p in GetProcesses("mysqld"))
        {
            try { p.Kill(); } catch { }
        }
        Thread.Sleep(400);
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

    private async void UsePhpButton_Click(object sender, RoutedEventArgs e)
    {
        if (PhpList.SelectedItem is not PhpInstall selected) return;
        if (!selected.HasApacheModule)
        {
            await ShowMessageAsync("Tidak bisa switch", "PHP ini tidak punya php8apache2_4.dll.");
            return;
        }

        var phpForward = selected.Path.Replace('\\', '/');
        var phpEnv = @"\\xampp\\" + selected.Name;
        var backup = xamppConf + ".app-bak-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        File.Copy(xamppConf, backup, true);
        var content = File.ReadAllText(xamppConf);

        content = Regex.Replace(content, "SetEnv MIBDIRS \"C:/xampp/php(?:[-_][^/\"]+)?/extras/mibs\"", "SetEnv MIBDIRS \"" + phpForward + "/extras/mibs\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "SetEnv PHP_PEAR_SYSCONF_DIR \"\\\\\\\\xampp\\\\\\\\php(?:[-_][^\"]+)?\"", "SetEnv PHP_PEAR_SYSCONF_DIR \"" + phpEnv + "\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "SetEnv PHPRC \"\\\\\\\\xampp\\\\\\\\php(?:[-_][^\"]+)?\"", "SetEnv PHPRC \"" + phpEnv + "\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "LoadFile \"C:/xampp/php(?:[-_][^/\"]+)?/php8ts\\.dll\"", "LoadFile \"" + phpForward + "/php8ts.dll\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "LoadFile \"C:/xampp/php(?:[-_][^/\"]+)?/libpq\\.dll\"", "LoadFile \"" + phpForward + "/libpq.dll\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "LoadFile \"C:/xampp/php(?:[-_][^/\"]+)?/libsqlite3\\.dll\"", "LoadFile \"" + phpForward + "/libsqlite3.dll\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "LoadModule php_module \"C:/xampp/php(?:[-_][^/\"]+)?/php8apache2_4\\.dll\"", "LoadModule php_module \"" + phpForward + "/php8apache2_4.dll\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "PHPINIDir \"C:/xampp/php(?:[-_][^/\"]+)?\"", "PHPINIDir \"" + phpForward + "\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "ScriptAlias /php-cgi/ \"C:/xampp/php(?:[-_][^/\"]+)?/\"", "ScriptAlias /php-cgi/ \"" + phpForward + "/\"", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, "<Directory \"C:/xampp/php(?:[-_][^/\"]+)?\">", "<Directory \"" + phpForward + "\">", RegexOptions.IgnoreCase);

        File.WriteAllText(xamppConf, content);
        var test = RunCapture(apacheExe, "-t -f \"" + apacheConf + "\"");
        if (!test.Contains("Syntax OK"))
        {
            File.Copy(backup, xamppConf, true);
            await ShowMessageAsync("Apache config gagal, rollback", test);
            return;
        }

        await ShowMessageAsync("PHP switched", "Apache diarahkan ke " + selected.Name + ". Restart Apache agar aktif.");
        RefreshPhpList();
        RefreshStatus();
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

    private static void OpenPath(string path) => Process.Start("explorer.exe", "\"" + path + "\"");

    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private sealed class XamppState
    {
        public bool ApacheRunning { get; set; }
        public bool MysqlRunning { get; set; }
        public List<int> ApachePids { get; set; } = new();
        public List<int> MysqlPids { get; set; } = new();
        public string? Port80 { get; set; }
        public string? Port443 { get; set; }
        public string? Port3306 { get; set; }
        public string ApacheVersion { get; set; } = "-";
        public string ApachePhpVersion { get; set; } = "-";
        public string PhpCliVersion { get; set; } = "-";
        public string MysqlVersion { get; set; } = "-";
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
}
