using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace NewVistas.StartInBackground;

public partial class MainWindow : Window
{
    private readonly List<Process> _launchedProcesses = new();
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        LaunchButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        LogTextBox.Clear();
        _cts = new CancellationTokenSource();

        try
        {
            string solutionDir = FindSolutionDirectory();

            if (!int.TryParse(SiloDelayTextBox.Text, out int siloDelay) || siloDelay < 0)
                siloDelay = 15;
            if (!int.TryParse(WebDelayTextBox.Text, out int webDelay) || webDelay < 0)
                webDelay = 10;

            // 1. Start SiloHost
            string siloArgs = UseSqlExpressCheckBox.IsChecked == true ? "-- --use-sqlexpress" : "";
            string siloTitle = UseSqlExpressCheckBox.IsChecked == true
                ? "SiloHost (SQL Express)"
                : "SiloHost (In-Memory)";

            SetStatus($"Starting {siloTitle}...", Brushes.DodgerBlue);
            LaunchInTerminal(solutionDir, "NewVistas.SiloHost", siloArgs, siloTitle);
            Log($"[{DateTime.Now:HH:mm:ss}] Started {siloTitle}");

            // Wait for SiloHost to initialize
            SetStatus($"Waiting {siloDelay}s for SiloHost to initialize...", Brushes.DodgerBlue);
            await DelayWithCountdown(siloDelay, "SiloHost", _cts.Token);

            if (_cts.Token.IsCancellationRequested) return;

            // 2. Start WebServer — use the "https" launch profile so it binds
            //    to https://localhost:7127 (the URL the WPF clients expect).
            //    The default "http" profile only binds to http://localhost:5298.
            SetStatus("Starting WebServer...", Brushes.DodgerBlue);
            LaunchInTerminal(solutionDir, "NewVistas.WebServer", "--launch-profile https", "WebServer");
            Log($"[{DateTime.Now:HH:mm:ss}] Started WebServer");

            // Determine selected UI
            string? uiProject = GetSelectedUIProject();

            if (uiProject != null)
            {
                // Wait for WebServer to initialize
                SetStatus($"Waiting {webDelay}s for WebServer to initialize...", Brushes.DodgerBlue);
                await DelayWithCountdown(webDelay, "WebServer", _cts.Token);

                if (_cts.Token.IsCancellationRequested) return;

                // 3. Start UI
                // CharUI is an interactive TUI and needs a terminal; all other frontends
                // (WPF, Blazor, Patient Portal) create their own window — no console needed.
                string uiName = uiProject.Replace("NewVistas.", "");
                SetStatus($"Starting {uiName}...", Brushes.DodgerBlue);
                if (uiProject == "NewVistas.CharUI")
                    LaunchInTerminal(solutionDir, uiProject, "", uiName);
                else
                    LaunchSilent(solutionDir, uiProject);
                Log($"[{DateTime.Now:HH:mm:ss}] Started {uiName}");
            }

            SetStatus("All services launched successfully.", Brushes.Green);
            Log($"[{DateTime.Now:HH:mm:ss}] Launch sequence complete. {_launchedProcesses.Count} terminal(s) opened.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Launch cancelled.", Brushes.Orange);
            Log($"[{DateTime.Now:HH:mm:ss}] Launch cancelled by user.");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", Brushes.Red);
            Log($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
        }
        finally
        {
            LaunchButton.IsEnabled = true;
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();

        int killed = 0;
        foreach (Process proc in _launchedProcesses)
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                    killed++;
                }
            }
            catch
            {
                // Process may have already exited
            }
        }
        _launchedProcesses.Clear();

        Log($"[{DateTime.Now:HH:mm:ss}] Stopped {killed} process(es).");
        SetStatus("All services stopped.", Brushes.Orange);
        StopButton.IsEnabled = false;
    }

    private void LaunchInTerminal(string workingDir, string project, string extraArgs, string tabTitle)
    {
        string dotnetArgs = $"run --project {project}";
        if (!string.IsNullOrWhiteSpace(extraArgs))
            dotnetArgs += $" {extraArgs}";

        if (!TryLaunchViaWindowsTerminal(workingDir, dotnetArgs, tabTitle))
            LaunchViaPowerShell(workingDir, dotnetArgs, tabTitle);
    }

    // Windows Terminal: groups tabs in one window; falls back gracefully if wt.exe isn't installed.
    // Strategy: open a bare WT window on the first call (blank tab 0), then always use
    // "new-tab" for the actual commands.  Combining new-window + a command in one invocation
    // has proven unreliable on machines where wt.exe is an App Execution Alias.
    private bool TryLaunchViaWindowsTerminal(string workingDir, string dotnetArgs, string tabTitle)
    {
        // Full absolute path — WT must not need to search PATH to find cmd.exe.
        string cmdExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

        // App Execution Aliases activate through a broker that ignores psi.EnvironmentVariables.
        // Patching the current process PATH before Process.Start means the broker (and therefore
        // WT and its terminal sessions) inherit the real Windows PATH from the registry.
        string savedPath = Environment.GetEnvironmentVariable("Path") ?? "";
        string cleanPath = (Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) ?? "")
                         + ";" +
                           (Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "");
        Environment.SetEnvironmentVariable("Path", cleanPath);
        try
        {
            // Step 1 (first call only): open a bare WT window with an idle cmd.exe tab.
            // Supplying an explicit shell path means WT never has to search PATH for its
            // default profile (usually pwsh.exe), which fails under the VS-modified PATH.
            if (_launchedProcesses.Count == 0)
            {
                var openPsi = new ProcessStartInfo { FileName = "wt.exe", UseShellExecute = false };
                openPsi.ArgumentList.Add("new-window");
                openPsi.ArgumentList.Add("--");
                openPsi.ArgumentList.Add(cmdExe);           // idle cmd.exe — no PATH search needed
                Process? openProc = Process.Start(openPsi);
                if (openProc == null) return false;
                _launchedProcesses.Add(openProc);
                Log($"[{DateTime.Now:HH:mm:ss}] Opened WT window — waiting 1.5 s for it to register...");
                System.Threading.Thread.Sleep(1500);        // give WT time to fully register window 0
            }

            // Step 2: add the command as a new tab in the already-open window 0.
            // "dotnet <args>" is ONE ArgumentList entry so .NET quotes it as a single string.
            // cmd.exe then receives:  /k "dotnet run --project ..."
            // and executes the full command — NOT just the first token.
            var psi = new ProcessStartInfo { FileName = "wt.exe", UseShellExecute = false };
            psi.ArgumentList.Add("-w");      psi.ArgumentList.Add("0");
            psi.ArgumentList.Add("new-tab");
            psi.ArgumentList.Add("--title"); psi.ArgumentList.Add(tabTitle);
            psi.ArgumentList.Add("-d");      psi.ArgumentList.Add(workingDir);
            psi.ArgumentList.Add("--");      psi.ArgumentList.Add(cmdExe);
            psi.ArgumentList.Add("/k");
            psi.ArgumentList.Add("dotnet " + dotnetArgs);  // single quoted arg → full "dotnet run ..." command

            Process? proc = Process.Start(psi);
            if (proc != null) { _launchedProcesses.Add(proc); return true; }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2) // ERROR_FILE_NOT_FOUND
        {
            Log($"[{DateTime.Now:HH:mm:ss}] Windows Terminal (wt.exe) not found — falling back to PowerShell.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("Path", savedPath);  // restore immediately
        }
        return false;
    }

    // GUI and web frontends: start dotnet run with no visible console window.
    // WPF apps create their own window; Blazor/web servers run silently in the background.
    private void LaunchSilent(string workingDir, string project)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project {project}",
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Process? proc = Process.Start(psi);
        if (proc != null)
            _launchedProcesses.Add(proc);
    }

    // Fallback: open a new PowerShell window with the title set and -NoExit so the output stays visible.
    private void LaunchViaPowerShell(string workingDir, string dotnetArgs, string tabTitle)
    {
        string safeTitle = tabTitle.Replace("'", "''");   // escape PS single-quote
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoExit -Command \"$host.UI.RawUI.WindowTitle = '{safeTitle}'; dotnet {dotnetArgs}\"",
            WorkingDirectory = workingDir,
            UseShellExecute = true
        };
        Process? proc = Process.Start(psi);
        if (proc != null)
            _launchedProcesses.Add(proc);
    }

    private async Task DelayWithCountdown(int totalSeconds, string waitingFor, CancellationToken ct)
    {
        for (int remaining = totalSeconds; remaining > 0; remaining--)
        {
            ct.ThrowIfCancellationRequested();
            SetStatus($"Waiting for {waitingFor}... {remaining}s remaining", Brushes.DodgerBlue);
            await Task.Delay(1000, ct);
        }
    }

    private string? GetSelectedUIProject()
    {
        if (BlazorRadio.IsChecked == true) return "NewVistas.BlazorWeb";
        if (CharUIRadio.IsChecked == true) return "NewVistas.CharUI";
        if (PatientPortalRadio.IsChecked == true) return "NewVistas.PatientPortal";
        if (WpfDelphiRadio.IsChecked == true) return "NewVistas.WpfDelphiUI";
        if (WpfUIRadio.IsChecked == true) return "NewVistas.Wpf_UI";
        return null; // "None" selected
    }

    private void SetStatus(string text, Brush color)
    {
        StatusText.Text = text;
        StatusText.Foreground = color;
    }

    private void Log(string message)
    {
        LogTextBox.AppendText(message + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }

    private static string FindSolutionDirectory()
    {
        // Walk up from the executable to find the directory containing the .sln
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        // Fallback: assume we're in the repo structure
        string fallback = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
        if (Directory.GetFiles(fallback, "*.sln").Length > 0)
            return fallback;

        throw new InvalidOperationException("Could not locate the solution directory. Make sure NewVistas.sln is in a parent directory.");
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        base.OnClosed(e);
    }
}
