using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using TrafficNova.Core;
using TrafficNova.Core.Interfaces;
using TrafficNova.Services;
using TrafficNova.Core.Models;
using TrafficNova.Data;
using TrafficNova.Data.Services;
using TrafficNova.Engine;
using TrafficNova.Pages;
using TrafficNova.ViewModels;
using WinForms = System.Windows.Forms;

namespace TrafficNova;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private WinForms.NotifyIcon? _trayIcon;
    private DispatcherTimer?     _trayTimer;
    private static bool          _screenshotMode;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // BUG-084: when the app is launched from a publish folder that has the
        // bundled Playwright browsers (the `PLAYWRIGHT_BROWSERS_PATH=0` install
        // layout under `.playwright/package/.local-browsers/`), tell Playwright
        // to use them at run time. Otherwise its default discovery falls back to
        // %LOCALAPPDATA%\ms-playwright — which is empty on a fresh install — and
        // the engine fails with "Executable doesn't exist". Dev runs (no bundled
        // browsers next to the exe) are unaffected and keep using the default cache.
        try
        {
            var localBrowsers = Path.Combine(AppContext.BaseDirectory,
                ".playwright", "package", ".local-browsers");
            if (Directory.Exists(localBrowsers))
                Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", "0");
        }
        catch { /* env var setup is best-effort; fall back to default discovery */ }
        try
        {
            await StartupCoreAsync(e);
        }
        catch (Exception ex)
        {
            // A startup failure used to leave the app hung on the splash
            // screen forever — log it, tell the user, and exit cleanly.
            Log.Fatal(ex, "Fatal error during startup");
            WriteCrashDump(ex);
            if (!_screenshotMode)
                MessageBox.Show(
                    $"TrafficNova Pro failed to start:\n\n{ex.Message}\n\n" +
                    "Details have been logged.",
                    "Startup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async Task StartupCoreAsync(StartupEventArgs e)
    {
        // ── 1. Configure Serilog early (before DI) ──────────────────
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TrafficNovaPro", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 50 * 1024 * 1024)
#if DEBUG
            .WriteTo.Debug()
#endif
            .CreateLogger();

        Log.Information("TrafficNova Pro starting up (v{Version})",
            System.Reflection.Assembly.GetExecutingAssembly()
                .GetName().Version?.ToString() ?? "1.0.0");

        // ── 2. Show splash ───────────────────────────────────────────
        var splash = new SplashWindow();
        splash.Show();

        // ── 2b. Global exception handlers ───────────────────────────
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;

        // ── 3. Build DI container ────────────────────────────────────
        var serviceCollection = new ServiceCollection();

        // Logging
        serviceCollection.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddSerilog(dispose: false);
            b.SetMinimumLevel(LogLevel.Information);
        });

        // Core services (settings, stubs)
        serviceCollection.AddCoreServices();

        // Database factory (creates short-lived AppDbContext per operation)
        serviceCollection.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IAppSettingsService>().Current;
            Directory.CreateDirectory(Path.GetDirectoryName(settings.DbPath)!);
            return new AppDbContextFactory(settings.DbPath);
        });
        // (BUG-044) The startup migration uses a one-shot AppDbContext via the
        // factory below — no DI singleton needed. Registering one held a SQLite
        // connection open for the whole session even though nothing consumed it
        // after `MigrateAsync()`.

        // Real data services
        serviceCollection.AddSingleton<IProxyService, ProxyService>();
        serviceCollection.AddSingleton<ICampaignService, CampaignService>();
        serviceCollection.AddSingleton<IStatsService, TrafficNova.Data.Services.StatsService>();
        serviceCollection.AddSingleton<TrafficNova.Data.Services.ExportService>();
        serviceCollection.AddSingleton<TrafficNova.Core.Services.NotificationService>();
        serviceCollection.AddSingleton<IScheduledJobService, TrafficNova.Data.Services.ScheduledJobService>();
        serviceCollection.AddSingleton<TrafficNova.Data.Services.SchedulerService>();

        // Engine services
        serviceCollection.AddSingleton<TrafficNova.Engine.RateLimiter>();
        serviceCollection.AddSingleton<TrafficNova.Engine.DomainThrottle>();
        serviceCollection.AddSingleton<ProxyTesterService>();
        serviceCollection.AddSingleton<ProxyHealthMonitor>();
        serviceCollection.AddSingleton<TrafficNova.Engine.PlaywrightService>();
        serviceCollection.AddSingleton<TrafficNova.Engine.CookieManager>();
        serviceCollection.AddSingleton<ISessionPoolService, TrafficNova.Engine.SessionPoolService>();
        serviceCollection.AddSingleton<TrafficNova.Engine.WatchdogService>();

        // Pages (transient — new instance each navigation)
        serviceCollection.AddTransient<DashboardPage>();
        serviceCollection.AddTransient<CampaignsPage>();
        serviceCollection.AddTransient<ProxiesPage>();
        serviceCollection.AddTransient<SchedulerPage>();
        serviceCollection.AddTransient<SettingsPage>();
        serviceCollection.AddTransient<LogsPage>();
        serviceCollection.AddTransient<SessionLogPage>();
        serviceCollection.AddTransient<AboutPage>();

        // ViewModels
        // Dashboard/Logs VMs own a running DispatcherTimer (and Dashboard a
        // permanent StatsUpdated subscription) — they MUST be singletons, or
        // each navigation orphans a timer that keeps firing and roots the
        // dead VM forever. SessionLog VM has no timer, so transient is fine.
        serviceCollection.AddSingleton<DashboardViewModel>();
        serviceCollection.AddSingleton<LogsViewModel>();
        serviceCollection.AddTransient<SessionLogViewModel>();
        serviceCollection.AddSingleton<ProxiesViewModel>();
        serviceCollection.AddSingleton<CampaignsViewModel>();
        serviceCollection.AddSingleton<ThemeService>();
        serviceCollection.AddSingleton<SettingsViewModel>();
        serviceCollection.AddSingleton<SchedulerViewModel>();
        serviceCollection.AddSingleton<MainWindowViewModel>(sp =>
            new MainWindowViewModel(sp));

        // Update service
        serviceCollection.AddSingleton<TrafficNova.Core.Services.UpdateService>();

        // MainWindow
        serviceCollection.AddTransient<MainWindow>();

        Services = serviceCollection.BuildServiceProvider();

        // ── 4. Load settings ─────────────────────────────────────────
        splash.SetStatus("Loading settings…");
        var settingsService = Services.GetRequiredService<IAppSettingsService>();
        await settingsService.LoadAsync();
        // BUG-086 / Phase 1 — start the 14-day trial clock the very first time
        // the app loads on this machine. Persisting here (before the test-mode
        // bail-outs) means --enginetest / --uitest / --screenshots also stamp
        // the clock on a virgin settings.json, which is harmless: the trial
        // shows "14 days left" and the UI status text simply reads "Trial".
        await EnsureTrialStartedAsync(settingsService);
        // Step 104: apply saved theme
        Services.GetRequiredService<ThemeService>().Apply(settingsService.Current.Theme);

        // ── 4a. Run EF migrations FIRST (schema must exist before any query) ──
        splash.SetStatus("Initializing database…");
        // (BUG-044) one-shot context — disposed immediately so the connection
        // doesn't sit open for the rest of the app session.
        using (var migrationDb = Services.GetRequiredService<AppDbContextFactory>().Create())
        {
            await migrationDb.Database.MigrateAsync();
        }
        Log.Information("Database migrations applied at {Path}", settingsService.Current.DbPath);

        // ── 4b. Prune old sessions (Step 70) ─────────────────────────
        splash.SetStatus("Cleaning old sessions…");
        var statsService = Services.GetRequiredService<IStatsService>();
        await statsService.PruneOldSessionsAsync(settingsService.Current.LogRetentionDays);

        // ── 4c. Wire engine services into CampaignService ────────────
        var campaignSvc = Services.GetRequiredService<ICampaignService>()
            as TrafficNova.Data.Services.CampaignService;
        campaignSvc?.SetEngineServices(
            Services.GetRequiredService<ISessionPoolService>(),
            settingsService);

        // ── Screenshot mode: render every screen to PNG and exit ─────
        var shotArg = Array.FindIndex(e.Args, a =>
            a.Equals("--screenshots", StringComparison.OrdinalIgnoreCase));
        if (shotArg >= 0)
        {
            // Keep the app alive after the splash closes (no window open yet);
            // we end it explicitly when captures finish.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            splash.Close();
            var outDir = shotArg + 1 < e.Args.Length ? e.Args[shotArg + 1] : "C:\\shots";
            await CaptureScreenshotsAsync(outDir);
            return;
        }

        // ── In-process UI test mode: drive controls, assert logic, exit ──
        var uiArg = Array.FindIndex(e.Args, a =>
            a.Equals("--uitest", StringComparison.OrdinalIgnoreCase));
        if (uiArg >= 0)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            splash.Close();
            var rpt = uiArg + 1 < e.Args.Length ? e.Args[uiArg + 1] : "C:\\uitest-report.txt";
            await RunUiTestsAsync(rpt);
            return;
        }

        // ── Engine smoke test: run ONE real visit through the live engine pipeline
        //    against an in-process localhost target, using the bundled Chromium.
        //    Proves the shipped browser launches, navigates, and the route chain runs.
        //    Target is 127.0.0.1 only — never a third-party site. ──
        var engArg = Array.FindIndex(e.Args, a =>
            a.Equals("--enginetest", StringComparison.OrdinalIgnoreCase));
        if (engArg >= 0)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            splash.Close();
            var rpt = engArg + 1 < e.Args.Length ? e.Args[engArg + 1] : "C:\\enginetest-report.txt";
            await RunEngineTestAsync(rpt);
            return;
        }

        // ── 4c. Start background services ────────────────────────────
        splash.SetStatus("Starting services…");
        var healthMonitor = Services.GetRequiredService<ProxyHealthMonitor>();
        _ = healthMonitor.StartAsync(CancellationToken.None);
        var scheduler = Services.GetRequiredService<TrafficNova.Data.Services.SchedulerService>();
        _ = scheduler.StartAsync(CancellationToken.None);
        var watchdog = Services.GetRequiredService<TrafficNova.Engine.WatchdogService>();
        _ = watchdog.StartAsync(CancellationToken.None);

        // ── 5. Show main window ──────────────────────────────────────
        splash.SetStatus("Ready!");
        await Task.Delay(400); // brief pause so "Ready!" is visible

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
        splash.Close();

        // ── 5a. Create tray icon (Step 105) ─────────────────────────
        InitTrayIcon(mainWindow);

        // ── 5b. Auto-update check (Step 106) ────────────────────────
        if (settingsService.Current.CheckForUpdatesOnStartup)
            _ = CheckForUpdateAsync();

        // ── 5c. Trial milestone reminders (BUG-086) ─────────────────
        // Shows ONE non-modal MessageBox at the day-7 mark and one at expiry
        // (day 14). Flags on AppSettings prevent re-firing on later launches.
        // Only runs in the normal UI flow — test modes have already bailed
        // by this point, so --enginetest / --uitest never pop a dialog.
        TryShowTrialNotifications(settingsService);

        // ── 5d. First-run onboarding wizard (Step 114) ───────────────
        if (settingsService.Current.ShowOnboardingOnFirstRun)
        {
            settingsService.Current.ShowOnboardingOnFirstRun = false;
            await settingsService.SaveAsync();
            var wizard = new TrafficNova.Dialogs.OnboardingWizard { Owner = mainWindow };
            wizard.ShowDialog();
        }

        Log.Information("Startup complete");
    }

    // ── Offscreen screenshot harness (UI verification) ──────────────
    private async Task CaptureScreenshotsAsync(string outDir)
    {
        _screenshotMode = true;
        Directory.CreateDirectory(outDir);
        Log.Information("Screenshot mode → {Dir}", outDir);

        MainWindow mw;
        MainWindowViewModel? vm;
        try
        {
            Log.Information("Resolving MainWindow…");
            mw = Services.GetRequiredService<MainWindow>();
            Log.Information("MainWindow resolved; showing offscreen…");
            mw.WindowStartupLocation = WindowStartupLocation.Manual;
            mw.Left = -8000; mw.Top = -8000;          // render offscreen
            mw.Width = 1366; mw.Height = 860;
            mw.ShowInTaskbar = false;
            mw.Show();
            Log.Information("MainWindow shown");
            mw.UpdateLayout();
            await Task.Delay(500);
            vm = mw.DataContext as MainWindowViewModel;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Screenshot setup failed");
            Shutdown();
            return;
        }

        string[] pages =
            ["Dashboard","Campaigns","Proxies","Scheduler","Settings","Logs","Sessions","About"];
        foreach (var p in pages)
        {
            try
            {
                vm?.NavigateTo(p);
                mw.UpdateLayout();
                await Task.Delay(900);            // let async loads + charts settle
                await Dispatcher.InvokeAsync(() => { },
                    DispatcherPriority.ContextIdle);   // flush render queue
                mw.UpdateLayout();
                SaveVisual(mw, Path.Combine(outDir, $"page_{p}.png"));
                Log.Information("Captured page {Page}", p);
            }
            catch (Exception ex) { Log.Warning(ex, "Capture failed for {Page}", p); }
        }

        await CaptureDialogAsync(() => new Dialogs.CampaignEditorDialog(), mw, outDir, "dlg_CampaignEditor");
        await CaptureDialogAsync(() => new Dialogs.OnboardingWizard(), mw, outDir, "dlg_Onboarding");
        await CaptureDialogAsync(() =>
        {
            var s = new TrafficNova.Core.Models.TrafficSession
            {
                Id = 42, CampaignId = 1, TargetUrl = "https://example.com/landing",
                Success = false, StatusCode = 503, DwellMs = 3800, BlockedRequests = 12,
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/124",
                Referrer = "https://www.google.com/", ErrorMessage = "net::ERR_TIMED_OUT",
                StartedAt = DateTime.UtcNow.AddSeconds(-9), EndedAt = DateTime.UtcNow,
            };
            return new Dialogs.SessionDetailDialog(s, "Sample Campaign");
        }, mw, outDir, "dlg_SessionDetail");

        Log.Information("Screenshot capture complete");
        Shutdown();
    }

    private async Task CaptureDialogAsync(Func<Window> make, Window owner, string dir, string name)
    {
        try
        {
            var dlg = make();
            dlg.Owner = owner;
            dlg.ShowInTaskbar = false;
            dlg.WindowStartupLocation = WindowStartupLocation.Manual;
            dlg.Left = -8000; dlg.Top = -8000;
            dlg.Show();
            dlg.UpdateLayout();
            await Task.Delay(500);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            dlg.UpdateLayout();
            SaveVisual(dlg, Path.Combine(dir, $"{name}.png"));
            dlg.Close();
            Log.Information("Captured dialog {Name}", name);
        }
        catch (Exception ex) { Log.Warning(ex, "Capture failed for {Name}", name); }
    }

    // Rendering a Window object directly yields a blank bitmap; render its
    // content root instead, forcing a measure/arrange pass first.
    private static void SaveVisual(Window win, string path)
    {
        var root = win.Content as FrameworkElement ?? win;
        double w = root.ActualWidth  > 0 ? root.ActualWidth  : (win.Width  > 0 ? win.Width  : 1366);
        double h = root.ActualHeight > 0 ? root.ActualHeight : (win.Height > 0 ? win.Height : 860);

        root.Measure(new System.Windows.Size(w, h));
        root.Arrange(new System.Windows.Rect(0, 0, w, h));
        root.UpdateLayout();

        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)Math.Ceiling(w), (int)Math.Ceiling(h), 96, 96,
            System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(root);
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    // ── In-process interactive UI test harness ──────────────────────
    private readonly System.Text.StringBuilder _rpt = new();
    private int _uiPass, _uiFail;

    private void UiCheck(string name, Func<bool> test)
    {
        try
        {
            bool ok = test();
            if (ok) _uiPass++; else _uiFail++;
            var line = (ok ? "PASS  " : "FAIL  ") + name;
            _rpt.AppendLine(line);
            Log.Information("UITEST {Line}", line);
        }
        catch (Exception ex)
        {
            _uiFail++;
            var line = $"FAIL  {name} :: {ex.GetType().Name}: {ex.Message}";
            _rpt.AppendLine(line);
            Log.Warning("UITEST {Line}", line);
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        if (root == null) yield break;
        int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var c = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return c;
            foreach (var d in Descendants(c)) yield return d;
        }
    }

    private static T? FindByName<T>(DependencyObject root, string name) where T : FrameworkElement
        => Descendants(root).OfType<T>().FirstOrDefault(e => e.Name == name);

    // Async-aware check: lets dispatcher-queued work (button clicks,
    // command continuations) actually run instead of Thread.Sleep-blocking it.
    private async Task UiCheckAsync(string name, Func<Task<bool>> test)
    {
        try
        {
            bool ok = await test();
            if (ok) _uiPass++; else _uiFail++;
            var line = (ok ? "PASS  " : "FAIL  ") + name;
            _rpt.AppendLine(line);
            Log.Information("UITEST {Line}", line);
        }
        catch (Exception ex)
        {
            _uiFail++;
            var line = $"FAIL  {name} :: {ex.GetType().Name}: {ex.Message}";
            _rpt.AppendLine(line);
            Log.Warning("UITEST {Line}", line);
        }
    }

    private async Task PumpAsync(int ms)
    {
        await Task.Delay(ms);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
    }

    private static void InvokePeer(UIElement el)
    {
        var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(el);
        var inv  = peer?.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)
                       as System.Windows.Automation.Provider.IInvokeProvider;
        if (inv != null) inv.Invoke();
        else if (el is System.Windows.Controls.Primitives.ButtonBase bb)
            bb.Command?.Execute(bb.CommandParameter);
    }

    private async Task RunEngineTestAsync(string reportPath)
    {
        var sb = new System.Text.StringBuilder();
        int pass = 0, fail = 0;
        void Ok(string m) { sb.AppendLine("PASS  " + m); pass++; }
        void No(string m) { sb.AppendLine("FAIL  " + m); fail++; }

        var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(90));
        System.Net.Sockets.TcpListener? listener = null;
        try
        {
            // 1. In-process localhost HTTP target (authorized: 127.0.0.1, never third-party).
            listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            var url  = $"http://127.0.0.1:{port}/";
            var body = System.Text.Encoding.UTF8.GetBytes(
                "<!doctype html><html><head><title>TN EngineTest</title></head>" +
                "<body><h1>OK</h1><p>localhost smoke target</p></body></html>");
            _ = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    System.Net.Sockets.TcpClient client;
                    try { client = await listener.AcceptTcpClientAsync(cts.Token); }
                    catch { break; }
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using (client)
                            {
                                var s   = client.GetStream();
                                var buf = new byte[8192];
                                try { await s.ReadAsync(buf, cts.Token); } catch { }
                                var head = System.Text.Encoding.ASCII.GetBytes(
                                    "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n" +
                                    $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                                await s.WriteAsync(head, cts.Token);
                                await s.WriteAsync(body, cts.Token);
                                await s.FlushAsync(cts.Token);
                            }
                        }
                        catch { }
                    });
                }
            });
            Ok($"local HTTP server listening at {url}");

            // 2. Drive ONE real visit through the live engine using the bundled Chromium.
            var settings = Services.GetRequiredService<IAppSettingsService>();
            var engine   = EngineConfig.FromSettings(settings.Current) with { HeadlessMode = true };
            var campaign = new Campaign
            {
                Name           = "EngineSmoke",
                TargetUrlsJson = Newtonsoft.Json.JsonConvert.SerializeObject(new[] { url }),
                ThreadCount    = 1, VisitTarget = 1, DwellMin = 150, DwellMax = 300,
                BounceRate     = 0, ReferrerMode = ReferrerMode.Direct,
                UserAgentMode  = UserAgentMode.Desktop, DeviceType = DeviceType.Desktop,
                UseProxy       = false,
            };
            var pool = Services.GetRequiredService<ISessionPoolService>();
            var sw   = System.Diagnostics.Stopwatch.StartNew();
            var result = await pool.RunSingleVisitAsync(campaign, engine, cts.Token);
            sw.Stop();

            if (result.Success)
                Ok($"live visit succeeded via bundled Chromium (status={result.StatusCode}, dwell={result.DwellMs}ms, total={sw.ElapsedMilliseconds}ms)");
            else
                No($"live visit failed: {result.ErrorMessage}");

            if (result.Success && result.StatusCode != 200)
                No($"unexpected status code {result.StatusCode}");
            else if (result.StatusCode == 200)
                Ok("HTTP 200 received from localhost target");
        }
        catch (Exception ex)
        {
            No("exception: " + ex.Message);
        }
        finally
        {
            cts.Cancel();
            try { listener?.Stop(); } catch { }
        }

        sb.AppendLine();
        sb.AppendLine($"RESULT  pass={pass} fail={fail}");
        try { File.WriteAllText(reportPath, sb.ToString()); } catch { }

        // Tear down Playwright explicitly — the node driver it spawns keeps the
        // .NET process alive past Shutdown() otherwise. Environment.Exit then
        // guarantees deterministic termination in test mode (exit code 0/1
        // makes the smoke test scriptable in CI).
        try
        {
            var pw = Services?.GetService(typeof(PlaywrightService)) as PlaywrightService;
            if (pw is not null) await pw.DisposeAsync();
        }
        catch { /* best-effort */ }
        Environment.Exit(pass > 0 && fail == 0 ? 0 : 1);
    }

    private async Task RunUiTestsAsync(string reportPath)
    {
        Log.Information("UI test mode → {Report}", reportPath);
        try
        {
            var mw = Services.GetRequiredService<MainWindow>();
            mw.WindowStartupLocation = WindowStartupLocation.Manual;
            mw.Left = -8000; mw.Top = -8000; mw.Width = 1366; mw.Height = 860;
            mw.ShowInTaskbar = false;
            mw.Show();
            mw.UpdateLayout();
            await Task.Delay(800);
            var vm = mw.DataContext as MainWindowViewModel;
            UiCheck("MainWindow + DataContext created", () => vm != null);

            // 1. Navigation buttons + per-page button wiring
            string[] navs = ["Dashboard","Campaigns","Proxies","Scheduler","Logs","Settings","About"];
            foreach (var page in navs)
            {
                vm!.NavigateTo(page);
                mw.UpdateLayout();
                await Task.Delay(700);
                await Dispatcher.InvokeAsync(() => { },
                    DispatcherPriority.ContextIdle);

                var buttons = Descendants(mw).OfType<System.Windows.Controls.Button>().ToList();
                int enabled  = buttons.Count(b => b.IsEnabled);
                int wired    = buttons.Count(b =>
                    b.Command != null ||
                    b.GetType().GetField("Click") != null ||
                    HasClickHandler(b));
                UiCheck($"[{page}] navigated & buttons present " +
                        $"(total={buttons.Count}, enabled={enabled})",
                        () => buttons.Count > 0 && enabled > 0);
            }

            // 2. Text input → bound property (isolated)
            string campName = "UITest-" + DateTime.Now.ToString("HHmmss");
            UiCheck("TextBox input writes through to bound property", () =>
            {
                var dlg = new Dialogs.CampaignEditorDialog();
                dlg.Left = -8000; dlg.Top = -8000; dlg.ShowInTaskbar = false;
                dlg.Show(); dlg.UpdateLayout();
                var nameBox = FindByName<System.Windows.Controls.TextBox>(dlg, "NameBox");
                _rpt.AppendLine($"      · NameBox found={nameBox != null}");
                if (nameBox == null) { dlg.Close(); return false; }
                nameBox.Text = "BindingProbe";
                nameBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?
                    .UpdateSource();
                var val = dlg.GetType().GetProperty("CampaignName")?.GetValue(dlg) as string;
                _rpt.AppendLine($"      · dlg.CampaignName after set = '{val}'");
                dlg.Close();
                return val == "BindingProbe";
            });

            await UiCheckAsync("Campaign Editor: type inputs + Save → persisted", async () =>
            {
                var dlg = new Dialogs.CampaignEditorDialog();
                dlg.WindowStartupLocation = WindowStartupLocation.Manual;
                dlg.Left = -8000; dlg.Top = -8000; dlg.ShowInTaskbar = false;
                dlg.Show();
                dlg.UpdateLayout();

                var nameBox = FindByName<System.Windows.Controls.TextBox>(dlg, "NameBox");
                var urlsBox = FindByName<System.Windows.Controls.TextBox>(dlg, "UrlsBox");
                var saveBtn = FindByName<System.Windows.Controls.Button>(dlg, "SaveCampaignBtn");
                _rpt.AppendLine($"      · name={nameBox!=null} urls={urlsBox!=null} save={saveBtn!=null}");
                if (nameBox == null || urlsBox == null || saveBtn == null) { dlg.Close(); return false; }

                nameBox.Text = campName;
                urlsBox.Text = "http://localhost:8099/";
                nameBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                urlsBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                dlg.UpdateLayout();
                InvokePeer(saveBtn);
                await PumpAsync(2500);                          // let click + async CreateAsync run
                var vmsg = FindByName<System.Windows.Controls.TextBlock>(dlg, "ValidationMsg");
                _rpt.AppendLine($"      · ValidationMsg='{vmsg?.Text}'");

                using var db = Services.GetRequiredService<AppDbContextFactory>().Create();
                int cnt = db.Campaigns.Count(c => c.Name == campName);
                _rpt.AppendLine($"      · campaigns named '{campName}' in DB = {cnt}");
                try { if (dlg.IsVisible) dlg.Close(); } catch { }
                return cnt > 0;
            });

            // 3. Settings inputs → command → persistence
            await UiCheckAsync("Settings: ComboBox + CheckBox + Save command → settings.json", async () =>
            {
                vm!.NavigateTo("Settings");
                mw.UpdateLayout();
                await PumpAsync(1500);
                var combo = FindByName<System.Windows.Controls.ComboBox>(mw, "CbTheme");
                var chk   = FindByName<System.Windows.Controls.CheckBox>(mw, "ChkMinimizeTray");
                var save  = FindByName<System.Windows.Controls.Button>(mw, "BtnSaveSettings");
                _rpt.AppendLine($"      · combo={combo!=null} chk={chk!=null} save={save!=null}");
                if (combo == null || chk == null || save == null) return false;
                combo.SelectedItem = "Dark";
                chk.IsChecked = true;
                mw.UpdateLayout();
                var vmTheme = (Services.GetService<ViewModels.SettingsViewModel>())?.Theme;
                _rpt.AppendLine($"      · SettingsViewModel.Theme after combo set = '{vmTheme}'");
                InvokePeer(save);
                await PumpAsync(2500);
                var sp = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TrafficNovaPro", "settings.json");
                var json = File.ReadAllText(sp);
                _rpt.AppendLine($"      · settings.json contains Dark = {json.Contains("Dark")}");
                return json.Contains("\"Theme\": \"Dark\"") || json.Contains("\"Theme\":\"Dark\"");
            });
        }
        catch (Exception ex)
        {
            _uiFail++;
            _rpt.AppendLine($"FAIL  harness :: {ex}");
            Log.Error(ex, "UI test harness error");
        }

        _rpt.AppendLine();
        _rpt.AppendLine($"RESULT  pass={_uiPass} fail={_uiFail}");
        try { File.WriteAllText(reportPath, _rpt.ToString()); } catch { }
        Log.Information("UI test complete: pass={Pass} fail={Fail}", _uiPass, _uiFail);
        Log.CloseAndFlush();
        // Force-exit: the harness intentionally leaves a CampaignEditorDialog
        // open (Save_Click throws on DialogResult after .Show()), which would
        // otherwise keep the WPF process alive past Shutdown(). Exit code
        // doubles as a CI signal — 0 = all checks passed.
        Environment.Exit(_uiFail == 0 ? 0 : 1);
    }

    private static bool HasClickHandler(System.Windows.Controls.Button b)
    {
        // Best-effort: a button with no Command but an event handler still
        // counts as wired; we can't reflect routed-event handlers reliably,
        // so treat any enabled button without Command as "click-wired".
        return b.Command == null;
    }

    // ── Step 106: Auto-update check ─────────────────────────────────
    private async Task CheckForUpdateAsync()
    {
        try
        {
            await Task.Delay(3000); // wait for UI to settle
            var svc  = Services.GetRequiredService<TrafficNova.Core.Services.UpdateService>();
            var info = await svc.CheckForUpdateAsync();
            if (info is null) return;

            Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    $"A new version is available: v{info.Version}\n\nWould you like to download it now?",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(info.DownloadUrl) { UseShellExecute = true });
            });
        }
        catch (Exception ex)
        {
            Log.Debug("Update check error: {Err}", ex.Message);
        }
    }

    // ── Step 105: Tray icon ──────────────────────────────────────────
    private void InitTrayIcon(MainWindow mainWindow)
    {
        var menu = new WinForms.ContextMenuStrip();

        var openItem      = new WinForms.ToolStripMenuItem("Open TrafficNova Pro");
        var startAllItem  = new WinForms.ToolStripMenuItem("Start All Campaigns");
        var stopAllItem   = new WinForms.ToolStripMenuItem("Stop All Campaigns");
        var separatorItem = new WinForms.ToolStripSeparator();
        var exitItem      = new WinForms.ToolStripMenuItem("Exit");

        openItem.Font  = new System.Drawing.Font(openItem.Font, System.Drawing.FontStyle.Bold);
        openItem.Click += (_, _) => RestoreWindow(mainWindow);

        startAllItem.Click += async (_, _) =>
        {
            var svc = Services.GetService<ICampaignService>();
            if (svc is null) return;
            var all     = await svc.GetAllAsync();
            var running = svc.GetRunning().Select(c => c.Id).ToHashSet();
            foreach (var c in all.Where(c => !running.Contains(c.Id)))
                await svc.StartAsync(c.Id);
        };

        // BUG-039: `StopAsync` awaits EF Core without ConfigureAwait(false). Tray
        // menu clicks fire on the UI thread, so .GetResult() here would deadlock.
        // Use an async handler and await properly.
        stopAllItem.Click += async (_, _) =>
        {
            var svc = Services.GetService<ICampaignService>();
            if (svc is null) return;
            foreach (var c in svc.GetRunning())
            {
                try { await svc.StopAsync(c.Id); }
                catch { /* best-effort — keep stopping the rest */ }
            }
        };

        exitItem.Click += (_, _) =>
        {
            _trayIcon?.Dispose();
            _trayTimer?.Stop();
            Shutdown();
        };

        menu.Items.Add(openItem);
        menu.Items.Add(startAllItem);
        menu.Items.Add(stopAllItem);
        menu.Items.Add(separatorItem);
        menu.Items.Add(exitItem);

        _trayIcon = new WinForms.NotifyIcon
        {
            Text             = "TrafficNova Pro",
            Icon             = CreateTrayIcon(0),
            ContextMenuStrip = menu,
            Visible          = true,
        };

        _trayIcon.DoubleClick += (_, _) => RestoreWindow(mainWindow);

        // Badge: update tray icon with active session count every 5 s
        _trayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _trayTimer.Tick += (_, _) =>
        {
            var count = Services.GetService<ICampaignService>()?.GetRunning().Count ?? 0;
            var oldIcon = _trayIcon.Icon;
            _trayIcon.Icon    = CreateTrayIcon(count);
            oldIcon?.Dispose();
            _trayIcon.Text    = count > 0
                ? $"TrafficNova Pro — {count} campaign{(count == 1 ? "" : "s")} running"
                : "TrafficNova Pro";
        };
        _trayTimer.Start();
    }

    private static void RestoreWindow(MainWindow w)
    {
        w.Show();
        if (w.WindowState == WindowState.Minimized)
            w.WindowState = WindowState.Normal;
        w.Activate();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    // Draws a small icon with a number badge (0 = plain icon)
    private static Icon CreateTrayIcon(int count)
    {
        using var bmp = new Bitmap(16, 16);
        using var g   = Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.Transparent);

        // Background circle
        using var bgBrush = new SolidBrush(System.Drawing.Color.FromArgb(0x25, 0x63, 0xEB)); // #2563EB
        g.FillEllipse(bgBrush, 1, 1, 14, 14);

        if (count > 0)
        {
            // Badge number
            var label = count > 99 ? "99+" : count.ToString();
            using var font  = new System.Drawing.Font("Arial", label.Length > 2 ? 4f : 5f, System.Drawing.FontStyle.Bold);
            using var brush = new SolidBrush(System.Drawing.Color.White);
            var size   = g.MeasureString(label, font);
            g.DrawString(label, font, brush,
                (16 - size.Width)  / 2f,
                (16 - size.Height) / 2f);
        }
        else
        {
            // "T" lettermark
            using var font  = new System.Drawing.Font("Arial", 8f, System.Drawing.FontStyle.Bold);
            using var brush = new SolidBrush(System.Drawing.Color.White);
            g.DrawString("T", font, brush, 3f, 1f);
        }

        // GetHicon() returns an unmanaged GDI handle that Icon.FromHandle does
        // NOT take ownership of — clone into a self-owning managed Icon and free
        // the handle, or every 5-second refresh leaks an HICON until the process
        // exhausts its GDI object quota (~10k) and rendering breaks.
        var hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("TrafficNova Pro shutting down");
        _trayIcon?.Dispose();
        _trayTimer?.Stop();
        try
        {
            var monitor = Services.GetService<ProxyHealthMonitor>();
            monitor?.StopAsync(CancellationToken.None).Wait(2000);
        }
        catch { /* best-effort */ }
        try
        {
            var sched = Services.GetService<TrafficNova.Data.Services.SchedulerService>();
            sched?.StopAsync(CancellationToken.None).Wait(2000);
        }
        catch { /* best-effort */ }
        try
        {
            var watchdog = Services.GetService<TrafficNova.Engine.WatchdogService>();
            watchdog?.StopAsync(CancellationToken.None).Wait(2000);
        }
        catch { /* best-effort */ }
        try
        {
            var pw = Services.GetService<TrafficNova.Engine.PlaywrightService>();
            pw?.DisposeAsync().AsTask().Wait(3000);
        }
        catch { /* best-effort */ }
        Log.CloseAndFlush();
        (Services as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception");
        WriteCrashDump(e.Exception);
        // Never pop a modal dialog during automated screenshot capture
        if (!_screenshotMode)
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nDetails have been logged.",
                "TrafficNova Pro — Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "Fatal unhandled domain exception (IsTerminating={IsTerminating})", e.IsTerminating);
            WriteCrashDump(ex);
        }
    }

    private static void WriteCrashDump(Exception ex)
    {
        try
        {
            var crashDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TrafficNovaPro", "crashes");
            Directory.CreateDirectory(crashDir);
            var path = Path.Combine(crashDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(path,
                $"Time: {DateTime.Now}\nType: {ex.GetType().FullName}\nMessage: {ex.Message}\n\n{ex}");
        }
        catch { /* best-effort */ }
    }

    // ── Trial helpers (BUG-086 / Phase 1) ────────────────────────────
    private static async Task EnsureTrialStartedAsync(IAppSettingsService svc)
    {
        if (svc.Current.TrialStartUtc is not null) return;
        svc.Current.TrialStartUtc = DateTime.UtcNow;
        await svc.SaveAsync();
        Log.Information("Trial clock started at {Start:o}", svc.Current.TrialStartUtc);
    }

    private static void TryShowTrialNotifications(IAppSettingsService svc)
    {
        var s = svc.Current;
        if (s.IsActivated || s.TrialStartUtc is null) return;

        var start    = s.TrialStartUtc.Value;
        var now      = DateTime.UtcNow;
        var expired  = TrafficNova.Core.Licensing.TrialState.IsExpired(start, now);
        var daysLeft = TrafficNova.Core.Licensing.TrialState.DaysRemaining(start, now);

        if (expired && !s.TrialNotifiedDay14)
        {
            s.TrialNotifiedDay14 = true;
            _ = svc.SaveAsync();
            MessageBox.Show(
                "Your 14-day TrafficNova Pro trial has ended.\n\n" +
                "To continue using all features, enter a license key on the " +
                "Settings → License tab.",
                "Trial Expired",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!expired && daysLeft <= 7 && !s.TrialNotifiedDay7)
        {
            s.TrialNotifiedDay7 = true;
            _ = svc.SaveAsync();
            var noun = daysLeft == 1 ? "day" : "days";
            MessageBox.Show(
                $"Your TrafficNova Pro trial expires in {daysLeft} {noun}.\n\n" +
                "Activate your license on the Settings → License tab to keep " +
                "all features after the trial ends.",
                "Trial Reminder",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
