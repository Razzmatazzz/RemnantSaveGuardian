using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace RemnantSaveGuardian
{
    public class RemnantSave
    {
        private List<RemnantCharacter>? _Characters;
        public List<RemnantCharacter> Characters {
            get => _Characters ??= RemnantCharacter.GetCharactersFromSave(this, RemnantCharacter.CharacterProcessingMode.NoEvents);
        }

        public static readonly string DefaultWgsSaveFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Packages\PerfectWorldEntertainment.RemnantFromtheAshes_jrajkyc4tsa6w\SystemAppData\wgs";
        private string savePath;
        private string profileFile;
        private RemnantSaveType saveType;
        private WindowsSave winSave;

        public static readonly Guid FOLDERID_SavedGames = new(0x4C5C32FF, 0xBB9D, 0x43B0, 0xB5, 0xB4, 0x2D, 0x72, 0xE5, 0x4E, 0xAA, 0xA4);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        static extern string SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken = default);

        public RemnantSave(string path)
        {
            if (!Directory.Exists(path))
            {
                throw new Exception(path + " does not exist.");
            }

            if (File.Exists(path + @"\profile.sav"))
            {
                this.saveType = RemnantSaveType.Normal;
                this.profileFile = "profile.sav";
            }
            else
            {
                var winFiles = Directory.GetFiles(path, "container.*");
                if (winFiles.Length > 0)
                {
                    this.winSave = new WindowsSave(winFiles[0]);
                    this.saveType = RemnantSaveType.WindowsStore;
                    profileFile = winSave.Profile;
                }
                else
                {
                    throw new Exception(path + " is not a valid save.");
                }
            }
            this.savePath = path;
        }

        public string SaveFolderPath
        {
            get
            {
                return this.savePath;
            }
        }

        public string SaveProfilePath
        {
            get
            {
                return this.savePath + $@"\{this.profileFile}";
            }
        }
        public RemnantSaveType SaveType
        {
            get { return this.saveType; }
        }
        public string[] WorldSaves
        {
            get
            {
                if (this.saveType == RemnantSaveType.Normal)
                {
                    return Directory.GetFiles(SaveFolderPath, "save_*.sav");
                }
                else
                {
                    System.Console.WriteLine(this.winSave.Worlds.ToArray());
                    return this.winSave.Worlds.ToArray();
                }
            }
        }

        public bool Valid
        {
            get
            {
                return this.saveType == RemnantSaveType.Normal || this.winSave.Valid;
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
                var winFiles = Directory.GetFiles(folder, "container.*");
                if (winFiles.Length > 0)
                {
                    return true;
                }
            }
            return false;
        }

        public void UpdateCharacters()
        {
            Characters.Clear();
            Characters.AddRange(RemnantCharacter.GetCharactersFromSave(this));
        }

        public string GetProfileData()
        {
            return DecompressSaveAsString(this.SaveProfilePath);
        }

        public static string DefaultSaveFolder()
        {
            var saveFolder = SHGetKnownFolderPath(FOLDERID_SavedGames, 0) + @"\Remnant2";
            if (Directory.Exists($@"{saveFolder}\Steam"))
            {
                saveFolder += @"\Steam";
                var userFolders = Directory.GetDirectories(saveFolder);
                if (userFolders.Length > 0)
                {
                    return userFolders[0];
                }
            }
            else
            {
                var folders = Directory.GetDirectories(saveFolder);
                if (folders.Length > 0)
                {
                    return folders[0];
                }
            }
            return saveFolder;
        }

        // Credit to https://gist.github.com/crackedmind
        internal class ChunkHeader
        {
            public ulong unknown;
            public ulong unknown2;
            public byte unknown3;
            public ulong CompressedSize1;
            public ulong DecompressedSize1; // only valid for profile.sav with 1 chunk?
            public ulong CompressedSize2;
            public ulong DecompressedSize2; // only valid for profile.sav with 1 chunk?

            public static ChunkHeader ReadFromStream(Stream stream)
            {
                ChunkHeader header = new ChunkHeader();
                using var reader = new BinaryReader(stream, Encoding.UTF8, true);
                header.unknown = reader.ReadUInt64();
                header.unknown2 = reader.ReadUInt64();
                header.unknown3 = reader.ReadByte();
                header.CompressedSize1 = reader.ReadUInt64();
                header.DecompressedSize1 = reader.ReadUInt64();
                header.CompressedSize2 = reader.ReadUInt64();
                header.DecompressedSize2 = reader.ReadUInt64();
                return header;
            }
        }

        public static byte[] DecompressSave(string saveFilePath)
        {
            if (File.Exists(saveFilePath))
            {
                using var memstream = new MemoryStream();
                using var fileStream = File.Open(saveFilePath, FileMode.Open);

                fileStream.Seek(0xC, SeekOrigin.Current);
                while (fileStream.Position < fileStream.Length)
                {
                    ChunkHeader header = ChunkHeader.ReadFromStream(fileStream);
                    byte[] buffer = new byte[header.CompressedSize1];
                    fileStream.Read(buffer);

                    using var bufferStream = new MemoryStream(buffer);
                    using var decompressor = new ZLibStream(bufferStream, CompressionMode.Decompress);
                    decompressor.CopyTo(memstream);
                }
                fileStream.Dispose();

                var res = memstream.ToArray();

                return res;
            }
            return new byte[] { };
        }
        public static string DecompressSaveAsString(string saveFilePath)
        {
            return Encoding.ASCII.GetString(DecompressSave(saveFilePath));
        }
    }

    public enum RemnantSaveType
    {
        Normal,
        WindowsStore
    }
}
