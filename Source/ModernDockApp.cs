using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace MyCustomDock
{
    public class DockItem
    {
        public string Title { get; set; }
        public string TargetPath { get; set; }
        public string Arguments { get; set; }
        public string PathMatch { get; set; }
        public string ProcessNameMatch { get; set; }
        public string WindowClassMatch { get; set; }
        public string IconFile { get; set; }
        // Optional metadata for dropped shortcuts. It is deliberately kept
        // outside the seven-column config format and persisted by the small
        // dock_metadata.json sidecar.
        public string ShortcutSource { get; set; }
        public bool AutoDerivedPathMatch { get; set; }
        public bool AutoDerivedProcessNameMatch { get; set; }
        public ImageSource IconSource { get; set; }
        public Action CustomAction { get; set; }
        public Ellipse IndicatorDot { get; set; }
        public bool IsFixed { get; set; }
        public IntPtr DynamicHwnd { get; set; }
        public uint DynamicPid { get; set; }
        public string DynamicIdentityKey { get; set; }
    }

    public static class EntryPoint
    {
        private static string LogPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dock_fatal.log");

        public static void Log(string msg)
        {
            try { File.AppendAllText(LogPath, string.Format("[{0}] {1}\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), msg)); }
            catch (Exception) { /* Logging must never affect application behavior. */ }
        }

        public static void LogTiming(string phase, Stopwatch timer)
        {
            if (timer == null)
            {
                Log("Timing " + phase + " elapsed_ms=unknown");
                return;
            }

            Log(string.Format("Timing {0} elapsed_ms={1}", phase, timer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)));
        }

        public static void LogException(string context, Exception ex)
        {
            Log(context + ": " + (ex == null ? "<null>" : ex.ToString()));
        }

        [STAThread]
        public static void Main()
        {
            using (SingleInstanceGuard instanceGuard = SingleInstanceGuard.TryAcquire("Local\\ModernDock.SingleInstance"))
            {
                if (instanceGuard == null) return;
                RunApplication();
            }
        }

        private static void RunApplication()
        {
            Stopwatch mainTimer = Stopwatch.StartNew();
            DockWindow win = null;
            try
            {
                Log("Starting ModernDock Application");

                Stopwatch applicationTimer = Stopwatch.StartNew();
                Application app = new Application();
                LogTiming("startup.application_initialization", applicationTimer);
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                app.DispatcherUnhandledException += (s, e) => {
                    LogException("dispatcher.unhandled_exception", e.Exception);
                    e.Handled = true;
                };

                app.SessionEnding += (s, e) => {
                    if (win != null) win.RequestShutdown("session-ending");
                };

                Stopwatch windowTimer = Stopwatch.StartNew();
                win = new DockWindow();
                LogTiming("startup.dock_window_creation", windowTimer);
                Log("Timing startup.application_run enter");
                app.Run(win);
            }
            catch (Exception ex)
            {
                LogException("main.fatal", ex);
            }
            finally
            {
                if (win != null) win.RequestShutdown("main.finally");
                LogTiming("lifecycle.main_total", mainTimer);
            }
        }
    }

    public class DockWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uAction, uint uParam, string lpvParam, uint fuWinIni);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint WM_CLOSE = 0x0010;
        private const uint WM_SYSCOMMAND = 0x0112;
        private const int SC_MINIMIZE = 0xF020;
        private const int SC_RESTORE = 0xF120;
        private const uint GW_OWNER = 4;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int SW_HIDE = 0;
        private const int SW_MINIMIZE = 6;
        private const int SW_MAXIMIZE = 3;
        private const int SW_SHOWMAXIMIZED = 3;
        private const int WPF_RESTORETOMAXIMIZED = 0x0002;

        private const int GWL_EXSTYLE = -20;
        private const int GWL_STYLE = -16;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;

        private Grid mainPanel;
        private StackPanel fixedPanel;
        private System.Windows.Shapes.Rectangle dynamicSeparator;
        private StackPanel dynamicPanel;
        private Button clockArea;
        private StackPanel applicationArea;
        private Button volumeArea;
        private System.Windows.Shapes.Rectangle clockSeparator;
        private System.Windows.Shapes.Rectangle volumeSeparator;
        private TextBlock clockText;
        private TextBlock dateText;
        private System.Windows.Shapes.Path volumeSpeakerGlyph;
        private System.Windows.Shapes.Path volumeWaveGlyph;
        private System.Windows.Shapes.Path volumeMuteGlyph;
        private Grid volumeVisual;
        private Border dockBorder;
        private VolumeFlyout volumeFlyout;

        private List<DockItem> fixedItems;
        private Dictionary<string, DockItem> dynamicItemsMap = new Dictionary<string, DockItem>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Grid> dynamicContainersMap = new Dictionary<string, Grid>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, ApplicationGroup> dynamicGroupsMap = new Dictionary<string, ApplicationGroup>(StringComparer.OrdinalIgnoreCase);
        private int lastLoggedDynamicGroupCount = -1;
        private string lastLoggedDynamicIdentityOrder = string.Empty;
        private DispatcherTimer refreshTimer;
        private Popup currentFluentPopup = null;
        private string configPath;
        private string iconDirectory;
        private string metadataPath;
        private bool shutdownStarted;
        private bool cleanupCompleted;
        private bool closeRequested;
        private bool applicationShutdownRequested;
        private IDictionary<IntPtr, string> currentShellIdentityMap = new Dictionary<IntPtr, string>();
        private IDictionary<IntPtr, string> shellIdentitySnapshot = new Dictionary<IntPtr, string>();
        private DateTime shellIdentitySnapshotAtUtc = DateTime.MinValue;
        private readonly RefreshPhaseProfiler refreshPhaseProfiler = new RefreshPhaseProfiler();
        private const int ShellIdentitySnapshotTtlMilliseconds = 1000;
        private const double FixedDragThreshold = 8.0;
        private DockItem dragItem;
        private Grid dragContainer;
        private System.Windows.Point dragStartPoint;
        private bool dragTracking;
        private bool dragInProgress;
        private DispatcherTimer systemInfoTimer;
        private DispatcherTimer volumeStateTimer;
        private bool nativeTaskbarVisibleOverride;
        private bool volumeMuted;
        private bool volumeStateKnown;
        private float volumeLevel;
        private bool volumeLevelKnown;

        private IntPtr lastForegroundAppHwnd = IntPtr.Zero;
        private List<WindowSnapshot> currentEnumList;
        private EnumWindowsProc enumProcDelegate;
        private Dictionary<string, DateTime> lastActionTimeMap = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private int refreshTickCount;

        public DockWindow()
        {
            Stopwatch constructorTimer = Stopwatch.StartNew();
            try
            {
                this.Title = "ModernDock";
                this.WindowStyle = WindowStyle.None;
                this.AllowsTransparency = true;
                this.Background = System.Windows.Media.Brushes.Transparent;
                this.Topmost = true;
                this.ShowInTaskbar = false;
                this.ResizeMode = ResizeMode.NoResize;
                this.SizeToContent = SizeToContent.WidthAndHeight;
                this.SnapsToDevicePixels = true;
                this.UseLayoutRounding = true;
                this.AllowDrop = true;
                this.DragOver += HandleDockDragOver;
                this.Drop += HandleDockDrop;
                this.Closed += (s, e) => HandleWindowClosed();

                string baseDir = Environment.GetEnvironmentVariable("MODERNDock_DATA_DIR");
                if (string.IsNullOrWhiteSpace(baseDir) || !System.IO.Path.IsPathRooted(baseDir))
                {
                    baseDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\ModernDock");
                }
                configPath = System.IO.Path.Combine(baseDir, "dock_config.txt");
                iconDirectory = System.IO.Path.Combine(baseDir, "Icons");
                metadataPath = System.IO.Path.Combine(baseDir, "dock_metadata.json");
                if (!Directory.Exists(iconDirectory)) Directory.CreateDirectory(iconDirectory);

                Stopwatch registryTimer = Stopwatch.StartNew();
                EnsureAutoStartRegistry();
                EntryPoint.LogTiming("startup.autostart_registry", registryTimer);

                Stopwatch dockInitializationTimer = Stopwatch.StartNew();
                InitializeDock();
                EntryPoint.LogTiming("startup.dock_initialization", dockInitializationTimer);

                this.SourceInitialized += (s, e) => {
                    Stopwatch sourceInitializedTimer = Stopwatch.StartNew();
                    try
                    {
                        IntPtr hwnd = new WindowInteropHelper(this).Handle;
                        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
                        EntryPoint.Log("SourceInitialized completed");
                    }
                    catch (Exception ex)
                    {
                        EntryPoint.LogException("startup.source_initialized", ex);
                    }
                    finally
                    {
                        EntryPoint.LogTiming("startup.source_initialized", sourceInitializedTimer);
                    }
                };

                this.Loaded += (s, e) => {
                    Stopwatch loadedTimer = Stopwatch.StartNew();
                    try
                    {
                        PositionDock();
                        StartRefreshTimer();
                        StartSystemInfoTimer();
                        EntryPoint.Log("Dock loaded and timers started");
                    }
                    catch (Exception ex)
                    {
                        EntryPoint.LogException("startup.loaded", ex);
                    }
                    finally
                    {
                        EntryPoint.LogTiming("startup.loaded_handler", loadedTimer);
                    }
                };
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("startup.dock_window_constructor", ex);
            }
            finally
            {
                EntryPoint.LogTiming("startup.dock_window_constructor", constructorTimer);
            }
        }

        private void EnsureAutoStartRegistry()
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!StartupPathPolicy.IsOfficialInstallPath(exePath))
                {
                    EntryPoint.Log("startup.autostart_registry skipped_non_official_path=" + exePath);
                    return;
                }

                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null && File.Exists(exePath))
                    {
                        key.SetValue("ModernDock", "\"" + exePath + "\"");
                    }
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("startup.autostart_registry", ex);
            }
        }

        private void PositionDock()
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                HideNativeTaskbarIfAllowed();

                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;

                this.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                double actualW = this.DesiredSize.Width > 100 ? this.DesiredSize.Width : 760;
                double actualH = this.DesiredSize.Height > 20 ? this.DesiredSize.Height : 46;

                this.Left = (screenWidth - actualW) / 2.0;
                this.Top = screenHeight - actualH - 1.0;
                this.Opacity = 1.0;

                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, NativeConstants.DockTopmostNoActivateFlags);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("dock.position", ex);
            }
            finally
            {
                EntryPoint.LogTiming("dock.position", timer);
            }
        }

        private void StartRefreshTimer()
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                refreshTimer = new DispatcherTimer();
                refreshTimer.Interval = TimeSpan.FromMilliseconds(250);
                refreshTimer.Tick += (s, e) => {
                    Stopwatch tickTimer = Stopwatch.StartNew();
                    try
                    {
                        if (shutdownStarted) return;

                        Stopwatch phaseTimer = Stopwatch.StartNew();
                        HideNativeTaskbarIfAllowed();
                        refreshPhaseProfiler.Record("taskbar_visibility", phaseTimer.Elapsed.TotalMilliseconds);

                        IntPtr hwnd = new WindowInteropHelper(this).Handle;

                        phaseTimer = Stopwatch.StartNew();
                        IntPtr curFg = GetForegroundWindow();
                        if (curFg != IntPtr.Zero && curFg != hwnd)
                        {
                            lastForegroundAppHwnd = curFg;
                        }

                        bool hideForFullscreen = IsForegroundApplicationFullscreen(curFg, hwnd);
                        refreshPhaseProfiler.Record("foreground_fullscreen", phaseTimer.Elapsed.TotalMilliseconds);
                        if (hideForFullscreen)
                        {
                            if (this.Visibility != Visibility.Hidden)
                            {
                                this.Visibility = Visibility.Hidden;
                                if (currentFluentPopup != null)
                                {
                                    currentFluentPopup.IsOpen = false;
                                    currentFluentPopup = null;
                                }
                                if (volumeFlyout != null) volumeFlyout.Close();
                            }
                        }
                        else if (this.Visibility != Visibility.Visible)
                        {
                            this.Visibility = Visibility.Visible;
                        }

                        phaseTimer = Stopwatch.StartNew();
                        double screenWidth = SystemParameters.PrimaryScreenWidth;
                        double screenHeight = SystemParameters.PrimaryScreenHeight;
                        double actualW = this.DesiredSize.Width > 100 ? this.DesiredSize.Width : 760;
                        double actualH = this.DesiredSize.Height > 20 ? this.DesiredSize.Height : 46;

                        this.Left = (screenWidth - actualW) / 2.0;
                        this.Top = screenHeight - actualH - 1.0;
                        this.Opacity = 1.0;

                        if (!hideForFullscreen)
                        {
                            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, NativeConstants.DockTopmostNoActivateFlags);
                        }
                        refreshPhaseProfiler.Record("layout_topmost", phaseTimer.Elapsed.TotalMilliseconds);

                        UpdateWindowIndicatorsAndDynamicApps(refreshPhaseProfiler);
                        phaseTimer = Stopwatch.StartNew();
                        CheckPopupDismissal();
                        refreshPhaseProfiler.Record("popup_dismissal", phaseTimer.Elapsed.TotalMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        EntryPoint.LogException("refresh.tick", ex);
                    }
                    finally
                    {
                        refreshTickCount++;
                        if (refreshTickCount <= 5 || (refreshTickCount % 20) == 0)
                        {
                            EntryPoint.LogTiming("refresh.tick count=" + refreshTickCount, tickTimer);
                        }
                        if ((refreshTickCount % 20) == 0)
                        {
                            EntryPoint.Log("refresh.phase_aggregate count=" + refreshTickCount + " " + refreshPhaseProfiler.SnapshotAndReset());
                        }
                    }
                };
                refreshTimer.Start();
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("startup.refresh_timer_initialization", ex);
            }
            finally
            {
                EntryPoint.LogTiming("startup.refresh_timer_initialization", timer);
            }
        }

        public void RequestShutdown(string reason)
        {
            EnsureCleanup(reason);
            if (!closeRequested)
            {
                closeRequested = true;
                try
                {
                    if (IsLoaded || IsVisible) Close();
                }
                catch (Exception ex)
                {
                    EntryPoint.LogException("lifecycle.close", ex);
                }
            }

            ShutdownApplication(reason);
        }

        private void HandleWindowClosed()
        {
            EnsureCleanup("window.closed");
            ShutdownApplication("window.closed");
        }

        private void ShutdownApplication(string reason)
        {
            if (applicationShutdownRequested) return;
            applicationShutdownRequested = true;
            try
            {
                if (Application.Current != null) Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("lifecycle.application_shutdown reason=" + (reason ?? "unknown"), ex);
            }
        }

        public void EnsureCleanup(string reason)
        {
            if (cleanupCompleted) return;
            shutdownStarted = true;

            try
            {
                if (refreshTimer != null) refreshTimer.Stop();
                if (systemInfoTimer != null) systemInfoTimer.Stop();
                if (volumeStateTimer != null) volumeStateTimer.Stop();
                if (currentFluentPopup != null) currentFluentPopup.IsOpen = false;
                if (volumeFlyout != null) volumeFlyout.Close();
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("lifecycle.stop_timers reason=" + reason, ex);
            }

            try
            {
                RestoreWindowsTaskbar();
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("lifecycle.restore_taskbar reason=" + reason, ex);
            }

            cleanupCompleted = true;
            EntryPoint.Log("lifecycle.cleanup_completed reason=" + (reason ?? "unknown"));
        }

        private void RestoreWindowsTaskbar()
        {
            IntPtr hTray = FindWindow("Shell_TrayWnd", null);
            if (hTray != IntPtr.Zero) ShowWindow(hTray, SW_SHOW);
        }

        private void HideNativeTaskbarIfAllowed()
        {
            IntPtr hTray = FindWindow("Shell_TrayWnd", null);
            if (hTray != IntPtr.Zero && !nativeTaskbarVisibleOverride && !shutdownStarted) ShowWindow(hTray, SW_HIDE);
        }

        private void HideNativeTaskbarCore()
        {
            IntPtr hTray = FindWindow("Shell_TrayWnd", null);
            if (hTray != IntPtr.Zero) ShowWindow(hTray, SW_HIDE);
        }

        private bool IsForegroundApplicationFullscreen(IntPtr foregroundHwnd, IntPtr dockHwnd)
        {
            try
            {
                if (foregroundHwnd == IntPtr.Zero || foregroundHwnd == dockHwnd)
                {
                    return false;
                }

                if (!IsWindowVisible(foregroundHwnd) || IsIconic(foregroundHwnd))
                {
                    return false;
                }

                uint pid;
                GetWindowThreadProcessId(foregroundHwnd, out pid);
                if (pid == 0)
                {
                    return false;
                }

                string processPath = GetProcessPath(pid);
                string processName = string.Empty;
                if (!string.IsNullOrEmpty(processPath)) processName = System.IO.Path.GetFileNameWithoutExtension(processPath);
                var className = new StringBuilder(256);
                GetClassName(foregroundHwnd, className, 256);
                RECT rect;
                if (!GetWindowRect(foregroundHwnd, out rect))
                {
                    return false;
                }

                IntPtr monitor = MonitorFromWindow(foregroundHwnd, MONITOR_DEFAULTTONEAREST);
                if (monitor == IntPtr.Zero) return false;
                MONITORINFO monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (!GetMonitorInfo(monitor, ref monitorInfo)) return false;

                var candidate = new FullscreenWindowInfo
                {
                    Left = rect.Left,
                    Top = rect.Top,
                    Right = rect.Right,
                    Bottom = rect.Bottom,
                    MonitorLeft = monitorInfo.rcMonitor.Left,
                    MonitorTop = monitorInfo.rcMonitor.Top,
                    MonitorRight = monitorInfo.rcMonitor.Right,
                    MonitorBottom = monitorInfo.rcMonitor.Bottom,
                    WorkLeft = monitorInfo.rcWork.Left,
                    WorkTop = monitorInfo.rcWork.Top,
                    WorkRight = monitorInfo.rcWork.Right,
                    WorkBottom = monitorInfo.rcWork.Bottom,
                    IsVisible = true,
                    IsMinimized = false,
                    IsZoomed = IsZoomed(foregroundHwnd),
                    WindowStyle = GetWindowLong(foregroundHwnd, GWL_STYLE),
                    ProcessName = processName,
                    WindowClass = className.ToString(),
                    IsDock = foregroundHwnd == dockHwnd
                };
                return FullscreenWindowPolicy.IsFullscreenCandidate(candidate);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("refresh.fullscreen_check", ex);
                return false;
            }
        }

        private void CheckPopupDismissal()
        {
            try
            {
                if (currentFluentPopup != null && currentFluentPopup.IsOpen)
                {
                    POINT pt;
                    GetCursorPos(out pt);

                    short lBtn = GetAsyncKeyState(VK_LBUTTON);
                    short rBtn = GetAsyncKeyState(VK_RBUTTON);
                    bool mouseClicked = ((lBtn & 0x8000) != 0) || ((rBtn & 0x8000) != 0);

                    if (mouseClicked)
                    {
                        IntPtr hwnd = new WindowInteropHelper(this).Handle;
                        RECT r;
                        if (GetWindowRect(hwnd, out r))
                        {
                            if (pt.X < r.Left || pt.X > r.Right || pt.Y < r.Top - 220 || pt.Y > r.Bottom + 40)
                            {
                                currentFluentPopup.IsOpen = false;
                                currentFluentPopup = null;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("refresh.popup_dismissal", ex);
            }
        }

        private bool EnumWindowCallback(IntPtr hWnd, IntPtr lParam)
        {
            try
            {
                if (!IsWindowVisible(hWnd)) return true;

                int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                if ((exStyle & WS_EX_TOOLWINDOW) != 0 && (exStyle & WS_EX_APPWINDOW) == 0) return true;

                IntPtr owner = GetWindow(hWnd, GW_OWNER);
                if (owner != IntPtr.Zero && (exStyle & WS_EX_APPWINDOW) == 0) return true;

                bool isMin = IsIconic(hWnd);
                RECT r;
                if (!GetWindowRect(hWnd, out r)) return true;

                if (!isMin)
                {
                    if ((r.Right - r.Left) < 100 || (r.Bottom - r.Top) < 60) return true;
                }

                var sbClass = new StringBuilder(256);
                GetClassName(hWnd, sbClass, 256);
                string cls = sbClass.ToString();

                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "ModernDock" || cls == "Windows.UI.Core.CoreWindow" || cls == "TopLevelWindowForOverflowList" || cls == "XamlExplorerHostIslandWindow" || cls == "MSCTFIME UI" || cls == "IME") return true;

                var sbTitle = new StringBuilder(256);
                GetWindowText(hWnd, sbTitle, 256);
                string title = sbTitle.ToString().Trim();

                if (string.IsNullOrEmpty(title) && cls != "CabinetWClass") return true;

                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (pid == 0) return true;

                string procPath = GetProcessPath(pid);
                string procName = "";
                if (!string.IsNullOrEmpty(procPath))
                {
                    procName = System.IO.Path.GetFileNameWithoutExtension(procPath);
                }

                string shellIdentity = string.Empty;
                if (cls.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentShellIdentityMap != null)
                    {
                        string snapshotIdentity;
                        if (currentShellIdentityMap.TryGetValue(hWnd, out snapshotIdentity))
                        {
                            shellIdentity = snapshotIdentity;
                        }
                    }
                }

                if (procName.Equals("conhost", StringComparison.OrdinalIgnoreCase) ||
                    procName.Equals("dwm", StringComparison.OrdinalIgnoreCase) ||
                    procName.Equals("sihost", StringComparison.OrdinalIgnoreCase)) return true;

                if (currentEnumList != null)
                {
                    currentEnumList.Add(new WindowSnapshot
                    {
                        Handle = hWnd,
                        ProcessPath = procPath,
                        ProcessName = procName,
                        WindowClass = cls,
                        WindowTitle = title,
                        Pid = pid,
                        IsMinimized = isMin,
                        ShellIdentity = shellIdentity
                    });
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("refresh.enumerate_window_callback hwnd=0x" + hWnd.ToInt64().ToString("X"), ex);
            }

            return true;
        }

        private List<WindowSnapshot> GetTopLevelWindows()
        {
            return GetTopLevelWindows(false, null);
        }

        private List<WindowSnapshot> GetTopLevelWindows(bool forceShellIdentityRefresh, RefreshPhaseProfiler profiler)
        {
            var list = new List<WindowSnapshot>();
            try
            {
                Stopwatch shellTimer = Stopwatch.StartNew();
                bool refreshShellIdentity = forceShellIdentityRefresh ||
                    shellIdentitySnapshot == null ||
                    DateTime.UtcNow >= shellIdentitySnapshotAtUtc.AddMilliseconds(ShellIdentitySnapshotTtlMilliseconds);
                if (refreshShellIdentity)
                {
                    shellIdentitySnapshot = ShellIdentityResolver.GetIdentitySnapshot() ?? new Dictionary<IntPtr, string>();
                    shellIdentitySnapshotAtUtc = DateTime.UtcNow;
                }
                if (profiler != null) profiler.Record("shell_identity_snapshot", shellTimer.Elapsed.TotalMilliseconds);

                currentShellIdentityMap = shellIdentitySnapshot;
                if (enumProcDelegate == null)
                {
                    enumProcDelegate = new EnumWindowsProc(EnumWindowCallback);
                }

                Stopwatch enumTimer = Stopwatch.StartNew();
                currentEnumList = list;
                EnumWindows(enumProcDelegate, IntPtr.Zero);
                if (profiler != null) profiler.Record("enum_windows_process_path", enumTimer.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("refresh.enumerate_windows", ex);
            }
            finally
            {
                currentEnumList = null;
                currentShellIdentityMap = new Dictionary<IntPtr, string>();
            }
            return list;
        }

        private string GetProcessPath(uint pid)
        {
            IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProc == IntPtr.Zero) return "";
            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (QueryFullProcessImageName(hProc, 0, sb, ref size))
                {
                    return sb.ToString();
                }
            }
            finally
            {
                CloseHandle(hProc);
            }
            return "";
        }

        private bool MatchesItem(DockItem item, WindowSnapshot win)
        {
            if (item == null || win == null) return false;
            FixedItemMatch match = FixedItemMatcher.Resolve(new DockItem[] { item }, win);
            return !match.IsAmbiguous && match.Item == item;
        }

        private void UpdateWindowIndicatorsAndDynamicApps(RefreshPhaseProfiler profiler)
        {
            try
            {
                var activeWindows = GetTopLevelWindows(false, profiler);
                var unpinnedWindows = new List<WindowSnapshot>();
                var windowMatches = new Dictionary<IntPtr, FixedItemMatch>();
                Stopwatch fixedMatchingTimer = Stopwatch.StartNew();
                FixedItemMatcher.MatchContext fixedMatchContext = FixedItemMatcher.CreateContext(fixedItems);
                foreach (var win in activeWindows)
                {
                    windowMatches[win.Handle] = fixedMatchContext.Resolve(win);
                }
                if (profiler != null) profiler.Record("fixed_matching", fixedMatchingTimer.Elapsed.TotalMilliseconds);

                // 1. Update fixed items indicators
                Stopwatch indicatorTimer = Stopwatch.StartNew();
                foreach (var item in fixedItems)
                {
                    if (item.CustomAction != null &&
                        string.IsNullOrEmpty(item.PathMatch) &&
                        string.IsNullOrEmpty(item.ProcessNameMatch) &&
                        string.IsNullOrEmpty(item.WindowClassMatch) &&
                        string.IsNullOrEmpty(item.TargetPath))
                    {
                        if (item.IndicatorDot != null) item.IndicatorDot.Visibility = Visibility.Hidden;
                        continue;
                    }

                    bool hasOpen = false;
                    foreach (var win in activeWindows)
                    {
                        FixedItemMatch match;
                        if (!windowMatches.TryGetValue(win.Handle, out match)) continue;
                        if (!match.IsAmbiguous && match.Item == item)
                        {
                            hasOpen = true;
                            break;
                        }
                    }

                    if (item.IndicatorDot != null)
                    {
                        item.IndicatorDot.Visibility = hasOpen ? Visibility.Visible : Visibility.Hidden;
                    }
                }

                // 2. Identify unpinned running windows
                foreach (var win in activeWindows)
                {
                    FixedItemMatch match;
                    if (!windowMatches.TryGetValue(win.Handle, out match)) continue;
                    if (match.IsAmbiguous)
                    {
                        EntryPoint.Log("refresh.ambiguous_window_skipped hwnd=0x" + win.Handle.ToInt64().ToString("X"));
                        continue;
                    }
                    if (match.Item == null)
                    {
                        unpinnedWindows.Add(win);
                    }
                }
                if (profiler != null) profiler.Record("indicator_ui_update", indicatorTimer.Elapsed.TotalMilliseconds);

                // 3. Remove closed dynamic items
                var toRemove = new List<string>();
                foreach (var kvp in dynamicItemsMap)
                {
                    bool stillExists = false;
                    string identityKey = kvp.Key;
                    foreach (var win in unpinnedWindows)
                    {
                        if (string.Equals(ApplicationIdentityResolver.GetDynamicIdentityKey(win), identityKey, StringComparison.OrdinalIgnoreCase))
                        {
                            stillExists = true;
                            break;
                        }
                    }
                    if (!stillExists)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
                foreach (var identityKey in toRemove)
                {
                    dynamicItemsMap.Remove(identityKey);
                    dynamicGroupsMap.Remove(identityKey);
                    if (dynamicContainersMap.ContainsKey(identityKey))
                    {
                        dynamicPanel.Children.Remove(dynamicContainersMap[identityKey]);
                        dynamicContainersMap.Remove(identityKey);
                    }
                }

                // 4. Add or refresh one dynamic item per application identity.
                Stopwatch groupingTimer = Stopwatch.StartNew();
                var applicationGroups = ApplicationIdentityResolver.GroupByIdentity(unpinnedWindows);
                if (profiler != null) profiler.Record("dynamic_grouping", groupingTimer.Elapsed.TotalMilliseconds);
                Stopwatch dynamicUiTimer = Stopwatch.StartNew();
                foreach (var group in applicationGroups)
                {
                    WindowSnapshot representative = group.Representative;
                    if (representative == null) continue;

                    string identityKey = group.Identity.Key;
                    DockItem existingItem;
                    if (dynamicItemsMap.TryGetValue(identityKey, out existingItem))
                    {
                        existingItem.DynamicHwnd = representative.Handle;
                        existingItem.DynamicPid = representative.Pid;
                        existingItem.DynamicIdentityKey = identityKey;
                        dynamicGroupsMap[identityKey] = group;
                        continue;
                    }

                    var icon = ExtractIconForWindow(representative.Handle, representative.ProcessPath, representative.ProcessName, representative.WindowTitle);
                        if (icon == null) continue;

                    var dItem = new DockItem
                    {
                        Title = string.IsNullOrEmpty(representative.WindowTitle) ? representative.ProcessName : representative.WindowTitle,
                            IconSource = icon,
                            IsFixed = false,
                        DynamicHwnd = representative.Handle,
                        DynamicPid = representative.Pid,
                        DynamicIdentityKey = identityKey,
                        ProcessNameMatch = representative.ProcessName,
                        TargetPath = representative.ProcessPath,
                        PathMatch = representative.ProcessPath
                    };
                    dynamicItemsMap[identityKey] = dItem;
                    dynamicGroupsMap[identityKey] = group;

                    var container = CreateItemContainer(dItem);
                    dynamicContainersMap[identityKey] = container;
                    dynamicPanel.Children.Add(container);
                    EntryPoint.Log("refresh.dynamic_group_created identity=" + identityKey +
                        " slot=" + (dynamicPanel.Children.Count - 1) + " windows=" + group.Windows.Count);
                }

                if (dynamicItemsMap.Count != lastLoggedDynamicGroupCount)
                {
                    lastLoggedDynamicGroupCount = dynamicItemsMap.Count;
                    var dynamicIdentityKeys = new List<string>();
                    foreach (var key in dynamicGroupsMap.Keys) dynamicIdentityKeys.Add(key);
                    EntryPoint.Log("refresh.dynamic_group_count count=" + dynamicItemsMap.Count +
                        " identities=" + string.Join(",", dynamicIdentityKeys.ToArray()));
                }

                var identityOrder = new List<string>();
                foreach (var group in applicationGroups)
                {
                    if (group != null && group.Identity != null) identityOrder.Add(group.Identity.Key);
                }
                string identityOrderValue = string.Join("|", identityOrder.ToArray());
                if (!string.Equals(identityOrderValue, lastLoggedDynamicIdentityOrder, StringComparison.Ordinal))
                {
                    lastLoggedDynamicIdentityOrder = identityOrderValue;
                    EntryPoint.Log("refresh.dynamic_group_order identities=" + identityOrderValue);
                }

                dynamicSeparator.Visibility = (dynamicItemsMap.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
                if (profiler != null) profiler.Record("indicator_ui_update", dynamicUiTimer.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("refresh.update_windows", ex);
            }
        }

        private ImageSource ExtractIconForWindow(IntPtr hWnd, string procPath, string procName, string winTitle)
        {
            try
            {
                // 1. Photos & Image Files Detection (Windows Photos, WPS图片, 2345看图, etc.)
                if ((winTitle != null && (winTitle.IndexOf("照片", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          winTitle.IndexOf("图片", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          winTitle.IndexOf("看图", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          winTitle.IndexOf("Photos", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          winTitle.IndexOf(".png", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          winTitle.IndexOf(".jpg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          winTitle.IndexOf(".jpeg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          winTitle.IndexOf(".bmp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          winTitle.IndexOf(".gif", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          winTitle.IndexOf(".webp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          winTitle.IndexOf(".ico", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          winTitle.IndexOf(".svg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          winTitle.IndexOf(".jfif", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          winTitle.IndexOf(".heic", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                    (procName != null && (procName.IndexOf("Photos", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          procName.IndexOf("photolaunch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          procName.IndexOf("photo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          procName.IndexOf("picture", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          procName.IndexOf("ImageGlass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          procName.IndexOf("Honeyview", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                    (procPath != null && (procPath.IndexOf("Photos", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          procPath.IndexOf("photolaunch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          procPath.IndexOf("photo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          procPath.IndexOf("picture", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          procPath.IndexOf("ImageGlass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          procPath.IndexOf("Honeyview", StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    string photosPng = System.IO.Path.Combine(iconDirectory, "Photos.png");
                    var src = LoadPngIcon(photosPng);
                    if (src != null) return src;
                }

                // 2. Settings
                if ((winTitle != null && (winTitle.IndexOf("设置", StringComparison.OrdinalIgnoreCase) >= 0 || winTitle.IndexOf("Settings", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                    (procName != null && procName.IndexOf("SystemSettings", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (procPath != null && procPath.IndexOf("SystemSettings", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    string settingsPng = System.IO.Path.Combine(iconDirectory, "Settings.png");
                    var src = LoadPngIcon(settingsPng);
                    if (src != null) return src;
                }

                // 3. Calculator
                if ((winTitle != null && (winTitle.IndexOf("计算器", StringComparison.OrdinalIgnoreCase) >= 0 || winTitle.IndexOf("Calculator", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                    (procName != null && procName.IndexOf("Calculator", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (procPath != null && procPath.IndexOf("Calculator", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    string calcPng = System.IO.Path.Combine(iconDirectory, "Calculator.png");
                    var src = LoadPngIcon(calcPng);
                    if (src != null) return src;
                }

                // 4. Snipping Tool
                if ((winTitle != null && (winTitle.IndexOf("截图", StringComparison.OrdinalIgnoreCase) >= 0 || winTitle.IndexOf("Snipping", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                    (procName != null && (procName.IndexOf("ScreenSketch", StringComparison.OrdinalIgnoreCase) >= 0 || procName.IndexOf("SnippingTool", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                    (procPath != null && (procPath.IndexOf("ScreenSketch", StringComparison.OrdinalIgnoreCase) >= 0 || procPath.IndexOf("SnippingTool", StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    string snipPng = System.IO.Path.Combine(iconDirectory, "SnippingTool.png");
                    var src = LoadPngIcon(snipPng);
                    if (src != null) return src;
                }

                // 5. Special fallback for ApplicationFrameHost windows
                if (procPath != null && procPath.IndexOf("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string photosPng = System.IO.Path.Combine(iconDirectory, "Photos.png");
                    var src = LoadPngIcon(photosPng);
                    if (src != null) return src;
                }

                // 6. General executable icon extraction. IconService keeps the
                // source resolution and owns its bounded identity/source cache.
                if (!string.IsNullOrEmpty(procPath) && procPath.IndexOf("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return IconService.ExtractBest(procPath);
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("refresh.extract_icon process=" + procName + " path=" + procPath, ex);
            }

            return null;
        }

        private void InitializeDock()
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                dockBorder = new Border
                {
                    Background = System.Windows.Media.Brushes.Transparent,
                    CornerRadius = new CornerRadius(0),
                    BorderThickness = new Thickness(0),
                    BorderBrush = System.Windows.Media.Brushes.Transparent,
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0),
                    Effect = null
                };

                mainPanel = new Grid
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = System.Windows.Media.Brushes.Transparent
                };

                mainPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                mainPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                mainPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                mainPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                mainPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                fixedPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = System.Windows.Media.Brushes.Transparent,
                    AllowDrop = true
                };

                dynamicSeparator = new System.Windows.Shapes.Rectangle
                {
                    Width = 1,
                    Height = 22,
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 255, 255, 255)),
                    Margin = new Thickness(6, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Collapsed
                };

                dynamicPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = System.Windows.Media.Brushes.Transparent
                };

                applicationArea = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = System.Windows.Media.Brushes.Transparent,
                    AllowDrop = true
                };
                System.Windows.Automation.AutomationProperties.SetName(applicationArea, "ModernDock.ApplicationArea");
                System.Windows.Automation.AutomationProperties.SetAutomationId(applicationArea, "ModernDock.ApplicationArea");
                applicationArea.Children.Add(fixedPanel);
                applicationArea.Children.Add(dynamicSeparator);
                applicationArea.Children.Add(dynamicPanel);

                clockArea = CreateClockArea();
                clockSeparator = CreateSystemSeparator(5, 7);
                volumeSeparator = CreateSystemSeparator();
                volumeArea = CreateVolumeArea();
                volumeFlyout = new VolumeFlyout(volumeArea);
                // Keep the anchor explicit at the composition boundary so
                // diagnostics and future changes preserve the VolumeArea
                // relationship.
                volumeFlyout.PlacementTarget = volumeArea;
                volumeFlyout.VolumeChanged += HandleVolumeFlyoutValueChanged;

                Grid.SetColumn(clockArea, 0);
                Grid.SetColumn(clockSeparator, 1);
                Grid.SetColumn(applicationArea, 2);
                Grid.SetColumn(volumeSeparator, 3);
                Grid.SetColumn(volumeArea, 4);
                mainPanel.Children.Add(clockArea);
                mainPanel.Children.Add(clockSeparator);
                mainPanel.Children.Add(applicationArea);
                mainPanel.Children.Add(volumeSeparator);
                mainPanel.Children.Add(volumeArea);

                dockBorder.Child = mainPanel;
                dockBorder.AllowDrop = true;
                this.Content = dockBorder;
                UpdateSystemInfo();
                // Handle the preview event at the Window boundary.  On a
                // transparent, borderless WPF window the bubbling right-button
                // event can be consumed by the native hit-test surface before
                // it reaches dockBorder, while left-button routing still works.
                // Resolving the item from the live layout here keeps the
                // context menu behavior independent of that implementation
                // detail and prevents a blank Dock context menu from masking
                // an item menu.
                this.PreviewMouseRightButtonUp += (s, e) => {
                    if (e.Handled) return;
                    if (TryShowItemPopupAtPoint(e))
                    {
                        e.Handled = true;
                        return;
                    }
                    ShowDockContextPopup();
                    e.Handled = true;
                };

                Stopwatch fixedItemsTimer = Stopwatch.StartNew();
                LoadFixedDockItems();
                EntryPoint.LogTiming("startup.fixed_items_initialization", fixedItemsTimer);
                BuildFixedUI();
            }
            finally
            {
                EntryPoint.LogTiming("startup.initialize_dock", timer);
            }
        }

        private void HandleDockDragOver(object sender, DragEventArgs e)
        {
            try
            {
                string droppedPath;
                int targetIndex;
                if (TryGetDropPath(e, out droppedPath) && TryGetFixedDropIndex(e, out targetIndex))
                {
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                EntryPoint.LogException("ui.drop.drag_over", ex);
            }
        }

        private void HandleDockDrop(object sender, DragEventArgs e)
        {
            try
            {
                string droppedPath;
                int targetIndex;
                if (TryGetDropPath(e, out droppedPath) && TryGetFixedDropIndex(e, out targetIndex))
                {
                    if (!TryAddDroppedItem(droppedPath, targetIndex))
                    {
                        EntryPoint.Log("ui.drop.rejected path=" + droppedPath);
                    }
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                e.Handled = true;
                EntryPoint.LogException("ui.drop.failed", ex);
            }
        }

        private bool TryGetDropPath(DragEventArgs e, out string droppedPath)
        {
            droppedPath = string.Empty;
            if (e == null || e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length != 1 || !DockDropService.IsSupportedPath(paths[0])) return false;
            droppedPath = paths[0];
            return true;
        }

        private bool TryGetFixedDropIndex(DragEventArgs e, out int targetIndex)
        {
            targetIndex = -1;
            if (e == null || fixedPanel == null) return false;

            System.Windows.Point point = e.GetPosition(fixedPanel);
            double width = fixedPanel.ActualWidth > 0 ? fixedPanel.ActualWidth : fixedPanel.RenderSize.Width;
            double height = fixedPanel.ActualHeight > 0 ? fixedPanel.ActualHeight : fixedPanel.RenderSize.Height;
            if (point.X < 0 || point.Y < 0 || point.X > width || point.Y > height) return false;

            targetIndex = fixedPanel.Children.Count;
            for (int i = 0; i < fixedPanel.Children.Count; i++)
            {
                FrameworkElement child = fixedPanel.Children[i] as FrameworkElement;
                if (child == null) continue;
                System.Windows.Point origin = child.TranslatePoint(new System.Windows.Point(0, 0), fixedPanel);
                double midpoint = origin.X + child.RenderSize.Width / 2.0;
                if (point.X <= midpoint)
                {
                    targetIndex = i;
                    break;
                }
            }
            return true;
        }

        public bool TryAddDroppedItem(string droppedPath, int targetIndex)
        {
            DockItem newItem = null;
            string iconFullPath = string.Empty;
            int insertedIndex = -1;
            bool configChanged = false;
            bool commitCompleted = false;
            try
            {
                string error;
                if (!DockDropService.TryCreateDockItem(droppedPath, out newItem, out error))
                {
                    EntryPoint.Log("ui.drop.parse_failed path=" + droppedPath + " error=" + error);
                    return false;
                }

                if (HasFixedIdentityConflict(newItem))
                {
                    EntryPoint.Log("ui.drop.duplicate_identity path=" + newItem.TargetPath);
                    return false;
                }

                BitmapSource bitmap = newItem.IconSource as BitmapSource;
                if (bitmap == null)
                {
                    EntryPoint.Log("ui.drop.icon_missing path=" + newItem.TargetPath);
                    return false;
                }

                string cleanName = SanitizeFileName(newItem.Title);
                string iconFileName = "drop_" + cleanName + "_" + Guid.NewGuid().ToString("N") + ".png";
                iconFullPath = System.IO.Path.Combine(iconDirectory, iconFileName);
                if (!IconService.SavePng(bitmap, iconFullPath))
                {
                    EntryPoint.Log("ui.drop.icon_save_failed path=" + iconFullPath);
                    return false;
                }
                newItem.IconFile = iconFileName;

                if (targetIndex < 0) targetIndex = 0;
                if (targetIndex > fixedItems.Count) targetIndex = fixedItems.Count;
                fixedItems.Insert(targetIndex, newItem);
                insertedIndex = targetIndex;
                if (!SaveConfigToFile()) return false;
                configChanged = true;

                BuildFixedUI();
                PositionDock();
                EntryPoint.Log("ui.drop.added title=" + newItem.Title + " index=" + targetIndex +
                    " target=" + newItem.TargetPath);
                commitCompleted = true;
                return true;
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("ui.drop.add", ex);
                return false;
            }
            finally
            {
                if (!commitCompleted && newItem != null)
                {
                    if (insertedIndex >= 0 && fixedItems.Contains(newItem)) fixedItems.Remove(newItem);
                    if (configChanged)
                    {
                        try
                        {
                            // The in-memory rollback above is mirrored to disk
                            // if a post-save UI/layout step failed.
                            SaveConfigToFile();
                        }
                        catch (Exception rollbackEx) { EntryPoint.LogException("ui.drop.config_rollback", rollbackEx); }
                    }
                    try { if (!string.IsNullOrEmpty(iconFullPath) && File.Exists(iconFullPath)) File.Delete(iconFullPath); }
                    catch (Exception cleanupEx) { EntryPoint.LogException("ui.drop.cleanup", cleanupEx); }
                }
            }
        }

        private bool TryShowItemPopupAtPoint(MouseButtonEventArgs e)
        {
            try
            {
                if (e == null) return false;

                if (fixedPanel != null)
                {
                    System.Windows.Point point = e.GetPosition(fixedPanel);
                    for (int i = 0; i < fixedPanel.Children.Count && i < fixedItems.Count; i++)
                    {
                        FrameworkElement child = fixedPanel.Children[i] as FrameworkElement;
                        if (child == null) continue;
                        System.Windows.Point origin = child.TranslatePoint(new System.Windows.Point(0, 0), fixedPanel);
                        System.Windows.Rect bounds = new System.Windows.Rect(origin, child.RenderSize);
                        if (bounds.Contains(point))
                        {
                            ShowWindows11FluentPopup(fixedItems[i], child);
                            return true;
                        }
                    }
                }

                if (dynamicPanel != null)
                {
                    System.Windows.Point point = e.GetPosition(dynamicPanel);
                    foreach (var pair in dynamicContainersMap)
                    {
                        FrameworkElement child = pair.Value as FrameworkElement;
                        DockItem item;
                        if (child == null || !dynamicItemsMap.TryGetValue(pair.Key, out item)) continue;
                        System.Windows.Point origin = child.TranslatePoint(new System.Windows.Point(0, 0), dynamicPanel);
                        System.Windows.Rect bounds = new System.Windows.Rect(origin, child.RenderSize);
                        if (bounds.Contains(point))
                        {
                            ShowWindows11FluentPopup(item, child);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("ui.context_hit_test", ex);
            }
            return false;
        }

        private Button CreateClockArea()
        {
            var area = new Button
            {
                // A one-alpha hit surface gives the WPF automation peer and
                // the whole clock area a reliable hit target on a transparent
                // borderless window.
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(3, 0, 12, 0),
                OverridesDefaultStyle = true,
                Focusable = false,
                IsTabStop = false,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = CreateToolTip("日期和时间")
            };
            var buttonTemplate = new ControlTemplate(typeof(Button));
            var buttonBorder = new FrameworkElementFactory(typeof(Border));
            buttonBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            buttonBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            buttonBorder.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            buttonBorder.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            var buttonContent = new FrameworkElementFactory(typeof(ContentPresenter));
            buttonContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            buttonContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            buttonContent.SetValue(ContentPresenter.ContentSourceProperty, "Content");
            buttonBorder.AppendChild(buttonContent);
            buttonTemplate.VisualTree = buttonBorder;
            area.Template = buttonTemplate;
            System.Windows.Automation.AutomationProperties.SetName(area, "ModernDock.Clock");
            System.Windows.Automation.AutomationProperties.SetAutomationId(area, "ModernDock.ClockArea");

            var line = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            clockText = new TextBlock
            {
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(232, 255, 255, 255)),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Microsoft YaHei UI"),
                Margin = new Thickness(0, 0, 4, 0)
            };
            var dot = new TextBlock
            {
                Text = "·",
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(86, 255, 255, 255)),
                FontSize = 11.0,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Microsoft YaHei UI"),
                Margin = new Thickness(0, 0, 4, 0)
            };
            dateText = new TextBlock
            {
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(153, 255, 255, 255)),
                FontSize = 10.75,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Microsoft YaHei UI")
            };
            System.Windows.Automation.AutomationProperties.SetName(clockText, "ModernDock.Clock.Time");
            System.Windows.Automation.AutomationProperties.SetAutomationId(clockText, "ModernDock.Clock.Time");
            System.Windows.Automation.AutomationProperties.SetName(dateText, "ModernDock.Clock.Date");
            System.Windows.Automation.AutomationProperties.SetAutomationId(dateText, "ModernDock.Clock.Date");
            line.Children.Add(clockText);
            line.Children.Add(dot);
            line.Children.Add(dateText);
            area.Content = line;
            area.MouseEnter += (s, e) => {
                area.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(10, 255, 255, 255));
            };
            area.MouseLeave += (s, e) => {
                area.Background = System.Windows.Media.Brushes.Transparent;
            };
            area.Click += (s, e) => {
                OpenSystemSettings("ms-settings:dateandtime");
            };
            return area;
        }

        private System.Windows.Shapes.Rectangle CreateSystemSeparator()
        {
            return CreateSystemSeparator(3, 3);
        }

        private System.Windows.Shapes.Rectangle CreateSystemSeparator(int leftMargin, int rightMargin)
        {
            return new System.Windows.Shapes.Rectangle
            {
                Width = 1,
                Height = 20,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(36, 255, 255, 255)),
                Margin = new Thickness(leftMargin, 0, rightMargin, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private Button CreateVolumeArea()
        {
            var area = new Button
            {
                Width = 36,
                Height = 38,
                // A one-alpha hit surface keeps the whole 36 DIP control
                // interactive on an AllowsTransparency window without
                // producing a visible default background.
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                OverridesDefaultStyle = true,
                Focusable = false,
                IsTabStop = false,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = CreateToolTip("音量\n滚轮调节 · 点击静音")
            };
            var buttonTemplate = new ControlTemplate(typeof(Button));
            var buttonBorder = new FrameworkElementFactory(typeof(Border));
            buttonBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            buttonBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            buttonBorder.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            buttonBorder.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            var buttonContent = new FrameworkElementFactory(typeof(ContentPresenter));
            buttonContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            buttonContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            buttonContent.SetValue(ContentPresenter.ContentSourceProperty, "Content");
            buttonBorder.AppendChild(buttonContent);
            buttonTemplate.VisualTree = buttonBorder;
            area.Template = buttonTemplate;
            System.Windows.Automation.AutomationProperties.SetName(area, "ModernDock.Volume");
            System.Windows.Automation.AutomationProperties.SetAutomationId(area, "ModernDock.VolumeArea");

            volumeVisual = new Grid { Width = 18, Height = 18 };
            volumeSpeakerGlyph = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M1,7 L5,7 L10,3 L10,15 L5,11 L1,11 Z"),
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(232, 255, 255, 255)),
                Stretch = Stretch.Uniform,
                Width = 18,
                Height = 18
            };
            volumeWaveGlyph = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M12,6 C14,8 14,10 12,12 M14,4 C18,8 18,10 14,14"),
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 255, 255, 255)),
                StrokeThickness = 1.35,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Fill = System.Windows.Media.Brushes.Transparent,
                Stretch = Stretch.Uniform,
                Width = 18,
                Height = 18
            };
            volumeMuteGlyph = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M12,5 L18,13 M18,5 L12,13"),
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(232, 255, 255, 255)),
                StrokeThickness = 1.45,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Fill = System.Windows.Media.Brushes.Transparent,
                Stretch = Stretch.Uniform,
                Width = 18,
                Height = 18,
                Visibility = Visibility.Collapsed
            };
            volumeVisual.Children.Add(volumeSpeakerGlyph);
            volumeVisual.Children.Add(volumeWaveGlyph);
            volumeVisual.Children.Add(volumeMuteGlyph);
            System.Windows.Automation.AutomationProperties.SetName(volumeVisual, "ModernDock.VolumeGlyph");
            area.Content = volumeVisual;

            area.MouseEnter += (s, e) => {
                SetVolumeAreaBackground(22);
                if (volumeFlyout != null) volumeFlyout.SetTargetPointerOver(true);
            };
            area.MouseLeave += (s, e) => {
                SetVolumeAreaBackground(0);
                if (volumeFlyout != null) volumeFlyout.SetTargetPointerOver(false);
            };
            area.PreviewMouseDown += (s, e) => SetVolumeAreaBackground(36);
            area.PreviewMouseUp += (s, e) => SetVolumeAreaBackground(area.IsMouseOver ? (byte)22 : (byte)0);
            area.PreviewMouseWheel += (s, e) => {
                EntryPoint.Log("system_volume.input=wheel delta=" + e.Delta);
                AdjustVolumeByStep(e.Delta > 0 ? 1 : -1);
                e.Handled = true;
            };
            // ButtonBase owns the left-button routing, so use its Click event
            // instead of an instance MouseLeftButtonUp handler that can be
            // marked handled before it reaches this element.
            area.Click += (s, e) => {
                EntryPoint.Log("system_volume.input=left_click");
                ToggleVolumeMute();
            };
            area.MouseDown += (s, e) => {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    EntryPoint.Log("system_volume.input=middle_click");
                    ToggleVolumeMute();
                    e.Handled = true;
                }
            };
            area.MouseRightButtonUp += (s, e) => {
                OpenSystemSettings("ms-settings:sound");
                e.Handled = true;
            };
            return area;
        }

        private void SetVolumeAreaBackground(byte alpha)
        {
            if (volumeArea == null) return;
            volumeArea.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
                alpha == 0 ? (byte)1 : alpha, 255, 255, 255));
        }

        private void HandleVolumeFlyoutValueChanged(float scalar)
        {
            try
            {
                if (shutdownStarted) return;
                if (AudioStateReader.TrySetMasterVolume(scalar))
                {
                    UpdateVolumeStateFromSystem();
                    ShowVolumeFlyout();
                }
                else
                {
                    EntryPoint.Log("system_volume.flyout_set_failed scalar=" + scalar.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("system_volume.flyout_set", ex);
            }
        }

        private void AdjustVolumeByStep(int direction)
        {
            try
            {
                float current;
                if (!AudioStateReader.TryGetMasterVolume(out current))
                {
                    ShowVolumeFlyout();
                    return;
                }

                float next = current + (direction >= 0 ? 0.03f : -0.03f);
                next = Math.Max(0.0f, Math.Min(1.0f, next));
                if (AudioStateReader.TrySetMasterVolume(next))
                {
                    UpdateVolumeStateFromSystem();
                    ShowVolumeFlyout();
                }
                else
                {
                    EntryPoint.Log("system_volume.wheel_set_failed direction=" + direction);
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("system_volume.wheel", ex);
            }
        }

        private void ToggleVolumeMute()
        {
            try
            {
                bool muted;
                if (AudioStateReader.TryGetMuteState(out muted) && AudioStateReader.TrySetMuteState(!muted))
                {
                    UpdateVolumeStateFromSystem();
                    ShowVolumeFlyout();
                }
                else
                {
                    EntryPoint.Log("system_volume.mute_toggle_failed");
                    UpdateVolumeStateFromSystem();
                    ShowVolumeFlyout();
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("system_volume.mute_toggle", ex);
            }
        }

        private void ShowVolumeFlyout()
        {
            try
            {
                if (shutdownStarted || volumeFlyout == null) return;
                UpdateVolumeStateFromSystem();
                volumeFlyout.Show();
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("system_volume.flyout_show", ex);
            }
        }

        private void StartSystemInfoTimer()
        {
            try
            {
                if (systemInfoTimer != null) return;
                UpdateSystemInfo();
                systemInfoTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
                systemInfoTimer.Tick += (s, e) => UpdateSystemInfo();
                systemInfoTimer.Start();
                StartVolumeStateTimer();
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("startup.system_info_timer", ex);
            }
        }

        public void UpdateSystemInfo()
        {
            try
            {
                DateTime now = DateTime.Now;
                string currentClock = SystemInfoFormatter.FormatClock(now);
                string currentDate = SystemInfoFormatter.FormatDate(now);
                if (clockText != null)
                {
                    clockText.Text = currentClock;
                    System.Windows.Automation.AutomationProperties.SetName(clockText, currentClock);
                }
                if (dateText != null)
                {
                    dateText.Text = currentDate;
                    System.Windows.Automation.AutomationProperties.SetName(dateText, currentDate);
                }
                UpdateVolumeStateFromSystem();
                UpdateVolumeVisual();
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("system_info.update", ex);
            }
        }

        private void UpdateVolumeVisual()
        {
            if (volumeVisual == null) return;

            bool showMute = volumeStateKnown && volumeMuted;
            bool showWaves = volumeStateKnown && !volumeMuted;
            if (volumeSpeakerGlyph != null) volumeSpeakerGlyph.Visibility = Visibility.Visible;
            if (volumeWaveGlyph != null) volumeWaveGlyph.Visibility = showWaves ? Visibility.Visible : Visibility.Collapsed;
            if (volumeMuteGlyph != null) volumeMuteGlyph.Visibility = showMute ? Visibility.Visible : Visibility.Collapsed;

            string accessibilityName = !volumeStateKnown
                ? "音量（状态未知）"
                : (volumeMuted ? "音量（已静音）" : "音量");
            // Keep the automation identity stable so deployment/runtime probes and
            // assistive technology can find the volume control after its state changes.
            // Put the live state in HelpText instead of replacing the control name.
            System.Windows.Automation.AutomationProperties.SetName(volumeArea, "ModernDock.Volume");
            System.Windows.Automation.AutomationProperties.SetHelpText(volumeArea, accessibilityName);
            System.Windows.Automation.AutomationProperties.SetName(volumeVisual, accessibilityName + " 图标");
            if (volumeFlyout != null)
            {
                volumeFlyout.Refresh(volumeLevel, volumeMuted, volumeLevelKnown, volumeStateKnown);
            }
        }

        private void UpdateVolumeStateFromSystem()
        {
            float level;
            if (AudioStateReader.TryGetMasterVolume(out level))
            {
                volumeLevel = Math.Max(0.0f, Math.Min(1.0f, level));
                volumeLevelKnown = true;
            }
            else
            {
                volumeLevelKnown = false;
            }

            bool muted;
            if (AudioStateReader.TryGetMuteState(out muted))
            {
                volumeMuted = muted;
                volumeStateKnown = true;
            }
            else
            {
                volumeStateKnown = false;
            }
            UpdateVolumeVisual();
        }

        private void StartVolumeStateTimer()
        {
            if (volumeStateTimer != null) return;
            volumeStateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            volumeStateTimer.Tick += (s, e) => {
                if (!shutdownStarted) UpdateVolumeStateFromSystem();
            };
            volumeStateTimer.Start();
        }

        private void QueueVolumeStateRefresh()
        {
            try
            {
                if (shutdownStarted || Dispatcher == null || Dispatcher.HasShutdownStarted) return;
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(UpdateVolumeStateFromSystem));
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("system_volume.state_refresh", ex);
            }
        }

        public void SendVolumeKey(byte virtualKey)
        {
            try
            {
                keybd_event(virtualKey, 0, 0, 0);
                keybd_event(virtualKey, 0, 0x0002, 0);
                QueueVolumeStateRefresh();
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("system_volume.key=" + virtualKey, ex);
            }
        }

        public void ShowNativeTaskbar()
        {
            try
            {
                nativeTaskbarVisibleOverride = true;
                IntPtr hTray = FindWindow("Shell_TrayWnd", null);
                if (hTray != IntPtr.Zero) ShowWindow(hTray, SW_SHOW);
                EntryPoint.Log("taskbar.explicit_show");
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("taskbar.explicit_show", ex);
            }
        }

        public void HideNativeTaskbar()
        {
            try
            {
                nativeTaskbarVisibleOverride = false;
                HideNativeTaskbarCore();
                EntryPoint.Log("taskbar.explicit_hide");
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("taskbar.explicit_hide", ex);
            }
        }

        private void ShowDockContextPopup()
        {
            try
            {
                if (currentFluentPopup != null && currentFluentPopup.IsOpen) currentFluentPopup.IsOpen = false;
                var popup = new Popup
                {
                    PlacementTarget = dockBorder,
                    Placement = PlacementMode.Top,
                    VerticalOffset = -10,
                    HorizontalOffset = -100,
                    AllowsTransparency = true,
                    StaysOpen = false,
                    PopupAnimation = PopupAnimation.Fade
                };
                var border = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(228, 26, 24, 30)),
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 255, 255)),
                    Padding = new Thickness(4, 5, 4, 5),
                    Width = 230,
                    Effect = new DropShadowEffect { Color = System.Windows.Media.Colors.Black, BlurRadius = 24, ShadowDepth = 6, Opacity = 0.55 }
                };
                var stack = new StackPanel();
                stack.Children.Add(CreateFluentRow("▣", "显示 Windows 系统任务栏", () => {
                    popup.IsOpen = false;
                    ShowNativeTaskbar();
                }));
                stack.Children.Add(CreateFluentRow("▣", "隐藏 Windows 系统任务栏", () => {
                    popup.IsOpen = false;
                    HideNativeTaskbar();
                }));
                stack.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Height = 1,
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 255, 255, 255)),
                    Margin = new Thickness(6, 4, 6, 4)
                });
                stack.Children.Add(CreateFluentRow("🚪", "退出 ModernDock", () => {
                    popup.IsOpen = false;
                    RequestShutdown("dock.context_menu");
                }));
                border.Child = stack;
                popup.Child = border;
                popup.Closed += (s, e) => { if (currentFluentPopup == popup) currentFluentPopup = null; };
                currentFluentPopup = popup;
                popup.IsOpen = true;
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("ui.dock_context_popup", ex);
            }
        }

        private void OpenSystemSettings(string uri)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("system.settings uri=" + uri, ex);
            }
        }

        private void LoadFixedDockItems()
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                fixedItems = new List<DockItem>();

                if (File.Exists(configPath))
                {
                    Stopwatch configTimer = Stopwatch.StartNew();
                    try
                    {
                        LoadConfigFromFile();
                    }
                    finally
                    {
                        EntryPoint.LogTiming("startup.config_and_icon_load", configTimer);
                    }
                }
                else
                {
                    LoadDefaultFixedItems();
                    Stopwatch saveTimer = Stopwatch.StartNew();
                    SaveConfigToFile();
                    EntryPoint.LogTiming("startup.initial_config_save", saveTimer);
                }
            }
            finally
            {
                EntryPoint.LogTiming("startup.fixed_items_load", timer);
            }
        }

        private void LoadDefaultFixedItems()
        {
            Stopwatch timer = Stopwatch.StartNew();
            fixedItems.Clear();

            fixedItems.Add(new DockItem {
                Title = "File Explorer",
                TargetPath = "explorer.exe",
                WindowClassMatch = "CabinetWClass",
                IsFixed = true
            });
            fixedItems.Add(new DockItem {
                Title = "Recycle Bin",
                TargetPath = "explorer.exe",
                Arguments = "shell:RecycleBinFolder",
                IsFixed = true
            });
            fixedItems.Add(new DockItem {
                Title = "Settings",
                TargetPath = "ms-settings:",
                IsFixed = true
            });

            Stopwatch iconTimer = Stopwatch.StartNew();
            foreach (var item in fixedItems)
            {
                item.IconSource = LoadFixedItemIcon(item);
            }
            EntryPoint.LogTiming("startup.default_icon_load", iconTimer);
            EntryPoint.LogTiming("startup.default_fixed_items", timer);
        }

        private bool SaveConfigToFile()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var item in fixedItems)
                {
                    sb.AppendLine(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}",
                        item.Title ?? "",
                        item.TargetPath ?? "",
                        item.Arguments ?? "",
                        item.PathMatch ?? "",
                        item.ProcessNameMatch ?? "",
                        item.WindowClassMatch ?? "",
                        item.IconFile ?? ""));
                }
                AtomicFileWriter.WriteAllText(configPath, sb.ToString(), Encoding.UTF8);
                DockMetadataStore.Save(metadataPath, fixedItems);
                return true;
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("config.save path=" + configPath, ex);
                return false;
            }
        }

        private void LoadConfigFromFile()
        {
            try
            {
                fixedItems.Clear();
                var seenIdentityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string[] lines = File.ReadAllLines(configPath, Encoding.UTF8);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split('\t');
                    if (parts.Length >= 7)
                    {
                        var item = new DockItem
                        {
                            Title = parts[0],
                            TargetPath = parts[1],
                            Arguments = parts[2],
                            PathMatch = parts[3],
                            ProcessNameMatch = parts[4],
                            WindowClassMatch = parts[5],
                            IconFile = parts[6],
                            IsFixed = true
                        };

                        string identityKey = ApplicationIdentityResolver.GetFixedIdentityKey(item);
                        if (!seenIdentityKeys.Add(identityKey))
                        {
                            EntryPoint.Log("config.duplicate_fixed_item skipped title=" + item.Title + " identity=" + identityKey);
                            continue;
                        }

                        if (item.Title == "开始菜单" || item.Title == "Start Menu") item.CustomAction = () => { SendWinKey(); };

                        item.IconSource = LoadFixedItemIcon(item);

                        fixedItems.Add(item);
                    }
                }
                DockMetadataStore.Apply(metadataPath, fixedItems);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("config.load path=" + configPath, ex);
                LoadDefaultFixedItems();
            }
        }

        private ImageSource LoadFixedItemIcon(DockItem item)
        {
            try
            {
                if (item == null) return null;

                if (!string.IsNullOrEmpty(item.IconFile))
                {
                    ImageSource configured = LoadPngIcon(System.IO.Path.Combine(iconDirectory, item.IconFile));
                    if (configured != null) return configured;
                }

                if ((item.Arguments ?? string.Empty).IndexOf("shell:RecycleBinFolder", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    BitmapSource stock;
                    if (IconService.TryExtractRecycleBinStockIcon(out stock) && stock != null) return stock;
                }

                string target = (item.TargetPath ?? string.Empty).Trim().Trim('"');
                if (string.Equals(target, "ms-settings:", StringComparison.OrdinalIgnoreCase))
                {
                    return IconService.GetGenericApplicationIcon();
                }

                if (string.Equals(target, "explorer.exe", StringComparison.OrdinalIgnoreCase))
                {
                    target = System.IO.Path.Combine(Environment.SystemDirectory, "explorer.exe");
                }

                if (!string.IsNullOrEmpty(target) &&
                    (!ApplicationIdentityResolver.IsFullyQualifiedPath(target) || File.Exists(target)))
                {
                    return IconService.ExtractBest(target);
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("startup.fixed_icon title=" + (item == null ? string.Empty : item.Title), ex);
            }

            return null;
        }

        private ImageSource LoadPngIcon(string iconPng)
        {
            return IconService.LoadImage(iconPng);
        }

        private void BuildFixedUI()
        {
            fixedPanel.Children.Clear();
            foreach (var item in fixedItems)
            {
                var container = CreateItemContainer(item);
                fixedPanel.Children.Add(container);
            }
        }

        public bool MoveFixedItem(DockItem item, int targetIndex)
        {
            try
            {
                int originalIndex = fixedItems.IndexOf(item);
                if (!FixedItemOrder.Move(fixedItems, item, targetIndex)) return false;
                if (!SaveConfigToFile())
                {
                    fixedItems.Remove(item);
                    if (originalIndex >= 0 && originalIndex <= fixedItems.Count) fixedItems.Insert(originalIndex, item);
                    return false;
                }
                BuildFixedUI();
                PositionDock();
                return true;
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("ui.fixed_reorder", ex);
                return false;
            }
        }

        private Grid CreateItemContainer(DockItem item)
        {
            var itemContainer = new Grid
            {
                Width = 40,
                Height = 44,
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                ToolTip = CreateToolTip(item.Title),
                Background = System.Windows.Media.Brushes.Transparent
            };
            System.Windows.Automation.AutomationProperties.SetName(itemContainer, item.Title ?? string.Empty);
            string automationId = item.IsFixed
                ? "ModernDock.Fixed." + (item.Title ?? string.Empty)
                : "ModernDock.Dynamic." + (item.DynamicIdentityKey ?? string.Empty);
            if (!string.IsNullOrEmpty(automationId))
            {
                System.Windows.Automation.AutomationProperties.SetAutomationId(itemContainer, automationId);
            }

            var scaleTransform = new ScaleTransform(1.0, 1.0, 20, 22);
            itemContainer.RenderTransform = scaleTransform;

            BitmapSource rawIcon = item.IconSource as BitmapSource;
            BitmapSource visualIcon = rawIcon == null ? null : IconVisualNormalizer.Normalize(rawIcon);
            var img = new System.Windows.Controls.Image
            {
                Source = visualIcon ?? item.IconSource,
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 3, 0, 0),
                Effect = new DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.Black,
                    BlurRadius = 4,
                    ShadowDepth = 1,
                    Opacity = 0.3
                }
            };
            // Image is the first concrete automation peer under the item
            // container. Keep the semantic identity on that peer as well as
            // the visual Grid so controlled UI tools can locate the live
            // fixed/dynamic item without relying on screen coordinates.
            System.Windows.Automation.AutomationProperties.SetName(img, item.Title ?? string.Empty);
            if (!string.IsNullOrEmpty(automationId))
            {
                System.Windows.Automation.AutomationProperties.SetAutomationId(img, automationId);
            }
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            itemContainer.Children.Add(img);

            var dot = new Ellipse
            {
                Width = 4.0,
                Height = 4.0,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(245, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 1),
                Visibility = item.IsFixed ? Visibility.Hidden : Visibility.Visible,
                Effect = new DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.White,
                    BlurRadius = 6,
                    ShadowDepth = 0,
                    Opacity = 0.95
                }
            };
            item.IndicatorDot = dot;
            itemContainer.Children.Add(dot);

            itemContainer.MouseEnter += (s, e) => {
                try
                {
                    var animX = new DoubleAnimation(1.19, TimeSpan.FromMilliseconds(80)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                    var animY = new DoubleAnimation(1.19, TimeSpan.FromMilliseconds(80)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animX);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animY);
                }
                catch (Exception ex)
                {
                    EntryPoint.LogException("ui.item.mouse_enter", ex);
                }
            };

            itemContainer.MouseLeave += (s, e) => {
                try
                {
                    var animX = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                    var animY = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animX);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animY);
                }
                catch (Exception ex)
                {
                    EntryPoint.LogException("ui.item.mouse_leave", ex);
                }
            };

            itemContainer.PreviewMouseLeftButtonDown += (s, e) => {
                try
                {
                    if (!item.IsFixed || fixedPanel == null) return;
                    dragItem = item;
                    dragContainer = itemContainer;
                    dragStartPoint = e.GetPosition(fixedPanel);
                    dragTracking = true;
                    dragInProgress = false;
                    itemContainer.CaptureMouse();
                }
                catch (Exception ex)
                {
                    EntryPoint.LogException("ui.item.drag_start", ex);
                }
            };

            itemContainer.PreviewMouseMove += (s, e) => {
                try
                {
                    if (!item.IsFixed || !dragTracking || dragItem != item || e.LeftButton != MouseButtonState.Pressed)
                    {
                        return;
                    }

                    System.Windows.Point currentPoint = e.GetPosition(fixedPanel);
                    if (!dragInProgress &&
                        Math.Abs(currentPoint.X - dragStartPoint.X) >= FixedDragThreshold)
                    {
                        dragInProgress = true;
                        itemContainer.Opacity = 0.72;
                    }

                    if (dragInProgress)
                    {
                        ReorderFixedContainerVisual(itemContainer, currentPoint.X);
                        e.Handled = true;
                    }
                }
                catch (Exception ex)
                {
                    EntryPoint.LogException("ui.item.drag_move", ex);
                }
            };

            itemContainer.PreviewMouseLeftButtonUp += (s, e) => {
                try
                {
                    e.Handled = true;
                    bool wasDragging = item.IsFixed && dragTracking && dragItem == item && dragInProgress;
                    if (item.IsFixed && dragTracking && dragItem == item)
                    {
                        dragTracking = false;
                        dragInProgress = false;
                        dragItem = null;
                        dragContainer = null;
                        itemContainer.ReleaseMouseCapture();
                    }

                    if (wasDragging)
                    {
                        int targetIndex = fixedPanel.Children.IndexOf(itemContainer);
                        itemContainer.Opacity = 1.0;
                        MoveFixedItem(item, targetIndex);
                        return;
                    }

                    itemContainer.Opacity = 1.0;
                    var bounceAnim = new DoubleAnimation(0.85, TimeSpan.FromMilliseconds(60)) { AutoReverse = true };
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, bounceAnim);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, bounceAnim);

                    ExecuteOrToggleItem(item, false);
                }
                catch (Exception ex)
                {
                    EntryPoint.LogException("ui.item.left_click", ex);
                }
            };

            itemContainer.PreviewMouseRightButtonUp += (s, e) => {
                try
                {
                    e.Handled = true;
                    ShowWindows11FluentPopup(item, itemContainer);
                }
                catch (Exception ex)
                {
                    EntryPoint.LogException("ui.item.right_click", ex);
                }
            };

            return itemContainer;
        }

        private void ReorderFixedContainerVisual(Grid container, double x)
        {
            if (fixedPanel == null || container == null) return;

            int targetIndex = 0;
            foreach (UIElement child in fixedPanel.Children)
            {
                if (child == container) continue;
                System.Windows.Point origin = child.TranslatePoint(new System.Windows.Point(0, 0), fixedPanel);
                double midpoint = origin.X + (child.RenderSize.Width / 2.0);
                if (x > midpoint) targetIndex++;
                else break;
            }

            int currentIndex = fixedPanel.Children.IndexOf(container);
            if (currentIndex < 0 || currentIndex == targetIndex) return;
            fixedPanel.Children.RemoveAt(currentIndex);
            if (targetIndex > fixedPanel.Children.Count) targetIndex = fixedPanel.Children.Count;
            fixedPanel.Children.Insert(targetIndex, container);
        }

        private void ShowWindows11FluentPopup(DockItem item, FrameworkElement placementTarget)
        {
            try
            {
                if (currentFluentPopup != null && currentFluentPopup.IsOpen)
                {
                    currentFluentPopup.IsOpen = false;
                }

                var popup = new Popup
                {
                    PlacementTarget = placementTarget,
                    Placement = PlacementMode.Top,
                    VerticalOffset = -10,
                    HorizontalOffset = -40,
                    AllowsTransparency = true,
                    StaysOpen = false,
                    PopupAnimation = PopupAnimation.Fade
                };

                var rootBorder = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(228, 26, 24, 30)),
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 255, 255)),
                    Padding = new Thickness(4, 5, 4, 5),
                    Width = 210,
                    Effect = new DropShadowEffect
                    {
                        Color = System.Windows.Media.Colors.Black,
                        BlurRadius = 24,
                        ShadowDepth = 6,
                        Opacity = 0.55
                    }
                };

                var menuStack = new StackPanel();

                // Header Title
                var headerGrid = new Grid { Margin = new Thickness(8, 5, 8, 4) };
                var headerText = new TextBlock
                {
                    Text = item.Title,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(170, 255, 255, 255)),
                    FontSize = 12.0,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI, Segoe UI Variable Display")
                };
                headerGrid.Children.Add(headerText);
                menuStack.Children.Add(headerGrid);

                IList<WindowSnapshot> windowsForItem = GetWindowsForItem(item);
                EntryPoint.Log("ui.item_popup title=" + (item.Title ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ') +
                    " windows=" + windowsForItem.Count + " fixed=" + item.IsFixed +
                    " identity=" + (item.IsFixed ? "fixed" : (item.DynamicIdentityKey ?? string.Empty)));
                if (windowsForItem.Count > 0)
                {
                    menuStack.Children.Add(CreateFluentRow("▤", "当前窗口", null));
                    int windowLimit = Math.Min(8, windowsForItem.Count);
                    for (int i = 0; i < windowLimit; i++)
                    {
                        WindowSnapshot window = windowsForItem[i];
                        string windowTitle = GetShortWindowTitle(window);
                        menuStack.Children.Add(CreateFluentRow("　", windowTitle, () => {
                            popup.IsOpen = false;
                            ToggleWindow(window.Handle, false);
                        }));
                    }
                }

                menuStack.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Height = 1,
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 255, 255, 255)),
                    Margin = new Thickness(6, 4, 6, 4)
                });

                if (item.IsFixed)
                {
                    menuStack.Children.Add(CreateFluentRow("🖼", "图标：自动重新获取", () => {
                        popup.IsOpen = false;
                        RefreshItemIcon(item, false);
                    }));
                    menuStack.Children.Add(CreateFluentRow("▣", "图标：从程序重新读取", () => {
                        popup.IsOpen = false;
                        RefreshItemIcon(item, true);
                    }));
                    menuStack.Children.Add(CreateFluentRow("▧", "图标：选择 PNG", () => {
                        popup.IsOpen = false;
                        ChooseCustomIcon(item, "PNG 文件|*.png|所有文件|*.*");
                    }));
                    menuStack.Children.Add(CreateFluentRow("▧", "图标：选择 ICO", () => {
                        popup.IsOpen = false;
                        ChooseCustomIcon(item, "ICO 文件|*.ico|所有文件|*.*");
                    }));
                    menuStack.Children.Add(CreateFluentRow("✎", "编辑", () => {
                        popup.IsOpen = false;
                        ShowEditItemDialog(item);
                    }));
                }

                // Item 1: 打开 / 新建窗口
                menuStack.Children.Add(CreateFluentRow("🗔", "打开 / 新建窗口", () => {
                    popup.IsOpen = false;
                    LaunchNewInstance(item);
                }));

                // Item 2: 最小化 / 还原
                menuStack.Children.Add(CreateFluentRow("➖", "最小化 / 还原窗口", () => {
                    popup.IsOpen = false;
                    ExecuteOrToggleItem(item, false);
                }));

                // Item 3: 固定到 Dock / 从 Dock 取消固定
                if (item.IsFixed)
                {
                    menuStack.Children.Add(CreateFluentRow("📌", "从 Dock 取消固定", () => {
                        popup.IsOpen = false;
                        UnpinItem(item);
                    }));
                }
                else
                {
                    menuStack.Children.Add(CreateFluentRow("📌", "固定到 Dock", () => {
                        popup.IsOpen = false;
                        PinItem(item);
                    }));
                }

                // Item 4: 关闭应用 (退出)
                menuStack.Children.Add(CreateFluentRow("✕", "关闭应用 (退出)", () => {
                    popup.IsOpen = false;
                    CloseApplication(item);
                }));

                menuStack.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Height = 1,
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 255, 255, 255)),
                    Margin = new Thickness(6, 4, 6, 4)
                });

                // Item 5: 退出 ModernDock
                menuStack.Children.Add(CreateFluentRow("🚪", "退出 ModernDock", () => {
                    popup.IsOpen = false;
                    RequestShutdown("menu");
                }));

                rootBorder.Child = menuStack;
                popup.Child = rootBorder;

                popup.Closed += (s, e) => {
                    currentFluentPopup = null;
                };

                currentFluentPopup = popup;
                popup.IsOpen = true;
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("ui.popup", ex);
            }
        }

        public IList<WindowSnapshot> GetWindowsForItem(DockItem item)
        {
            var result = new List<WindowSnapshot>();
            if (item == null) return result;

            try
            {
                if (!item.IsFixed && !string.IsNullOrEmpty(item.DynamicIdentityKey))
                {
                    ApplicationGroup group;
                    if (dynamicGroupsMap.TryGetValue(item.DynamicIdentityKey, out group) && group != null)
                    {
                        foreach (WindowSnapshot window in group.Windows) result.Add(window);
                    }
                    return result;
                }

                if (!item.IsFixed || item.CustomAction != null) return result;
                foreach (WindowSnapshot window in GetTopLevelWindows(true, null))
                {
                    FixedItemMatch match = FixedItemMatcher.Resolve(fixedItems, window);
                    if (!match.IsAmbiguous && match.Item == item) result.Add(window);
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("ui.window_list item=" + item.Title, ex);
            }
            return result;
        }

        private string GetShortWindowTitle(WindowSnapshot window)
        {
            string title = window == null ? string.Empty : window.WindowTitle;
            if (string.IsNullOrWhiteSpace(title)) title = window == null ? string.Empty : window.ProcessName;
            if (string.IsNullOrWhiteSpace(title)) title = "未命名窗口";
            return title.Length > MaxWindowTitleLength ? title.Substring(0, MaxWindowTitleLength) + "…" : title;
        }

        private const int MaxWindowTitleLength = 32;

        public bool SetCustomIcon(DockItem item, string sourcePath)
        {
            try
            {
                if (item == null || !item.IsFixed || string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return false;
                ImageSource loaded = IconService.LoadImage(sourcePath);
                BitmapSource bitmap = loaded as BitmapSource;
                if (bitmap == null) return false;

                string iconFileName = "custom_" + SanitizeFileName(item.Title) + "_" + Guid.NewGuid().ToString("N") + ".png";
                string iconFullPath = System.IO.Path.Combine(iconDirectory, iconFileName);
                if (!IconService.SavePng(bitmap, iconFullPath)) return false;

                string previousIcon = item.IconFile;
                ImageSource previousIconSource = item.IconSource;
                item.IconFile = iconFileName;
                IconService.Invalidate(iconFullPath);
                item.IconSource = IconService.LoadImage(iconFullPath);
                if (!SaveConfigToFile())
                {
                    item.IconFile = previousIcon;
                    item.IconSource = previousIconSource;
                    try { if (File.Exists(iconFullPath)) File.Delete(iconFullPath); }
                    catch (Exception cleanupEx) { EntryPoint.LogException("icon.custom_save_cleanup path=" + iconFullPath, cleanupEx); }
                    return false;
                }
                BuildFixedUI();
                PositionDock();
                DeleteGeneratedIconIfReplaced(previousIcon, iconFileName);
                return true;
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("icon.custom_set item=" + (item == null ? "<null>" : item.Title), ex);
                return false;
            }
        }

        public bool RefreshItemIcon(DockItem item, bool fromExecutable)
        {
            try
            {
                if (item == null || !item.IsFixed) return false;
                if (!fromExecutable && IsGeneratedCustomIcon(item.IconFile))
                {
                    EntryPoint.Log("icon.auto_refresh_skipped_custom title=" + item.Title);
                    return false;
                }

                string sourcePath = FindIconSourcePath(item);
                if (string.IsNullOrEmpty(sourcePath))
                {
                    EntryPoint.Log("icon.refresh_source_missing title=" + item.Title);
                    return false;
                }

                IconService.InvalidateExecutable(sourcePath);
                ImageSource source = IconService.ExtractBest(sourcePath);
                BitmapSource bitmap = source as BitmapSource;
                if (bitmap == null) return false;

                string iconFileName = "auto_" + SanitizeFileName(item.Title) + ".png";
                string iconFullPath = System.IO.Path.Combine(iconDirectory, iconFileName);
                if (!IconService.SavePng(bitmap, iconFullPath)) return false;

                string previousIcon = item.IconFile;
                ImageSource previousIconSource = item.IconSource;
                item.IconFile = iconFileName;
                IconService.Invalidate(iconFullPath);
                item.IconSource = IconService.LoadImage(iconFullPath);
                if (!SaveConfigToFile())
                {
                    item.IconFile = previousIcon;
                    item.IconSource = previousIconSource;
                    try { if (File.Exists(iconFullPath)) File.Delete(iconFullPath); }
                    catch (Exception cleanupEx) { EntryPoint.LogException("icon.refresh_save_cleanup path=" + iconFullPath, cleanupEx); }
                    return false;
                }
                BuildFixedUI();
                PositionDock();
                DeleteGeneratedIconIfReplaced(previousIcon, iconFileName);
                EntryPoint.Log("icon.refresh_completed title=" + item.Title + " source=" + sourcePath);
                return true;
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("icon.refresh item=" + (item == null ? "<null>" : item.Title), ex);
                return false;
            }
        }

        private void ChooseCustomIcon(DockItem item, string filter)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = filter,
                    CheckFileExists = true,
                    Multiselect = false,
                    Title = "选择 Dock 图标"
                };
                if (dialog.ShowDialog() == true) SetCustomIcon(item, dialog.FileName);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("icon.custom_dialog", ex);
            }
        }

        public bool ShowEditItemDialog(DockItem item)
        {
            if (item == null || !item.IsFixed) return false;
            try
            {
                string originalTitle = item.Title;
                string originalTarget = item.TargetPath;
                string originalArguments = item.Arguments;
                string originalPathMatch = item.PathMatch;
                string originalProcessNameMatch = item.ProcessNameMatch;
                string originalWindowClassMatch = item.WindowClassMatch;
                string originalIconFile = item.IconFile;
                string originalShortcutSource = item.ShortcutSource;
                bool originalPathMatchAuto = item.AutoDerivedPathMatch;
                bool originalProcessNameMatchAuto = item.AutoDerivedProcessNameMatch;
                ImageSource originalIconSource = item.IconSource;
                string selectedIconPath = null;
                var editor = new Window
                {
                    Title = "编辑 Dock 项目 - " + (item.Title ?? string.Empty),
                    Width = 460,
                    Height = 270,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false,
                    Owner = this
                };

                var root = new Grid { Margin = new Thickness(14) };
                for (int i = 0; i < 5; i++) root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
                root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                TextBox titleBox = AddEditorField(root, 0, "显示名称", item.Title);
                TextBox pathBox = AddEditorField(root, 1, "程序路径", item.TargetPath);
                TextBox argsBox = AddEditorField(root, 2, "启动参数", item.Arguments);
                TextBlock iconText = new TextBlock
                {
                    Text = string.IsNullOrEmpty(item.IconFile) ? "自动图标" : item.IconFile,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetRow(iconText, 3);
                Grid.SetColumn(iconText, 1);
                root.Children.Add(iconText);

                var chooseIconButton = new Button { Content = "选择图标", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 4, 0, 4) };
                chooseIconButton.Click += (s, e) => {
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "图标文件|*.png;*.ico|PNG 文件|*.png|ICO 文件|*.ico|所有文件|*.*",
                        CheckFileExists = true,
                        Multiselect = false,
                        Title = "选择 Dock 图标"
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        selectedIconPath = dialog.FileName;
                        iconText.Text = System.IO.Path.GetFileName(selectedIconPath);
                    }
                };
                Grid.SetRow(chooseIconButton, 4);
                Grid.SetColumn(chooseIconButton, 1);
                root.Children.Add(chooseIconButton);

                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var cancelButton = new Button { Content = "取消", IsCancel = true, Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(0, 10, 8, 0) };
                var saveButton = new Button { Content = "保存", IsDefault = true, Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(0, 10, 0, 0) };
                bool saved = false;
                saveButton.Click += (s, e) => {
                    if (string.IsNullOrWhiteSpace(titleBox.Text)) return;
                    item.Title = titleBox.Text.Trim();
                    item.TargetPath = pathBox.Text.Trim();
                    item.Arguments = argsBox.Text;
                    UpdateMatchingFieldsAfterPathChange(item, originalTarget);
                    saved = true;
                    editor.DialogResult = true;
                };
                buttons.Children.Add(cancelButton);
                buttons.Children.Add(saveButton);
                Grid.SetRow(buttons, 6);
                Grid.SetColumn(buttons, 1);
                root.Children.Add(buttons);
                editor.Content = root;

                bool? result = editor.ShowDialog();
                if (result != true || !saved) return false;

                if (HasFixedIdentityConflict(item))
                {
                    RestoreEditedItem(item, originalTitle, originalTarget, originalArguments,
                        originalPathMatch, originalProcessNameMatch, originalWindowClassMatch,
                        originalIconFile, originalShortcutSource, originalPathMatchAuto,
                        originalProcessNameMatchAuto, originalIconSource);
                    MessageBox.Show("该路径与另一个固定项目冲突，未保存。", "ModernDock", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                bool iconChanged = false;
                if (!string.IsNullOrEmpty(selectedIconPath))
                {
                    iconChanged = SetCustomIcon(item, selectedIconPath);
                    if (!iconChanged)
                    {
                        RestoreEditedItem(item, originalTitle, originalTarget, originalArguments,
                            originalPathMatch, originalProcessNameMatch, originalWindowClassMatch,
                            originalIconFile, originalShortcutSource, originalPathMatchAuto,
                            originalProcessNameMatchAuto, originalIconSource);
                        return false;
                    }
                }
                else if (!SaveConfigToFile())
                {
                    RestoreEditedItem(item, originalTitle, originalTarget, originalArguments,
                        originalPathMatch, originalProcessNameMatch, originalWindowClassMatch,
                        originalIconFile, originalShortcutSource, originalPathMatchAuto,
                        originalProcessNameMatchAuto, originalIconSource);
                    return false;
                }

                BuildFixedUI();
                PositionDock();
                EntryPoint.Log("config.edit_completed title=" + item.Title + " icon_changed=" + iconChanged);
                return true;
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("config.edit_item", ex);
                return false;
            }
        }

        private void RestoreEditedItem(DockItem item, string title, string target, string arguments,
            string pathMatch, string processNameMatch, string windowClassMatch, string iconFile,
            string shortcutSource, bool pathMatchAuto, bool processNameMatchAuto, ImageSource iconSource)
        {
            if (item == null) return;
            item.Title = title;
            item.TargetPath = target;
            item.Arguments = arguments;
            item.PathMatch = pathMatch;
            item.ProcessNameMatch = processNameMatch;
            item.WindowClassMatch = windowClassMatch;
            item.IconFile = iconFile;
            item.ShortcutSource = shortcutSource;
            item.AutoDerivedPathMatch = pathMatchAuto;
            item.AutoDerivedProcessNameMatch = processNameMatchAuto;
            item.IconSource = iconSource;
        }

        private TextBox AddEditorField(Grid root, int row, string label, string value)
        {
            var labelText = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
            Grid.SetRow(labelText, row);
            Grid.SetColumn(labelText, 0);
            root.Children.Add(labelText);
            var box = new TextBox { Text = value ?? string.Empty, Margin = new Thickness(0, 4, 0, 4) };
            Grid.SetRow(box, row);
            Grid.SetColumn(box, 1);
            root.Children.Add(box);
            return box;
        }

        private void UpdateMatchingFieldsAfterPathChange(DockItem item, string originalTarget)
        {
            try
            {
                if (MatchingFieldUpdater.Update(item, originalTarget))
                {
                    EntryPoint.Log("config.edit_matching_fields_updated title=" + (item.Title ?? string.Empty));
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("config.edit_path_match", ex);
            }
        }

        private bool HasFixedIdentityConflict(DockItem item)
        {
            if (item == null) return false;
            string identityKey = ApplicationIdentityResolver.GetFixedIdentityKey(item);
            if (string.IsNullOrEmpty(identityKey)) return false;

            foreach (DockItem other in fixedItems)
            {
                if (other == null || other == item) continue;
                if (string.Equals(ApplicationIdentityResolver.GetFixedIdentityKey(other), identityKey, StringComparison.OrdinalIgnoreCase))
                {
                    EntryPoint.Log("config.fixed_identity_conflict title=" + (item.Title ?? string.Empty) +
                        " other=" + (other.Title ?? string.Empty) + " identity=" + identityKey);
                    return true;
                }
            }
            return false;
        }

        private bool IsGeneratedCustomIcon(string iconFile)
        {
            return !string.IsNullOrEmpty(iconFile) && iconFile.StartsWith("custom_", StringComparison.OrdinalIgnoreCase);
        }

        private void DeleteGeneratedIconIfReplaced(string previousIcon, string currentIcon)
        {
            if (string.IsNullOrEmpty(previousIcon) || string.Equals(previousIcon, currentIcon, StringComparison.OrdinalIgnoreCase)) return;
            if (!previousIcon.StartsWith("custom_", StringComparison.OrdinalIgnoreCase) && !previousIcon.StartsWith("auto_", StringComparison.OrdinalIgnoreCase)) return;
            string previousPath = System.IO.Path.Combine(iconDirectory, previousIcon);
            try
            {
                if (File.Exists(previousPath)) File.Delete(previousPath);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("icon.old_generated_delete path=" + previousPath, ex);
            }
        }

        private string FindIconSourcePath(DockItem item)
        {
            if (item == null) return string.Empty;
            string target = item.TargetPath;
            if (!string.IsNullOrEmpty(target) && ApplicationIdentityResolver.IsFullyQualifiedPath(target) &&
                !ApplicationIdentityResolver.IsGenericLauncherOrHostPath(target) && File.Exists(target))
            {
                return target;
            }

            foreach (WindowSnapshot window in GetTopLevelWindows())
            {
                FixedItemMatch match = FixedItemMatcher.Resolve(fixedItems, window);
                if (!match.IsAmbiguous && match.Item == item && !string.IsNullOrEmpty(window.ProcessPath) && File.Exists(window.ProcessPath))
                {
                    return window.ProcessPath;
                }
            }
            if (!string.IsNullOrEmpty(target) && File.Exists(target)) return target;
            return string.Empty;
        }

        private void PinItem(DockItem item)
        {
            try
            {
                string identityKey = ApplicationIdentityResolver.GetFixedIdentityKey(item);
                foreach (var fixedItem in fixedItems)
                {
                    if (string.Equals(ApplicationIdentityResolver.GetFixedIdentityKey(fixedItem), identityKey, StringComparison.OrdinalIgnoreCase))
                    {
                        EntryPoint.Log("pin.duplicate_fixed_item skipped title=" + item.Title + " identity=" + identityKey);
                        return;
                    }
                }

                string cleanName = SanitizeFileName(item.Title);
                string iconFileName = "dyn_" + cleanName + ".png";
                string iconFullPath = System.IO.Path.Combine(iconDirectory, iconFileName);

                BitmapSource bs = item.IconSource as BitmapSource;
                if (bs != null)
                {
                    using (var fs = new FileStream(iconFullPath, FileMode.Create))
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bs));
                        encoder.Save(fs);
                    }
                }

                var newItem = new DockItem
                {
                    Title = item.Title,
                    TargetPath = item.TargetPath,
                    Arguments = item.Arguments,
                    PathMatch = item.PathMatch,
                    ProcessNameMatch = item.ProcessNameMatch,
                    WindowClassMatch = item.WindowClassMatch,
                    IconFile = iconFileName,
                    ShortcutSource = item.ShortcutSource,
                    AutoDerivedPathMatch = item.AutoDerivedPathMatch,
                    AutoDerivedProcessNameMatch = item.AutoDerivedProcessNameMatch,
                    IconSource = item.IconSource,
                    IsFixed = true
                };

                fixedItems.Add(newItem);
                SaveConfigToFile();
                BuildFixedUI();
                PositionDock();
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("pin.item", ex);
            }
        }

        private void UnpinItem(DockItem item)
        {
            try
            {
                fixedItems.Remove(item);
                SaveConfigToFile();
                BuildFixedUI();
                PositionDock();
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("unpin.item", ex);
            }
        }

        private string SanitizeFileName(string name)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (char c in name)
            {
                if (Array.IndexOf(invalid, c) < 0 && c != ' ') sb.Append(c);
            }
            return sb.Length > 0 ? sb.ToString() : "app";
        }

        private Border CreateFluentRow(string iconGlyph, string titleText, Action onClick)
        {
            var rowBorder = new Border
            {
                CornerRadius = new CornerRadius(5),
                // Keep a virtually invisible hit surface across the full row
                // on an AllowsTransparency window. Otherwise only the glyph
                // pixels can receive a real mouse click.
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 255, 255, 255)),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(2, 1, 2, 1),
                Cursor = Cursors.Hand
            };
            System.Windows.Automation.AutomationProperties.SetName(rowBorder, titleText);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var tbIcon = new TextBlock
            {
                Text = iconGlyph,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 255, 255, 255)),
                FontSize = 13.0,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(tbIcon, 0);
            grid.Children.Add(tbIcon);

            var tbText = new TextBlock
            {
                Text = titleText,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI, Segoe UI Variable Display")
            };
            Grid.SetColumn(tbText, 1);
            grid.Children.Add(tbText);

            rowBorder.Child = grid;

            rowBorder.MouseEnter += (s, e) => {
                rowBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(35, 255, 255, 255));
            };
            rowBorder.MouseLeave += (s, e) => {
                rowBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 255, 255, 255));
            };
            rowBorder.PreviewMouseLeftButtonDown += (s, e) => {
                rowBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(55, 255, 255, 255));
            };
            rowBorder.PreviewMouseLeftButtonUp += (s, e) => {
                rowBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 255, 255, 255));
                if (onClick != null) onClick();
            };

            return rowBorder;
        }

        private void CloseApplication(DockItem item)
        {
            try
            {
                if (!item.IsFixed)
                {
                    ApplicationGroup group;
                    if (!string.IsNullOrEmpty(item.DynamicIdentityKey) && dynamicGroupsMap.TryGetValue(item.DynamicIdentityKey, out group))
                    {
                        EntryPoint.Log("app.close.dynamic identity=" + item.DynamicIdentityKey + " windows=" + group.Windows.Count);
                        foreach (var window in group.Windows)
                        {
                            if (window != null && window.Handle != IntPtr.Zero)
                            {
                                PostMessage(window.Handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            }
                        }
                    }
                    else if (item.DynamicHwnd != IntPtr.Zero)
                    {
                        PostMessage(item.DynamicHwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    }
                    return;
                }

                var activeWindows = GetTopLevelWindows(true, null);
                foreach (var win in activeWindows)
                {
                    if (MatchesItem(item, win))
                    {
                        PostMessage(win.Handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    }
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("app.close", ex);
            }
        }

        private void LaunchNewInstance(DockItem item)
        {
            try
            {
                if (item == null) return;
                if (item.CustomAction != null)
                {
                    item.CustomAction();
                    return;
                }

                if (TryStartItem(item)) return;

                LaunchResolutionResult resolution = LaunchTargetResolver.Resolve(
                    item, GetTopLevelWindows(true, null));
                if (resolution.Status == LaunchResolutionStatus.Found &&
                    TryApplyResolvedTarget(item, resolution.ResolvedTargetPath) &&
                    TryStartItem(item))
                {
                    return;
                }

                ShowLaunchRepairDialog(item, resolution);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("app.launch", ex);
            }
        }

        private bool TryStartItem(DockItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.TargetPath)) return false;

            string target = item.TargetPath.Trim().Trim('"');
            // Shell activations such as explorer.exe shell:RecycleBinFolder and
            // AppsFolder entries are intentionally allowed to resolve through
            // ShellExecute even though they are not rooted file paths.
            if (ApplicationIdentityResolver.IsFullyQualifiedPath(target) && !File.Exists(target)) return false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = true
                };
                if (!string.IsNullOrEmpty(item.Arguments)) psi.Arguments = item.Arguments;
                if (target.EndsWith("cmd.exe", StringComparison.OrdinalIgnoreCase)) psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
                EntryPoint.Log("app.launch_started target=" + target);
                return true;
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("app.launch_start_failed target=" + target, ex);
                return false;
            }
        }

        private bool TryApplyResolvedTarget(DockItem item, string resolvedTarget)
        {
            return TryApplyResolvedTarget(item, resolvedTarget, false);
        }

        private bool TryApplyResolvedTarget(DockItem item, string resolvedTarget, bool clearShortcutSource)
        {
            if (item == null || string.IsNullOrWhiteSpace(resolvedTarget) || !File.Exists(resolvedTarget)) return false;

            string originalTarget = item.TargetPath;
            string originalPathMatch = item.PathMatch;
            string originalProcessNameMatch = item.ProcessNameMatch;
            string originalWindowClassMatch = item.WindowClassMatch;
            string originalShortcutSource = item.ShortcutSource;
            bool originalPathMatchAuto = item.AutoDerivedPathMatch;
            bool originalProcessNameMatchAuto = item.AutoDerivedProcessNameMatch;

            try
            {
                item.TargetPath = resolvedTarget;
                if (clearShortcutSource) item.ShortcutSource = string.Empty;
                MatchingFieldUpdater.Update(item, originalTarget);
                if (HasFixedIdentityConflict(item))
                {
                    RestoreResolvedTarget(item, originalTarget, originalPathMatch, originalProcessNameMatch,
                        originalWindowClassMatch, originalShortcutSource, originalPathMatchAuto,
                        originalProcessNameMatchAuto);
                    EntryPoint.Log("launch.resolve_identity_conflict target=" + resolvedTarget);
                    return false;
                }

                if (!SaveConfigToFile())
                {
                    RestoreResolvedTarget(item, originalTarget, originalPathMatch, originalProcessNameMatch,
                        originalWindowClassMatch, originalShortcutSource, originalPathMatchAuto,
                        originalProcessNameMatchAuto);
                    return false;
                }

                TryRefreshResolvedIcon(item);
                BuildFixedUI();
                PositionDock();
                EntryPoint.Log("launch.resolve_applied old=" + originalTarget + " new=" + resolvedTarget);
                return true;
            }
            catch (Exception ex)
            {
                RestoreResolvedTarget(item, originalTarget, originalPathMatch, originalProcessNameMatch,
                    originalWindowClassMatch, originalShortcutSource, originalPathMatchAuto,
                    originalProcessNameMatchAuto);
                EntryPoint.LogException("launch.resolve_apply", ex);
                return false;
            }
        }

        private void RestoreResolvedTarget(DockItem item, string target, string pathMatch,
            string processNameMatch, string windowClassMatch, string shortcutSource,
            bool pathMatchAuto, bool processNameMatchAuto)
        {
            if (item == null) return;
            item.TargetPath = target;
            item.PathMatch = pathMatch;
            item.ProcessNameMatch = processNameMatch;
            item.WindowClassMatch = windowClassMatch;
            item.ShortcutSource = shortcutSource;
            item.AutoDerivedPathMatch = pathMatchAuto;
            item.AutoDerivedProcessNameMatch = processNameMatchAuto;
        }

        private void TryRefreshResolvedIcon(DockItem item)
        {
            try
            {
                if (item == null || string.IsNullOrWhiteSpace(item.IconFile) ||
                    IconUpgradeService.IsCustomOverride(item.IconFile)) return;

                string iconPath = System.IO.Path.Combine(iconDirectory, item.IconFile);
                IconCandidate candidate = null;
                bool found = false;
                if (IconUpgradeService.IsLauncherHostPath(item.TargetPath))
                {
                    found = IconUpgradeService.TryFindWindowIconCandidate(
                        item, iconPath, GetTopLevelWindows(true, null), out candidate);
                }
                if (!found) found = IconUpgradeService.TryFindHighConfidenceCandidate(item, iconPath, out candidate);
                if (!found) return;
                if (IconUpgradeService.TryApplyCandidate(item, candidate, iconPath))
                {
                    IconService.Invalidate(iconPath);
                    item.IconSource = IconService.LoadImage(iconPath);
                    EntryPoint.Log("launch.resolve_icon_refreshed title=" + (item.Title ?? string.Empty) +
                        " source=" + candidate.SourceType + " size=" + candidate.PixelWidth + "x" + candidate.PixelHeight);
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("launch.resolve_icon_refresh", ex);
            }
        }

        private void ShowLaunchRepairDialog(DockItem item, LaunchResolutionResult initialResolution)
        {
            try
            {
                if (item == null) return;
                var dialog = new Window
                {
                    Title = "程序位置已变化",
                    Width = 390,
                    Height = 190,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false,
                    Owner = this
                };
                var root = new Grid { Margin = new Thickness(14) };
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var message = new TextBlock
                {
                    Text = BuildLaunchRepairMessage(item, initialResolution),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                System.Windows.Automation.AutomationProperties.SetAutomationId(message, "ModernDock.LaunchRepair.Message");
                root.Children.Add(message);

                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var findButton = new Button { Content = "自动查找", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0) };
                var chooseButton = new Button { Content = "重新选择程序", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0) };
                var cancelButton = new Button { Content = "取消", IsCancel = true, Padding = new Thickness(12, 5, 12, 5) };
                System.Windows.Automation.AutomationProperties.SetAutomationId(findButton, "ModernDock.LaunchRepair.AutoFind");
                System.Windows.Automation.AutomationProperties.SetAutomationId(chooseButton, "ModernDock.LaunchRepair.Choose");
                System.Windows.Automation.AutomationProperties.SetAutomationId(cancelButton, "ModernDock.LaunchRepair.Cancel");
                buttons.Children.Add(findButton);
                buttons.Children.Add(chooseButton);
                buttons.Children.Add(cancelButton);
                Grid.SetRow(buttons, 2);
                root.Children.Add(buttons);

                findButton.Click += (s, e) => {
                    LaunchResolutionResult resolution = LaunchTargetResolver.Resolve(item, GetTopLevelWindows(true, null));
                    if (resolution.Status == LaunchResolutionStatus.Found &&
                        TryApplyResolvedTarget(item, resolution.ResolvedTargetPath) && TryStartItem(item))
                    {
                        dialog.Close();
                    }
                    else
                    {
                        message.Text = BuildLaunchRepairMessage(item, resolution);
                    }
                };
                chooseButton.Click += (s, e) => {
                    var fileDialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "程序文件|*.exe|所有文件|*.*",
                        CheckFileExists = true,
                        Multiselect = false,
                        Title = "重新选择程序"
                    };
                    if (fileDialog.ShowDialog() != true) return;
                    if (TryApplyResolvedTarget(item, fileDialog.FileName, true) && TryStartItem(item)) dialog.Close();
                    else message.Text = "所选程序与其他固定项目冲突，或无法启动。";
                };
                dialog.Content = root;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("app.launch_repair_dialog", ex);
            }
        }

        private string BuildLaunchRepairMessage(DockItem item, LaunchResolutionResult resolution)
        {
            if (resolution != null && resolution.Status == LaunchResolutionStatus.Ambiguous)
            {
                return "“" + (item.Title ?? "程序") + "”找到多个可能的新位置，请手动选择。";
            }
            return "“" + (item.Title ?? "程序") + "”的位置已变化，无法自动启动。";
        }

        private void ExecuteOrToggleItem(DockItem item, bool forceMinimize)
        {
            try
            {
                string key = item.Title ?? item.TargetPath ?? "";
                if (!string.IsNullOrEmpty(key))
                {
                    DateTime lastTime;
                    if (lastActionTimeMap.TryGetValue(key, out lastTime))
                    {
                        if ((DateTime.Now - lastTime).TotalMilliseconds < 500)
                        {
                            return; // Debounce rapid double-click or multi-click
                        }
                    }
                    lastActionTimeMap[key] = DateTime.Now;
                }

                if (item.CustomAction != null)
                {
                    item.CustomAction();
                    return;
                }

                if (!item.IsFixed)
                {
                    ApplicationGroup group;
                    IntPtr representative = item.DynamicHwnd;
                    if (!string.IsNullOrEmpty(item.DynamicIdentityKey) && dynamicGroupsMap.TryGetValue(item.DynamicIdentityKey, out group) && group.Representative != null)
                    {
                        representative = group.Representative.Handle;
                    }
                    if (representative != IntPtr.Zero)
                    {
                        ToggleWindow(representative, forceMinimize);
                        return;
                    }
                }

                var activeWindows = GetTopLevelWindows(true, null);
                var matchingWindows = new List<WindowSnapshot>();
                foreach (var win in activeWindows)
                {
                    if (MatchesItem(item, win))
                    {
                        matchingWindows.Add(win);
                    }
                }

                if (matchingWindows.Count > 0)
                {
                    ToggleWindow(matchingWindows[0].Handle, forceMinimize);
                    return;
                }

                if (!forceMinimize)
                {
                    LaunchNewInstance(item);
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("app.toggle", ex);
            }
        }

        private void ToggleWindow(IntPtr hWnd, bool forceMinimize)
        {
            try
            {
                IntPtr dockHwnd = new WindowInteropHelper(this).Handle;
                IntPtr curFg = GetForegroundWindow();

                uint curFgPid;
                GetWindowThreadProcessId(curFg, out curFgPid);
                uint lastFgPid;
                GetWindowThreadProcessId(lastForegroundAppHwnd, out lastFgPid);
                uint targetPid;
                GetWindowThreadProcessId(hWnd, out targetPid);

                bool isMin = IsIconic(hWnd);

                // Check if target window was the active window right before the Dock was clicked
                bool isTargetActive = (curFg == hWnd) ||
                                     (lastForegroundAppHwnd == hWnd) ||
                                     (curFgPid != 0 && curFgPid == targetPid && curFg != dockHwnd) ||
                                     (lastFgPid != 0 && lastFgPid == targetPid);

                if (forceMinimize || (isTargetActive && !isMin))
                {
                    PostMessage(hWnd, WM_SYSCOMMAND, (IntPtr)SC_MINIMIZE, IntPtr.Zero);
                    ShowWindow(hWnd, SW_MINIMIZE);
                    lastForegroundAppHwnd = IntPtr.Zero;
                }
                else
                {
                    WINDOWPLACEMENT wp = new WINDOWPLACEMENT();
                    wp.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
                    GetWindowPlacement(hWnd, ref wp);

                    bool wasMaximized = (wp.flags & WPF_RESTORETOMAXIMIZED) != 0 ||
                                        (wp.showCmd == SW_SHOWMAXIMIZED) ||
                                        (wp.showCmd == SW_MAXIMIZE);

                    uint currentThread = GetCurrentThreadId();
                    uint targetThread;
                    GetWindowThreadProcessId(hWnd, out targetThread);

                    bool inputAttached = false;
                    try
                    {
                        if (currentThread != 0 && targetThread != 0 && currentThread != targetThread)
                        {
                            inputAttached = AttachThreadInput(currentThread, targetThread, true);
                        }

                        if (wasMaximized)
                        {
                            ShowWindow(hWnd, SW_MAXIMIZE);
                        }
                        else if (isMin)
                        {
                            ShowWindow(hWnd, SW_RESTORE);
                        }
                        else
                        {
                            ShowWindow(hWnd, SW_SHOW);
                        }

                        BringWindowToTop(hWnd);
                        SetForegroundWindow(hWnd);
                        SwitchToThisWindow(hWnd, true);
                    }
                    finally
                    {
                        if (inputAttached)
                        {
                            AttachThreadInput(currentThread, targetThread, false);
                        }
                    }

                    lastForegroundAppHwnd = hWnd;
                }

                SetWindowPos(dockHwnd, HWND_TOPMOST, 0, 0, 0, 0, NativeConstants.DockTopmostNoActivateFlags);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("app.toggle_window", ex);
            }
        }

        private System.Windows.Controls.ToolTip CreateToolTip(string text)
        {
            return new System.Windows.Controls.ToolTip
            {
                Content = text,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(240, 20, 20, 24)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                Placement = System.Windows.Controls.Primitives.PlacementMode.Top,
                VerticalOffset = -6
            };
        }

        private void SendWinKey()
        {
            try
            {
                const byte VK_LWIN = 0x5B;
                const uint KEYEVENTF_KEYUP = 0x0002;
                keybd_event(VK_LWIN, 0, 0, 0);
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("shell.send_win_key", ex);
            }
        }
    }
}
