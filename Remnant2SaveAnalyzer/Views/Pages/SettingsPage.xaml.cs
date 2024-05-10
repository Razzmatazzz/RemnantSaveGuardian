using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Common.Interfaces;
using Remnant2SaveAnalyzer.Helpers;
using Wpf.Ui.Appearance;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using Remnant2SaveAnalyzer.Logging;

namespace Remnant2SaveAnalyzer.Views.Pages
{
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : INavigableView<ViewModels.SettingsViewModel>
    {
        public ViewModels.SettingsViewModel ViewModel
        {
            get;
        }

        public SettingsPage(ViewModels.SettingsViewModel viewModel)
        {
            ViewModel = viewModel;

            InitializeComponent();

            try
            {
                //cmbMissingItemColor.DisplayMemberPath = "Content";
                //cmbMissingItemColor.SelectedValuePath = "Tag";
                cmbMissingItemColor.SelectedIndex = Properties.Settings.Default.MissingItemColor == "Highlight" ? 1 : 0;
                cmbLootedItemColor.SelectedIndex = Properties.Settings.Default.LootedItemColor == "Dim" ? 1 : 0;
                cmbLogLevel.SelectedIndex = Properties.Settings.Default.LogLevel switch
                {
                    "Verbose" => 0,
                    "Debug" => 1,
                    "Information" => 2,
                    "Warning" => 3,
                    "Error" => 4,
                    "Fatal" => 5,
                    _ => 2
                };

                foreach (ComboBoxItem item in cmbStartPage.Items)
                {
                    if (item.Tag.ToString() == Properties.Settings.Default.StartPage)
                    {
                        cmbStartPage.SelectedItem = item;
                    }
                }

                foreach (ComboBoxItem item in cmbWiki.Items)
                {
                    if (item.Tag.ToString() == Properties.Settings.Default.Wiki)
                    {
                        cmbWiki.SelectedItem = item;
                    }
                }
                
                List<CultureInfo>? languages = Application.Current.Properties["langs"] as List<CultureInfo>;

                Debug.Assert(languages != null, nameof(languages) + " != null");

                cmbSwitchLanguage.ItemsSource = languages.Select(e => e.NativeName);
                if (Properties.Settings.Default.Language != "")
                {
                    cmbSwitchLanguage.SelectedItem = languages.First(e => Properties.Settings.Default.Language == e.Name).NativeName;
                }
                else
                {
                    CultureInfo culture = Thread.CurrentThread.CurrentCulture;

                    if (!string.IsNullOrEmpty(culture.Parent.Name)  || culture.Name != "pt-BR")
                        cmbSwitchLanguage.SelectedItem = culture.Parent.NativeName;
                    else
                        cmbSwitchLanguage.SelectedItem = culture.NativeName;
                }
                cmbSwitchLanguage.SelectionChanged += CmbSwitchLanguage_SelectionChanged;

                radThemeLight.IsChecked = Properties.Settings.Default.Theme == "Light";

                radThemeDark.IsChecked = Properties.Settings.Default.Theme != "Light";

                LinkToRepo.Content = $"{Loc.T("AboutRSG")} {GetAssemblyVersion()}";

                Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;
            } catch (Exception ex) {
                Notifications.Error($"Error initializing settings page: {ex}");
            }
        }

        private void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            UpdateCheck.CheckForNewVersion();
        }

        private void BackupFolderOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFolder(Properties.Settings.Default.BackupFolder);
        }

        private void GameFolderOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFolder(Properties.Settings.Default.GameFolder);
        }

        private void SaveFolderOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFolder(Properties.Settings.Default.SaveFolder);
        }

        private void Default_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "GameFolder")
            {
                Debug.Assert(txtGameFolder.ContextMenu != null, "txtGameFolder.ContextMenu != null");
                txtGameFolder.ContextMenu.IsEnabled = Directory.Exists(Properties.Settings.Default.GameFolder) && Properties.Settings.Default.GameFolder.Length > 0;
            }
            if (e.PropertyName == "SaveFolder")
            {
                Debug.Assert(txtSaveFolder.ContextMenu != null, "txtSaveFolder.ContextMenu != null");
                txtSaveFolder.ContextMenu.IsEnabled = Directory.Exists(Properties.Settings.Default.SaveFolder) && Properties.Settings.Default.SaveFolder.Length > 0;
            }
            if (e.PropertyName == "BackupFolder")
            {
                Debug.Assert(txtBackupFolder.ContextMenu != null, "txtBackupFolder.ContextMenu != null");
                txtBackupFolder.ContextMenu.IsEnabled = Directory.Exists(Properties.Settings.Default.BackupFolder) && Properties.Settings.Default.BackupFolder.Length > 0;
            }
            if (e.PropertyName == "EnableOpacity")
            {
                Notifications.Normal(Loc.T("Opacity_toggle_notice"));
            }
            if (e.PropertyName == "Opacity" || e.PropertyName == "OnlyInactive" || e.PropertyName == "Theme")
            {
                if (Properties.Settings.Default.EnableOpacity == false) { return; }
                float value = Properties.Settings.Default.Opacity;
                Window? mainWindow = Application.Current.MainWindow;
                Debug.Assert(mainWindow != null, nameof(mainWindow) + " != null");
                if (Math.Abs(value - 1) < 0.001 || (e.PropertyName == "OnlyInactive" && Properties.Settings.Default.OnlyInactive))
                {
                    WindowDwmHelper.ApplyDwm(mainWindow, WindowDwmHelper.UxMaterials.Mica);
                }
                else
                {
                    WindowDwmHelper.ApplyDwm(mainWindow, WindowDwmHelper.UxMaterials.None);
                }
                if (e.PropertyName == "OnlyInactive" && Properties.Settings.Default.OnlyInactive)
                {
                    mainWindow.Opacity = 1;
                }
                else
                {
                    mainWindow.Opacity = value;
                }
            }
        }

        private void RadThemeDark_Checked(object sender, RoutedEventArgs e)
        {
            ChangeTheme("Dark");
            CmbMissingItemColor_SelectionChanged(sender, null);
        }

        private void RadThemeLight_Checked(object sender, RoutedEventArgs e)
        {
            ChangeTheme("Light");
            CmbMissingItemColor_SelectionChanged(sender, null);
        }
        private static void ChangeTheme(string parameter)
        {
            ThemeType currentTheme = (ThemeType)Enum.Parse(typeof(ThemeType), Properties.Settings.Default.Theme);
            switch (parameter)
            {
                case "Light":
                    if (currentTheme == ThemeType.Light)
                        break;

                    Theme.Apply(ThemeType.Light);
                    Properties.Settings.Default.Theme = parameter;

                    break;

                default:
                    if (currentTheme == ThemeType.Dark)
                        break;

                    Theme.Apply(ThemeType.Dark);
                    Properties.Settings.Default.Theme = parameter;

                    break;
            }
        }

        private void BtnBackupFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFolderDialog = new()
            {
                SelectedPath = Properties.Settings.Default.BackupFolder,
                Description = Loc.T("Backup Folder"),
                UseDescriptionForTitle = true
            };
            System.Windows.Forms.DialogResult result = openFolderDialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            string folderName = openFolderDialog.SelectedPath;
            if (folderName.Equals(Properties.Settings.Default.SaveFolder))
            {
                Notifications.Warn(Loc.T("InvalidBackupFolderNoBackupsInSaves"));
                return;
            }
            if (folderName.Equals(Properties.Settings.Default.BackupFolder))
            {
                return;
            }
            if (Properties.Settings.Default.BackupFolder.Length > 0 && Directory.Exists(Properties.Settings.Default.BackupFolder))
            {
                List<string> backupFiles = [.. Directory.GetDirectories(Properties.Settings.Default.BackupFolder)];
                if (backupFiles.Count > 0)
                {
                    MessageBox messageBox = new()
                    {
                        Title = Loc.T("Move Backups"),
                        Content = Loc.T("Do you want to move your backups to this new folder?"),
                        ButtonLeftName = "Yes",
                        ButtonRightName = "No",
                    };
                    messageBox.ButtonRightClick += (_, _) => {
                        messageBox.Hide();
                    };
                    messageBox.ButtonLeftClick += (_, _) =>
                    {
                        foreach (string file in backupFiles)
                        {
                            FileAttributes attr = File.GetAttributes(file);
                            if ((attr & FileAttributes.Directory) != FileAttributes.Directory)
                            {
                                // skip anything that's not a folder
                                continue;
                            }
                            string subFolderName = Path.GetFileName(file);
                            DirectoryInfo currentBackupFolder = new(file);
                            DirectoryInfo newBackupFolder = Directory.CreateDirectory(Path.Combine(folderName, subFolderName));

                            foreach (FileInfo fileInfo in currentBackupFolder.GetFiles())
                            {
                                fileInfo.CopyTo(Path.Combine(newBackupFolder.FullName, fileInfo.Name), true);
                            }

                            Directory.SetCreationTime(Path.Combine(folderName, subFolderName), Directory.GetCreationTime(file));
                            Directory.SetLastWriteTime(Path.Combine(folderName, subFolderName), Directory.GetCreationTime(file));
                            Directory.Delete(file, true);
                        }
                        messageBox.Hide();
                    };
                    messageBox.Show();
                }
                
            }
            txtBackupFolder.Text = folderName;
            Properties.Settings.Default.BackupFolder = folderName;
        }

        private void BtnGameFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFolderDialog = new()
            {
                SelectedPath = Properties.Settings.Default.GameFolder,
                Description = Loc.T("Game Folder"),
                UseDescriptionForTitle = true
            };
            System.Windows.Forms.DialogResult result = openFolderDialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            string folderName = openFolderDialog.SelectedPath;
            if (!File.Exists(folderName + "\\Remnant2.exe"))
            {
                Notifications.Warn(Loc.T("InvalidGameFolder"));
                return;
            }
            if (folderName.Equals(Properties.Settings.Default.GameFolder))
            {
                return;
            }
            txtGameFolder.Text = folderName;
            Properties.Settings.Default.GameFolder = folderName;
        }

        private void BtnSaveFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFolderDialog = new()
            {
                SelectedPath = Properties.Settings.Default.SaveFolder,
                Description = Loc.T("Save Folder"),
                UseDescriptionForTitle = true
            };
            System.Windows.Forms.DialogResult result = openFolderDialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            string folderName = openFolderDialog.SelectedPath;
            if (folderName.Equals(Properties.Settings.Default.BackupFolder))
            {
                Notifications.Warn(Loc.T("InvalidSaveFolderNoSavesInBackups"));
                return;
            }
            if (folderName.Equals(Properties.Settings.Default.BackupFolder))
            {
                return;
            }
            if (!RemnantSave.ValidSaveFolder(folderName))
            {
                Notifications.Warn(Loc.T("InvalidSaveFolder"));
                return;
            }
            txtSaveFolder.Text = folderName;
            Properties.Settings.Default.SaveFolder = folderName;
        }

        private void CmbMissingItemColor_SelectionChanged(object sender, SelectionChangedEventArgs? e)
        {
            Properties.Settings.Default.MissingItemColor = ((ComboBoxItem)cmbMissingItemColor.SelectedItem).Tag.ToString();
        }

        private static string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
        }

        private static void OpenFolder(string path)
        {
            Process.Start("explorer.exe", @$"{path}\");
        }

        private void CmbStartPage_SelectionChanged(object sender, SelectionChangedEventArgs? e)
        {
            if (cmbStartPage.SelectedItem is not ComboBoxItem selected)
            {
                return;
            }
            string? startPage = selected.Tag.ToString();
            if (startPage == Properties.Settings.Default.StartPage)
            {
                return;
            }
            Properties.Settings.Default.StartPage = startPage;
        }

        private void CmbSwitchLanguage_SelectionChanged(object sender, SelectionChangedEventArgs? e)
        {
            if (cmbSwitchLanguage.SelectedIndex > -1)
            {
                List<CultureInfo>? langs = Application.Current.Properties["langs"] as List<CultureInfo>;
                Debug.Assert(langs != null, nameof(langs) + " != null");
                CultureInfo culture = langs[cmbSwitchLanguage.SelectedIndex];

                Thread.CurrentThread.CurrentCulture = culture;
                WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.Culture = culture;
                Window? mainWindow = Application.Current.MainWindow;
                Debug.Assert(mainWindow != null, nameof(mainWindow) + " != null");
                mainWindow.Language = System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag);
                Properties.Settings.Default.Language = langs[cmbSwitchLanguage.SelectedIndex].Name;
                Notifications.Success(Loc.T("Language_change_notice_{chosenLanguage}", new() { { "chosenLanguage", culture.DisplayName } }));
            }
        }

        private void SldOpacitySlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (Properties.Settings.Default.OnlyInactive)
            {
                Window? mainWindow = Application.Current.MainWindow;
                Debug.Assert(mainWindow != null, nameof(mainWindow) + " != null");
                mainWindow.Opacity = 1;
                WindowDwmHelper.ApplyDwm(mainWindow, WindowDwmHelper.UxMaterials.Mica);
            }
        }

        private void CmbWiki_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbWiki.SelectedItem is not ComboBoxItem selected)
            {
                return;
            }
            var wiki = selected.Tag.ToString();
            if (wiki == Properties.Settings.Default.Wiki)
            {
                return;
            }
            Properties.Settings.Default.Wiki = wiki;
        }

        private void CmbLootedItemColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Properties.Settings.Default.LootedItemColor = ((ComboBoxItem)cmbLootedItemColor.SelectedItem).Tag.ToString();
        }

        private void CmbLogLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Properties.Settings.Default.LogLevel = ((ComboBoxItem)cmbLogLevel.SelectedItem).Tag.ToString();
        }
    }
}