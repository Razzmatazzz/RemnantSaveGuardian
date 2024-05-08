using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using lib.remnant2.analyzer.Model;
using lib.remnant2.analyzer;
using Newtonsoft.Json;
using Remnant2SaveAnalyzer.Properties;

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
        private readonly object _loadLock = new();

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

                try
                {
                    _remnantDataset = Analyzer.Analyze(_savePath, _remnantDataset);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    return;
                }

                if (first)
                {
                    ReportAnalyzerDebugMessages();
                    if (Settings.Default.ReportPerformance)
                    {
                        ReportAnalyzerPerformanceMetrics();
                    }

                    if (Settings.Default.ReportPlayerInfo)
                    {
                        ReportPlayerInfo();
                    }

                    if (Settings.Default.DumpAnalyzerJson)
                    {
                        DumpAnalyzerJson();
                    }
                }
            }
        }

        private void ReportAnalyzerDebugMessages()
        {
            if (_remnantDataset is { DebugMessages.Count: > 0 })
            {
                Logger.WarnSilent("BEGIN Analyser warnings");
                foreach (string message in _remnantDataset.DebugMessages)
                {
                    Logger.WarnSilent("  " + message);
                }
                Logger.Warn("There were some analyzer warnings");
            }
        }

        private void ReportAnalyzerPerformanceMetrics()
        {
            if (_remnantDataset is { DebugPerformance.Count: > 0 })
            {
                Logger.LogSilent("BEGIN Performance metrics");
                foreach (KeyValuePair<string, TimeSpan> message in _remnantDataset.DebugPerformance)
                {
                    Logger.LogSilent($"  {message.Key}; {message.Value}");
                }
                Logger.LogSilent("END Performance metrics");
            }
        }

        private void DumpAnalyzerJson()
        {
            JsonSerializer serializer = new()
            {
                Formatting = Formatting.Indented,
                ContractResolver = new Exporter.IgnorePropertiesResolver([
                    "Dataset",
                    "Parent",
                    "ProfileSaveFile",
                    "ProfileNavigator",
                    "WorldSaveFile",
                    "WorldNavigator",
                    "Character"
                ]),
                NullValueHandling = NullValueHandling.Ignore
            };

            using StreamWriter sw = new(@"analyzer.json");
            using JsonWriter writer = new JsonTextWriter(sw);
            serializer.Serialize(writer, _remnantDataset);
        }

        private void ReportPlayerInfo()
        {
            Debug.Assert(_remnantDataset != null, nameof(_remnantDataset) + " != null");
            Logger.LogSilent($"Active character save: save_{_remnantDataset.ActiveCharacterIndex}.sav");

            // Account Awards ------------------------------------------------------------
            Logger.LogSilent("BEGIN Account Awards");
            foreach (string award in _remnantDataset.AccountAwards)
            {
                if (award.StartsWith("AccountAward_"))
                {
                    LootItem? lootItem = ItemDb.GetItemByIdOrDefault(award);
                    if (lootItem == null)
                    {
                        Logger.WarnSilent($"  UnknownMarker account award: {award}");
                    }
                    else
                    {
                        Logger.LogSilent($"  Account award: {lootItem.Name}");
                    }
                }
            }
            foreach (Dictionary<string, string> m in ItemDb.Db.Where(x => x["Type"] == "award" && !_remnantDataset.AccountAwards.Exists(y => y == x["Id"])))
            {
                Logger.LogSilent($"  Missing {Utils.Capitalize(m["Type"])}: {m["Name"]}");
            }
            Logger.LogSilent("END Account Awards");

            for (int index = 0; index < _remnantDataset.Characters.Count; index++)
            {
                // Character ------------------------------------------------------------
                var character = _remnantDataset.Characters[index];
                int acquired = character.Profile.AcquiredItems;
                int missing = character.Profile.MissingItems.Count;
                int total = acquired + missing;
                Logger.LogSilent($"Character {index+1} (save_{character.Index}), Acquired Items: {acquired}, Missing Items: {missing}, Total: {total}");
                Logger.LogSilent($"Is Hardcore: {character.Profile.IsHardcore}");
                Logger.LogSilent($"Trait Rank: {character.Profile.TraitRank}");
                Logger.LogSilent($"Last Saved Trait Points: {character.Profile.LastSavedTraitPoints}");
                Logger.LogSilent($"Power Level: {character.Profile.PowerLevel}");
                Logger.LogSilent($"Item Level: {character.Profile.ItemLevel}");
                Logger.LogSilent($"Gender: {character.Profile.Gender}");

                // Inventory ------------------------------------------------------------
                Logger.LogSilent($"BEGIN Inventory, Character {index+1} (save_{character.Index}), mode: inventory");

                IEnumerable<LootItem> lootItems = character.Profile.Inventory
                    .Select(ItemDb.GetItemByProfileId)
                    .Where(x => x != null)!;

                IEnumerable<string> absent = character.Profile.Inventory.Where(x => ItemDb.GetItemByProfileId(x) == null);
                foreach (string s in absent)
                {
                    if (!Utils.IsKnownInventoryItem(Utils.GetNameFromProfileId(s)))
                    {

                        Logger.WarnSilent($"  Inventory item not found in database: {s}");
                    }
                }

                List<IGrouping<string, LootItem>> itemTypes = [.. lootItems
                    .GroupBy(x => x.Type)
                    .OrderBy(x=> x.Key)];

                foreach (IGrouping<string, LootItem> type in itemTypes)
                {
                    Logger.LogSilent("  " + Utils.Capitalize(type.Key)+":");
                    if (!type.Any())
                    {
                        Logger.LogSilent("    None");
                    }
                    foreach (LootItem lootItem in type.OrderBy(x => x.Name))
                    {
                        Logger.LogSilent("    " + lootItem.Name);
                    }
                }

                Logger.LogSilent($"END Inventory, Character {index+1} (save_{character.Index}), mode: inventory");


                // Campaign ------------------------------------------------------------
                Logger.LogSilent($"Save play time: {Utils.FormatPlaytime(character.Save.Playtime)}");
                foreach (Zone z in character.Save.Campaign.Zones)
                {
                    Logger.LogSilent($"Campaign story: {z.Story}");
                }
                Logger.LogSilent($"Campaign difficulty: {character.Save.Campaign.Difficulty}");
                Logger.LogSilent($"Campaign play time: {Utils.FormatPlaytime(character.Save.Campaign.Playtime)}");

                // Campaign Quest Inventory ------------------------------------------------------------
                Logger.LogSilent($"BEGIN Quest inventory, Character {index+1} (save_{character.Index}), mode: campaign");
                lootItems = character.Save.Campaign.QuestInventory.Select(ItemDb.GetItemByProfileId).Where(x => x != null).OrderBy(x => x!.Name)!;
                absent = character.Save.Campaign.QuestInventory.Where(x => ItemDb.GetItemByProfileId(x) == null);
                foreach (string s in absent)
                {
                    Logger.WarnSilent($"  Quest item not found in database: {s}");
                }

                foreach (LootItem lootItem in lootItems)
                {
                    Logger.LogSilent("  " + lootItem.Name);
                }
                Logger.LogSilent($"END Quest inventory, Character {index+1} (save_{character.Index}), mode: campaign");

                if (character.Save.Adventure != null)
                {
                    // Adventure ------------------------------------------------------------
                    Logger.LogSilent($"Adventure story: {character.Save.Adventure.Zones[0].Story}");
                    Logger.LogSilent($"Adventure difficulty: {character.Save.Adventure.Difficulty}");
                    Logger.LogSilent($"Adventure play time: {Utils.FormatPlaytime(character.Save.Adventure.Playtime)}");
                    
                    // Adventure Quest Inventory ------------------------------------------------------------
                    Logger.LogSilent($"BEGIN Quest inventory, Character {index+1} (save_{character.Index}), mode: adventure");
                    lootItems = character.Save.Adventure.QuestInventory.Select(ItemDb.GetItemByProfileId).Where(x => x != null).OrderBy(x => x!.Name)!;
                    absent = character.Save.Adventure.QuestInventory.Where(x => ItemDb.GetItemByProfileId(x) == null);
                    foreach (string s in absent)
                    {
                        Logger.WarnSilent($"  Quest item not found in database: {s}");
                    }

                    foreach (LootItem lootItem in lootItems)
                    {
                        Logger.LogSilent("  " + lootItem.Name);
                    }

                    Logger.LogSilent($"END Quest inventory, Character {index+1} (save_{character.Index}), mode: adventure");
                }

                // Cass shop ------------------------------------------------------------
                Logger.LogSilent($"BEGIN Cass shop, Character {index+1} (save_{character.Index})");
                foreach (LootItem lootItem in character.Save.CassShop)
                {
                    Logger.LogSilent("  " + lootItem.Name);
                }
                Logger.LogSilent($"END Cass shop, Character {index+1} (save_{character.Index})");

                // Quest log ------------------------------------------------------------
                Logger.LogSilent($"BEGIN Quest log, Character {index+1} (save_{character.Index})");
                lootItems = character.Save.QuestCompletedLog
                    .Select(x => ItemDb.GetItemByIdOrDefault($"Quest_{x}")).Where(x => x != null)!;
                absent = character.Save.QuestCompletedLog.Where(x => ItemDb.GetItemByIdOrDefault($"Quest_{x}") == null);
                foreach (string s in absent)
                {
                    Logger.WarnSilent($"  Quest not found in database: {s}");
                }
                foreach (LootItem lootItem in lootItems)
                {
                    Logger.LogSilent($"  {lootItem.Name} ({lootItem.Item["Subtype"]})");
                }
                Logger.LogSilent($"END Quest log, Character {index+1} (save_{character.Index})");

                // Achievements ------------------------------------------------------------
                Logger.LogSilent($"BEGIN Achievements for Character {index+1} (save_{character.Index})");
                foreach (ObjectiveProgress objective in character.Profile.Objectives)
                {
                    if (objective.Type == "achievement")
                    {
                        Logger.LogSilent($"  {Utils.Capitalize(objective.Type)}: {objective.Description} - {objective.Progress}");
                    }
                }

                foreach (Dictionary<string, string> m in ItemDb.Db.Where(x => x["Type"] == "achievement" && !character.Profile.Objectives.Exists(y => y.Id == x["Id"])))
                {
                    Logger.LogSilent($"  Missing {Utils.Capitalize(m["Type"])}: {m["Name"]}");
                }

                Logger.LogSilent($"END Achievements for Character {index+1} (save_{character.Index})");

                // Challenges ------------------------------------------------------------
                Logger.LogSilent($"BEGIN Challenges for Character {index+1} (save_{character.Index})");
                foreach (ObjectiveProgress objective in character.Profile.Objectives)
                {
                    if (objective.Type == "challenge")
                    {
                        Logger.LogSilent($"  {Utils.Capitalize(objective.Type)}: {objective.Description} - {objective.Progress}");
                    }
                }
                foreach (Dictionary<string, string> m in ItemDb.Db.Where(x => x["Type"] == "challenge" && !character.Profile.Objectives.Exists(y => y.Id == x["Id"])))
                {
                    Logger.LogSilent($"  Missing {Utils.Capitalize(m["Type"])}: {m["Name"]}");
                }
                Logger.LogSilent($"END Challenges for Character {index+1} (save_{character.Index})");
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
