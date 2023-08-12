using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;

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
                string profileData = remnantSave.GetProfileData();
                var archetypes = Regex.Matches(profileData, @"/Game/World_Base/Items/Archetypes/(?<archetype>\w+)/Archetype_\w+_UI\.Archetype_\w+_UI_C");
                var inventoryStarts = Regex.Matches(profileData, "/Game/Characters/Player/Base/Character_Master_Player.Character_Master_Player_C");
                var inventoryEnds = Regex.Matches(profileData, "[^.]Character_Master_Player_C");
                for (var i = 0; i < inventoryStarts.Count; i++)
                {
                    //Debug.WriteLine($"character {i}");
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

                    /*rx = new Regex(@"/Items/QuestItems(/[a-zA-Z0-9_]+)+/[a-zA-Z0-9_]+");
                    matches = rx.Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Value);
                    }

                    rx = new Regex(@"/Quests/[a-zA-Z0-9_]+/[a-zA-Z0-9_]+");
                    matches = rx.Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Value);
                    }

                    rx = new Regex(@"/Player/Emotes/Emote_[a-zA-Z0-9]+");
                    matches = rx.Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Value);
                    }*/

                    if (mode == CharacterProcessingMode.All)
                    {
                        cd.LoadWorldData();
                    }
                    charData.Add(cd);
                }
            }
            catch (IOException ex)
            {
                if (ex.Message.Contains("being used by another process"))
                {
                    Logger.Warn(Loc.T("Save file in use; waiting 0.5 seconds and retrying."));
                    System.Threading.Thread.Sleep(500);
                    charData = GetCharactersFromSave(remnantSave, mode);
                }
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
