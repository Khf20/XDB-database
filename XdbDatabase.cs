using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XdbDatabase
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            try
            {
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "XDB-database failed to start", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    internal sealed class MainForm : Form
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

        private readonly Panel sidebar = new Panel();
        private readonly Panel content = new Panel();
        private readonly Label pageTitle = new Label();
        private Label sidebarState = new Label();
        private Label sidebarTime = new Label();
        private readonly Timer timer = new Timer();
        private const int SidebarWidth = 270;
        private const int ContentPadding = 28;
        private const int PageTop = 104;

        private Button navDashboard;
        private Button navLogs;
        private Button navPhp;
        private Button navTools;
        private Button refreshButton;
        private Button phpMyAdminButton;
        private Label rootPathLabel;

        private Panel dashboardPage;
        private Panel logsPage;
        private Panel phpPage;
        private Panel toolsPage;

        private Label apacheVersion;
        private Label apachePhpVersion;
        private Label phpCliVersion;
        private Label mysqlVersion;
        private Label apacheHeader;
        private Label mysqlHeader;
        private Label apacheState;
        private Label mysqlState;
        private Label apachePid;
        private Label mysqlPid;
        private Label apachePorts;
        private Label mysqlPorts;
        private Button startApache;
        private Button stopApache;
        private Button restartApache;
        private Button startMysql;
        private Button stopMysql;
        private Button restartMysql;

        private ComboBox logCombo;
        private TextBox logBox;
        private Label logPath;

        private ListBox phpList;
        private Button usePhpButton;

        private readonly Color bg = Color.FromArgb(245, 247, 251);
        private readonly Color sidebarBg = Color.FromArgb(24, 34, 53);
        private readonly Color sidebarActive = Color.FromArgb(44, 55, 74);
        private readonly Color text = Color.FromArgb(23, 32, 51);
        private readonly Color muted = Color.FromArgb(102, 112, 133);
        private readonly Color line = Color.FromArgb(220, 227, 239);
        private readonly Color accent = Color.FromArgb(240, 90, 40);
        private readonly Color green = Color.FromArgb(19, 138, 76);
        private readonly Color red = Color.FromArgb(200, 63, 54);
        private readonly Color blue = Color.FromArgb(36, 107, 254);

        public MainForm()
        {
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

            Text = "XDB-database";
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;
            MinimumSize = new Size(760, 560);
            Size = new Size(1120, 740);
            BackColor = bg;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            Icon = SystemIcons.Application;
            DoubleBuffered = true;
            FitToScreen();

            BuildLayout();
            ShowPage(dashboardPage, navDashboard, "Dashboard");

            timer.Interval = 5000;
            timer.Tick += (s, e) => RefreshStatus();
            timer.Start();

            RefreshPhpList();
            RefreshStatus();
            LoadLog();
        }

        private void BuildLayout()
        {
            sidebar.BackColor = sidebarBg;
            content.BackColor = bg;
            Controls.Add(content);
            Controls.Add(sidebar);

            BuildSidebar();
            BuildHeader();
            BuildPages();
            Resize += (s, e) => LayoutShell();
            Shown += (s, e) => LayoutShell();
            LayoutShell();
        }

        private void LayoutShell()
        {
            sidebar.SetBounds(0, 0, SidebarWidth, ClientSize.Height);
            content.SetBounds(SidebarWidth, 0, Math.Max(0, ClientSize.Width - SidebarWidth), ClientSize.Height);
            sidebar.BringToFront();
            LayoutHeader();
            LayoutPages();
            LayoutDashboard();
            LayoutToolsPage();
        }

        private void FitToScreen()
        {
            var area = Screen.PrimaryScreen.WorkingArea;
            var width = Math.Min(1120, Math.Max(MinimumSize.Width, area.Width - 80));
            var height = Math.Min(740, Math.Max(MinimumSize.Height, area.Height - 80));
            Size = new Size(width, height);
            Location = new Point(area.Left + Math.Max(0, (area.Width - width) / 2), area.Top + Math.Max(0, (area.Height - height) / 2));
        }

        private void BuildSidebar()
        {
            var logo = new Panel
            {
                BackColor = accent,
                Size = new Size(44, 44),
                Location = new Point(20, 26)
            };
            var logoText = new Label
            {
                Text = "X",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            logo.Controls.Add(logoText);
            sidebar.Controls.Add(logo);

            sidebar.Controls.Add(new Label
            {
                Text = "XDB-database",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Location = new Point(84, 27),
                Size = new Size(176, 26),
                AutoEllipsis = false
            });
            sidebar.Controls.Add(new Label
            {
                Text = "Database control",
                ForeColor = Color.FromArgb(174, 184, 200),
                Location = new Point(85, 54),
                Size = new Size(174, 20),
                AutoEllipsis = true
            });

            navDashboard = NavButton("Dashboard", 98);
            navLogs = NavButton("Logs", 148);
            navPhp = NavButton("PHP Switcher", 198);
            navTools = NavButton("Tools", 248);
            sidebar.Controls.AddRange(new Control[] { navDashboard, navLogs, navPhp, navTools });

            navDashboard.Click += (s, e) => ShowPage(dashboardPage, navDashboard, "Dashboard");
            navLogs.Click += (s, e) => { ShowPage(logsPage, navLogs, "Logs"); LoadLog(); };
            navPhp.Click += (s, e) => { ShowPage(phpPage, navPhp, "PHP Switcher"); RefreshPhpList(); };
            navTools.Click += (s, e) => ShowPage(toolsPage, navTools, "Tools");

            var statusPanel = new Panel
            {
                BackColor = Color.FromArgb(38, 50, 71),
                Location = new Point(20, 590),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Size = new Size(230, 76)
            };
            sidebarState = new Label
            {
                Text = "Checking services",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(14, 16),
                Size = new Size(190, 20)
            };
            sidebarTime = new Label
            {
                Text = "-",
                ForeColor = Color.FromArgb(174, 184, 200),
                Location = new Point(14, 40),
                Size = new Size(190, 20)
            };
            statusPanel.Controls.Add(sidebarState);
            statusPanel.Controls.Add(sidebarTime);
            sidebar.Controls.Add(statusPanel);
        }

        private Button NavButton(string label, int top)
        {
            return new Button
            {
                Text = label,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(214, 222, 234),
                BackColor = sidebarBg,
                Location = new Point(20, top),
                Size = new Size(230, 40),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            }.WithFlatBorder(sidebarBg);
        }

        private void BuildHeader()
        {
            pageTitle.Text = "Dashboard";
            pageTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
            pageTitle.ForeColor = text;
            pageTitle.Location = new Point(ContentPadding, 24);
            pageTitle.Size = new Size(360, 48);
            content.Controls.Add(pageTitle);

            rootPathLabel = new Label
            {
                Text = root,
                ForeColor = muted,
                Location = new Point(ContentPadding + 3, 68),
                Size = new Size(360, 22)
            };
            content.Controls.Add(rootPathLabel);

            refreshButton = ActionButton("Refresh", 100, Color.White, text, line);
            refreshButton.Click += (s, e) => RefreshStatus();
            content.Controls.Add(refreshButton);

            phpMyAdminButton = ActionButton("phpMyAdmin", 140, text, Color.White, text);
            phpMyAdminButton.Click += (s, e) => OpenUrl("http://localhost/phpmyadmin/");
            content.Controls.Add(phpMyAdminButton);
        }

        private void LayoutHeader()
        {
            if (refreshButton == null || phpMyAdminButton == null) return;
            var compact = content.ClientSize.Width < 620;
            refreshButton.Width = compact ? 88 : 100;
            phpMyAdminButton.Width = compact ? 124 : 140;
            pageTitle.Font = new Font("Segoe UI", compact ? 19F : 22F, FontStyle.Bold);
            var right = Math.Max(ContentPadding, content.ClientSize.Width - ContentPadding);
            phpMyAdminButton.Location = new Point(right - phpMyAdminButton.Width, 30);
            refreshButton.Location = new Point(phpMyAdminButton.Left - 12 - refreshButton.Width, 30);

            var titleWidth = Math.Max(180, refreshButton.Left - ContentPadding - 16);
            pageTitle.Size = new Size(Math.Min(420, titleWidth), 48);
            if (rootPathLabel != null) rootPathLabel.Size = new Size(Math.Min(520, titleWidth), 22);
        }

        private void BuildPages()
        {
            dashboardPage = PagePanel();
            logsPage = PagePanel();
            phpPage = PagePanel();
            toolsPage = PagePanel();
            content.Controls.AddRange(new Control[] { dashboardPage, logsPage, phpPage, toolsPage });
            BuildDashboardPage();
            BuildLogsPage();
            BuildPhpPage();
            BuildToolsPage();
            LayoutPages();
        }

        private Panel PagePanel()
        {
            return new Panel
            {
                BackColor = bg,
                Location = new Point(ContentPadding, PageTop),
                Size = new Size(100, 100)
            };
        }

        private void LayoutPages()
        {
            var width = Math.Max(0, content.ClientSize.Width - (ContentPadding * 2));
            var height = Math.Max(0, content.ClientSize.Height - PageTop - ContentPadding);
            foreach (var page in new[] { dashboardPage, logsPage, phpPage, toolsPage })
            {
                if (page != null) page.SetBounds(ContentPadding, PageTop, width, height);
            }
        }

        private void BuildDashboardPage()
        {
            dashboardPage.AutoScroll = true;
            dashboardPage.Resize += (s, e) => LayoutDashboard();

            apacheVersion = VersionCard(dashboardPage, "APACHE", 0);
            apachePhpVersion = VersionCard(dashboardPage, "PHP VIA APACHE", 1);
            phpCliVersion = VersionCard(dashboardPage, "PHP CLI", 2);
            mysqlVersion = VersionCard(dashboardPage, "MARIADB", 3);

            var apacheCard = Card(0, 96, 0, 0);
            apacheCard.Name = "ApacheCard";
            dashboardPage.Controls.Add(apacheCard);

            var mysqlCard = Card(0, 96, 0, 0);
            mysqlCard.Name = "MysqlCard";
            dashboardPage.Controls.Add(mysqlCard);

            apacheHeader = HeaderLabel("Apache", 18, 20, 20);
            apacheCard.Controls.Add(apacheHeader);
            apacheState = Pill(apacheCard, "Stopped", 0);
            apachePid = SmallLabel("PID -", 22, 58);
            apachePorts = SmallLabel("Port 80: -\r\nPort 443: -", 22, 98);
            apachePorts.Size = new Size(320, 52);
            apacheCard.Controls.Add(apachePid);
            apacheCard.Controls.Add(apachePorts);

            startApache = ActionButton("Start", 92, Color.White, green, Color.FromArgb(191, 232, 206));
            stopApache = ActionButton("Stop", 92, Color.White, red, Color.FromArgb(243, 197, 192));
            restartApache = ActionButton("Restart", 100, Color.White, blue, line);
            startApache.Click += async (s, e) => await RunBusy(StartApache);
            stopApache.Click += async (s, e) => await RunBusy(StopApache);
            restartApache.Click += async (s, e) => await RunBusy(() => { StopApache(); StartApache(); });
            apacheCard.Controls.AddRange(new Control[] { startApache, stopApache, restartApache });

            mysqlHeader = HeaderLabel("MySQL / MariaDB", 15, 20, 22);
            mysqlCard.Controls.Add(mysqlHeader);
            mysqlState = Pill(mysqlCard, "Stopped", 0);
            mysqlPid = SmallLabel("PID -", 22, 58);
            mysqlPorts = SmallLabel("Port 3306: -", 22, 98);
            mysqlCard.Controls.Add(mysqlPid);
            mysqlCard.Controls.Add(mysqlPorts);

            startMysql = ActionButton("Start", 92, Color.White, green, Color.FromArgb(191, 232, 206));
            stopMysql = ActionButton("Stop", 92, Color.White, red, Color.FromArgb(243, 197, 192));
            restartMysql = ActionButton("Restart", 100, Color.White, blue, line);
            startMysql.Click += async (s, e) => await RunBusy(StartMysql);
            stopMysql.Click += async (s, e) => await RunBusy(StopMysql);
            restartMysql.Click += async (s, e) => await RunBusy(() => { StopMysql(); StartMysql(); });
            mysqlCard.Controls.AddRange(new Control[] { startMysql, stopMysql, restartMysql });

            LayoutDashboard();
        }

        private void LayoutDashboard()
        {
            if (dashboardPage == null) return;
            var gap = 14;
            var availableWidth = Math.Max(1, dashboardPage.ClientSize.Width);
            var versionCards = dashboardPage.Controls.OfType<Panel>().Where(p => p.Tag as string == "version").OrderBy(p => p.TabIndex).ToList();
            var columns = availableWidth < 430 ? 1 : (availableWidth < 980 ? 2 : 4);
            var versionHeight = 72;
            var w = Math.Max(120, (availableWidth - gap * (columns - 1)) / columns);
            for (var i = 0; i < versionCards.Count; i++)
            {
                var row = i / columns;
                var col = i % columns;
                versionCards[i].Location = new Point(col * (w + gap), row * (versionHeight + gap));
                versionCards[i].Size = new Size(w, versionHeight);
                foreach (var label in versionCards[i].Controls.OfType<Label>())
                {
                    label.Width = Math.Max(80, w - 28);
                }
            }

            var rows = versionCards.Count == 0 ? 0 : (int)Math.Ceiling(versionCards.Count / (double)columns);
            var serviceTop = rows == 0 ? 0 : rows * versionHeight + Math.Max(0, rows - 1) * gap + 24;
            var twoColumns = availableWidth >= 980;
            var cardW = twoColumns ? Math.Max(300, (availableWidth - gap) / 2) : availableWidth;
            var cardH = twoColumns ? Math.Max(260, dashboardPage.ClientSize.Height - serviceTop - 4) : 260;
            var apacheCard = dashboardPage.Controls.Find("ApacheCard", false).FirstOrDefault();
            var mysqlCard = dashboardPage.Controls.Find("MysqlCard", false).FirstOrDefault();
            if (apacheCard != null)
            {
                apacheCard.Location = new Point(0, serviceTop);
                apacheCard.Size = new Size(cardW, cardH);
                PositionButtons(apacheCard, startApache, stopApache, restartApache);
                apacheState.Size = new Size(80, 28);
                apacheState.Location = new Point(Math.Max(22, apacheCard.Width - apacheState.Width - 22), 56);
                if (apacheHeader != null) apacheHeader.Size = new Size(Math.Max(160, apacheCard.Width - 44), 42);
                apacheState.BringToFront();
            }
            if (mysqlCard != null)
            {
                mysqlCard.Location = twoColumns ? new Point(cardW + gap, serviceTop) : new Point(0, serviceTop + cardH + gap);
                mysqlCard.Size = new Size(twoColumns ? Math.Max(300, availableWidth - cardW - gap) : availableWidth, cardH);
                PositionButtons(mysqlCard, startMysql, stopMysql, restartMysql);
                mysqlState.Size = new Size(80, 28);
                mysqlState.Location = new Point(Math.Max(22, mysqlCard.Width - mysqlState.Width - 22), 56);
                if (mysqlHeader != null) mysqlHeader.Size = new Size(Math.Max(160, mysqlCard.Width - 44), 42);
                mysqlState.BringToFront();
            }
            var bottom = twoColumns ? serviceTop + cardH : serviceTop + (cardH * 2) + gap;
            dashboardPage.AutoScrollMinSize = bottom > dashboardPage.ClientSize.Height ? new Size(0, bottom + 8) : Size.Empty;
        }

        private void PositionButtons(Control parent, Button a, Button b, Button c)
        {
            var gap = 8;
            var buttonWidth = Math.Min(106, Math.Max(76, (parent.Width - 44 - (gap * 2)) / 3));
            a.Width = buttonWidth;
            b.Width = buttonWidth;
            c.Width = buttonWidth;
            var totalWidth = (buttonWidth * 3) + (gap * 2);
            var left = Math.Max(18, (parent.Width - totalWidth) / 2);
            var top = Math.Max(172, parent.Height - 58);
            a.Location = new Point(left, top);
            b.Location = new Point(left + buttonWidth + gap, top);
            c.Location = new Point(left + (buttonWidth + gap) * 2, top);
        }

        private Label VersionCard(Control parent, string title, int index)
        {
            var card = Card(0, 0, 220, 72);
            card.Tag = "version";
            card.TabIndex = index;
            card.Controls.Add(new Label
            {
                Text = title,
                ForeColor = muted,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Location = new Point(14, 12),
                Size = new Size(180, 18)
            });
            var value = new Label
            {
                Text = "-",
                ForeColor = text,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Location = new Point(14, 36),
                Size = new Size(190, 24),
                AutoEllipsis = true
            };
            card.Controls.Add(value);
            parent.Controls.Add(card);
            return value;
        }

        private void BuildLogsPage()
        {
            var card = Card(0, 0, 0, 0);
            card.Dock = DockStyle.Fill;
            logsPage.Controls.Add(card);

            card.Controls.Add(HeaderLabel("Log Viewer", 18, 20, 18));
            logPath = SmallLabel("-", 22, 54);
            logPath.Size = new Size(560, 24);
            card.Controls.Add(logPath);

            logCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(20, 88),
                Size = new Size(180, 32)
            };
            logCombo.Items.AddRange(new object[] { "Apache Error", "MySQL Error", "XAMPP Control", "Apache Access" });
            logCombo.SelectedIndex = 0;
            logCombo.SelectedIndexChanged += (s, e) => LoadLog();
            card.Controls.Add(logCombo);

            var reload = ActionButton("Reload", 90, Color.White, text, line);
            reload.Location = new Point(214, 87);
            reload.Click += (s, e) => LoadLog();
            card.Controls.Add(reload);

            logBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BackColor = Color.FromArgb(16, 23, 36),
                ForeColor = Color.FromArgb(220, 232, 255),
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9F),
                Location = new Point(20, 138),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Size = new Size(card.Width - 40, card.Height - 158)
            };
            card.Controls.Add(logBox);
            card.Resize += (s, e) => logBox.Size = new Size(card.Width - 40, card.Height - 158);
        }

        private void BuildPhpPage()
        {
            var card = Card(0, 0, 0, 0);
            card.Dock = DockStyle.Fill;
            phpPage.Controls.Add(card);
            card.Controls.Add(HeaderLabel("PHP Switcher", 18, 20, 18));
            card.Controls.Add(SmallLabel("Pilih PHP yang dipakai Apache. Setelah switch, restart Apache.", 22, 54));

            phpList = new ListBox
            {
                Location = new Point(20, 92),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Size = new Size(card.Width - 40, card.Height - 160),
                Font = new Font("Segoe UI", 9F)
            };
            card.Controls.Add(phpList);

            var reload = ActionButton("Reload", 90, Color.White, text, line);
            reload.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            reload.Click += (s, e) => RefreshPhpList();
            usePhpButton = ActionButton("Use for Apache", 140, text, Color.White, text);
            usePhpButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            usePhpButton.Click += (s, e) => SwitchPhp();
            card.Controls.Add(reload);
            card.Controls.Add(usePhpButton);
            card.Resize += (s, e) =>
            {
                phpList.Size = new Size(card.Width - 40, card.Height - 160);
                reload.Location = new Point(card.Width - 250, card.Height - 52);
                usePhpButton.Location = new Point(card.Width - 150, card.Height - 52);
            };
            reload.Location = new Point(card.Width - 250, card.Height - 52);
            usePhpButton.Location = new Point(card.Width - 150, card.Height - 52);
        }

        private void BuildToolsPage()
        {
            var labels = new[]
            {
                "Open htdocs", "Open Apache config", "Open MySQL data",
                "Open C:\\xampp", "Open phpMyAdmin", "Open XAMPP dashboard"
            };
            var actions = new Action[]
            {
                () => OpenPath(Path.Combine(root, "htdocs")),
                () => OpenPath(Path.Combine(root, @"apache\conf")),
                () => OpenPath(Path.Combine(root, @"mysql\data")),
                () => OpenPath(root),
                () => OpenUrl("http://localhost/phpmyadmin/"),
                () => OpenUrl("http://localhost/dashboard/")
            };

            for (var i = 0; i < labels.Length; i++)
            {
                var button = ActionButton(labels[i], 0, Color.White, text, line);
                button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                var index = i;
                button.Click += (s, e) => actions[index]();
                toolsPage.Controls.Add(button);
            }
            toolsPage.Resize += (s, e) =>
            {
                LayoutToolsPage();
            };
            LayoutToolsPage();
        }

        private void LayoutToolsPage()
        {
            if (toolsPage == null) return;
            var gap = 14;
            var columns = toolsPage.Width < 430 ? 1 : (toolsPage.Width < 920 ? 2 : 3);
            var w = Math.Max(160, (toolsPage.Width - gap * (columns - 1)) / columns);
            var h = 118;
            for (var i = 0; i < toolsPage.Controls.Count; i++)
            {
                var row = i / columns;
                var col = i % columns;
                toolsPage.Controls[i].Location = new Point(col * (w + gap), row * (h + gap));
                toolsPage.Controls[i].Size = new Size(w, h);
            }
        }

        private Panel Card(int left, int top, int width, int height)
        {
            return new Panel
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(left, top),
                Size = new Size(width, height)
            };
        }

        private Label HeaderLabel(string value, int size, int left, int top)
        {
            return new Label
            {
                Text = value,
                ForeColor = text,
                Font = new Font("Segoe UI", size, FontStyle.Bold),
                Location = new Point(left, top),
                Size = new Size(360, 42),
                AutoEllipsis = true
            };
        }

        private Label SmallLabel(string value, int left, int top)
        {
            return new Label
            {
                Text = value,
                ForeColor = muted,
                Location = new Point(left, top),
                Size = new Size(500, 24)
            };
        }

        private Label Pill(Control parent, string label, int left)
        {
            var pill = new Label
            {
                Text = label,
                BackColor = Color.FromArgb(238, 242, 248),
                ForeColor = muted,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(left, 22),
                Size = new Size(80, 28)
            };
            parent.Controls.Add(pill);
            return pill;
        }

        private Button ActionButton(string label, int width, Color backColor, Color foreColor, Color borderColor)
        {
            var button = new Button
            {
                Text = label,
                Size = new Size(width <= 0 ? 120 : width, 38),
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            button.FlatAppearance.BorderColor = borderColor;
            button.FlatAppearance.BorderSize = 1;
            return button;
        }

        private void ShowPage(Panel page, Button nav, string title)
        {
            foreach (var panel in new[] { dashboardPage, logsPage, phpPage, toolsPage })
            {
                if (panel != null) panel.Visible = false;
            }
            foreach (var button in new[] { navDashboard, navLogs, navPhp, navTools })
            {
                if (button != null) button.BackColor = sidebarBg;
            }
            page.Visible = true;
            nav.BackColor = sidebarActive;
            pageTitle.Text = title;
        }

        private async Task RunBusy(Action action)
        {
            SetBusy(true);
            try
            {
                await Task.Run(action);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "XDB-database", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
                RefreshStatus();
            }
        }

        private void SetBusy(bool busy)
        {
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            foreach (var button in Controls.OfType<Button>()) button.Enabled = !busy;
        }

        private void RefreshStatus()
        {
            var state = GetState();
            apacheVersion.Text = state.ApacheVersion;
            apachePhpVersion.Text = state.ApachePhpVersion;
            phpCliVersion.Text = state.PhpCliVersion;
            mysqlVersion.Text = state.MysqlVersion;

            SetStateLabel(apacheState, state.ApacheRunning);
            SetStateLabel(mysqlState, state.MysqlRunning);
            apachePid.Text = state.ApachePids.Any() ? "PID " + string.Join(", ", state.ApachePids) : "PID -";
            mysqlPid.Text = state.MysqlPids.Any() ? "PID " + string.Join(", ", state.MysqlPids) : "PID -";
            apachePorts.Text = "Port 80: " + (state.Port80 ?? "free") + Environment.NewLine +
                               "Port 443: " + (state.Port443 ?? "free");
            mysqlPorts.Text = "Port 3306: " + (state.Port3306 ?? "free");
            startApache.Enabled = !state.ApacheRunning;
            stopApache.Enabled = state.ApacheRunning;
            restartApache.Enabled = state.ApacheRunning;
            startMysql.Enabled = !state.MysqlRunning;
            stopMysql.Enabled = state.MysqlRunning;
            restartMysql.Enabled = state.MysqlRunning;
            var runningCount = (state.ApacheRunning ? 1 : 0) + (state.MysqlRunning ? 1 : 0);
            sidebarState.Text = runningCount + " service aktif";
            sidebarTime.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        private void SetStateLabel(Label label, bool running)
        {
            label.Text = running ? "Running" : "Stopped";
            label.BackColor = running ? Color.FromArgb(233, 248, 239) : Color.FromArgb(238, 242, 248);
            label.ForeColor = running ? green : muted;
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
                Port80 = ports.ContainsKey(80) ? ports[80] : null,
                Port443 = ports.ContainsKey(443) ? ports[443] : null,
                Port3306 = ports.ContainsKey(3306) ? ports[3306] : null,
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
                    var file = process.MainModule.FileName;
                    if (file.StartsWith(root, StringComparison.OrdinalIgnoreCase)) list.Add(process);
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
            foreach (var port in new[] { 80, 443, 3306 })
            {
                var text = RunCapture("netstat.exe", "-ano -p tcp");
                foreach (var lineText in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var lineTrim = lineText.Trim();
                    if (!lineTrim.Contains("LISTENING")) continue;
                    var parts = Regex.Split(lineTrim, "\\s+");
                    if (parts.Length < 5) continue;
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
                using (var process = Process.Start(psi))
                {
                    if (process == null) return "";
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit(10000);
                    return output + error;
                }
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
            if (match.Success) return Path.GetDirectoryName(match.Groups[1].Value.Replace('/', '\\'));
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
            System.Threading.Thread.Sleep(1800);
        }

        private void StopApache()
        {
            RunCapture(apacheExe, "-k shutdown -f \"" + apacheConf + "\"");
            System.Threading.Thread.Sleep(700);
            foreach (var p in GetProcesses("httpd"))
            {
                try { p.Kill(); } catch { }
            }
            System.Threading.Thread.Sleep(400);
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
            System.Threading.Thread.Sleep(1800);
        }

        private void StopMysql()
        {
            RunCapture(mysqlAdminExe, "--user=root shutdown");
            System.Threading.Thread.Sleep(900);
            foreach (var p in GetProcesses("mysqld"))
            {
                try { p.Kill(); } catch { }
            }
            System.Threading.Thread.Sleep(400);
        }

        private void LoadLog()
        {
            var file = apacheErrorLog;
            if (logCombo != null)
            {
                if (logCombo.SelectedIndex == 1) file = mysqlErrorLog;
                if (logCombo.SelectedIndex == 2) file = xamppControlLog;
                if (logCombo.SelectedIndex == 3) file = apacheAccessLog;
            }
            logPath.Text = file;
            if (!File.Exists(file))
            {
                logBox.Text = "Log file not found.";
                return;
            }
            var lines = ReadAllLinesShared(file);
            logBox.Text = string.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - 250)));
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }

        private string[] ReadAllLinesShared(string file)
        {
            try
            {
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                }
            }
            catch (Exception ex)
            {
                return new[] { "Could not read log file:", ex.Message };
            }
        }

        private void RefreshPhpList()
        {
            phpList.Items.Clear();
            foreach (var install in GetPhpInstalls())
            {
                phpList.Items.Add(install);
                if (install.Active) phpList.SelectedItem = install;
            }
        }

        private IEnumerable<PhpInstall> GetPhpInstalls()
        {
            var active = GetApachePhpDir();
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

        private void SwitchPhp()
        {
            var selected = phpList.SelectedItem as PhpInstall;
            if (selected == null) return;
            if (!selected.HasApacheModule)
            {
                MessageBox.Show("PHP ini tidak punya php8apache2_4.dll.", "Tidak bisa switch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(test, "Apache config gagal, rollback", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show("Apache diarahkan ke " + selected.Name + ". Restart Apache agar aktif.", "PHP switched", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshPhpList();
            RefreshStatus();
        }

        private void OpenPath(string path)
        {
            Process.Start("explorer.exe", "\"" + path + "\"");
        }

        private void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private sealed class XamppState
        {
            public bool ApacheRunning { get; set; }
            public bool MysqlRunning { get; set; }
            public List<int> ApachePids { get; set; }
            public List<int> MysqlPids { get; set; }
            public string Port80 { get; set; }
            public string Port443 { get; set; }
            public string Port3306 { get; set; }
            public string ApacheVersion { get; set; }
            public string ApachePhpVersion { get; set; }
            public string PhpCliVersion { get; set; }
            public string MysqlVersion { get; set; }
        }

        private sealed class PhpInstall
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Version { get; set; }
            public bool Active { get; set; }
            public bool HasApacheModule { get; set; }

            public override string ToString()
            {
                return Name + " | " + Version + (Active ? " | ACTIVE" : "") + (HasApacheModule ? "" : " | CLI only");
            }
        }
    }

    internal static class ControlExtensions
    {
        public static Button WithFlatBorder(this Button button, Color color)
        {
            button.FlatAppearance.BorderColor = color;
            button.FlatAppearance.BorderSize = 1;
            return button;
        }
    }
}
