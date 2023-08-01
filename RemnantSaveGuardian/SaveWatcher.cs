using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemnantSaveGuardian
{
    internal static class SaveWatcher
    {
        public static event EventHandler SaveUpdated;
        private static FileSystemWatcher watcher = new ()
        {
            //NotifyFilter = NotifyFilters.LastWrite,
            Filter = "profile.sav",
        };
        private static System.Timers.Timer saveTimer = new()
        {
            Interval = 2000,
            AutoReset = false,
        };

        static SaveWatcher()
        {
            watcher.Changed += OnSaveFileChanged;
            watcher.Created += OnSaveFileChanged;
            watcher.Deleted += OnSaveFileChanged;

            saveTimer.Elapsed += SaveTimer_Elapsed;

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
                saveTimer.Stop();
                saveTimer.Start();
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
                if (watcher.Path != path)
                {
                    watcher.Path = path;
                }
                watcher.EnableRaisingEvents = true;
            }
            else
            {
                watcher.EnableRaisingEvents = false;
            }
        }

        public static void Pause()
        {
            watcher.EnableRaisingEvents = false;
        }
        public static void Resume()
        {
            watcher.EnableRaisingEvents = true;
        }
    }
}
