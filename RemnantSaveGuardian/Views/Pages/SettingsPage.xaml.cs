using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Common;
using Wpf.Ui.Common.Interfaces;
using Wpf.Ui.Extensions;

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

        public SettingsPage(ViewModels.SettingsViewModel viewModel)
        {
            ViewModel = viewModel;

            InitializeComponent();

            btnSaveFolder.Click += BtnSaveFolder_Click;
            var saveFolderContext = new ContextMenu();
            var saveFolderOpen = new MenuItem { Header = Loc.T("Open Folder") };
            saveFolderOpen.Click += SaveFolderOpen_Click;
            saveFolderContext.Items.Add(saveFolderOpen);
            txtSaveFolder.ContextMenu = saveFolderContext;

            btnGameFolder.Click += BtnGameFolder_Click;
            var gameFolderContext = new ContextMenu();
            var gameFolderOpen = new MenuItem { Header = Loc.T("Open Folder") };
            gameFolderOpen.Click += GameFolderOpen_Click;
            gameFolderContext.Items.Add(gameFolderOpen);
            txtGameFolder.ContextMenu = gameFolderContext;

            btnBackupFolder.Click += BtnBackupFolder_Click;
            var backupFolderContext = new ContextMenu();
            var backupFolderOpen = new MenuItem { Header = Loc.T("Open Folder") };
            backupFolderOpen.Click += BackupFolderOpen_Click;
            backupFolderContext.Items.Add(backupFolderOpen);
            txtBackupFolder.ContextMenu = backupFolderContext;
            cmbMissingItemColor.ItemsSource = new Dictionary<string, string>
            {
                { "Red", Loc.T("Red") },
                { "White", Loc.T("White") }
            };
            cmbMissingItemColor.DisplayMemberPath = "Value";
            cmbMissingItemColor.SelectedValuePath = "Key";
            if (Properties.Settings.Default.MissingItemColor == "Red")
            {
                cmbMissingItemColor.SelectedIndex = 0;
            }
            else
            {
                cmbMissingItemColor.SelectedIndex = 1;
            }
            cmbMissingItemColor.SelectionChanged += CmbMissingItemColor_SelectionChanged;
            cmbMissingItemColor.Visibility = Visibility.Collapsed;

            btnCheckUpdate.Click += BtnCheckUpdate_Click;

            radThemeLight.IsChecked = Properties.Settings.Default.Theme == "Light";
            radThemeLight.Checked += RadThemeLight_Checked;

            radThemeDark.IsChecked = Properties.Settings.Default.Theme != "Light";
            radThemeDark.Checked += RadThemeDark_Checked;

            lblVersion.Text = $"Remnant Save Guardian - {GetAssemblyVersion()}";

            Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;
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
        }

        private void RadThemeDark_Checked(object sender, RoutedEventArgs e)
        {
            ChangeTheme("Dark");
        }

        private void RadThemeLight_Checked(object sender, RoutedEventArgs e)
        {
            ChangeTheme("Light");
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
                var confirmResult = MessageBoxResult.No;
                List<String> backupFiles = Directory.GetDirectories(Properties.Settings.Default.BackupFolder).ToList();
                if (backupFiles.Count > 0)
                {
                    confirmResult = MessageBox.Show(Loc.T("Do you want to move your backups to this new folder?"),
                                    Loc.T("Move Backups"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                }
                if (confirmResult == MessageBoxResult.Yes)
                {
                    foreach (string file in backupFiles)
                    {
                        string subFolderName = file.Substring(file.LastIndexOf(@"\"));
                        Directory.CreateDirectory(folderName + subFolderName);
                        Directory.SetCreationTime(folderName + subFolderName, Directory.GetCreationTime(file));
                        Directory.SetLastWriteTime(folderName + subFolderName, Directory.GetCreationTime(file));
                        foreach (string filename in Directory.GetFiles(file))
                        {
                            File.Copy(filename, filename.Replace(Properties.Settings.Default.BackupFolder, folderName));
                        }
                        Directory.Delete(file, true);
                        //Directory.Move(file, folderName + subFolderName);
                    }
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
            Properties.Settings.Default.MissingItemColor = ((KeyValuePair<string, string>)cmbMissingItemColor.SelectedItem).Key;
        }

        private string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? String.Empty;
        }

        private void OpenFolder(string path)
        {
            Process.Start("explorer.exe", @$"{path}\");
        }
    }
}