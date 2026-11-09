using Microsoft.AspNetCore.Components.WebView.Wpf;

using RemnantSaveGuardian.Helpers;
using RemnantSaveGuardian.Services;

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace RemnantSaveGuardian
{
    /// <summary>
    /// The only WPF window: hosts the MudBlazor UI (<see cref="Components.Main"/>) in a BlazorWebView
    /// and owns the window-level features (size, always on top, opacity, tray icon, title bar theme).
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly AppState appState;
        private readonly BackupService backupService;
        private System.Windows.Forms.NotifyIcon? trayIcon;

        public MainWindow(AppState appState, BackupService backupService)
        {
            this.appState = appState;
            this.backupService = backupService;

            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }

            InitializeComponent();

            Width = Math.Max(MinWidth, Properties.Settings.Default.WindowWidth);
            Height = Math.Max(MinHeight, Properties.Settings.Default.WindowHeight);
            Topmost = Properties.Settings.Default.TopMost;
            SetUpCustomChrome();
            ApplyThemeBackground();

            blazorWebView.Services = App.Services;
            blazorWebView.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(Components.Main) });
            blazorWebView.BlazorWebViewInitialized += (s, e) => e.WebView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;

            try
            {
                if (Properties.Settings.Default.SaveFolder.Length == 0)
                {
                    Logger.Log("Save folder not set; reverting to default.");
                    Properties.Settings.Default.SaveFolder = RemnantSave.DefaultSaveFolder();
                    if (!Directory.Exists(RemnantSave.DefaultSaveFolder()))
                    {
                        Logger.Error(Loc.T("Could not find save file location; please set manually"));
                    }
                }
                else if (!Directory.Exists(Properties.Settings.Default.SaveFolder) && !Properties.Settings.Default.SaveFolder.Equals(RemnantSave.DefaultSaveFolder()))
                {
                    Logger.Log($"Save folder ({Properties.Settings.Default.SaveFolder}) not found; reverting to default.");
                    Properties.Settings.Default.SaveFolder = RemnantSave.DefaultSaveFolder();
                }
                if (!Directory.Exists(Properties.Settings.Default.SaveFolder))
                {
                    Logger.Log("Save folder not found, creating...");
                    Directory.CreateDirectory(Properties.Settings.Default.SaveFolder);
                }
                SaveWatcher.Watch(Properties.Settings.Default.SaveFolder);

                if (!Directory.Exists(Properties.Settings.Default.GameFolder))
                {
                    Logger.Log("Game folder not found...");
                    if (Properties.Settings.Default.GameFolder == "")
                    {
                        TryToFindGameFolder();
                    }
                }

                backupService.Initialize();

                UpdateCheck.NewVersion += UpdateCheck_NewVersion;
                Properties.Settings.Default.PropertyChanged += Settings_PropertyChanged;
                CreateTrayIcon();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading main window: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes the system title bar so the app bar can act as the title bar (and auto-hide with
        /// the rest of the navigation). WindowChrome keeps resizing from the edges, Aero Snap and the
        /// system menu. With window opacity enabled the window is also transparent; WPF only allows
        /// that before the window is shown, so it applies at startup.
        /// </summary>
        private void SetUpCustomChrome()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            if (Properties.Settings.Default.EnableOpacity)
            {
                AllowsTransparency = true;
            }
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 0, // dragging is started from the app bar instead
                ResizeBorderThickness = SystemParameters.WindowResizeBorderThickness,
                // No DWM glass frame: extending it into the client area stops the web view from being
                // drawn. Rounded corners are requested separately (NativeWindow.SetRoundedCorners).
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false,
            });

            appState.DragWindow = StartTitleBarDrag;
            appState.MinimizeWindow = () => SystemCommands.MinimizeWindow(this);
            appState.ToggleMaximizeWindow = () =>
            {
                if (WindowState == WindowState.Maximized)
                {
                    SystemCommands.RestoreWindow(this);
                }
                else
                {
                    SystemCommands.MaximizeWindow(this);
                }
            };
            appState.CloseWindow = () => SystemCommands.CloseWindow(this);
            appState.ShowSystemMenu = () =>
            {
                if (!GetCursorPos(out var cursor))
                {
                    return;
                }
                var source = PresentationSource.FromVisual(this);
                var point = new Point(cursor.X, cursor.Y);
                if (source?.CompositionTarget != null)
                {
                    point = source.CompositionTarget.TransformFromDevice.Transform(point);
                }
                SystemCommands.ShowSystemMenu(this, point);
            };
            StateChanged += (s, e) => UpdateMaximizedLayout();
            BorderThickness = new Thickness(1);
        }

        /// <summary>Tells the app bar which caption button to show and drops the edge line while maximized.</summary>
        private void UpdateMaximizedLayout()
        {
            var maximized = WindowState == WindowState.Maximized;
            BorderThickness = new Thickness(maximized ? 0 : 1);
            appState.SetWindowMaximized(maximized);
        }

        private const int WM_GETMINMAXINFO = 0x0024;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        /// <summary>
        /// Without a system title bar Windows would maximize the window over the taskbar; limit the
        /// maximized size and position to the work area of the monitor the window is on.
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
                if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
                {
                    var mmi = System.Runtime.InteropServices.Marshal.PtrToStructure<MINMAXINFO>(lParam);
                    mmi.ptMaxPosition.X = info.rcWork.Left - info.rcMonitor.Left;
                    mmi.ptMaxPosition.Y = info.rcWork.Top - info.rcMonitor.Top;
                    mmi.ptMaxSize.X = info.rcWork.Right - info.rcWork.Left;
                    mmi.ptMaxSize.Y = info.rcWork.Bottom - info.rcWork.Top;
                    System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, true);
                }
            }
            return IntPtr.Zero; // let WPF apply its MinWidth/MinHeight handling too
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTCAPTION = 2;
        private const int VK_LBUTTON = 0x01;
        private const int SM_CXDOUBLECLK = 36;
        private const int SM_CYDOUBLECLK = 37;
        private long lastTitleBarPress;
        private POINT lastTitleBarPressPoint;

        /// <summary>
        /// Called when the left button goes down on the app bar's title area. Hands the drag to the
        /// system's own title-bar move loop, as if the press were on a real caption: that restores a
        /// maximized window once it is dragged, and gives Aero Snap. (WPF's DragMove ignores maximized
        /// windows.) A second press within the double-click time and distance toggles maximize, since
        /// the move loop swallows the mouse-up the web page would need to see a double-click itself.
        /// </summary>
        private void StartTitleBarDrag()
        {
            if (!GetCursorPos(out var cursor))
            {
                return;
            }
            var now = Environment.TickCount64;
            var isDoubleClick = now - lastTitleBarPress <= GetDoubleClickTime()
                && Math.Abs(cursor.X - lastTitleBarPressPoint.X) <= GetSystemMetrics(SM_CXDOUBLECLK) / 2
                && Math.Abs(cursor.Y - lastTitleBarPressPoint.Y) <= GetSystemMetrics(SM_CYDOUBLECLK) / 2;
            if (isDoubleClick)
            {
                lastTitleBarPress = 0;
                appState.ToggleMaximizeWindow?.Invoke();
                return;
            }
            lastTitleBarPress = now;
            lastTitleBarPressPoint = cursor;

            // The press reaches us asynchronously from the web view; only start a drag if the button
            // is still physically down, or the move loop would grab the mouse after a plain click.
            if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0)
            {
                return;
            }
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            System.Windows.Input.Mouse.Capture(null);
            ReleaseCapture();
            SendMessage(hwnd, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), new IntPtr((cursor.Y << 16) | (cursor.X & 0xFFFF)));
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT point);

        /// <summary>
        /// Imports files or folders dropped on the Backups page. The page script sends the dropped
        /// File objects with postMessageWithAdditionalObjects, which gives us their real paths.
        /// Blazor's own messages use a different format and are left alone.
        /// </summary>
        private void WebView_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            string? message;
            try
            {
                message = e.TryGetWebMessageAsString();
            }
            catch (ArgumentException)
            {
                return; // not a string message
            }
            if (message != "rsg-import-drop" || e.AdditionalObjects == null)
            {
                return;
            }
            var path = e.AdditionalObjects.OfType<Microsoft.Web.WebView2.Core.CoreWebView2File>().Select(f => f.Path).FirstOrDefault();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            backupService.ImportBackup(path);
        }

        private void CreateTrayIcon()
        {
            trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "RemnantSaveGuardian",
                Icon = Environment.ProcessPath != null ? System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath) : null,
                Visible = true,
                ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(),
            };
            trayIcon.ContextMenuStrip.Items.Add(Loc.T("Home"), null, (s, e) => Dispatcher.Invoke(() =>
            {
                FocusWindow();
                appState.Navigate(AppState.BackupsKey);
            }));
            trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    Dispatcher.Invoke(FocusWindow);
                }
            };
        }

        private void FocusWindow()
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Show();
            Activate();
        }

        private bool IsDarkTheme => Properties.Settings.Default.Theme != "Light";

        private void ApplyThemeBackground()
        {
            // Matches the MudBlazor palette background so there is no flash while the web view loads.
            Background = new SolidColorBrush(IsDarkTheme ? Color.FromRgb(0x1a, 0x1a, 0x27) : Color.FromRgb(0xff, 0xff, 0xff));
            // Thin edge so the borderless window stays distinguishable from what's behind it.
            BorderBrush = new SolidColorBrush(IsDarkTheme ? Color.FromRgb(0x3a, 0x3a, 0x4c) : Color.FromRgb(0xc8, 0xc8, 0xc8));
            NativeWindow.SetDarkTitleBar(this, IsDarkTheme);
        }

        private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                switch (e.PropertyName)
                {
                    case "Theme":
                        ApplyThemeBackground();
                        break;
                    case "TopMost":
                        Topmost = Properties.Settings.Default.TopMost;
                        break;
                    case "EnableOpacity":
                        Logger.Log(Loc.T("Opacity_toggle_notice"));
                        break;
                    case "Opacity":
                    case "OnlyInactive":
                        // The window is active while the settings are being changed.
                        ApplyOpacity(active: IsActive);
                        break;
                    case "AutoHideNaviAndTitleBar":
                        appState.SetChromeVisible(true);
                        break;
                }
            });
        }

        /// <summary>Applies the opacity settings for the window's current activation state.</summary>
        private void ApplyOpacity(bool active)
        {
            if (!AllowsTransparency)
            {
                return; // opacity was off at startup; it takes effect after a restart
            }
            var settings = Properties.Settings.Default;
            if (!settings.EnableOpacity || (settings.OnlyInactive && active))
            {
                Opacity = 1;
                return;
            }
            Opacity = Math.Clamp(settings.Opacity, 0.2, 1);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            NativeWindow.SetDarkTitleBar(this, IsDarkTheme);
            NativeWindow.SetRoundedCorners(this);
            System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle)?.AddHook(WndProc);
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            ApplyOpacity(active: true);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (Properties.Settings.Default.AutoHideNaviAndTitleBar)
            {
                appState.SetChromeVisible(true);
            }
            ApplyOpacity(active: true);
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            if (Properties.Settings.Default.AutoHideNaviAndTitleBar)
            {
                appState.SetChromeVisible(false);
            }
            ApplyOpacity(active: false);
        }

        private void UpdateCheck_NewVersion(object? sender, NewVersionEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                Logger.Log(Loc.T("New version available!"));
            });
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (WindowState == WindowState.Normal)
            {
                Properties.Settings.Default.WindowWidth = (int)Math.Round(ActualWidth);
                Properties.Settings.Default.WindowHeight = (int)Math.Round(ActualHeight);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            Properties.Settings.Default.Save();

            // Make sure that closing this window will begin the process of closing the application.
            Application.Current.Shutdown();
        }

        private void TryToFindGameFolder()
        {
            if (File.Exists(Properties.Settings.Default.GameFolder + @"\Remnant2.exe"))
            {
                return;
            }

            // Check if game is installed via Steam
            // In registry, we can see IF the game is installed with steam or not
            // To find the actual game, we need to search within ALL library folders
            Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam\Apps\1282100", false);
            if (key != null) // null if remnant is not in steam library (or steam itself is not (or never was) installed)
            {
                bool? steamRemnantInstalled = null;
                object? keyValue = key.GetValue("Installed"); // Value is true when remnant is installed
                if (keyValue != null) steamRemnantInstalled = Convert.ToBoolean(keyValue);
                if (steamRemnantInstalled.HasValue && steamRemnantInstalled.Value)
                {
                    Microsoft.Win32.RegistryKey? steamRegKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam", false);
                    string? steamInstallPath = steamRegKey?.GetValue("SteamPath") as string; // Get install path for steam
                    if (steamInstallPath != null)
                    {
                        DirectoryInfo steamInstallDir = new DirectoryInfo(steamInstallPath);
                        if (steamInstallDir.Exists)
                        {
                            FileInfo libraryFolders = new FileInfo(@$"{steamInstallDir.FullName}\steamapps\libraryfolders.vdf");
                            // Find Steam-Library, remnant is installed in
                            string[] libraryFolderContent = File.ReadAllLines(libraryFolders.FullName);
                            int remnantIndex = Array.IndexOf(libraryFolderContent, libraryFolderContent.FirstOrDefault(t => t.Contains("\"1282100\"")));
                            if (remnantIndex == -1)
                            {
                                remnantIndex = libraryFolderContent.Length;
                            }
                            libraryFolderContent = libraryFolderContent.Take(remnantIndex).ToArray();
                            string? steamLibraryPathRaw = libraryFolderContent.LastOrDefault(t => t.Contains("\"path\""));
                            string[]? steamLibraryPathRawSplit = steamLibraryPathRaw?.Split('\"');
                            string? steamLibraryPath = steamLibraryPathRawSplit?[3];
                            string steamRemnantInstallPath = @$"{steamLibraryPath?.Replace(@"\\", @"\")}\steamapps\common\Remnant2";
                            if (Directory.Exists(steamRemnantInstallPath))
                            {
                                if (File.Exists(@$"{steamRemnantInstallPath}\Remnant2.exe"))
                                {
                                    Properties.Settings.Default.GameFolder = steamRemnantInstallPath;
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            // Check if game is installed via Epic
            // Epic stores manifests for every installed game withing "C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests"
            // These "Manifests" are in json format, so if one of them is for Remnant, then Remnant is installed with epic
            var epicManifestFolder = new DirectoryInfo(@"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests");
            if (epicManifestFolder.Exists) // If Folder don't exist, epic is not installed
            {
                foreach (FileInfo fi in epicManifestFolder.GetFiles("*.item"))
                {
                    string[] itemContent = File.ReadAllLines(fi.FullName);
                    if (itemContent.All(t => t.Contains("Remnant II") == false)) continue;

                    string? epicRemnantInstallPathRaw = itemContent.FirstOrDefault(t => t.Contains("\"InstallLocation\""));
                    string[]? epicRemnantInstallPathRawSplit = epicRemnantInstallPathRaw?.Split('\"');
                    string? epicRemnantInstallPath = epicRemnantInstallPathRawSplit?[3].Replace(@"\\", @"\");

                    if (Directory.Exists(epicRemnantInstallPath))
                    {
                        if (File.Exists(@$"{epicRemnantInstallPath}\Remnant2.exe"))
                        {
                            Properties.Settings.Default.GameFolder = epicRemnantInstallPath;
                            return;
                        }
                    }

                    break;
                }
            }
            // Check if game is installed via Windows Store
            // TODO - don't have windows store version

            // Remnant not found or not installed, clear path
            Properties.Settings.Default.GameFolder = "";
        }
    }
}
