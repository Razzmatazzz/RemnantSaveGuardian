using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace RemnantSaveGuardian.Services
{
    /// <summary>
    /// Save backup management (formerly the code-behind of the WPF Backups page).
    /// Lives for the whole session so automatic backups run whichever page is open.
    /// All state changes happen on the UI thread.
    /// </summary>
    public class BackupService
    {
        private static readonly string defaultBackupFolder = @$"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\Save Backups\Remnant 2";
        private readonly AppState appState;
        private List<SaveBackup> listBackups = new();
        private Process? gameProcess;
        private bool initialized;

        public BackupService(AppState appState)
        {
            this.appState = appState;
        }

        /// <summary>Raised whenever the backup list or its state changes.</summary>
        public event EventHandler? Changed;

        public IReadOnlyList<SaveBackup> Backups => listBackups;
        public bool IsLoaded { get; private set; }
        /// <summary>False when the active save already has a backup.</summary>
        public bool CanBackup { get; private set; } = true;
        public bool GameRunning { get; private set; }

        private static void OnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.Invoke(action);
            }
        }

        private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            try
            {
                if (Properties.Settings.Default.BackupFolder.Length == 0)
                {
                    Logger.Log(Loc.T("Backup folder not set; reverting to default."));
                    if (!Directory.Exists(defaultBackupFolder))
                    {
                        Directory.CreateDirectory(defaultBackupFolder);
                    }
                    Properties.Settings.Default.BackupFolder = defaultBackupFolder;
                }

                SaveWatcher.SaveUpdated += SaveWatcher_SaveUpdated;
                Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;

                GameRunning = IsRemnantRunning();
                Task.Run(LoadBackups);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading backups page: {ex}");
            }
        }

        private void Default_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "BackupFolder" || e.PropertyName == "SaveFolder")
            {
                Task.Run(LoadBackups);
            }
        }

        private bool ActiveSaveIsBackedUp
        {
            get
            {
                var activeSave = new RemnantSave(Properties.Settings.Default.SaveFolder);
                DateTime saveDate = File.GetLastWriteTime(activeSave.SaveProfilePath);
                return listBackups.Any(backup => saveDate.Equals(backup.SaveDate));
            }
            set
            {
                CanBackup = !value;
            }
        }

        public void OpenBackupFolder(SaveBackup backup)
        {
            Process.Start("explorer.exe", @$"{backup.Save.SaveFolderPath}\");
        }

        public void OpenBackupsFolder()
        {
            Process.Start("explorer.exe", @$"{Properties.Settings.Default.BackupFolder}\");
        }

        public void StartGame()
        {
            var gameDirPath = Properties.Settings.Default.GameFolder;
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

        public void Rename(SaveBackup backup, string name)
        {
            var old = backup.Name;
            backup.Name = name; // an empty name resets it to the save date ticks
            if (old != backup.Name)
            {
                backup.OnUpdated(new UpdatedEventArgs("Name"));
            }
            NotifyChanged();
        }

        public void SetKeep(SaveBackup backup, bool keep)
        {
            backup.Keep = keep; // raises Updated("Keep")
            NotifyChanged();
        }

        private void SaveWatcher_SaveUpdated(object? sender, EventArgs e)
        {
            OnUiThread(() =>
            {
                try
                {
                    if (Properties.Settings.Default.AutoBackup)
                    {
                        DateTime latestBackupTime;
                        DateTime newBackupTime;
                        if (listBackups.Count > 0)
                        {
                            latestBackupTime = listBackups[listBackups.Count - 1].SaveDate;
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

                    if (gameProcess == null || gameProcess.HasExited)
                    {
                        Process[] processes = Process.GetProcessesByName("Remnant2");
                        if (processes.Length > 0)
                        {
                            GameRunning = true;
                            gameProcess = processes[0];
                            gameProcess.EnableRaisingEvents = true;
                            gameProcess.Exited += (s, eargs) =>
                            {
                                OnUiThread(() =>
                                {
                                    GameRunning = false;
                                    DoBackup();
                                });
                            };
                            NotifyChanged();
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
            OnUiThread(() =>
            {
                ActiveSaveIsBackedUp = false;
                foreach (SaveBackup backup in listBackups)
                {
                    if (backup.Active) backup.Active = false;
                }
                NotifyChanged();
            });
        }

        private void LoadBackups()
        {
            try
            {
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
                // Oldest first, like the folder order the rest of the logic expects.
                list.Sort((a, b) => a.SaveDate.CompareTo(b.SaveDate));
                OnUiThread(() =>
                {
                    listBackups = list;
                    Logger.Log($"{Loc.T("Backups found")}: {listBackups.Count}");
                    if (listBackups.Count > 0)
                    {
                        Logger.Log($"{Loc.T("Last backup save date")}: {listBackups[listBackups.Count - 1].SaveDate}");
                    }
                    ActiveSaveIsBackedUp = (activeBackup != null);
                    IsLoaded = true;
                    NotifyChanged();
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading backups: {ex.Message}");
                OnUiThread(() =>
                {
                    IsLoaded = true;
                    NotifyChanged();
                });
            }
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
                    names[long.Parse(vals[0])] = System.Net.WebUtility.UrlDecode(vals[1]);
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
                    keeps[long.Parse(vals[0])] = bool.Parse(vals[1]);
                }
            }
            return keeps;
        }

        private bool BackupActive(SaveBackup saveBackup)
        {
            var activeSave = new RemnantSave(Properties.Settings.Default.SaveFolder);
            return DateTime.Compare(saveBackup.SaveDate, File.GetLastWriteTime(activeSave.SaveProfilePath)) == 0;
        }

        public void DoBackup()
        {
            try
            {
                var activeSave = new RemnantSave(Properties.Settings.Default.SaveFolder);
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
                    for (int i = listBackups.Count - 1; i >= 0; i--)
                    {
                        if (listBackups[i].SaveDate.Ticks == saveDate.Ticks)
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
                    foreach (SaveBackup saveBackup in listBackups)
                    {
                        saveBackup.Active = false;
                    }
                    backup.Active = true;
                    backup.Updated += SaveUpdated;
                    if (existingSaveIndex > -1)
                    {
                        listBackups[existingSaveIndex] = backup;
                    }
                    else
                    {
                        listBackups.Add(backup);
                    }
                }
                CheckBackupLimit();
                RefreshBackups();
                ActiveSaveIsBackedUp = true;
                Logger.Success($"{Loc.T("Backup completed")} ({saveDate})!");
                SaveFolderUnrecognizedFilesCheck();
                NotifyChanged();
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
            if (listBackups.Count > Properties.Settings.Default.BackupLimit && Properties.Settings.Default.BackupLimit > 0)
            {
                List<SaveBackup> removeBackups = new List<SaveBackup>();
                int delNum = listBackups.Count - Properties.Settings.Default.BackupLimit;
                for (int i = 0; i < listBackups.Count && delNum > 0; i++)
                {
                    if (!listBackups[i].Keep && !listBackups[i].Active)
                    {
                        Logger.Log($"{Loc.T("Deleting excess backup")} {listBackups[i].Name} ({listBackups[i].SaveDate})");
                        Directory.Delete($@"{Properties.Settings.Default.BackupFolder}\{listBackups[i].SaveDate.Ticks}", true);
                        removeBackups.Add(listBackups[i]);
                        delNum--;
                    }
                }

                for (int i = 0; i < removeBackups.Count; i++)
                {
                    listBackups.Remove(removeBackups[i]);
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
            foreach (SaveBackup s in listBackups)
            {
                if (!s.Name.Equals(s.SaveDate.Ticks.ToString()))
                {
                    savedNames.Add(s.SaveDate.Ticks + "=" + System.Net.WebUtility.UrlEncode(s.Name));
                }
            }
            Properties.Settings.Default.BackupName = savedNames.Count > 0 ? string.Join(",", savedNames) : "";
            Properties.Settings.Default.Save();
        }

        private void UpdateSavedKeeps()
        {
            List<string> savedKeeps = new List<string>();
            foreach (SaveBackup s in listBackups)
            {
                if (s.Keep)
                {
                    savedKeeps.Add(s.SaveDate.Ticks + "=True");
                }
            }
            Properties.Settings.Default.BackupKeep = savedKeeps.Count > 0 ? string.Join(",", savedKeeps) : "";
            Properties.Settings.Default.Save();
        }

        private static bool IsRemnantRunning()
        {
            return Process.GetProcessesByName("Remnant2").Length > 0;
        }

        /// <param name="type">"All", "World" or "Character".</param>
        public void RestoreBackup(SaveBackup? backup, string type = "All")
        {
            if (IsRemnantRunning())
            {
                Logger.Log(Loc.T("Exit the game before restoring a save backup."), LogType.Error);
                return;
            }

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

            var saveDirPath = Properties.Settings.Default.SaveFolder;
            var backupDirPath = Properties.Settings.Default.BackupFolder;

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
                    SaveWatcher.Resume();
                    return;
            }

            foreach (SaveBackup saveBackup in listBackups)
            {
                saveBackup.Active = false;
            }

            RefreshBackups();
            Logger.Log(Loc.T("Backup restored"), LogType.Success);
            SaveWatcher.Resume();
            appState.RaiseBackupSaveRestored();
            SaveFolderUnrecognizedFilesCheck();
            NotifyChanged();
        }

        private void SaveFolderUnrecognizedFilesCheck()
        {
            var invalidFiles = new List<string>();
            foreach (string file in Directory.GetFiles(Properties.Settings.Default.SaveFolder))
            {
                var fileName = Path.GetFileName(file);
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

        public void DeleteBackup(SaveBackup backup)
        {
            try
            {
                Directory.Delete(backup.Save.SaveFolderPath, true);

                listBackups.Remove(backup);
                RefreshBackups();
                Logger.Success(Loc.T("Backup deleted"));
            }
            catch (Exception ex)
            {
                Logger.Error($"{Loc.T("Could not delete backup:")} {ex.Message}");
            }
            NotifyChanged();
        }

        /// <summary>
        /// Imports a save folder (or the folder containing a picked file) as a new backup.
        /// Replaces drag-and-drop onto the WPF grid.
        /// </summary>
        public void ImportBackup(string path)
        {
            try
            {
                string? folder;
                FileAttributes attr = File.GetAttributes(path);
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                    folder = path;
                else
                    folder = Path.GetDirectoryName(path);
                if (folder == null)
                {
                    return;
                }
                var files = Directory.GetFiles(folder);
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
                backup.Updated += SaveUpdated;
                Logger.Success(Loc.T("Import_save_success"));
                listBackups.Add(backup);
                RefreshBackups();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error importing backup: {ex.Message}");
            }
            NotifyChanged();
        }

        private void RefreshBackups()
        {
            listBackups.Sort((a, b) => a.SaveDate.CompareTo(b.SaveDate));
            foreach (SaveBackup backup in listBackups)
            {
                if (BackupActive(backup))
                {
                    backup.Active = true;
                    break;
                }
            }
        }
    }
}
