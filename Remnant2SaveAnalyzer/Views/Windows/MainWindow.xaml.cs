using Remnant2SaveAnalyzer.Helpers;
using Remnant2SaveAnalyzer.ViewModels;
using Remnant2SaveAnalyzer.Views.Pages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Remnant2SaveAnalyzer.Properties;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;
using WPFLocalizeExtension.Engine;
using MenuItem = Wpf.Ui.Controls.MenuItem;
using Remnant2SaveAnalyzer.Logging;

namespace Remnant2SaveAnalyzer.Views.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel
        {
            get;
        }

        public MainWindow(MainWindowViewModel viewModel, IPageService pageService, INavigationService navigationService)
        {
            ViewModel = viewModel;
            DataContext = this;

            if (Settings.Default.UpgradeRequired)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpgradeRequired = false;
                Settings.Default.Save();
            }

            InitializeComponent();            
            SetPageService(pageService);

            navigationService.SetNavigationControl(RootNavigation);

            Topmost = Settings.Default.TopMost;

            if (Settings.Default.EnableOpacity)
            {
                Binding binding = new("background")
                {
                    Mode = BindingMode.TwoWay
                };
                SetBinding(BackgroundProperty, binding);
                AllowsTransparency = true;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.CanMinimize;
                WindowDwmHelper.RestoreBackground(this);
            }

            try
            {                
                Notifications.MessageLogged += Logger_MessageLogged;

                string? theme = Settings.Default.Theme;
                Wpf.Ui.Appearance.Theme.Apply(theme == "Light"
                    ? Wpf.Ui.Appearance.ThemeType.Light
                    : Wpf.Ui.Appearance.ThemeType.Dark);

                if (Settings.Default.SaveFolder.Length == 0)
                {
                    Notifications.Normal("Save folder not set; reverting to default.");
                    Settings.Default.SaveFolder = RemnantSave.DefaultSaveFolder();
                    if (!Directory.Exists(RemnantSave.DefaultSaveFolder()))
                    {
                        Notifications.Error(Loc.T("Could not find save file location; please set manually"));
                        /*if (Directory.Exists(RemnantSave.DefaultWgsSaveFolder))
                        {
                            var dirs = Directory.GetDirectories(RemnantSave.DefaultWgsSaveFolder);
                            foreach (var dir in dirs)
                            {
                                if (dir != "t" && Directory.GetDirectories(dir).Length > 0)
                                {
                                    var saveDir = Directory.GetDirectories(dir)[0];
                                    Properties.Settings.Default.SaveFolder = saveDir;
                                }
                            }
                        }*/
                    }
                }
                else if (!Directory.Exists(Settings.Default.SaveFolder) && !Settings.Default.SaveFolder.Equals(RemnantSave.DefaultSaveFolder()))
                {
                    Notifications.Normal($"Save folder ({Settings.Default.SaveFolder}) not found; reverting to default.");
                    Settings.Default.SaveFolder = RemnantSave.DefaultSaveFolder();
                }
                if (!Directory.Exists(Settings.Default.SaveFolder))
                {
                    Notifications.Normal("Save folder not found, creating...");
                    Directory.CreateDirectory(Settings.Default.SaveFolder);
                }
                SaveWatcher.Watch(Settings.Default.SaveFolder);

                if (!Directory.Exists(Settings.Default.GameFolder))
                {
                    Notifications.Normal("Game folder not found...");
                    //this.btnStartGame.IsEnabled = false;
                    //this.btnStartGame.Content = this.FindResource("PlayGrey");
                    //this.backupCMStart.IsEnabled = false;
                    //this.backupCMStart.Icon = this.FindResource("PlayGrey");
                    if (Settings.Default.GameFolder == "")
                    {
                        TryToFindGameFolder();
                    }
                }

                BackupsPage.BackupSaveViewed += BackupsPage_BackupSaveViewed;

                RootNavigation.Navigated += RootNavigation_Navigated;

                UpdateCheck.NewVersion += UpdateCheck_NewVersion;
                if (Settings.Default.AutoCheckUpdate)
                {
                    UpdateCheck.CheckForNewVersion();
                }
                LocalizeDictionary.Instance.MissingKeyEvent += (_, _) => {
                    //Notifications.Normal($"Missing translation for key: {e.Key}");
                };
            } catch (Exception ex)
            {
                Notifications.Error($"Error loading main window: {ex.Message}");
            }
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            if (Settings.Default.EnableOpacity == false) { return; }
            if (Settings.Default.OnlyInactive || Math.Abs(Settings.Default.Opacity - 1) < 0.01)
            {
                WindowDwmHelper.ApplyDwm(this, WindowDwmHelper.UxMaterials.Mica);
            }
            else
            {
                WindowDwmHelper.ApplyDwm(this, WindowDwmHelper.UxMaterials.None);
                Opacity = Settings.Default.Opacity;
            }
        }
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (Settings.Default.AutoHideNaviAndTitleBar) {
                TitleBar.Visibility = Visibility.Visible;
                RootNavigation.Visibility = Visibility.Visible;
                BtnAlwayOnTop.Visibility = Visibility.Visible;
                Caption.Visibility = Visibility.Visible;
                Border.Margin = new Thickness(0,46,0,0);
                EventTransfer.Transfer(Visibility.Visible);
            }
            if (Settings.Default.EnableOpacity == false) { return; }
            if (Settings.Default.OnlyInactive)
            {
                WindowDwmHelper.ApplyDwm(this, WindowDwmHelper.UxMaterials.Mica);
                Opacity = 1;
            }
        }
        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            if (Settings.Default.AutoHideNaviAndTitleBar)
            {
                TitleBar.Visibility = Visibility.Collapsed;
                RootNavigation.Visibility = Visibility.Collapsed;
                BtnAlwayOnTop.Visibility = Visibility.Collapsed;
                Caption.Visibility = Visibility.Collapsed;
                Border.Margin = new Thickness(0);
                EventTransfer.Transfer(Visibility.Collapsed);
            }
            if (Settings.Default.EnableOpacity == false) { return; }
            if (Settings.Default.OnlyInactive && Settings.Default.Opacity < 1)
            {
                WindowDwmHelper.ApplyDwm(this, WindowDwmHelper.UxMaterials.None);
                Opacity = Settings.Default.Opacity;
            }
        }
        private void UpdateCheck_NewVersion(object? sender, NewVersionEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                Notifications.Normal(Loc.T($"New version {e.Version} available!"));
            });
        }

        // ReSharper disable once RedundantNullableFlowAttribute
        private void RootNavigation_Navigated([NotNull] INavigation sender, RoutedNavigationEventArgs e)
        {
            // first navigation is to the backups page
            // check to see if the user wants to start on another page
            RootNavigation.Navigated -= RootNavigation_Navigated;
            if (Settings.Default.StartPage == "backups")
            {
                return;
            }
            if (ViewModel.NavigationItems.All(nav => (nav as NavigationItem)?.PageTag != Settings.Default.StartPage))
            {
                Settings.Default.StartPage = "backups";
                return;
            }
            RootNavigation.Navigate(Settings.Default.StartPage);
        }

        private void BackupsPage_BackupSaveViewed(object? sender, BackupSaveViewedEventArgs e)
        {
            string pageTag = $"world-analyzer-{e.SaveBackup.SaveDate.Ticks}";
            foreach (INavigationControl navigationControl in ViewModel.NavigationItems)
            {
                NavigationItem nav = (NavigationItem)navigationControl;
                if (nav.PageTag == pageTag)
                {
                    RootNavigation.Navigate(pageTag);
                    return;
                }
            }
            WorldAnalyzerViewModel? viewM = Activator.CreateInstance(typeof(WorldAnalyzerViewModel)) as WorldAnalyzerViewModel;
            Debug.Assert(viewM != null, nameof(viewM) + " != null");
            object[] parameters = [viewM, e.SaveBackup.SaveFolderPath];
            WorldAnalyzerPage? page = Activator.CreateInstance(typeof(WorldAnalyzerPage), parameters) as WorldAnalyzerPage;
            Debug.Assert(page != null, nameof(page) + " != null");


            NavigationItem navItem = new()
            {
                Content = $"{Loc.T("World Analyzer")} - {e.SaveBackup.Name}",
                ToolTip = $"{Loc.T("World Analyzer")} - {e.SaveBackup.Name}",
                PageTag = pageTag,
                Icon = SymbolRegular.GlobeClock24,
                PageType = typeof(WorldAnalyzerPage),
                ContextMenu = new()
            };
            MenuItem menuItem = new()
            {
                Header = Loc.T("Close"),
                Icon = new SymbolIcon { Symbol = SymbolRegular.Prohibited24 },
            };
            menuItem.Click += (_, _) => {
                foreach (INavigationControl navigationControl in ViewModel.NavigationItems)
                {
                    NavigationItem nav = (NavigationItem)navigationControl;
                    if (nav.PageTag == pageTag)
                    {
                        ViewModel.NavigationItems.Remove(navItem);
                        RootNavigation.Navigate("backups");
                        break;
                    }
                }
            };
            navItem.ContextMenu.Items.Add(menuItem);
            navItem.Click += (_, clickEvent) =>
            {
                RootNavigation.Navigate(pageTag);
                RootNavigation.NavigateExternal(page);
                navItem.IsActive = true;
                clickEvent.Handled = true;
            };
            ViewModel.NavigationItems.Add(navItem);
            RootNavigation.Navigate(pageTag);
            RootNavigation.NavigateExternal(page);
            navItem.IsActive = true;
        }

        private void Logger_MessageLogged(object? sender, MessageLoggedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ControlAppearance appearance = ControlAppearance.Info;
                SymbolRegular symbol = SymbolRegular.Info24;
                string title = Loc.T("Info");
                if (e.Message.NotificationType == NotificationType.Error)
                {
                    appearance = ControlAppearance.Danger;
                    symbol = SymbolRegular.ErrorCircle24;
                    title = Loc.T("Error");
                }
                if (e.Message.NotificationType == NotificationType.Warning)
                {
                    appearance = ControlAppearance.Caution;
                    symbol = SymbolRegular.Warning24;
                    title = Loc.T("Warning");
                }
                if (e.Message.NotificationType == NotificationType.Success)
                {
                    appearance = ControlAppearance.Success;
                    symbol = SymbolRegular.CheckmarkCircle24;
                    title = Loc.T("Success");
                }

                if (!snackbar.IsShown || CompareTitle(title, snackbar.Title) >= 0)
                {
                    snackbar.Show(title, e.Message.Text, symbol, appearance);
                }
            });
        }

        private static int CompareTitle(string s1, string s2)
        {
            List<string> titles = [
                "Info",
                "Success",
                "Warning",
                "Error"
            ];

            if (titles.IndexOf(s1) > titles.IndexOf(s2)) return 1;
            if (titles.IndexOf(s1) == titles.IndexOf(s2)) return 0;
            return -1;
        }

        #region INavigationWindow methods

        public Frame GetFrame()
            => RootFrame;

        public INavigation GetNavigation()
            => RootNavigation;

        public bool Navigate(Type pageType)
            => RootNavigation.Navigate(pageType);

        public void SetPageService(IPageService pageService)
            => RootNavigation.PageService = pageService;

        public void ShowWindow()
            => Show();

        public void CloseWindow()
            => Close();

        #endregion INavigationWindow methods

        /// <summary>
        /// Raises the closed event.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            Settings.Default.Save();

            // Make sure that closing this window will begin the process of closing the application.
            Application.Current.Shutdown();
        }

        private static void TryToFindGameFolder()
        {
            if (File.Exists(Settings.Default.GameFolder + @"\Remnant2.exe"))
            {
                return;
            }

            // If Remnant not found or not installed, clear path
            Settings.Default.GameFolder = "";

            string? steamPath = FindSteamPath();
            if (steamPath != null)
            {
                Settings.Default.GameFolder = steamPath;
                return;
            }

            string? epicPath = FindEpicPath(); 
            if (epicPath != null)
            {
                Settings.Default.GameFolder = epicPath;
            }

            
            // Check if game is installed via Windows Store
            // TODO - don't have windows store version

        }

        private static string? FindEpicPath()
        {
            // Check if game is installed via Epic
            // Epic stores manifests for every installed game withing "C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests"
            // These "Manifests" are in json format, so if one of them is for Remnant, then Remnant is installed with epic
            DirectoryInfo epicManifestFolder = new(@"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests");
            if (epicManifestFolder.Exists) // If Folder don't exist, epic is not installed
            {
                foreach (FileInfo fi in epicManifestFolder.GetFiles("*.item"))
                {
                    string[] itemContent = File.ReadAllLines(fi.FullName);
                    if (itemContent.All(t => t.Contains("Remnant II") == false)) continue;

                    string? epicRemnantInstallPathRaw = itemContent.FirstOrDefault(t => t.Contains("\"InstallLocation\""));
                    string[]? epicRemnantInstallPathRawSplit = epicRemnantInstallPathRaw?.Split('\"');
                    string? epicRemnantInstallPath = epicRemnantInstallPathRawSplit?[3].Replace(@"\\", @"\");

                    if (epicRemnantInstallPath != null && Directory.Exists(epicRemnantInstallPath))
                    {
                        if (File.Exists(@$"{epicRemnantInstallPath}\Remnant2.exe"))
                        {
                            return epicRemnantInstallPath;
                        }
                    }

                    break;
                }
            }
            return null;
        }

        private static string? FindSteamPath()
        {
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
                    if (steamRegKey == null) return null;
                    if (steamRegKey.GetValue("SteamPath") is not string steamInstallPath) return null;
                    DirectoryInfo steamInstallDir = new(steamInstallPath);
                    if (steamInstallDir.Exists)
                    {
                        FileInfo libraryFolders = new(@$"{steamInstallDir.FullName}\steamapps\libraryfolders.vdf");
                        // Find Steam-Library, remnant is installed in
                        //
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
                        if (steamLibraryPath != null && Directory.Exists(steamRemnantInstallPath))
                        {
                            if (File.Exists(@$"{steamRemnantInstallPath}\Remnant2.exe"))
                            {
                                return steamRemnantInstallPath;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            Topmost = Settings.Default.TopMost;
        }
    }
}