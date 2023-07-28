using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System.Runtime.CompilerServices;

namespace RemnantSaveGuardian
{
    public class RemnantCharacter
    {
        public List<string> Archetypes { get; set; }
        public string SecondArchetype { get; set; }
        public List<string> Inventory { get; set; }
        public List<RemnantWorldEvent> CampaignEvents { get; set; }
        public List<RemnantWorldEvent> AdventureEvents { get; set; }

        public int Progression
        {
            get
            {
                return this.Inventory.Count;
            }
        }

        private List<RemnantItem> missingItems;

        private RemnantSave save;
        private int worldIndex;

        public enum ProcessMode { Campaign, Adventure };

        public override string ToString()
        {
            return string.Join(", ", Archetypes.Select(arch => Loc.T(arch))) + " (" + this.Progression + ")";
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
            this.save = remnantSave;
            this.worldIndex = index;
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
                    Match invMatch = inventoryStarts[i];
                    var inventoryEnd = inventoryEnds[i].Index;
                    var inventory = profileData.Substring(invMatch.Index, inventoryEnd - invMatch.Index);
                    RemnantCharacter cd = new RemnantCharacter(remnantSave, i);
                    for (var m = 0; m < archetypes.Count; m++)
                    {
                        Match archMatch = archetypes[m];
                        int prevCharEnd = 0;
                        if (i > 0)
                        {
                            prevCharEnd = inventoryEnds[i-1].Index;
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
                    List<string> saveItems = new List<string>();

                    MatchCollection matches = new Regex(@"/Items/Weapons/(?<weaponType>\w+)/(?<weaponClass>\w+)/(?<weaponName>\w+)").Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Groups["weaponName"].Value);
                    }

                    matches = new Regex(@"/Items/Armor/(?<armorSource>\w+)/(?<armorSet>\w+)/(?<armorName>\w+)").Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Groups["armorName"].Value);
                    }

                    matches = new Regex(@"/Items/Trinkets/(BandsOfCastorAndPollux/)?(?<itemName>\w+)").Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Groups["itemName"].Value);
                    }

                    matches = new Regex(@"/Items/Mods/(?<itemName>\w+)").Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Groups["itemName"].Value);
                    }

                    matches = new Regex(@"/Items/Traits/(?<traitSource>\w+)/(?<traitName>\w+)").Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Groups["traitName"].Value);
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

                    matches = new Regex(@"/Items/Archetypes/(?<archetype>[\w]+)/Skills/(?<itemName>[\w]+)").Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Groups["itemName"].Value);
                    }

                    cd.Inventory = saveItems;
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
            if (this.save == null)
            {
                return;
            }
            if (this.CampaignEvents.Count != 0)
            {
                return;
            }
            if (this.worldIndex >= save.WorldSaves.Length)
            {
                return;
            }
            try
            {
                var savetext = RemnantSave.DecompressSaveAsString(this.save.WorldSaves[this.worldIndex]);
                File.WriteAllText(this.save.WorldSaves[this.worldIndex].Replace(".sav", ".txt"), savetext);
                //get campaign info
                string strCampaignEnd = "/Game/Campaign_Main/Quest_Campaign_Main.Quest_Campaign_Main_C";
                string strCampaignStart = "/Game/World_Base/Quests/Quest_Ward13/Quest_Ward13.Quest_Ward13_C";
                int campaignEnd = savetext.IndexOf(strCampaignEnd);
                int campaignStart = savetext.IndexOf(strCampaignStart);
                if (campaignStart != -1 && campaignEnd != -1)
                {
                    string campaigntext = savetext.Substring(0, campaignEnd);
                    campaignStart = campaigntext.LastIndexOf(strCampaignStart);
                    campaigntext = campaigntext.Substring(campaignStart);
                    RemnantWorldEvent.ProcessEvents(this, campaigntext, RemnantWorldEvent.ProcessMode.Campaign);
                }
                /*else
                {
                    strCampaignEnd = "/Game/Campaign_Clementine/Quest_Campaign_Clementine.Quest_Campaign_Clementine_C";
                    strCampaignStart = "/Game/World_Rural/Templates/Template_Rural_Overworld_0";
                    campaignEnd = savetext.IndexOf(strCampaignEnd);
                    campaignStart = savetext.IndexOf(strCampaignStart);
                    if (campaignStart != -1 && campaignEnd != -1)
                    {
                        string campaigntext = savetext.Substring(0, campaignEnd);
                        campaignStart = campaigntext.LastIndexOf(strCampaignStart);
                        campaigntext = campaigntext.Substring(campaignStart);
                        RemnantWorldEvent.ProcessEvents(this, campaigntext, RemnantWorldEvent.ProcessMode.Subject2923);
                    }
                    else
                    {
                        Console.WriteLine("Campaign not found; likely in tutorial mission.");
                    }
                }*/

                //get adventure info
                var adventureMatch = Regex.Match(savetext, @"/Game/World_(?<world>\w+)/Quests/Quest_AdventureMode/Quest_AdventureMode_\w+.Quest_AdventureMode_\w+_C");
                if (adventureMatch.Success)
                {
                    int adventureEnd = adventureMatch.Index;
                    int adventureStart = campaignEnd;
                    if (adventureStart > adventureEnd)
                    {
                        adventureStart = 0;
                    }
                    string advtext = savetext.Substring(adventureStart, adventureEnd);
                    RemnantWorldEvent.ProcessEvents(this, advtext, RemnantWorldEvent.ProcessMode.Adventure);
                }

                missingItems.Clear();
                foreach (RemnantItem[] eventItems in GameInfo.EventItem.Values)
                {
                    foreach (RemnantItem item in eventItems)
                    {
                        if (!this.Inventory.Contains(item.GetKey()))
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
