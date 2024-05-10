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
using Remnant2SaveAnalyzer.Logging;
using Wpf.Ui.Common.Interfaces;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace Remnant2SaveAnalyzer.Views.Pages
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
        private static readonly string DefaultBackupFolder = @$"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\Save Backups\Remnant 2";
        private List<SaveBackup> _listBackups;
        //private RemnantSave activeSave;
        private Process? _gameProcess;

        private bool ActiveSaveIsBackedUp
        {
            get
            {
                RemnantSave activeSave = new(Properties.Settings.Default.SaveFolder,true);
                DateTime saveDate = File.GetLastWriteTime(activeSave.SaveProfilePath);
                foreach (SaveBackup backup in _listBackups)
                {
                    DateTime backupDate = backup.SaveDate;
                    if (saveDate.Equals(backupDate))
                    {
                        return true;
                    }
                }
                return false;
            }
            set => btnBackup.IsEnabled = !value;
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

        public BackupsPage(ViewModels.BackupsViewModel viewModel)
        {
            ViewModel = viewModel;

            InitializeComponent();

            _listBackups = [];
            try
            {
                dataBackups.CanUserDeleteRows = false;
                dataBackups.CanUserAddRows = false;
                dataBackups.Items.SortDescriptions.Add(new SortDescription("SaveDate", ListSortDirection.Descending));

                if (Properties.Settings.Default.BackupFolder.Length == 0)
                {
                    Notifications.Normal(Loc.T("Backup folder not set; reverting to default."));
                    if (!Directory.Exists(DefaultBackupFolder))
                    {
                        Directory.CreateDirectory(DefaultBackupFolder);
                    }
                    Properties.Settings.Default.BackupFolder = DefaultBackupFolder;
                }

                SaveWatcher.SaveUpdated += SaveWatcher_SaveUpdated;

                btnStartGame.IsEnabled = !IsRemnantRunning();
                Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;
                Task task = new(LoadBackups);
                task.Start();
            } catch (Exception ex) {
                Notifications.Error($"Error loading backups page: {ex}");
            }
        }

        private void MenuAnalyze_Click(object sender, RoutedEventArgs e)
        {
            if (dataBackups.SelectedItem is not SaveBackup backup)
            {
                return;
            }
            BackupSaveViewed?.Invoke(this, new(backup));
        }

        private void MenuOpenBackup_Click(object sender, RoutedEventArgs e)
        {
            if (dataBackups.SelectedItem is not SaveBackup backup)
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

            FileInfo remnantExe = new(gameDirPath + "\\Remnant2.exe");
            FileInfo remnantExe64 = new(gameDirPath + "\\Remnant\\Binaries\\Win64\\Remnant2-Win64-Shipping.exe");
            if (!remnantExe64.Exists && !remnantExe.Exists)
            {
                return;
            }

            Process.Start(remnantExe64.Exists && Environment.Is64BitOperatingSystem ? remnantExe64.FullName : remnantExe.FullName);
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
            List<string> editableColumns = [ 
                "Name",
                "Keep"
            ];
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
                    //Notifications.Normal($"{DateTime.Now.ToString()} File: {e.FullPath} {e.ChangeType}");
                    if (Properties.Settings.Default.AutoBackup)
                    {
                        //Notifications.Normal($"Save: {File.GetLastWriteTime(e.FullPath)}; Last backup: {File.GetLastWriteTime(listBackups[listBackups.Count - 1].Save.SaveFolderPath + "\\profile.sav")}");
                        DateTime newBackupTime;
                        if (_listBackups.Count > 0)
                        {
                            DateTime latestBackupTime = _listBackups[^1].SaveDate;
                            newBackupTime = latestBackupTime.AddMinutes(Properties.Settings.Default.BackupMinutes);
                        }
                        else
                        {
                            newBackupTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                        }
                        if (DateTime.Compare(DateTime.Now, newBackupTime) >= 0)
                        {
                            DoBackup();
                        }
                        else
                        {
                            ResetActiveBackupStatus();

                            TimeSpan span = newBackupTime - DateTime.Now;
                            Notifications.Normal(Loc.T("Save change detected; waiting {numMinutes} minutes until next backup", new() { { "numMinutes", $"{Math.Round(span.Minutes + span.Seconds / 60.0, 2)}" } }));
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
                            _gameProcess.Exited += (_, _) =>
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
                    Notifications.Error($"{ex.GetType()} {Loc.T("processing save file change")}: {ex.Message} ({ex.StackTrace})");
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
                Notifications.Normal(Loc.T("Backups folder not found, creating..."));
                Directory.CreateDirectory(Properties.Settings.Default.BackupFolder);
            }
            Dictionary<long, string> backupNames = GetSavedBackupNames();
            Dictionary<long, bool> backupKeeps = GetSavedBackupKeeps();
            string[] files = Directory.GetDirectories(Properties.Settings.Default.BackupFolder);
            SaveBackup? activeBackup = null;
            List<SaveBackup> list = [];
            foreach (string path in files)
            {
                if (RemnantSave.ValidSaveFolder(path))
                {
                    SaveBackup backup = new(path);
                    if (backup.Progression == null)
                    {
                        continue;
                    }
                    if (backupNames.TryGetValue(backup.SaveDate.Ticks, out string? name))
                    {
                        backup.Name = name;
                    }
                    if (backupKeeps.TryGetValue(backup.SaveDate.Ticks, out bool keep))
                    {
                        backup.Keep = keep;
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
                Notifications.Normal($"{Loc.T("Backups found")}: {_listBackups.Count}"); 
                if (_listBackups.Count > 0)
                {
                    Notifications.Normal($"{Loc.T("Last backup save date")}: {_listBackups[^1].SaveDate}");
                }
                if (activeBackup != null)
                {
                    dataBackups.SelectedItem = activeBackup;
                }
                ActiveSaveIsBackedUp = activeBackup != null;
                progressRing.Visibility = Visibility.Collapsed;
            });
        }

        private static Dictionary<long, string> GetSavedBackupNames()
        {
            Dictionary<long, string> names = [];
            string savedString = Properties.Settings.Default.BackupName;
            string[] savedNames = savedString.Split(',');
            foreach (string name in savedNames)
            {
                string[] tokens = name.Split('=');
                if (tokens.Length == 2)
                {
                    names.Add(long.Parse(tokens[0]), System.Net.WebUtility.UrlDecode(tokens[1]));
                }
            }
            return names;
        }

        private static Dictionary<long, bool> GetSavedBackupKeeps()
        {
            Dictionary<long, bool> keeps = [];
            string savedString = Properties.Settings.Default.BackupKeep;
            string[] savedKeeps = savedString.Split(',');
            foreach (string keep in savedKeeps)
            {
                string[] tokens = keep.Split('=');
                if (tokens.Length == 2)
                {
                    long key = long.Parse(tokens[0]);
                    if (!keeps.ContainsKey(key))
                    {
                        keeps.Add(key, bool.Parse(tokens[1]));
                    }
                }
            }
            return keeps;
        }

        private static bool BackupActive(SaveBackup saveBackup)
        {
            RemnantSave activeSave = new(Properties.Settings.Default.SaveFolder, true);
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
                RemnantSave activeSave = new(Properties.Settings.Default.SaveFolder, true);
                if (!activeSave.Valid)
                {
                    Notifications.Normal("Active save is not valid; backup skipped.");
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
                    SaveBackup backup = new(backupFolder);
                    if (backupNames.TryGetValue(backup.SaveDate.Ticks, out string? name))
                    {
                        backup.Name = name;
                    }
                    if (backupKeeps.TryGetValue(backup.SaveDate.Ticks, out bool keep))
                    {
                        backup.Keep = keep;
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
                Notifications.Success($"{Loc.T("Backup completed")} ({saveDate})!");
                SaveFolderUnrecognizedFilesCheck();
            }
            catch (IOException ex)
            {
                if (ex.Message.Contains("being used by another process"))
                {
                    Notifications.Normal(Loc.T("Save file in use; waiting 0.5 seconds and retrying."));
                    System.Threading.Thread.Sleep(500);
                    DoBackup();
                }
            }
        }
        private void CheckBackupLimit()
        {
            if (_listBackups.Count > Properties.Settings.Default.BackupLimit && Properties.Settings.Default.BackupLimit > 0)
            {
                List<SaveBackup> removeBackups = [];
                int delNum = _listBackups.Count - Properties.Settings.Default.BackupLimit;
                for (int i = 0; i < _listBackups.Count && delNum > 0; i++)
                {
                    if (!_listBackups[i].Keep && !_listBackups[i].Active)
                    {
                        Notifications.Normal($"{Loc.T("Deleting excess backup")} {_listBackups[i].Name} ({_listBackups[i].SaveDate})");
                        Directory.Delete($@"{Properties.Settings.Default.BackupFolder}\{_listBackups[i].SaveDate.Ticks}", true);
                        removeBackups.Add(_listBackups[i]);
                        delNum--;
                    }
                }

                foreach (var backup in removeBackups)
                {
                    _listBackups.Remove(backup);
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
            List<string> savedNames = [];
            foreach (var backup in _listBackups)
            {
                if (!backup.Name.Equals(backup.SaveDate.Ticks.ToString()))
                {
                    savedNames.Add(backup.SaveDate.Ticks + "=" + System.Net.WebUtility.UrlEncode(backup.Name));
                }
            }
            Properties.Settings.Default.BackupName = savedNames.Count > 0 ? string.Join(",", [.. savedNames]) : "";
            Properties.Settings.Default.Save();
        }

        private void UpdateSavedKeeps()
        {
            List<string> savedKeeps = [];
            foreach (var backup in _listBackups)
            {
                if (backup.Keep)
                {
                    savedKeeps.Add(backup.SaveDate.Ticks + "=True");
                }
            }
            Properties.Settings.Default.BackupKeep = savedKeeps.Count > 0 ? string.Join(",", [.. savedKeeps]) : "";
            Properties.Settings.Default.Save();
        }

        private void CheckBox_PreviewMouseDownEvent(object sender, MouseButtonEventArgs e)
        {
            // Mark handled to skip change checked state
            e.Handled = true;
        }
        private DataGridTemplateColumn GeneratingColumn(string strHeader, bool bEditable)
        {
            FrameworkElementFactory stackPanelFactory = new(typeof(StackPanel));
            FrameworkElementFactory checkBox = new(typeof(CheckBox));
            
            checkBox.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkBox.SetBinding(ToggleButton.IsCheckedProperty,
                new Binding
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

            DataTemplate dataTemplate = new()
            {
                VisualTree = stackPanelFactory
            };
            DataGridTemplateColumn templateColumn = new()
            {
                Header = strHeader,
                CellTemplate = dataTemplate
            };
            return templateColumn;
        }

        private void DataBackups_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            List<string> allowColumns = [ 
                "Name",
                "SaveDate",
                "Progression",
                "Keep",
                "Active"
            ];
            string header = e.Column.Header.ToString() ?? "";
            if (!allowColumns.Contains(header))
            {
                e.Cancel = true;
                return;
            }

            e.Column = header switch
            {
                "Keep" => GeneratingColumn("Keep", true),
                "Active" => GeneratingColumn("Active", false),
                _ => e.Column
            };

            e.Column.Header = new LocalizedColumnHeader(header);
        }

        private static bool IsRemnantRunning()
        {
            Process[] processName = Process.GetProcessesByName("Remnant2");
            if (processName.Length == 0)
            {
                return false;
            }
            return true;
        }

        private void RestoreBackup(string type = "All")
        {
            if (IsRemnantRunning())
            {
                Notifications.Error(Loc.T("Exit the game before restoring a save backup."));
                return;
            }

            if (dataBackups.SelectedItem is not SaveBackup backup)
            {
                Notifications.Error(Loc.T("Choose a backup to restore from the list."));
                return;
            }
            
            if (!ActiveSaveIsBackedUp)
            {
                DoBackup();
            }

            SaveWatcher.Pause();

            string? saveDirPath = Properties.Settings.Default.SaveFolder;

            DirectoryInfo di = new(saveDirPath);
            DirectoryInfo buDi = new(backup.SaveFolderPath);

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
                        FileInfo oldFile = new($"{di.FullName}\\{file.Name}");
                        if (oldFile.Exists) oldFile.Delete();

                        file.CopyTo($"{saveDirPath}\\{file.Name}");
                    }
                    break;
                case "World":
                    foreach (FileInfo file in buDi.GetFiles("save_?.sav"))
                    {
                        FileInfo oldFile = new($"{di.FullName}\\{file.Name}");
                        if (oldFile.Exists) oldFile.Delete();

                        file.CopyTo($"{saveDirPath}\\{file.Name}");
                    }
                    break;
                default:
                    Notifications.Error($"{Loc.T("Invalid backup restore type")}: {type}");
                    return;
            }

            foreach (SaveBackup saveBackup in _listBackups)
            {
                saveBackup.Active = false;
            }

            RefreshBackups();
            Notifications.Success(Loc.T("Backup restored"));
            SaveWatcher.Resume();
            BackupSaveRestored?.Invoke(this, EventArgs.Empty);
            SaveFolderUnrecognizedFilesCheck();
        }

        private static void SaveFolderUnrecognizedFilesCheck()
        {
            List<string> invalidFiles = [];
            foreach (string file in Directory.GetFiles(Properties.Settings.Default.SaveFolder))
            {
                string fileName = Path.GetFileName(file);
                if (!SaveFolderRecognizedFiles().Match(fileName).Success)
                {
                    if (fileName.EndsWith(".sav"))
                    {
                        invalidFiles.Add(fileName);
                    }
                }
            }
            if (invalidFiles.Count > 0)
            {
                Notifications.Warn(Loc.T("Unrecognized_save_files_warning_{fileList}", new() { { "fileList", string.Join(", ", invalidFiles) } }));
            }
        }

        private void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dataBackups.SelectedItem is not SaveBackup backup)
            {
                return;
            }
            MessageBox messageBox = new()
            {
                Title = Loc.T("Confirm Delete"),
                Content = new TextBlock
                {
                    Text = Loc.T("Are you sure you want to delete backup {backupName}?", new() {
                    { "backupName", backup.Name } }) + $"\n{Loc.T("Characters")}: {backup.Progression}\n{Loc.T("Date")}: {backup.SaveDate}",
                    TextWrapping = TextWrapping.WrapWithOverflow
                },
                ButtonLeftName = Loc.T("Delete")
            };
            messageBox.ButtonLeftClick += (_, _) => {
                DeleteBackup(backup);
                Notifications.Success(Loc.T("Backup deleted"));
                messageBox.Close();
            };
            messageBox.ButtonRightName = Loc.T("Cancel");
            messageBox.ButtonRightClick += (_, _) => {
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
                Notifications.Error($"{Loc.T("Could not delete backup:")} {ex.Message}");
            }
        }

        private void DataBackups_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                //Notifications.Normal(string.Join("\n", e.Data.GetFormats()));
                return;
            }

            string[]? data = e.Data.GetData(DataFormats.FileDrop) as string[];

            Debug.Assert(data != null, nameof(data) + " != null");
            List<string> draggedFiles = [.. data];
            FileAttributes attr = File.GetAttributes(draggedFiles[0]);
            string? folder = (attr & FileAttributes.Directory) == FileAttributes.Directory ? draggedFiles[0] : Path.GetDirectoryName(draggedFiles[0]);

            Debug.Assert(folder != null, nameof(folder) + " != null");
            string[] files = Directory.GetFiles(folder);
            if (!files.Any(file => file.EndsWith("profile.sav")))
            {
                Notifications.Error(Loc.T("No_profile_sav_found_warning"));
                return;
            }
            if (!files.Any(file => WorldSaveFile().Match(file).Success))
            {
                Notifications.Error(Loc.T("No_world_found_warning"));
                return;
            }
            DateTime saveDate = File.GetLastWriteTime(files[0]);
            string backupFolder = $@"{Properties.Settings.Default.BackupFolder}\{saveDate.Ticks}";
            if (Directory.Exists(backupFolder))
            {
                Notifications.Error(Loc.T("Import_failed_backup_exists"));
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
            SaveBackup backup = new(backupFolder);
            if (backupNames.TryGetValue(backup.SaveDate.Ticks, out string? name))
            {
                backup.Name = name;
            }
            if (backupKeeps.TryGetValue(backup.SaveDate.Ticks, out bool keep))
            {
                backup.Keep = keep;
            }
            Notifications.Success(Loc.T("Import_save_success"));
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

        [GeneratedRegex(@"^(profile|save_\d+)\.(sav|bak\d?|onl)|steam_autocloud.vdf$")]
        private static partial Regex SaveFolderRecognizedFiles();
        [GeneratedRegex(@"save_\d.sav$")]
        private static partial Regex WorldSaveFile();
    }

    public class BackupSaveViewedEventArgs(SaveBackup saveBackup) : EventArgs
    {
        public SaveBackup SaveBackup { get; set; } = saveBackup;
    }

    public class LocalizedColumnHeader(string key)
    {
        public string Key => key;

        public string Name => Loc.T(key);

        public override string ToString()
        {
            return Name;
        }
    }
}