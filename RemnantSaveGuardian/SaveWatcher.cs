using System;
using System.IO;

namespace RemnantSaveGuardian
{
    internal static class SaveWatcher
    {
        public static event EventHandler SaveUpdated;
        private static readonly FileSystemWatcher _watcher = new ()
        {
            //NotifyFilter = NotifyFilters.LastWrite,
            Filter = "profile.sav",
        };
        private static readonly System.Timers.Timer _saveTimer = new()
        {
            Interval = 2000,
            AutoReset = false,
        };

        static SaveWatcher()
        {
            _watcher.Changed += OnSaveFileChanged;
            _watcher.Created += OnSaveFileChanged;
            _watcher.Deleted += OnSaveFileChanged;

            _saveTimer.Elapsed += SaveTimer_Elapsed;

            Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;
        }

        private static void Default_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "SaveFolder")
            {
                return;
            }
            Watch(Properties.Settings.Default.SaveFolder);
        }

        private static void SaveTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            SaveUpdated?.Invoke(sender, e);
        }

        private static void OnSaveFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                //When the save files are modified, they are modified
                //multiple times in relatively rapid succession.
                //This timer is refreshed each time the save is modified,
                //and a backup only occurs after the timer expires.
                _saveTimer.Stop();
                _saveTimer.Start();
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex.GetType()} {Loc.T("setting save file timer")}: {ex.Message} ({ex.StackTrace})");
            }
        }

        public static void Watch(string path)
        {
            if (Directory.Exists(path))
            {
                if (_watcher.Path != path)
                {
                    _watcher.Path = path;
                }
                _watcher.EnableRaisingEvents = true;
            }
            else
            {
                _watcher.EnableRaisingEvents = false;
            }
        }

        public static void Pause()
        {
            _watcher.EnableRaisingEvents = false;
        }
        public static void Resume()
        {
            _watcher.EnableRaisingEvents = true;
        }
    }
}
