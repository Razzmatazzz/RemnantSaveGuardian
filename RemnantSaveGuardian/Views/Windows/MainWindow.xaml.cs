using RemnantSaveGuardian.Services;
using RemnantSaveGuardian.ViewModels;
using RemnantSaveGuardian.Views.Pages;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
//using System.Windows.Forms;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Extensions;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Mvvm.Interfaces;
using WPFLocalizeExtension.Engine;

namespace RemnantSaveGuardian.Views.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INavigationWindow
    {
        public ViewModels.MainWindowViewModel ViewModel
        {
            get;
        }

        public MainWindow(ViewModels.MainWindowViewModel viewModel, IPageService pageService, INavigationService navigationService)
        {
            ViewModel = viewModel;
            DataContext = this;

            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }

            InitializeComponent();
            SetPageService(pageService);

            navigationService.SetNavigationControl(RootNavigation);

            try
            {
                Logger.MessageLogged += Logger_MessageLogged;

                var theme = Properties.Settings.Default.Theme;
                if (theme == "Light")
                {
                    Wpf.Ui.Appearance.Theme.Apply(Wpf.Ui.Appearance.ThemeType.Light);
                }
                else
                {
                    Wpf.Ui.Appearance.Theme.Apply(Wpf.Ui.Appearance.ThemeType.Dark);
                }

                if (Properties.Settings.Default.SaveFolder.Length == 0)
                {
                    Logger.Log("Save folder not set; reverting to default.");
                    Properties.Settings.Default.SaveFolder = RemnantSave.DefaultSaveFolder();
                    if (!Directory.Exists(RemnantSave.DefaultSaveFolder()))
                    {
                        Logger.Error(Loc.T("Could not find save file location; please set manually"));
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
                else if (!Directory.Exists(Properties.Settings.Default.SaveFolder) && !Properties.Settings.Default.SaveFolder.Equals(RemnantSave.DefaultSaveFolder))
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
                    //this.btnStartGame.IsEnabled = false;
                    //this.btnStartGame.Content = this.FindResource("PlayGrey");
                    //this.backupCMStart.IsEnabled = false;
                    //this.backupCMStart.Icon = this.FindResource("PlayGrey");
                    if (Properties.Settings.Default.GameFolder == "")
                    {
                        TryToFindGameFolder();
                    }
                }

                BackupsPage.BackupSaveViewed += BackupsPage_BackupSaveViewed;

                RootNavigation.Navigated += RootNavigation_Navigated;

                UpdateCheck.NewVersion += UpdateCheck_NewVersion;
                UpdateCheck.Error += UpdateCheck_Error;
                if (Properties.Settings.Default.AutoCheckUpdate)
                {
                    UpdateCheck.CheckForNewVersion();
                }
                LocalizeDictionary.Instance.MissingKeyEvent += (s, e) => {
                    //Logger.Log($"Missing translation for key: {e.Key}");
                };
            } catch (Exception ex)
            {
                Logger.Error($"Error loading main window: {ex}");
            }
        }

        private void UpdateCheck_Error(object? sender, UpdateCheckErrorEventArgs e)
        {
            Logger.Error($"{Loc.T("Error checking for new version")}: {e.Exception.Message}");
        }

        private void UpdateCheck_NewVersion(object? sender, NewVersionEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                Logger.Log(Loc.T("New version available!"));
            });
        }

        private void RootNavigation_Navigated([System.Diagnostics.CodeAnalysis.NotNull] INavigation sender, RoutedNavigationEventArgs e)
        {
            //Logger.Log(e.CurrentPage.ToString());
        }

        private void BackupsPage_BackupSaveViewed(object? sender, BackupSaveViewedEventArgs e)
        {
            var pageTag = $"world-analyzer-{e.SaveBackup.SaveDate.Ticks}";
            foreach (NavigationItem nav in ViewModel.NavigationItems)
            {
                if (nav.PageTag == pageTag)
                {
                    RootNavigation.Navigate(pageTag);
                    return;
                }
            }
            var viewM = Activator.CreateInstance(typeof(WorldAnalyzerViewModel)) as WorldAnalyzerViewModel;
            object[] parameters = { viewM, e.SaveBackup.Save.SaveFolderPath };
            var page = Activator.CreateInstance(typeof(WorldAnalyzerPage), parameters) as WorldAnalyzerPage;

            var navItem = new NavigationItem()
            {
                Content = $"{Loc.T("World Analyzer")} - {e.SaveBackup.Name}",
                ToolTip = $"{Loc.T("World Analyzer")} - {e.SaveBackup.Name}",
                PageTag = pageTag,
                Icon = SymbolRegular.GlobeClock24,
                PageType = typeof(WorldAnalyzerPage),
            };
            navItem.ContextMenu = new();
            var menuItem = new Wpf.Ui.Controls.MenuItem()
            {
                Header = Loc.T("Close"),
                Icon = new SymbolIcon() { Symbol = SymbolRegular.Prohibited24 },
            };
            menuItem.Click += (clickSender, clickEvent) => {
                foreach (NavigationItem nav in ViewModel.NavigationItems)
                {
                    if (nav.PageTag == pageTag)
                    {
                        ViewModel.NavigationItems.Remove(navItem);
                        RootNavigation.Navigate("backups");
                        break;
                    }
                }
            };
            navItem.ContextMenu.Items.Add(menuItem);
            navItem.Click += (clickSender, clickEvent) =>
            {
                RootNavigation.Navigate(pageTag);
                clickEvent.Handled = true;
            };
            ViewModel.NavigationItems.Add(navItem);
            RootNavigation.Navigate(pageTag);
        }

        private void Logger_MessageLogged(object? sender, MessageLoggedEventArgs e)
        {
            var appearance = ControlAppearance.Info;
            var symbol = SymbolRegular.Info24;
            var title = Loc.T("Info");
            if (e.LogType == LogType.Error)
            {
                appearance = ControlAppearance.Danger;
                symbol = SymbolRegular.ErrorCircle24;
                title = Loc.T("Error");
            }
            if (e.LogType == LogType.Warning)
            {
                appearance = ControlAppearance.Caution;
                symbol = SymbolRegular.Warning24;
                title = Loc.T("Warning");
            }
            if (e.LogType == LogType.Success)
            {
                appearance = ControlAppearance.Success;
                symbol = SymbolRegular.CheckmarkCircle24;
                title = Loc.T("Success");
            }
            snackbar.Show(title, e.Message, symbol, appearance);
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
            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam\Apps\1282100", false);
            if (key != null) // null if remnant is not in steam library (or steam itself is not (or never was) installed)
            {
                bool? steamRemnantInstalled = null;
                object keyValue = key.GetValue("Installed"); // Value is true when remnant is installed
                if (keyValue != null) steamRemnantInstalled = Convert.ToBoolean(keyValue);
                if (steamRemnantInstalled.HasValue && steamRemnantInstalled.Value)
                {
                    Microsoft.Win32.RegistryKey steamRegKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam", false);
                    string steamInstallPath = steamRegKey?.GetValue("SteamPath") as string; // Get install path for steam
                    DirectoryInfo steamInstallDir = new DirectoryInfo(steamInstallPath);
                    if (steamInstallDir.Exists)
                    {
                        FileInfo libraryFolders = new FileInfo(@$"{steamInstallDir.FullName}\steamapps\libraryfolders.vdf");
                        // Find Steam-Library, remnant is installed in
                        //
                        string[] libraryFolderContent = File.ReadAllLines(libraryFolders.FullName);
                        int remnantIndex = Array.IndexOf(libraryFolderContent, libraryFolderContent.FirstOrDefault(t => t.Contains("\"1282100\"")));
                        if (remnantIndex == -1)
                        {
                            remnantIndex = libraryFolderContent.Length;
                        }
                        libraryFolderContent = libraryFolderContent.Take(remnantIndex).ToArray();
                        string steamLibraryPathRaw = libraryFolderContent.LastOrDefault(t => t.Contains("\"path\""));
                        string[] steamLibraryPathRawSplit = steamLibraryPathRaw?.Split('\"');
                        string steamLibraryPath = steamLibraryPathRawSplit?[3];
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

                    string epicRemnantInstallPathRaw = itemContent.FirstOrDefault(t => t.Contains("\"InstallLocation\""));
                    string[] epicRemnantInstallPathRawSplit = epicRemnantInstallPathRaw?.Split('\"');
                    string epicRemnantInstallPath = epicRemnantInstallPathRawSplit?[3].Replace(@"\\", @"\");

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