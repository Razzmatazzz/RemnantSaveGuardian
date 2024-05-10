using System;
using System.IO;
using Remnant2SaveAnalyzer.Logging;

namespace Remnant2SaveAnalyzer
{
    internal static class SaveWatcher
    {
        public static event EventHandler? SaveUpdated;
        private static readonly FileSystemWatcher Watcher = new ()
        {
            //NotifyFilter = NotifyFilters.LastWrite,
            Filter = "profile.sav",
        };
        private static readonly System.Timers.Timer SaveTimer = new()
        {
            Interval = 2000,
            AutoReset = false,
        };

        static SaveWatcher()
        {
            Watcher.Changed += OnSaveFileChanged;
            Watcher.Created += OnSaveFileChanged;
            Watcher.Deleted += OnSaveFileChanged;

            SaveTimer.Elapsed += SaveTimer_Elapsed;

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
                SaveTimer.Stop();
                SaveTimer.Start();
            }
            catch (Exception ex)
            {
                Notifications.Error($"{ex.GetType()} {Loc.T("setting save file timer")}: {ex.Message} ({ex.StackTrace})");
            }
        }

        public static void Watch(string path)
        {
            if (Directory.Exists(path))
            {
                if (Watcher.Path != path)
                {
                    Watcher.Path = path;
                }
                Watcher.EnableRaisingEvents = true;
            }
            else
            {
                Watcher.EnableRaisingEvents = false;
            }
        }

        public static void Pause()
        {
            Watcher.EnableRaisingEvents = false;
        }
        public static void Resume()
        {
            Watcher.EnableRaisingEvents = true;
        }
    }
}
