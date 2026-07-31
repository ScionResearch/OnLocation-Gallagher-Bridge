using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text.Json;
using System.Windows.Forms;

namespace OnLocationGallagherBridge.Tray;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "OnLocationGallagherBridge.Tray", out bool created);
        if (!created)
        {
            MessageBox.Show(
                "The OnLocation-Gallagher Bridge status widget is already running.",
                "Already running",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApplicationContext());
    }
}

internal class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly string _statusPath;
    private readonly string _baseIconPath;
    private const string ServiceName = "OnLocationGallagherBridge";

    private ServiceStatus? _lastStatus;
    private ToolStripMenuItem _statusMenuItem = null!;
    private ToolStripMenuItem _serviceStatusMenuItem = null!;
    private ToolStripMenuItem _startServiceMenuItem = null!;
    private ToolStripMenuItem _stopServiceMenuItem = null!;
    private ToolStripMenuItem _restartServiceMenuItem = null!;
    private ToolStripMenuItem _enableAutoStartMenuItem = null!;
    private ToolStripMenuItem _disableAutoStartMenuItem = null!;
    private ToolStripMenuItem _openMenuItem = null!;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public TrayApplicationContext()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        _statusPath = Path.Combine(programData, "OnLocation-Gallagher-Bridge", "service-status.json");
        _baseIconPath = Path.Combine(AppContext.BaseDirectory, "favicon.ico");

        _notifyIcon = new NotifyIcon
        {
            Text = "OnLocation-Gallagher Bridge",
            Visible = true,
            Icon = CreateStatusIcon(StatusColor.Gray)
        };

        BuildMenu();
        _notifyIcon.DoubleClick += (s, e) => OpenWebUi();

        _timer = new System.Windows.Forms.Timer { Interval = 5000 };
        _timer.Tick += (s, e) => Refresh();
        _timer.Start();

        Refresh();
    }

    private void BuildMenu()
    {
        _statusMenuItem = new ToolStripMenuItem("Status: checking...") { Enabled = false };
        _serviceStatusMenuItem = new ToolStripMenuItem("Service: checking...") { Enabled = false };
        _openMenuItem = new ToolStripMenuItem("Open Web UI", null, (s, e) => OpenWebUi());
        var refreshMenuItem = new ToolStripMenuItem("Refresh", null, (s, e) => Refresh());
        var exitMenuItem = new ToolStripMenuItem("Exit", null, (s, e) => Exit());

        _startServiceMenuItem = new ToolStripMenuItem("Start Service", null, (s, e) => RunElevated($"sc start {ServiceName}"));
        _stopServiceMenuItem = new ToolStripMenuItem("Stop Service", null, (s, e) => RunElevated($"sc stop {ServiceName}"));
        _restartServiceMenuItem = new ToolStripMenuItem("Restart Service", null, (s, e) => RunElevated($"cmd /c sc stop {ServiceName} && timeout /t 2 /nobreak >nul && sc start {ServiceName}"));
        _enableAutoStartMenuItem = new ToolStripMenuItem("Enable Auto Start", null, (s, e) => RunElevated($"sc config {ServiceName} start= auto"));
        _disableAutoStartMenuItem = new ToolStripMenuItem("Disable Auto Start", null, (s, e) => RunElevated($"sc config {ServiceName} start= disabled"));

        var serviceMenu = new ToolStripMenuItem("Service");
        serviceMenu.DropDownItems.Add(_startServiceMenuItem);
        serviceMenu.DropDownItems.Add(_stopServiceMenuItem);
        serviceMenu.DropDownItems.Add(_restartServiceMenuItem);
        serviceMenu.DropDownItems.Add(new ToolStripSeparator());
        serviceMenu.DropDownItems.Add(_enableAutoStartMenuItem);
        serviceMenu.DropDownItems.Add(_disableAutoStartMenuItem);

        _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
        _notifyIcon.ContextMenuStrip.Items.Add(_statusMenuItem);
        _notifyIcon.ContextMenuStrip.Items.Add(_serviceStatusMenuItem);
        _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _notifyIcon.ContextMenuStrip.Items.Add(_openMenuItem);
        _notifyIcon.ContextMenuStrip.Items.Add(serviceMenu);
        _notifyIcon.ContextMenuStrip.Items.Add(refreshMenuItem);
        _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _notifyIcon.ContextMenuStrip.Items.Add(exitMenuItem);
    }

    private void Refresh()
    {
        try
        {
            _lastStatus = File.Exists(_statusPath)
                ? JsonSerializer.Deserialize<ServiceStatus>(File.ReadAllText(_statusPath))
                : null;
        }
        catch
        {
            _lastStatus = null;
        }

        UpdateIconAndMenu();
        RefreshServiceState();
    }

    private void UpdateIconAndMenu()
    {
        var status = _lastStatus;
        if (status is null || (DateTimeOffset.UtcNow - status.Timestamp) > TimeSpan.FromSeconds(30))
        {
            SetIcon(StatusColor.Gray);
            _statusMenuItem.Text = "Status: unknown (service not running?)";
            _openMenuItem.Enabled = false;
            return;
        }

        var color = status.Status switch
        {
            "Running" => status.OverallStatus == "Complete" &&
                         status.OnLocationConnected is true &&
                         status.GallagherConnected is true
                ? StatusColor.Green
                : StatusColor.Yellow,
            _ => StatusColor.Red
        };

        SetIcon(color);
        _openMenuItem.Enabled = true;
        _statusMenuItem.Text = $"Status: {status.Status} ({status.OverallStatus})";
        _notifyIcon.Text = $"OnLocation-Gallagher Bridge{Environment.NewLine}" +
                           $"Status: {status.Status}{Environment.NewLine}" +
                           $"Overall: {status.OverallStatus}{Environment.NewLine}" +
                           $"OnLocation: {FormatBool(status.OnLocationConnected)}{Environment.NewLine}" +
                           $"Gallagher: {FormatBool(status.GallagherConnected)}";
    }

    private void SetIcon(StatusColor color)
    {
        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = CreateStatusIcon(color);
        oldIcon?.Dispose();
    }

    private void RefreshServiceState()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            sc.Refresh();
            var status = sc.Status;
            var startType = sc.StartType;
            _serviceStatusMenuItem.Text = $"Service: {status} ({startType})";

            _startServiceMenuItem.Enabled = status == ServiceControllerStatus.Stopped;
            _stopServiceMenuItem.Enabled = status == ServiceControllerStatus.Running;
            _restartServiceMenuItem.Enabled = status == ServiceControllerStatus.Running;
            _enableAutoStartMenuItem.Enabled = startType != ServiceStartMode.Automatic;
            _disableAutoStartMenuItem.Enabled = startType != ServiceStartMode.Disabled;
        }
        catch
        {
            _serviceStatusMenuItem.Text = "Service: not installed";
            _startServiceMenuItem.Enabled = false;
            _stopServiceMenuItem.Enabled = false;
            _restartServiceMenuItem.Enabled = false;
            _enableAutoStartMenuItem.Enabled = false;
            _disableAutoStartMenuItem.Enabled = false;
        }
    }

    private static void RunElevated(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("cmd.exe", "/c " + arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to run command: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenWebUi()
    {
        var url = _lastStatus?.Url;
        if (string.IsNullOrWhiteSpace(url)) return;
        var browserUrl = MakeBrowserUrl(url);
        Process.Start(new ProcessStartInfo(browserUrl) { UseShellExecute = true });
    }

    private static string MakeBrowserUrl(string url)
    {
        var u = url.Trim();
        if (u.Contains("://*:")) u = u.Replace("://*:", "://localhost:");
        if (u.Contains("://0.0.0.0:")) u = u.Replace("://0.0.0.0:", "://localhost:");
        if (u.Contains("://+:")) u = u.Replace("://+:", "://localhost:");
        if (!u.Contains("://")) u = "http://" + u;
        return u;
    }

    private static string FormatBool(bool? value) => value switch
    {
        true => "OK",
        false => "Fail",
        _ => "unknown"
    };

    private void Exit()
    {
        _notifyIcon.Visible = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _notifyIcon.Dispose();
        }
        base.Dispose(disposing);
    }

    private Icon CreateStatusIcon(StatusColor color)
    {
        using var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (File.Exists(_baseIconPath))
            {
                try
                {
                    using var baseIcon = new Icon(_baseIconPath, 16, 16);
                    g.DrawIcon(baseIcon, new Rectangle(0, 0, 16, 16));
                }
                catch { }
            }

            var brush = color switch
            {
                StatusColor.Green => Brushes.LimeGreen,
                StatusColor.Yellow => Brushes.Orange,
                StatusColor.Red => Brushes.Red,
                _ => Brushes.Gray
            };
            g.FillEllipse(brush, 8, 8, 8, 8);
            g.DrawEllipse(Pens.Black, 8, 8, 7, 7);
        }

        var hIcon = bmp.GetHicon();
        using var temp = Icon.FromHandle(hIcon);
        var icon = new Icon(temp, 16, 16);
        DestroyIcon(hIcon);
        return icon;
    }
}

public record ServiceStatus(
    string Status,
    string Url,
    string OverallStatus,
    bool? OnLocationConnected,
    bool? GallagherConnected,
    DateTimeOffset Timestamp);

public enum StatusColor
{
    Gray,
    Green,
    Yellow,
    Red
}
