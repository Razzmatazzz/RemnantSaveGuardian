using System;
using System.IO;
using System.Runtime.InteropServices;
using lib.remnant2.analyzer.Model;
using lib.remnant2.analyzer;

namespace RemnantSaveGuardian
{
    public class RemnantSave
    {
        private Dataset _remnantDataset;
        public Dataset Dataset => _remnantDataset;

        public static readonly string DefaultWgsSaveFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Packages\PerfectWorldEntertainment.RemnantFromtheAshes_jrajkyc4tsa6w\SystemAppData\wgs";
        private string _savePath;
        private string _profileFile;
        private RemnantSaveType _saveType;
        private WindowsSave _winSave;

        public static readonly Guid FolderidSavedGames = new(0x4C5C32FF, 0xBB9D, 0x43B0, 0xB5, 0xB4, 0x2D, 0x72, 0xE5, 0x4E, 0xAA, 0xA4);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        static extern string SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken = default);

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

        public string SaveFolderPath
        {
            get
            {
                return _savePath;
            }
        }

        public string SaveProfilePath
        {
            get
            {
                return _savePath + $@"\{_profileFile}";
            }
        }
        public RemnantSaveType SaveType
        {
            get { return _saveType; }
        }

        public bool Valid
        {
            get
            {
                return _saveType == RemnantSaveType.Normal || _winSave.Valid;
            }
        }

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
            else
            {
                string[] winFiles = Directory.GetFiles(folder, "container.*");
                if (winFiles.Length > 0)
                {
                    return true;
                }
            }
            return false;
        }

        public void UpdateCharacters()
        {
            _remnantDataset = Analyzer.Analyze(_savePath);
        }

        public static string DefaultSaveFolder()
        {
            string saveFolder = SHGetKnownFolderPath(FolderidSavedGames, 0) + @"\Remnant2";
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
