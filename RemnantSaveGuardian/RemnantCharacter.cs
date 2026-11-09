using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;
using RemnantSaveGuardian.Saves;

namespace RemnantSaveGuardian
{
    public class RemnantCharacter
    {
        public List<string> Archetypes { get; set; }
        public string SecondArchetype { get; set; }
        public List<string> Inventory { get; set; }
        public List<RemnantWorldEvent> CampaignEvents { get; }
        public List<RemnantWorldEvent> AdventureEvents { get; }
        public RemnantSave Save { get; }
        public int WorldIndex { get; } = -1;

        public int Progression
        {
            get
            {
                return this.Inventory.Count;
            }
        }

        private List<RemnantItem> missingItems;

        public enum ProcessMode { Campaign, Adventure };

        public override string ToString()
        {
            return string.Join(", ", Archetypes.Select(arch => Loc.GameT(arch))) + " (" + this.Progression + ")";
        }

        public string ToFullString()
        {
            string str = "CharacterData{ Archetypes: [" + string.Join(", ", Archetypes.Select(arch => Loc.T(arch))) + "], Inventory: [" + string.Join(", ", this.Inventory) + "], CampaignEvents: [" + string.Join(", ", this.CampaignEvents) + "], AdventureEvents: [" + string.Join(", ", this.AdventureEvents) + "] }";
            return str;
        }

        public RemnantCharacter(RemnantSave remnantSave, int index)
        {
            this.Archetypes = new List<string>();
            this.Inventory = new List<string>();
            this.CampaignEvents = new List<RemnantWorldEvent>();
            this.AdventureEvents = new List<RemnantWorldEvent>();
            this.missingItems = new List<RemnantItem>();
            this.Save = remnantSave;
            this.WorldIndex = index;
        }

        public enum CharacterProcessingMode { All, NoEvents };

        public static List<RemnantCharacter> GetCharactersFromSave(RemnantSave remnantSave)
        {
            return GetCharactersFromSave(remnantSave, CharacterProcessingMode.All);
        }

        public static List<RemnantCharacter> GetCharactersFromSave(RemnantSave remnantSave, CharacterProcessingMode mode)
        {
            List<RemnantCharacter> charData = new List<RemnantCharacter>();
            try
            {
                byte[] profileBytes = RemnantSave.DecompressSave(remnantSave.SaveProfilePath);
        #if DEBUG
                File.WriteAllText(remnantSave.SaveProfilePath.Replace(".sav", ".txt"), Encoding.ASCII.GetString(profileBytes));
        #endif
                ProfileSave? profile = null;
                try
                {
                    profile = ProfileSave.FromArchive(SaveFile.ReadTopLevelArchive(profileBytes));
                }
                catch (Exception ex)
                {
                    // Unrecognized layout (e.g. a newer game version): fall back to scanning the save as text.
                    Logger.Warn($"Could not read profile as structured data ({ex.Message}); using text search instead.");
                }
                charData = profile != null
                    ? GetCharactersFromProfile(remnantSave, profile)
                    : GetCharactersFromProfileText(remnantSave, Encoding.ASCII.GetString(profileBytes));

                if (mode == CharacterProcessingMode.All)
                {
                    foreach (var cd in charData)
                    {
                        cd.LoadWorldData();
                    }
                }
            }
            catch (IOException ex)
            {
                if (ex.Message.Contains("being used by another process"))
                {
                    if (App.GetService<Services.BackupService>()?.IsLoaded == true)
                    {
                        Logger.Warn(Loc.T("Save file in use; waiting 0.5 seconds and retrying."));
                    }
                    System.Threading.Thread.Sleep(500);
                    charData = GetCharactersFromSave(remnantSave, mode);
                }
            }
            return charData;
        }

        private static readonly Regex ArchetypePattern = new(@"/Game/World_(Base|DLC\d+)/Items/Archetypes/(?<archetype>\w+)/Archetype_\w+_UI\.Archetype_\w+_UI_C");

        private static List<RemnantCharacter> GetCharactersFromProfile(RemnantSave remnantSave, ProfileSave profile)
        {
            var charData = new List<RemnantCharacter>();
            var worldSaves = remnantSave.WorldSaves;
            for (var i = 0; i < profile.Characters.Count; i++)
            {
                var saved = profile.Characters[i];
                // World saves are named after the character slot; containers without file names keep save order.
                var worldIndex = Array.FindIndex(worldSaves, p => Path.GetFileName(p).Equals($"save_{saved.SlotIndex}.sav", StringComparison.OrdinalIgnoreCase));
                var cd = new RemnantCharacter(remnantSave, worldIndex != -1 ? worldIndex : i);

                foreach (var archetypePath in new[] { saved.ArchetypePath, saved.SecondaryArchetypePath })
                {
                    var match = archetypePath != null ? ArchetypePattern.Match(archetypePath) : Match.Empty;
                    if (match.Success)
                    {
                        cd.Archetypes.Add(match.Groups["archetype"].Value);
                    }
                }
                cd.SecondArchetype = cd.Archetypes.Count > 1 ? cd.Archetypes[1] : "";
                if (cd.Archetypes.Count == 0)
                {
                    cd.Archetypes.Add("Unknown");
                }

                var itemPaths = saved.AllItemPaths.ToList();
                foreach (string pattern in RemnantItem.ItemKeyPatterns)
                {
                    var regex = new Regex(pattern);
                    foreach (var path in itemPaths)
                    {
                        foreach (Match itemMatch in regex.Matches(path))
                        {
                            cd.Inventory.Add(itemMatch.Value.Replace(".", "").ToLower());
                        }
                    }
                }
                charData.Add(cd);
            }
            return charData;
        }

        private static List<RemnantCharacter> GetCharactersFromProfileText(RemnantSave remnantSave, string profileData)
        {
            List<RemnantCharacter> charData = new List<RemnantCharacter>();
            var archetypes = ArchetypePattern.Matches(profileData);
            var inventoryStarts = Regex.Matches(profileData, "/Game/Characters/Player/Base/Character_Master_Player.Character_Master_Player_C");
            var inventoryEnds = Regex.Matches(profileData, "[^.]Character_Master_Player_C");
            for (var i = 0; i < inventoryStarts.Count; i++)
            {
                Match invMatch = inventoryStarts[i];
                Match invEndMatch = inventoryEnds.First(m => m.Index > invMatch.Index);
                var inventory = profileData.Substring(invMatch.Index, invEndMatch.Index - invMatch.Index);
                RemnantCharacter cd = new RemnantCharacter(remnantSave, i);
                for (var m = 0; m < archetypes.Count; m++)
                {
                    Match archMatch = archetypes[m];
                    int prevCharEnd = 0;
                    if (i > 0)
                    {
                        Match prevInvStart = inventoryStarts[i - 1];
                        prevCharEnd = inventoryEnds.First(m => m.Index > prevInvStart.Index).Index;
                    }
                    if (archMatch.Index > prevCharEnd && archMatch.Index < invMatch.Index)
                    {
                        cd.Archetypes.Add(archMatch.Groups["archetype"].Value);
                    }
                }
                if (cd.Archetypes.Count == 0)
                {
                    cd.Archetypes.Add("Unknown");
                }

                foreach (string pattern in RemnantItem.ItemKeyPatterns)
                {
                    var itemMatches = new Regex(pattern).Matches(inventory);
                    foreach (Match itemMatch in itemMatches)
                    {
                        cd.Inventory.Add(itemMatch.Value.Replace(".", "").ToLower());
                    }
                }
                charData.Add(cd);
            }
            return charData;
        }

        public void LoadWorldData()
        {
            if (this.Save == null)
            {
                return;
            }
            /*if (this.CampaignEvents.Count != 0)
            {
                return;
            }*/
            if (this.WorldIndex >= Save.WorldSaves.Length)
            {
                return;
            }
            try
            {
                RemnantWorldEvent.ProcessEvents(this);

                missingItems.Clear();
                foreach (List <RemnantItem> eventItems in GameInfo.EventItem.Values)
                {
                    foreach (RemnantItem item in eventItems)
                    {
                        if (!this.Inventory.Contains(item.Key.ToLower()))
                        {
                            if (!missingItems.Contains(item))
                            {
                                missingItems.Add(item);
                            }
                        }
                    }
                }
                missingItems.Sort();
            }
            catch (IOException ex)
            {
                if (ex.Message.Contains("being used by another process"))
                {
                    Logger.Warn(Loc.T("Save file in use; waiting 0.5 seconds and retrying."));
                    System.Threading.Thread.Sleep(500);
                    LoadWorldData();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading world Data in CharacterData.LoadWorldData: {ex.Message} {ex.StackTrace}");
            }
        }

        public List<RemnantItem> GetMissingItems()
        {
            return missingItems;
        }
    }
}
