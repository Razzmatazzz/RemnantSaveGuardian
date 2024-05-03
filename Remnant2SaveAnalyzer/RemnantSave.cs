using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using lib.remnant2.analyzer.Model;
using lib.remnant2.analyzer;

namespace Remnant2SaveAnalyzer
{
    public class RemnantSave
    {
        private Dataset? _remnantDataset;
        public Dataset? Dataset => _remnantDataset;

        
        // ReSharper disable CommentTypo
        //public static readonly string DefaultWgsSaveFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Packages\PerfectWorldEntertainment.RemnantFromtheAshes_jrajkyc4tsa6w\SystemAppData\wgs";
        // ReSharper restore CommentTypo
        private readonly string _savePath;
        private readonly string _profileFile;
        private readonly RemnantSaveType _saveType;
        private readonly WindowsSave? _winSave;
        private readonly object _loadLock = new object();

        public static readonly Guid FolderIdSavedGames = new(0x4C5C32FF, 0xBB9D, 0x43B0, 0xB5, 0xB4, 0x2D, 0x72, 0xE5, 0x4E, 0xAA, 0xA4);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
        static extern string SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken = default);
#pragma warning restore SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time

        public RemnantSave(string path, bool skipUpdate = false)
        {
            if (!Directory.Exists(path))
            {
                throw new Exception(path + " does not exist.");
            }

            if (File.Exists(path + @"\profile.sav"))
            {
                _saveType = RemnantSaveType.Normal;
                _profileFile = "profile.sav";
            }
            else
            {
                string[] winFiles = Directory.GetFiles(path, "container.*");
                if (winFiles.Length > 0)
                {
                    _winSave = new WindowsSave(winFiles[0]);
                    _saveType = RemnantSaveType.WindowsStore;
                    _profileFile = _winSave.Profile;
                }
                else
                {
                    throw new Exception(path + " is not a valid save.");
                }
            }
            _savePath = path;
            if (!skipUpdate)
            {
                UpdateCharacters();
            }
        }

        public string SaveFolderPath => _savePath;

        public string SaveProfilePath => _savePath + $@"\{_profileFile}";

        public bool Valid => _saveType == RemnantSaveType.Normal || (_winSave?.Valid ?? false);

        public static bool ValidSaveFolder(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return false;
            }

            if (File.Exists(folder + "\\profile.sav"))
            {
                return true;
            }

            string[] winFiles = Directory.GetFiles(folder, "container.*");
            if (winFiles.Length > 0)
            {
                return true;
            }
            return false;
        }

        public void UpdateCharacters()
        {
            lock (_loadLock)
            {
                bool first = _remnantDataset == null;

                _remnantDataset = Analyzer.Analyze(_savePath, _remnantDataset);
                if (first && _remnantDataset.DebugMessages.Count > 0)
                {
                    Logger.WarnSilent("BEGIN Analyser warnings");
                    foreach (string message in _remnantDataset.DebugMessages)
                    {
                        Logger.WarnSilent(message);
                    }

                    Logger.Warn("There were some analyzer warnings");
                }
                /*
                if (_remnantDataset.DebugPerformance.Count > 0)
                {
                    Logger.WarnSilent("BEGIN Performance metrics");
                    foreach (KeyValuePair<string,TimeSpan> message in _remnantDataset.DebugPerformance)
                    {
                        Logger.WarnSilent($"{message.Key}; {message.Value}");
                    }
                    Logger.WarnSilent("END Performance metrics");
                }
                */
            }
        }

        public static string DefaultSaveFolder()
        {
            string saveFolder = SHGetKnownFolderPath(FolderIdSavedGames, 0) + @"\Remnant2";
            if (Directory.Exists($@"{saveFolder}\Steam"))
            {
                saveFolder += @"\Steam";
                string[] userFolders = Directory.GetDirectories(saveFolder);
                if (userFolders.Length > 0)
                {
                    return userFolders[0];
                }
            }
            else
            {
                string[] folders = Directory.GetDirectories(saveFolder);
                if (folders.Length > 0)
                {
                    return folders[0];
                }
            }
            return saveFolder;
        }
    }

    public enum RemnantSaveType
    {
        Normal,
        WindowsStore
    }
}
