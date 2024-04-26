using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Wpf.Ui.Common.Interfaces;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace RemnantSaveGuardian.Views.Pages
{
    /// <summary>
    /// Interaction logic for BackupsPage.xaml
    /// </summary>
    public partial class BackupsPage : INavigableView<ViewModels.BackupsViewModel>
    {
        public ViewModels.BackupsViewModel ViewModel
        {
            get;
        }
        public static event EventHandler<BackupSaveViewedEventArgs>? BackupSaveViewed;
        public static event EventHandler? BackupSaveRestored;
        private static string _defaultBackupFolder = @$"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\Save Backups\Remnant 2";
        private List<SaveBackup> _listBackups;
        //private RemnantSave activeSave;
        private Process? _gameProcess;
        public static bool IsDataLoaded;

        private bool ActiveSaveIsBackedUp
        {
            get
            {
                RemnantSave activeSave = new RemnantSave(Properties.Settings.Default.SaveFolder);
                DateTime saveDate = File.GetLastWriteTime(activeSave.SaveProfilePath);
                for (int i = 0; i < _listBackups.Count; i++)
                {
                    DateTime backupDate = _listBackups[i].SaveDate;
                    if (saveDate.Equals(backupDate))
                    {
                        return true;
                    }
                }
                return false;
            }
            set
            {
                btnBackup.IsEnabled = !value;
                /*if (value)
                {
                    lblStatus.ToolTip = "Backed Up";
                    lblStatus.Content = FindResource("StatusOK");
                    btnBackup.IsEnabled = false;
                    btnBackup.Content = FindResource("SaveGrey");
                }
                else
                {
                    lblStatus.ToolTip = "Not Backed Up";
                    lblStatus.Content = FindResource("StatusNo");
                    btnBackup.IsEnabled = true;
                    btnBackup.Content = FindResource("Save");
                }*/
            }
        }

        public BackupsPage(ViewModels.BackupsViewModel viewModel)
        {
            ViewModel = viewModel;

            InitializeComponent();

            try
            {
                dataBackups.CanUserDeleteRows = false;
                dataBackups.CanUserAddRows = false;
                dataBackups.Items.SortDescriptions.Add(new SortDescription("SaveDate", ListSortDirection.Descending));

                if (Properties.Settings.Default.BackupFolder.Length == 0)
                {
                    Logger.Log(Loc.T("Backup folder not set; reverting to default."));
                    if (!Directory.Exists(_defaultBackupFolder))
                    {
                        Directory.CreateDirectory(_defaultBackupFolder);
                    }
                    Properties.Settings.Default.BackupFolder = _defaultBackupFolder;
                }

                _listBackups = new List<SaveBackup>();

                SaveWatcher.SaveUpdated += SaveWatcher_SaveUpdated;

                btnStartGame.IsEnabled = !IsRemnantRunning();
                Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;
                Task task = new Task(LoadBackups);
                task.Start();
            } catch (Exception ex) {
                Logger.Error($"Error loading backups page: {ex}");
            }
        }

        private void MenuAnalyze_Click(object sender, RoutedEventArgs e)
        {
            SaveBackup? backup = dataBackups.SelectedItem as SaveBackup;
            if (backup == null)
            {
                return;
            }
            BackupSaveViewed?.Invoke(this, new() { SaveBackup = backup });
        }

        private void MenuOpenBackup_Click(object sender, RoutedEventArgs e)
        {
            SaveBackup? backup = dataBackups.SelectedItem as SaveBackup;
            if (backup == null)
            {
                return;
            }
            Process.Start("explorer.exe", @$"{backup.SaveFolderPath}\");
        }

        //private void MenuDelete_Click(object sender, System.Windows.RoutedEventArgs e)
        //{
        //    var backup = dataBackups.SelectedItem as SaveBackup;
        //    if (backup == null)
        //    {
        //        return;
        //    }
        //    var messageBox = new Wpf.Ui.Controls.MessageBox();
        //    messageBox.Title = Loc.T("Confirm Delete");
        //    messageBox.Content = new TextBlock()
        //    {
        //        Text = Loc.T("Are you sure you want to delete backup {backupName}?", new() {
        //            { "backupName", backup.Name } }) + $"\n{Loc.T("Characters")}: {string.Join(", ", backup.Save.Characters)}\n{Loc.T("Date")}: {backup.SaveDate.ToString()}",
        //        TextWrapping = System.Windows.TextWrapping.WrapWithOverflow
        //    };
        //    messageBox.ButtonLeftName = Loc.T("Delete");
        //    messageBox.ButtonLeftClick += (send, updatedEvent) => {
        //        DeleteBackup(backup);
        //        messageBox.Close();
        //    };
        //    messageBox.ButtonRightName = Loc.T("Cancel");
        //    messageBox.ButtonRightClick += (send, updatedEvent) => {
        //        messageBox.Close();
        //    };
        //    messageBox.ShowDialog();
        //}

        private void BtnStartGame_Click(object sender, RoutedEventArgs e)
        {
            string? gameDirPath = Properties.Settings.Default.GameFolder;
            if (!Directory.Exists(gameDirPath))
            {
                return;
            }

            FileInfo remnantExe = new FileInfo(gameDirPath + "\\Remnant2.exe");
            FileInfo remnantExe64 = new FileInfo(gameDirPath + "\\Remnant\\Binaries\\Win64\\Remnant2-Win64-Shipping.exe");
            if (!remnantExe64.Exists && !remnantExe.Exists)
            {
                return;
            }

            Process.Start((remnantExe64.Exists && Environment.Is64BitOperatingSystem) ? remnantExe64.FullName : remnantExe.FullName);
        }

        private void MenuRestoreWorlds_Click(object sender, RoutedEventArgs e)
        {
            RestoreBackup("World");
        }

        private void MenuRestoreCharacters_Click(object sender, RoutedEventArgs e)
        {
            RestoreBackup("Character");
        }

        private void MenuRestoreAll_Click(object sender, RoutedEventArgs e)
        {
            RestoreBackup();
        }

        private void ContextBackups_Opened(object sender, RoutedEventArgs e)
        {
            if (dataBackups.SelectedItem == null)
            {
                contextBackups.Visibility = Visibility.Collapsed;
                return;
            }
            contextBackups.Visibility = Visibility.Visible;
        }

        private void ContextBackups_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            SaveBackup? save = dataBackups.SelectedItem as SaveBackup;
            if (save == null) {
                return;
            }
        }

        private void DataBackups_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            LocalizedColumnHeader? header = (LocalizedColumnHeader)e.Column.Header;
            if (header.Key == "Name" && e.EditAction == DataGridEditAction.Commit)
            {
                SaveBackup sb = (SaveBackup)e.Row.Item;
                if (sb.Name.Equals(""))
                {
                    sb.Name = sb.SaveDate.Ticks.ToString();
                }
            }
        }

        private void DataBackups_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
        {
            LocalizedColumnHeader? header = (LocalizedColumnHeader)e.Column.Header;
            List<string> editableColumns = new List<string>() { 
                "Name",
                "Keep"
            };
            if (!editableColumns.Contains(header.Key)) e.Cancel = true;
        }

        private void BtnOpenBackupsFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", @$"{Properties.Settings.Default.BackupFolder}\");
        }

        private void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            DoBackup();
        }

        private void SaveWatcher_SaveUpdated(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    //Logger.Log($"{DateTime.Now.ToString()} File: {e.FullPath} {e.ChangeType}");
                    if (Properties.Settings.Default.AutoBackup)
                    {
                        //Logger.Log($"Save: {File.GetLastWriteTime(e.FullPath)}; Last backup: {File.GetLastWriteTime(listBackups[listBackups.Count - 1].Save.SaveFolderPath + "\\profile.sav")}");
                        DateTime latestBackupTime;
                        DateTime newBackupTime;
                        if (_listBackups.Count > 0)
                        {
                            latestBackupTime = _listBackups[_listBackups.Count - 1].SaveDate;
                            newBackupTime = latestBackupTime.AddMinutes(Properties.Settings.Default.BackupMinutes);
                        }
                        else
                        {
                            latestBackupTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                            newBackupTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                        }
                        if (DateTime.Compare(DateTime.Now, newBackupTime) >= 0)
                        {
                            DoBackup();
                        }
                        else
                        {
                            ResetActiveBackupStatus();

                            TimeSpan span = (newBackupTime - DateTime.Now);
                            Logger.Log(Loc.T("Save change detected; waiting {numMinutes} minutes until next backup", new() { { "numMinutes", $"{Math.Round(span.Minutes + (span.Seconds / 60.0), 2)}" } }));
                        }
                    }
                    else
                    {
                        ResetActiveBackupStatus();
                    }

                    if (_gameProcess == null || _gameProcess.HasExited)
                    {
                        Process[] processes = Process.GetProcessesByName("Remnant2");
                        if (processes.Length > 0)
                        {
                            btnStartGame.IsEnabled = false;
                            _gameProcess = processes[0];
                            _gameProcess.EnableRaisingEvents = true;
                            _gameProcess.Exited += (s, eargs) =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    btnStartGame.IsEnabled = true;
                                    DoBackup();
                                });
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"{ex.GetType()} {Loc.T("processing save file change")}: {ex.Message} ({ex.StackTrace})");
                }
            });
        }

        private void ResetActiveBackupStatus()
        {
            Dispatcher.Invoke(() =>
            {
                ActiveSaveIsBackedUp = false;

                foreach (SaveBackup backup in _listBackups)
                {
                    if (backup.Active) backup.Active = false;
                }

                dataBackups.Items.Refresh();
            });
        }

        private void LoadBackups()
        {
            System.Threading.Thread.Sleep(500); //Wait for UI render first
            if (!Directory.Exists(Properties.Settings.Default.BackupFolder))
            {
                Logger.Log(Loc.T("Backups folder not found, creating..."));
                Directory.CreateDirectory(Properties.Settings.Default.BackupFolder);
            }
            Dictionary<long, string> backupNames = GetSavedBackupNames();
            Dictionary<long, bool> backupKeeps = GetSavedBackupKeeps();
            string[] files = Directory.GetDirectories(Properties.Settings.Default.BackupFolder);
            SaveBackup? activeBackup = null;
            List<SaveBackup> list = new List<SaveBackup>();
            for (int i = 0; i < files.Length; i++)
            {
                if (RemnantSave.ValidSaveFolder(files[i]))
                {
                    SaveBackup backup = new SaveBackup(files[i]);
                    if (backupNames.ContainsKey(backup.SaveDate.Ticks))
                    {
                        backup.Name = backupNames[backup.SaveDate.Ticks];
                    }
                    if (backupKeeps.ContainsKey(backup.SaveDate.Ticks))
                    {
                        backup.Keep = backupKeeps[backup.SaveDate.Ticks];
                    }

                    if (BackupActive(backup))
                    {
                        backup.Active = true;
                        activeBackup = backup;
                    }

                    backup.Updated += SaveUpdated;

                    list.Add(backup);
                }
            }
            Dispatcher.Invoke(() =>
            {
                _listBackups.Clear();
                _listBackups = list;
                dataBackups.ItemsSource = null;
                dataBackups.ItemsSource = _listBackups;
                Logger.Log($"{Loc.T("Backups found")}: {_listBackups.Count}"); 
                if (_listBackups.Count > 0)
                {
                    Logger.Log($"{Loc.T("Last backup save date")}: {_listBackups[_listBackups.Count - 1].SaveDate}");
                }
                if (activeBackup != null)
                {
                    dataBackups.SelectedItem = activeBackup;
                }
                ActiveSaveIsBackedUp = (activeBackup != null);
                progressRing.Visibility = Visibility.Collapsed;
                IsDataLoaded = true;
            });
        }

        private Dictionary<long, string> GetSavedBackupNames()
        {
            Dictionary<long, string> names = new Dictionary<long, string>();
            string savedString = Properties.Settings.Default.BackupName;
            string[] savedNames = savedString.Split(',');
            for (int i = 0; i < savedNames.Length; i++)
            {
                string[] vals = savedNames[i].Split('=');
                if (vals.Length == 2)
                {
                    names.Add(long.Parse(vals[0]), System.Net.WebUtility.UrlDecode(vals[1]));
                }
            }
            return names;
        }

        private Dictionary<long, bool> GetSavedBackupKeeps()
        {
            Dictionary<long, bool> keeps = new Dictionary<long, bool>();
            string savedString = Properties.Settings.Default.BackupKeep;
            string[] savedKeeps = savedString.Split(',');
            for (int i = 0; i < savedKeeps.Length; i++)
            {
                string[] vals = savedKeeps[i].Split('=');
                if (vals.Length == 2)
                {
                    keeps.Add(long.Parse(vals[0]), bool.Parse(vals[1]));
                }
            }
            return keeps;
        }

        private bool BackupActive(SaveBackup saveBackup)
        {
            RemnantSave activeSave = new RemnantSave(Properties.Settings.Default.SaveFolder, true);
            if (DateTime.Compare(saveBackup.SaveDate, File.GetLastWriteTime(activeSave.SaveProfilePath)) == 0)
            {
                return true;
            }
            return false;
        }

        private void DoBackup()
        {
            try
            {
                RemnantSave activeSave = new RemnantSave(Properties.Settings.Default.SaveFolder, true);
                if (!activeSave.Valid)
                {
                    Logger.Log("Active save is not valid; backup skipped.");
                    return;
                }
                int existingSaveIndex = -1;
                DateTime saveDate = File.GetLastWriteTime(activeSave.SaveProfilePath);
                string backupFolder = $@"{Properties.Settings.Default.BackupFolder}\{saveDate.Ticks}";
                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                }
                else if (RemnantSave.ValidSaveFolder(backupFolder))
                {
                    for (int i = _listBackups.Count - 1; i >= 0; i--)
                    {
                        if (_listBackups[i].SaveDate.Ticks == saveDate.Ticks)
                        {
                            existingSaveIndex = i;
                            break;
                        }
                    }
                }
                foreach (string file in Directory.GetFiles(Properties.Settings.Default.SaveFolder, "*.sav"))
                {
                    File.Copy(file, $@"{backupFolder}\{Path.GetFileName(file)}", true);
                }
                if (RemnantSave.ValidSaveFolder(backupFolder))
                {
                    Dictionary<long, string> backupNames = GetSavedBackupNames();
                    Dictionary<long, bool> backupKeeps = GetSavedBackupKeeps();
                    SaveBackup backup = new SaveBackup(backupFolder);
                    if (backupNames.ContainsKey(backup.SaveDate.Ticks))
                    {
                        backup.Name = backupNames[backup.SaveDate.Ticks];
                    }
                    if (backupKeeps.ContainsKey(backup.SaveDate.Ticks))
                    {
                        backup.Keep = backupKeeps[backup.SaveDate.Ticks];
                    }
                    foreach (SaveBackup saveBackup in _listBackups)
                    {
                        saveBackup.Active = false;
                    }
                    backup.Active = true;
                    backup.Updated += SaveUpdated;
                    if (existingSaveIndex > -1)
                    {
                        _listBackups[existingSaveIndex] = backup;
                    }
                    else
                    {
                        _listBackups.Add(backup);
                    }
                }
                CheckBackupLimit();
                RefreshBackups();
                ActiveSaveIsBackedUp = true;
                Logger.Success($"{Loc.T("Backup completed")} ({saveDate})!");
                SaveFolderUnrecognizedFilesCheck();
            }
            catch (IOException ex)
            {
                if (ex.Message.Contains("being used by another process"))
                {
                    Logger.Log(Loc.T("Save file in use; waiting 0.5 seconds and retrying."));
                    System.Threading.Thread.Sleep(500);
                    DoBackup();
                }
            }
        }
        private void CheckBackupLimit()
        {
            if (_listBackups.Count > Properties.Settings.Default.BackupLimit && Properties.Settings.Default.BackupLimit > 0)
            {
                List<SaveBackup> removeBackups = new List<SaveBackup>();
                int delNum = _listBackups.Count - Properties.Settings.Default.BackupLimit;
                for (int i = 0; i < _listBackups.Count && delNum > 0; i++)
                {
                    if (!_listBackups[i].Keep && !_listBackups[i].Active)
                    {
                        Logger.Log($"{Loc.T("Deleting excess backup")} {_listBackups[i].Name} ({_listBackups[i].SaveDate})");
                        Directory.Delete($@"{Properties.Settings.Default.BackupFolder}\{_listBackups[i].SaveDate.Ticks}", true);
                        removeBackups.Add(_listBackups[i]);
                        delNum--;
                    }
                }

                for (int i = 0; i < removeBackups.Count; i++)
                {
                    _listBackups.Remove(removeBackups[i]);
                }
            }
        }
        private void SaveUpdated(object? sender, UpdatedEventArgs args)
        {
            if (args.FieldName.Equals("Name"))
            {
                UpdateSavedNames();
            }
            else if (args.FieldName.Equals("Keep"))
            {
                UpdateSavedKeeps();
            }
        }
        private void UpdateSavedNames()
        {
            List<string> savedNames = new List<string>();
            for (int i = 0; i < _listBackups.Count; i++)
            {
                SaveBackup s = _listBackups[i];
                if (!s.Name.Equals(s.SaveDate.Ticks.ToString()))
                {
                    savedNames.Add(s.SaveDate.Ticks + "=" + System.Net.WebUtility.UrlEncode(s.Name));
                }
                else
                {
                }
            }
            if (savedNames.Count > 0)
            {
                Properties.Settings.Default.BackupName = string.Join(",", savedNames.ToArray());
            }
            else
            {
                Properties.Settings.Default.BackupName = "";
            }
            Properties.Settings.Default.Save();
        }

        private void UpdateSavedKeeps()
        {
            List<string> savedKeeps = new List<string>();
            for (int i = 0; i < _listBackups.Count; i++)
            {
                SaveBackup s = _listBackups[i];
                if (s.Keep)
                {
                    savedKeeps.Add(s.SaveDate.Ticks + "=True");
                }
            }
            if (savedKeeps.Count > 0)
            {
                Properties.Settings.Default.BackupKeep = string.Join(",", savedKeeps.ToArray());
            }
            else
            {
                Properties.Settings.Default.BackupKeep = "";
            }
            Properties.Settings.Default.Save();
        }

        private void CheckBox_PreviewMouseDownEvent(object sender, MouseButtonEventArgs e)
        {
            // Mark handled to skip change checked state
            e.Handled = true;
        }
        private DataGridTemplateColumn GeneratingColumn(string strHeader, bool bEditable)
        {
            FrameworkElementFactory stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            FrameworkElementFactory checkBox = new FrameworkElementFactory(typeof(CheckBox));
            
            checkBox.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkBox.SetBinding(ToggleButton.IsCheckedProperty,
                new Binding()
                {
                    Path = new PropertyPath(strHeader),
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                }
                );

            if (bEditable == false)
            {
                checkBox.SetValue(CursorProperty, Cursors.No);
                checkBox.AddHandler(PreviewMouseDownEvent, new MouseButtonEventHandler(CheckBox_PreviewMouseDownEvent));
            }

            stackPanelFactory.SetValue(WidthProperty, (double)40);
            stackPanelFactory.AppendChild(checkBox);

            DataTemplate dataTemplate = new DataTemplate
            {
                VisualTree = stackPanelFactory
            };
            DataGridTemplateColumn templateColumn = new DataGridTemplateColumn
            {
                Header = strHeader,
                CellTemplate = dataTemplate
            };
            return templateColumn;
        }

        private void dataBackups_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            List<string> allowColumns = new List<string>() { 
                "Name",
                "SaveDate",
                "Progression",
                "Keep",
                "Active"
            };
            if (!allowColumns.Contains(e.Column.Header.ToString()))
            {
                e.Cancel = true;
                return;
            }
            if (e.Column.Header.ToString() == "Keep")
            {
                e.Column = GeneratingColumn("Keep", true);
            }
            if (e.Column.Header.ToString() == "Active")
            {
                e.Column = GeneratingColumn("Active", false);
            }

            e.Column.Header = new LocalizedColumnHeader(e.Column.Header.ToString());
        }

        private bool IsRemnantRunning()
        {
            Process[] pname = Process.GetProcessesByName("Remnant2");
            if (pname.Length == 0)
            {
                return false;
            }
            return true;
        }

        private void RestoreBackup(string type = "All")
        {
            if (IsRemnantRunning())
            {
                Logger.Log(Loc.T("Exit the game before restoring a save backup."), LogType.Error);
                return;
            }

            SaveBackup? backup = dataBackups.SelectedItem as SaveBackup;
            if (backup == null)
            {
                Logger.Log(Loc.T("Choose a backup to restore from the list."), LogType.Error);
                return;
            }
            
            if (!ActiveSaveIsBackedUp)
            {
                DoBackup();
            }

            SaveWatcher.Pause();

            string? saveDirPath = Properties.Settings.Default.SaveFolder;
            string? backupDirPath = Properties.Settings.Default.BackupFolder;

            DirectoryInfo di = new DirectoryInfo(saveDirPath);
            DirectoryInfo buDi = new DirectoryInfo(backupDirPath + "\\" + backup.SaveDate.Ticks);

            switch (type)
            {
                case "All":
                    foreach (FileInfo file in di.GetFiles())
                    {
                        file.Delete();
                    }

                    foreach (FileInfo file in buDi.GetFiles())
                    {
                        file.CopyTo($"{saveDirPath}\\{file.Name}");
                    }
                    break;
                case "Character":

                    foreach (FileInfo file in buDi.GetFiles("profile.sav"))
                    {
                        FileInfo oldFile = new FileInfo($"{di.FullName}\\{file.Name}");
                        if (oldFile.Exists) oldFile.Delete();

                        file.CopyTo($"{saveDirPath}\\{file.Name}");
                    }
                    break;
                case "World":
                    foreach (FileInfo file in buDi.GetFiles("save_?.sav"))
                    {
                        FileInfo oldFile = new FileInfo($"{di.FullName}\\{file.Name}");
                        if (oldFile.Exists) oldFile.Delete();

                        file.CopyTo($"{saveDirPath}\\{file.Name}");
                    }
                    break;
                default:
                    Logger.Log($"{Loc.T("Invalid backup restore type")}: {type}", LogType.Error);
                    return;
            }

            foreach (SaveBackup saveBackup in _listBackups)
            {
                saveBackup.Active = false;
            }

            RefreshBackups();
            Logger.Log(Loc.T("Backup restored"), LogType.Success);
            SaveWatcher.Resume();
            BackupSaveRestored?.Invoke(this, new());
            SaveFolderUnrecognizedFilesCheck();
        }

        private void SaveFolderUnrecognizedFilesCheck()
        {
            List<string> invalidFiles = new List<string>();
            foreach (string file in Directory.GetFiles(Properties.Settings.Default.SaveFolder))
            {
                string fileName = Path.GetFileName(file);
                if (!Regex.Match(fileName, @"^(profile|save_\d+)\.(sav|bak\d?|onl)|steam_autocloud.vdf$").Success)
                {
                    if (fileName.EndsWith(".sav"))
                    {
                        invalidFiles.Add(fileName);
                    }
                }
            }
            if (invalidFiles.Count > 0)
            {
                Logger.Warn(Loc.T("Unrecognized_save_files_warning_{fileList}", new() { { "fileList", string.Join(", ", invalidFiles) } }));
            }
        }

        private void menuDelete_Click(object sender, RoutedEventArgs e)
        {
            SaveBackup? backup = dataBackups.SelectedItem as SaveBackup;
            if (backup == null)
            {
                return;
            }
            MessageBox messageBox = new MessageBox();
            messageBox.Title = Loc.T("Confirm Delete");
            messageBox.Content = new TextBlock() {
                Text = Loc.T("Are you sure you want to delete backup {backupName}?", new() {
                    { "backupName", backup.Name } })+$"\n{Loc.T("Characters")}: {backup.Progression}\n{Loc.T("Date")}: {backup.SaveDate.ToString()}", 
                TextWrapping = TextWrapping.WrapWithOverflow 
            };
            messageBox.ButtonLeftName = Loc.T("Delete");
            messageBox.ButtonLeftClick += (send, updatedEvent) => {
                DeleteBackup(backup);
                Logger.Success(Loc.T("Backup deleted"));
                messageBox.Close();
            };
            messageBox.ButtonRightName = Loc.T("Cancel");
            messageBox.ButtonRightClick += (send, updatedEvent) => {
                messageBox.Close();
            };
            messageBox.ShowDialog();
        }

        private void DeleteBackup(SaveBackup backup)
        {
            try
            {
                Directory.Delete(backup.SaveFolderPath, true);

                _listBackups.Remove(backup);
                RefreshBackups();
            }
            catch (Exception ex)
            {
                Logger.Error($"{Loc.T("Could not delete backup:")} {ex.Message}");
            }
        }

        private void dataBackups_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                //Logger.Log(string.Join("\n", e.Data.GetFormats()));
                return;
            }
            List<string> draggedFiles = ((string[])e.Data.GetData(DataFormats.FileDrop)).ToList<string>();
            string folder;
            FileAttributes attr = File.GetAttributes(draggedFiles[0]);
            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                folder = draggedFiles[0];
            else
                folder = Path.GetDirectoryName(draggedFiles[0]);
            string[] files = Directory.GetFiles(folder);
            if (!files.Any(file => file.EndsWith("profile.sav")))
            {
                Logger.Error(Loc.T("No_profile_sav_found_warning"));
                return;
            }
            if (!files.Any(file => Regex.Match(file, @"save_\d.sav$").Success))
            {
                Logger.Error(Loc.T("No_world_found_warning"));
                return;
            }
            DateTime saveDate = File.GetLastWriteTime(files[0]);
            string backupFolder = $@"{Properties.Settings.Default.BackupFolder}\{saveDate.Ticks}";
            if (Directory.Exists(backupFolder))
            {
                Logger.Error(Loc.T("Import_failed_backup_exists"));
                return;
            }
            Directory.CreateDirectory(backupFolder);
            foreach (string file in files)
            {
                if (!file.EndsWith(".sav"))
                {
                    continue;
                }
                File.Copy(file, $@"{backupFolder}\{Path.GetFileName(file)}", true);
            }
            Dictionary<long, string> backupNames = GetSavedBackupNames();
            Dictionary<long, bool> backupKeeps = GetSavedBackupKeeps();
            SaveBackup backup = new SaveBackup(backupFolder);
            if (backupNames.ContainsKey(backup.SaveDate.Ticks))
            {
                backup.Name = backupNames[backup.SaveDate.Ticks];
            }
            if (backupKeeps.ContainsKey(backup.SaveDate.Ticks))
            {
                backup.Keep = backupKeeps[backup.SaveDate.Ticks];
            }
            Logger.Success(Loc.T("Import_save_success"));
            _listBackups.Add(backup);
            RefreshBackups();
        }

        private void RefreshBackups()
        {
            SortDescription sorting = dataBackups.Items.SortDescriptions.First();
            dataBackups.ItemsSource = null;
            dataBackups.ItemsSource = _listBackups;
            dataBackups.Items.SortDescriptions.Add(sorting);
            foreach (SaveBackup backup in _listBackups) {
                if (BackupActive(backup))
                {
                    backup.Active = true;
                    dataBackups.SelectedItem = backup;
                    break;
                }
            }
        }

        private void Default_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "Language")
                dataBackups.Items.Refresh();
        }
    }

    public class BackupSaveViewedEventArgs : EventArgs
    {
        public SaveBackup SaveBackup { get; set; }
    }

    public class LocalizedColumnHeader
    {
        private string _key;
        public string Key { 
            get {
                return _key;
            } 
        }
        public string Name
        {
            get
            {
                return Loc.T(_key);
            }
        }
        private string _name;
        public LocalizedColumnHeader(string key)
        {
            _key = key;
        }
        override public string ToString()
        {
            return Name;
        }
    }
}