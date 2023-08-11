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
                            string subFolderName = file.Substring(file.LastIndexOf(@"\"));
                            DirectoryInfo currentBackupFolder = new DirectoryInfo(file);
                            DirectoryInfo newBackupFolder = Directory.CreateDirectory(folderName + subFolderName);

                            foreach (FileInfo fileInfo in currentBackupFolder.GetFiles())
                            {
                                fileInfo.CopyTo(Path.Combine(newBackupFolder.FullName, fileInfo.Name), true);
                            }

                            Directory.SetCreationTime(folderName + subFolderName, Directory.GetCreationTime(file));
                            Directory.SetLastWriteTime(folderName + subFolderName, Directory.GetCreationTime(file));
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
    }
}