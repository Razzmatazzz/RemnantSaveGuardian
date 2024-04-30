﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Common.Interfaces;
using RemnantSaveGuardian.Views.Windows;
using RemnantSaveGuardian.Helpers;

namespace RemnantSaveGuardian.Views.Pages
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

        private CultureInfo[] AvailableCultures = new CultureInfo[] { };

        public SettingsPage(ViewModels.SettingsViewModel viewModel)
        {
            ViewModel = viewModel;

            InitializeComponent();

            try
            {
                //cmbMissingItemColor.DisplayMemberPath = "Content";
                //cmbMissingItemColor.SelectedValuePath = "Tag";
                if (Properties.Settings.Default.MissingItemColor == "Highlight")
                {
                    cmbMissingItemColor.SelectedIndex = 1;
                }
                else
                {
                    cmbMissingItemColor.SelectedIndex = 0;
                }

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

                var langs = Application.Current.Properties["langs"] as CultureInfo[];

                cmbSwitchLanguage.ItemsSource = langs.Select(e => e.NativeName);
                if (Properties.Settings.Default.Language != "")
                {
                    cmbSwitchLanguage.SelectedItem = langs.First(e => Properties.Settings.Default.Language == e.Name).NativeName;
                }
                else
                {
                    var culture = Thread.CurrentThread.CurrentCulture;

                    if (culture.Parent != null || culture.Name != "pt-BR")
                        cmbSwitchLanguage.SelectedItem = culture.Parent.NativeName;
                    else
                        cmbSwitchLanguage.SelectedItem = culture.NativeName;
                }
                cmbSwitchLanguage.SelectionChanged += cmbSwitchLanguage_SelectionChanged;

                radThemeLight.IsChecked = Properties.Settings.Default.Theme == "Light";

                radThemeDark.IsChecked = Properties.Settings.Default.Theme != "Light";

                LinkToRepo.Content = $"{Loc.T("AboutRSG")} {GetAssemblyVersion()}";

                Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;
            } catch (Exception ex) {
                Logger.Error($"Error initializing settings page: {ex}");
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
                txtGameFolder.ContextMenu.IsEnabled = Directory.Exists(Properties.Settings.Default.GameFolder) && Properties.Settings.Default.GameFolder.Length > 0;
            }
            if (e.PropertyName == "SaveFolder")
            {
                txtSaveFolder.ContextMenu.IsEnabled = Directory.Exists(Properties.Settings.Default.SaveFolder) && Properties.Settings.Default.SaveFolder.Length > 0;
            }
            if (e.PropertyName == "BackupFolder")
            {
                txtBackupFolder.ContextMenu.IsEnabled = Directory.Exists(Properties.Settings.Default.BackupFolder) && Properties.Settings.Default.BackupFolder.Length > 0;
            }
            if (e.PropertyName == "EnableOpacity")
            {
                Logger.Log(Loc.T("Opacity_toggle_notice"));
            }
            if (e.PropertyName == "Opacity" || e.PropertyName == "OnlyInactive" || e.PropertyName == "Theme")
            {
                if (Properties.Settings.Default.EnableOpacity == false) { return; }
                var value = Properties.Settings.Default.Opacity;
                var mainWindow = Application.Current.MainWindow;
                if (value == 1 || (e.PropertyName == "OnlyInactive" && Properties.Settings.Default.OnlyInactive == true))
                {
                    WindowDwmHelper.ApplyDwm(mainWindow, WindowDwmHelper.UXMaterials.Mica);
                }
                else
                {
                    WindowDwmHelper.ApplyDwm(mainWindow, WindowDwmHelper.UXMaterials.None);
                }
                if (e.PropertyName == "OnlyInactive" && Properties.Settings.Default.OnlyInactive == true)
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
        private void ChangeTheme(string parameter)
        {
            var CurrentTheme = (Wpf.Ui.Appearance.ThemeType)Enum.Parse(typeof(Wpf.Ui.Appearance.ThemeType), Properties.Settings.Default.Theme);
            switch (parameter)
            {
                case "Light":
                    if (CurrentTheme == Wpf.Ui.Appearance.ThemeType.Light)
                        break;

                    Wpf.Ui.Appearance.Theme.Apply(Wpf.Ui.Appearance.ThemeType.Light);
                    Properties.Settings.Default.Theme = parameter;

                    break;

                default:
                    if (CurrentTheme == Wpf.Ui.Appearance.ThemeType.Dark)
                        break;

                    Wpf.Ui.Appearance.Theme.Apply(Wpf.Ui.Appearance.ThemeType.Dark);
                    Properties.Settings.Default.Theme = parameter;

                    break;
            }
        }

        private void BtnBackupFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
            openFolderDialog.SelectedPath = Properties.Settings.Default.BackupFolder;
            openFolderDialog.Description = Loc.T("Backup Folder");
            openFolderDialog.UseDescriptionForTitle = true;
            System.Windows.Forms.DialogResult result = openFolderDialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            string folderName = openFolderDialog.SelectedPath;
            if (folderName.Equals(Properties.Settings.Default.SaveFolder))
            {
                Logger.Warn(Loc.T("InvalidBackupFolderNoBackupsInSaves"));
                return;
            }
            if (folderName.Equals(Properties.Settings.Default.BackupFolder))
            {
                return;
            }
            if (Properties.Settings.Default.BackupFolder.Length > 0 && Directory.Exists(Properties.Settings.Default.BackupFolder))
            {
                List<String> backupFiles = Directory.GetDirectories(Properties.Settings.Default.BackupFolder).ToList();
                if (backupFiles.Count > 0)
                {
                    var messageBox = new Wpf.Ui.Controls.MessageBox()
                    {
                        Title = Loc.T("Move Backups"),
                        Content = Loc.T("Do you want to move your backups to this new folder?"),
                        ButtonLeftName = "Yes",
                        ButtonRightName = "No",
                    };
                    messageBox.ButtonRightClick += (s, ev) => {
                        messageBox.Hide();
                    };
                    messageBox.ButtonLeftClick += (s, ev) =>
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
                            DirectoryInfo currentBackupFolder = new DirectoryInfo(file);
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
            System.Windows.Forms.FolderBrowserDialog openFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
            openFolderDialog.SelectedPath = Properties.Settings.Default.GameFolder;
            openFolderDialog.Description = Loc.T("Game Folder");
            openFolderDialog.UseDescriptionForTitle = true;
            System.Windows.Forms.DialogResult result = openFolderDialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            string folderName = openFolderDialog.SelectedPath;
            if (!File.Exists(folderName + "\\Remnant2.exe"))
            {
                Logger.Warn(Loc.T("InvalidGameFolder"));
                return;
            }
            if (folderName.Equals(Properties.Settings.Default.GameFolder))
            {
                return;
            }
            txtGameFolder.Text = folderName;
            Properties.Settings.Default.GameFolder = folderName;
        }

        private void BtnSaveFolder_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
            openFolderDialog.SelectedPath = Properties.Settings.Default.SaveFolder;
            openFolderDialog.Description = Loc.T("Save Folder");
            openFolderDialog.UseDescriptionForTitle = true;
            System.Windows.Forms.DialogResult result = openFolderDialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            string folderName = openFolderDialog.SelectedPath;
            if (folderName.Equals(Properties.Settings.Default.BackupFolder))
            {
                Logger.Warn(Loc.T("InvalidSaveFolderNoSavesInBackups"));
                return;
            }
            if (folderName.Equals(Properties.Settings.Default.BackupFolder))
            {
                return;
            }
            if (!RemnantSave.ValidSaveFolder(folderName))
            {
                Logger.Warn(Loc.T("InvalidSaveFolder"));
                return;
            }
            txtSaveFolder.Text = folderName;
            Properties.Settings.Default.SaveFolder = folderName;
        }

        private void CmbMissingItemColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Properties.Settings.Default.MissingItemColor = ((ComboBoxItem)cmbMissingItemColor.SelectedItem).Tag.ToString();
        }

        private string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? String.Empty;
        }

        private void OpenFolder(string path)
        {
            Process.Start("explorer.exe", @$"{path}\");
        }

        private void cmbStartPage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = cmbStartPage.SelectedItem as ComboBoxItem;
            if (selected == null)
            {
                return;
            }
            var startPage = selected.Tag.ToString();
            if (startPage == Properties.Settings.Default.StartPage)
            {
                return;
            }
            Properties.Settings.Default.StartPage = startPage;
        }

        private void cmbWiki_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = cmbWiki.SelectedItem as ComboBoxItem;
            if (selected == null)
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

        private void cmbSwitchLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSwitchLanguage.SelectedIndex > -1)
            {
                var langs = Application.Current.Properties["langs"] as CultureInfo[];
                var culture = langs[cmbSwitchLanguage.SelectedIndex];

                Thread.CurrentThread.CurrentCulture = culture;
                WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.Culture = culture;
                Application.Current.MainWindow.Language = System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag);
                Properties.Settings.Default.Language = langs[cmbSwitchLanguage.SelectedIndex].Name;
                Logger.Success(Loc.T("Language_change_notice_{chosenLanguage}", new() { { "chosenLanguage", culture.DisplayName } }));
            }
        }

        private void sldOpacitySlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (Properties.Settings.Default.OnlyInactive == true)
            {
                var mainWindow = Application.Current.MainWindow;
                mainWindow.Opacity = 1;
                WindowDwmHelper.ApplyDwm(mainWindow, WindowDwmHelper.UXMaterials.Mica);
            }
        }
    }
}