using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Xml;
using System.Net;
using System.Diagnostics;
using System.Security.Policy;
using System.IO;
using System.Windows.Media.TextFormatting;
using System.Diagnostics.Eventing.Reader;

namespace RemnantSaveGuardian
{
    public class RemnantWorldEvent
    {
        //private string _world;
        private List<string> _locations;
        private string _type;
        private string _name;
        private string _key;
        private List<RemnantItem> mItems;
        public string World
        {
            get
            {
                return Loc.GameT(_locations.First());
            }
        }
        public List<string> Locations
        {
            get
            {
                return _locations;
            }
        }
        public string Location
        {
            get
            {
                return string.Join(": ", _locations.Select(loc => Loc.GameT(loc)));
            }
        }
        public string Type
        {
            get
            {
                return Loc.GameT(_type);
            }
        }
        public string Name
        {
            get
            {
                /*var parsed = new Regex(@"^(Ring|Amulet)_").Replace(_name, "");
                if (parsed.Length < 3) {
                    parsed = _name;
                }
                return Loc.GameT(parsed);*/
                return Loc.GameT(_name);
            }
        }
        public string MissingItems
        {
            get
            {
                return string.Join("\n", mItems);
            }
        }
        public string PossibleItems
        {
            get
            {
                return string.Join("\n", this.getPossibleItems());
            }
        }
        public string RawWorld
        {
            get
            {
                return _locations.First();
            }
        }
        public string RawName
        {
            get
            {
                return _name;
            }
        }
        public string RawType
        {
            get
            {
                return _type;
            }
        }
        public enum ProcessMode { Campaign, Adventure };

        public RemnantWorldEvent(string key, string name, List<string> locations, string type)
        {
            mItems = new();
            _locations = new();
            _key = key;
            _name = name;
            if (name == null)
            {
                _name = key;
            }
            if (locations != null)
            {
                _locations.AddRange(locations);
            }
            _type = type;
            if (type == "Ring" || type == "Amulet")
            {
                _name = $"{type}_{_name}";
                _type = "Item";
            }
            if (type.ToLower() == "traitbook")
            {
                _type = "Item";
                _name = "TraitBook";
            }
            if (!key.Contains("Quest_Event") && type == "Story")
            {
                _name += "Story";
            }
            if (type.Contains("Injectable") || type.Contains("Abberation"))
            {
                _name = _name.Split('_').Last();
            }
            if (type == "RootEarth")
            {
                //Logger.Log($"Locations: {string.Join(", ", _locations)}, type: {_type}, name: {_name}");
                _name = _locations.Last().Replace(" ", "");
                _type = "Location";
                _locations.Clear();
                _locations.Add("World_RootEarth");
            }
        }
        public RemnantWorldEvent(string key, string world, string type) : this(key, key, null, type) {
            _locations.Add(world);
        }
        public RemnantWorldEvent(string key, List<string> locations, string type) : this(key, key, locations, type) { }
        public RemnantWorldEvent(Match match) : this(match.Value, match.Groups["eventName"].Value, new() { match.Groups["world"].Value }, match.Groups["eventType"].Value)
        { }
        public RemnantWorldEvent(Match match, string location) : this(match.Value, match.Groups["eventName"].Value, new() { match.Groups["world"].Value, location }, match.Groups["eventType"].Value) { }

        public List<RemnantItem> getPossibleItems()
        {;
            if (GameInfo.EventItem.ContainsKey(this._name))
            {
                return GameInfo.EventItem[this._name];
            }
            return new List<RemnantItem>();
        }

        public void setMissingItems(RemnantCharacter charData)
        {
            mItems.Clear();
            List<RemnantItem> possibleItems = this.getPossibleItems();
            foreach (RemnantItem item in possibleItems)
            {
                if (!charData.Inventory.Contains(item.Key.ToLower()))
                {
                    mItems.Add(item);
                }
            }

            if (possibleItems.Count == 0 && !GameInfo.Events.ContainsKey(this._name) && !this._name.Equals("TraitBook") && !this.Name.Equals("Simulacrum"))
            {
                //RemnantItem ri = new RemnantItem("UnknownPotentialLoot");
                //mItems.Add(ri);
            }
        }

        public override string ToString()
        {
            return this.Name;
        }

        //credit to /u/hzla00 for original javascript implementation
        static public void ProcessEventsOld(RemnantCharacter character, string savetext, ProcessMode mode)
        {
            string eventsText = "";
            string strCampaignEnd = "/Game/Campaign_Main/Quest_Campaign_Main.Quest_Campaign_Main_C";
            int campaignEnd = savetext.IndexOf(strCampaignEnd);
            if (mode == ProcessMode.Campaign)
            {
                string strCampaignStart = "/Game/World_Base/Quests/Quest_Ward13/Quest_Ward13.Quest_Ward13_C";
                int campaignStart = savetext.IndexOf(strCampaignStart);
                if (campaignStart != -1 && campaignEnd != -1)
                {
                    eventsText = savetext.Substring(0, campaignEnd);
                    campaignStart = eventsText.LastIndexOf(strCampaignStart);
                    eventsText = eventsText.Substring(campaignStart);
                }
            }
            else if (mode == ProcessMode.Adventure)
            {
                var adventureMatch = Regex.Match(savetext, @"/Game/World_(?<world>\w+)/Quests/Quest_AdventureMode/Quest_AdventureMode_\w+.Quest_AdventureMode_\w+_C");
                if (adventureMatch.Success)
                {
                    int adventureEnd = adventureMatch.Index;
                    int adventureStart = campaignEnd;
                    if (adventureStart > adventureEnd)
                    {
                        adventureStart = 0;
                    }
                    eventsText = savetext.Substring(adventureStart, adventureEnd);
                }
            }

            if (eventsText.Length == 0)
            {
                return;
            }
            Dictionary<string, Dictionary<string, string>> zones = new Dictionary<string, Dictionary<string, string>>();
            Dictionary<string, List<RemnantWorldEvent>> zoneEvents = new Dictionary<string, List<RemnantWorldEvent>>();
            List<RemnantWorldEvent> churchEvents = new List<RemnantWorldEvent>();
            /*foreach (string z in GameInfo.Zones.Keys)
            {
                zones.Add(z, new Dictionary<string, string>());
                zoneEvents.Add(z, new List<RemnantWorldEvent>());
            }*/

            string currentMainLocation = null;
            string currentSublocation = null;

            string eventName = null;
            MatchCollection matches = Regex.Matches(eventsText, @"/Game/(?:World|Campaign)_(?<world>\w+)/Quests/(?:Quest_(?:(?<eventType>\w+?)_)?\w+/)?(?:Quest_(?:(?<eventType2>\w+?)_)?(?<eventName>\w+)|(?<eventName>\w+))\.\w+");
            foreach (Match match in matches)
            {
                string zone = null;
                string eventType = null;
                string lastEventname = eventName;
                eventName = null;

                string textLine = match.Value;
                try
                {
                    if (currentSublocation != null)
                    {
                        //Some world bosses don't have a preceding dungeon; subsequent items therefore spawn in the overworld
                        //if (currentSublocation.Equals("TheRavager'sHaunt") || currentSublocation.Equals("TheTempestCourt")) currentSublocation = null;
                    }
                    zone = match.Groups["world"].Value;

                    eventType = match.Groups["eventType"].Value;

                    eventName = match.Groups["eventName"].Value;

                    /*if (textLine.Contains("Overworld_Zone") || textLine.Contains("_Overworld_"))
                    {
                        //process overworld zone marker
                        currentMainLocation = textLine.Split('/')[4].Split('_')[1] + " " + textLine.Split('/')[4].Split('_')[2] + " " + textLine.Split('/')[4].Split('_')[3];
                        if (GameInfo.MainLocations.ContainsKey(currentMainLocation))
                        {
                            currentMainLocation = GameInfo.MainLocations[currentMainLocation];
                        }
                        else
                        {
                            currentMainLocation = null;
                        }
                        continue;
                    }
                    else if (textLine.Contains("Quest_Church"))
                    {
                        //process Root Mother event
                        currentMainLocation = "Chapel Station";
                        eventName = "RootMother";
                        currentSublocation = "Church of the Harbinger";
                    }
                    else if (eventType != null)
                    {
                        //process other events, if they're recognized by getEventType
                        eventName = textLine.Split('/')[4].Split('_')[2];
                        if (textLine.Contains("OverworldPOI"))
                        {
                            currentSublocation = null;
                        }
                        else if (!textLine.Contains("Quest_Event"))
                        {
                            if (GameInfo.SubLocations.ContainsKey(eventName))
                            {
                                currentSublocation = GameInfo.SubLocations[eventName];
                            }
                            else
                            {
                                currentSublocation = null;
                            }
                        }
                        if ("Chapel Station".Equals(currentMainLocation))
                        {
                            if (textLine.Contains("Quest_Boss"))
                            {
                                currentMainLocation = "Westcourt";
                            }
                            else
                            {
                                currentSublocation = null;
                            }
                        }
                    }*/

                    if (mode == ProcessMode.Adventure) currentMainLocation = null;

                    if (match.Groups.ContainsKey("eventType2"))
                    {
                        if (match.Groups["eventType2"].Value == "AdventureMode")
                        {
                            continue;
                        }
                    }

                    var worldEvent = new RemnantWorldEvent(match);
                    worldEvent.setMissingItems(character);
                    if (!zoneEvents.ContainsKey(zone))
                    {
                        zoneEvents.Add(zone, new List<RemnantWorldEvent>());
                    }
                    zoneEvents[zone].Add(worldEvent);

                    /*if (eventName != lastEventname)
                    {
                        RemnantWorldEvent se = new RemnantWorldEvent();
                        // Replacements
                        if (eventName != null)
                        {
                            se.setKey(eventName);
                            if (GameInfo.Events.ContainsKey(eventName))
                            {
                                se.Name = GameInfo.Events[eventName];
                            }
                            else
                            {
                                se.Name = eventName;
                            }
                            se.Name = Regex.Replace(se.Name, "([a-z])([A-Z])", "$1 $2");
                        }

                        if (zone != null && eventType != null && eventName != null)
                        {
                            if (!zones[zone].ContainsKey(eventType))
                            {
                                zones[zone].Add(eventType, "");
                            }
                            if (!zones[zone][eventType].Contains(eventName))
                            {
                                zones[zone][eventType] += ", " + eventName;
                                List<string> locationList = new List<string>();
                                string zonelabel = zone;
                                if (GameInfo.Zones.ContainsKey(zone))
                                {
                                    zonelabel = GameInfo.Zones[zone];
                                }
                                locationList.Add(zonelabel);
                                if (currentMainLocation != null) locationList.Add(Regex.Replace(currentMainLocation, "([a-z])([A-Z])", "$1 $2"));
                                if (currentSublocation != null) locationList.Add(Regex.Replace(currentSublocation, "([a-z])([A-Z])", "$1 $2"));
                                se.Location = string.Join(": ", locationList);
                                se.Type = eventType;
                                se.setMissingItems(character);
                                if (!"Chapel Station".Equals(currentMainLocation))
                                {
                                    zoneEvents[zone].Add(se);
                                }
                                else
                                {
                                    churchEvents.Insert(0, se);
                                }

                                // rings drop with the Cryptolith on Rhom
                                if (eventName.Equals("Cryptolith") && zone.Equals("Rhom"))
                                {
                                    RemnantWorldEvent ringdrop = new RemnantWorldEvent();
                                    ringdrop.Location = zone;
                                    ringdrop.setKey("SoulLink");
                                    ringdrop.Name = "Soul Link";
                                    ringdrop.Type = "Item Drop";
                                    ringdrop.setMissingItems(character);
                                    zoneEvents[zone].Add(ringdrop);
                                }
                                // beetle always spawns in Strange Pass
                                else if (eventName.Equals("BrainBug"))
                                {
                                    RemnantWorldEvent beetle = new RemnantWorldEvent();
                                    beetle.Location = se.Location;
                                    beetle.setKey("Sketterling");
                                    beetle.Name = "Sketterling";
                                    beetle.Type = "Loot Beetle";
                                    beetle.setMissingItems(character);
                                    zoneEvents[zone].Add(beetle);
                                }
                                else if (eventName.Equals("BarnSiege") || eventName.Equals("Homestead"))
                                {
                                    RemnantWorldEvent wardPrime = new RemnantWorldEvent();
                                    wardPrime.setKey("WardPrime");
                                    wardPrime.Name = "Ward Prime";
                                    wardPrime.Location = "Earth: Ward Prime";
                                    wardPrime.Type = "Quest Event";
                                    wardPrime.setMissingItems(character);
                                    zoneEvents[zone].Add(wardPrime);
                                }
                            }
                        }

                    }*/
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error parsing save event on {textLine}: {ex}");
                }
            }

            List<RemnantWorldEvent> eventList = character.CampaignEvents;
            if (mode == ProcessMode.Adventure)
            {
                eventList = character.AdventureEvents;
            }
            eventList.Clear();

            /*bool churchAdded = false;
            bool queenAdded = false;
            bool navunAdded = false;
            RemnantWorldEvent ward13 = new RemnantWorldEvent();
            RemnantWorldEvent hideout = new RemnantWorldEvent();
            RemnantWorldEvent undying = new RemnantWorldEvent();
            RemnantWorldEvent queen = new RemnantWorldEvent();
            RemnantWorldEvent navun = new RemnantWorldEvent();
            RemnantWorldEvent ward17 = new RemnantWorldEvent();
            if (mode == ProcessMode.Campaign)
            {
                ward13.setKey("Ward13");
                ward13.Name = "Ward 13";
                ward13.Location = "Earth: Ward 13";
                ward13.Type = "Home";
                ward13.setMissingItems(character);
                if (ward13.MissingItems.Length > 0) orderedEvents.Add(ward13);

                hideout.setKey("FoundersHideout");
                hideout.Name = "Founder's Hideout";
                hideout.Location = "Earth: Fairview";
                hideout.Type = "Point of Interest";
                hideout.setMissingItems(character);
                if (hideout.MissingItems.Length > 0) orderedEvents.Add(hideout);

                undying.setKey("UndyingKing");
                undying.Name = "Undying King";
                undying.Location = "Rhom: Undying Throne";
                undying.Type = "World Boss";
                undying.setMissingItems(character);

                queen.Name = "Iskal Queen";
                queen.setKey("IskalQueen");
                queen.Location = "Corsus: The Mist Fen";
                queen.Type = "Point of Interest";
                queen.setMissingItems(character);

                navun.Name = "Fight With The Rebels";
                navun.setKey("SlaveRevolt");
                navun.Location = "Yaesha: Shrine of the Immortals";
                navun.Type = "Siege";
                navun.setMissingItems(character);

                ward17.setKey("Ward17");
                ward17.Name = "The Dreamer";
                ward17.Location = "Earth: Ward 17";
                ward17.Type = "World Boss";
                ward17.setMissingItems(character);
            }

            for (int i = 0; i < zoneEvents["Earth"].Count; i++)
            {
                //if (mode == ProcessMode.Subject2923) Console.WriteLine(zoneEvents["Earth"][i].eventKey);
                if (mode == ProcessMode.Campaign && !churchAdded && zoneEvents["Earth"][i].Location.Contains("Westcourt"))
                {
                    foreach (RemnantWorldEvent rwe in churchEvents)
                    {
                        orderedEvents.Add(rwe);
                    }
                    churchAdded = true;
                }
                orderedEvents.Add(zoneEvents["Earth"][i]);
            }
            for (int i = 0; i < zoneEvents["Rhom"].Count; i++)
            {
                orderedEvents.Add(zoneEvents["Rhom"][i]);
            }
            if (mode == ProcessMode.Campaign && undying.MissingItems.Length > 0) orderedEvents.Add(undying);
            for (int i = 0; i < zoneEvents["Corsus"].Count; i++)
            {
                if (mode == ProcessMode.Campaign && !queenAdded && zoneEvents["Corsus"][i].Location.Contains("The Mist Fen"))
                {
                    if (queen.MissingItems.Length > 0) orderedEvents.Add(queen);
                    queenAdded = true;
                }
                orderedEvents.Add(zoneEvents["Corsus"][i]);
            }
            for (int i = 0; i < zoneEvents["Yaesha"].Count; i++)
            {
                if (mode == ProcessMode.Campaign && !navunAdded && zoneEvents["Yaesha"][i].Location.Contains("The Scalding Glade"))
                {
                    if (navun.MissingItems.Length > 0) orderedEvents.Add(navun);
                    navunAdded = true;
                }
                orderedEvents.Add(zoneEvents["Yaesha"][i]);
            }
            for (int i = 0; i < zoneEvents["Reisum"].Count; i++)
            {
                orderedEvents.Add(zoneEvents["Reisum"][i]);
            }

            if (mode == ProcessMode.Campaign)
            {
                if (ward17.MissingItems.Length > 0) orderedEvents.Add(ward17);
            }*/

            foreach (var zone in zoneEvents.Keys)
            {
                foreach(var worldEvent in zoneEvents[zone])
                {
                    eventList.Add(worldEvent);
                }
            }

            /*for (int i = 0; i < orderedEvents.Count; i++)
            {
                if (mode == ProcessMode.Campaign || mode == ProcessMode.Subject2923)
                {
                    character.CampaignEvents.Add(orderedEvents[i]);
                }
                else
                {
                    character.AdventureEvents.Add(orderedEvents[i]);
                }
            }

            if (mode == ProcessMode.Subject2923)
            {
                ward17.setKey("Ward17Root");
                ward17.Name = "Harsgaard";
                ward17.Location = "Earth: Ward 17 (Root Dimension)";
                ward17.Type = "World Boss";
                ward17.setMissingItems(character);
                character.CampaignEvents.Add(ward17);
            }*/
        }
        static public void ProcessEvents(RemnantCharacter character, string savetext, ProcessMode mode)
        {
            var eventsText = "";
            var eventsIndex = 0;
            var eventStarts = Regex.Matches(savetext, @"/Game/World_Base/Quests/Quest_Global/Quest_Global\.Quest_Global_C");
            var eventEnds = Regex.Matches(savetext, @"/Game/World_Base/Quests/Quest_Global/Quest_Global.{5}Quest_Global_C");
            var eventLengths = new List<int>();
            for (var i = 0; i < eventStarts.Count; i++)
            {
                eventLengths.Add(eventEnds[i].Index - eventStarts[i].Index);
            }
            //Logger.Log($"{mode}");
            if (mode == ProcessMode.Adventure && eventLengths.Count > 1 && eventLengths[1] < eventLengths[0])
            {
                eventsIndex = 1;
            }
            else if (mode == ProcessMode.Campaign && eventLengths.Count > 1 && eventLengths[1] > eventLengths[0])
            {
                eventsIndex = 1;
            }
            if (eventStarts.Count <= eventsIndex)
            {
                return;
            }
            
            if (eventEnds.Count != eventStarts.Count)
            {
                return;
            }
            eventsText = savetext[eventStarts[eventsIndex].Index..eventEnds[eventsIndex].Index];
            if (eventsText.Length == 0)
            {
                return;
            }

            Dictionary<string, Dictionary<string, string>> zones = new Dictionary<string, Dictionary<string, string>>();
            Dictionary<string, List<RemnantWorldEvent>> zoneEvents = new Dictionary<string, List<RemnantWorldEvent>>();
            List<RemnantWorldEvent> churchEvents = new List<RemnantWorldEvent>();

            var areas = Regex.Matches(eventsText, @"![\s\S]{3}(?<eventId>[ABCDEF\d]{32})[\s\S]{5}(?<location>[\w ']+)\W");
            //var eventStrings = new List<string>();
            string firstZone = "";
            string currentMainLocation = null;
            string currentSublocation = null;
            string lastTemplate = "N/A";
            for (var areaIndex = 0; areaIndex < areas.Count; areaIndex++)
            {
                var currentArea = areas[areaIndex];
                var areaEndIndex = eventsText.Length;
                if (areaIndex + 1 < areas.Count)
                {
                    areaEndIndex = areas[areaIndex + 1].Index;
                }
                var areaText = eventsText[currentArea.Index..areaEndIndex];
                MatchCollection eventMatches = Regex.Matches(areaText, @"/Game/(?<world>(?:World|Campaign)_\w+)/Quests/(?:\w+)_(?<eventType>\w+?)_(?<eventName>\w+)/\w+\.\w+");
                //MatchCollection eventMatches = Regex.Matches(eventsText, @"/\w+/(?:\w+)_(?<world>\w+)/(?:\w+/)?([a-zA-Z0-9]+_(?<eventType>[a-zA-Z0-9]+)_(?<eventName>[a-zA-Z0-9_]+))/(?:Q)");
                foreach (Match eventMatch in eventMatches)
                {
                    try
                    {
                        if (eventMatch.Value.Contains("TileInfo") || eventMatch.Value.Contains("Template") || eventMatch.Value.Contains("EventTree") || eventMatch.Value.EndsWith("_C")) {
                            if (!eventMatch.Value.EndsWith("C"))
                            {
                                if (eventMatch.Value.Contains("Template"))
                                {
                                    lastTemplate = eventMatch.Value;
                                }
                                //Logger.Log(currentArea.Groups["location"]);
                                //Logger.Log(eventMatch.Value);
                            }
                            continue;
                        }
                        //Logger.Log($"{eventMatch.Value}\n{lastTemplate}");
                        // determine location
                        if (eventMatch.Value.Contains("Ring") || eventMatch.Value.Contains("Amulet"))
                        {
                            //eventName = textLine.Split('/')[4].Split('_')[3];
                            currentSublocation = null;
                        }
                        else if (eventMatch.Groups["eventType"].Value.Contains("Injectable") || eventMatch.Groups["eventType"].Value.Contains("Abberation"))
                        {
                            if (GameInfo.SubLocations.ContainsKey(eventMatch.Groups["eventName"].Value))
                                currentSublocation = GameInfo.SubLocations[eventMatch.Groups["eventName"].Value];
                            else
                                currentSublocation = null;

                        }
                        if (eventMatch.Value.Contains("OverworldPOI"))
                        {
                            //Debug.WriteLine(textLine);
                            currentSublocation = null;
                        }
                        else if (!eventMatch.Value.Contains("Quest_Event"))
                        {
                            if (GameInfo.SubLocations.ContainsKey(eventMatch.Groups["eventName"].Value))
                            {
                                currentSublocation = GameInfo.SubLocations[eventMatch.Groups["eventName"].Value];
                            }
                            else
                            {
                                currentSublocation = null;
                            }
                        }

                        //eventStrings.Add(eventMatch.Value);
                        var worldEvent = new RemnantWorldEvent(eventMatch, currentArea.Groups["location"].Value.Trim());
                        //worldEvent.Locations.Add(currentArea.Groups["location"].Value.Trim());
                        /*RemnantWorldEvent worldEvent;
                        if (currentSublocation == null)
                        {
                            worldEvent = new RemnantWorldEvent(eventMatch);
                        }
                        else
                        {
                            worldEvent = new RemnantWorldEvent(eventMatch, currentSublocation);
                        }*/
                        if (firstZone.Length == 0 && worldEvent.RawWorld != "World_RootEarth") {
                            firstZone = worldEvent.RawWorld;
                            //Logger.Log($"Setting first zone to {firstZone} for {worldEvent.Name}");
                        }
                        worldEvent.setMissingItems(character);
                        if (!zoneEvents.ContainsKey(worldEvent.RawWorld))
                        {
                            zoneEvents.Add(worldEvent.RawWorld, new List<RemnantWorldEvent>());
                        }
                        if (zoneEvents[worldEvent.RawWorld].Exists(ev => ev.Locations.Last() == worldEvent.Locations.Last() && ev.RawName == worldEvent.RawName))
                        {
                            continue;
                        }
                        zoneEvents[worldEvent.RawWorld].Add(worldEvent);

                        // Add associated events
                        if (worldEvent.RawWorld == "World_Nerud" && worldEvent.RawName.Contains("Story"))
                        {
                            var cust = new RemnantWorldEvent("TheCustodian", "World_Nerud", "Point of Interest");
                            cust.setMissingItems(character);
                            zoneEvents["World_Nerud"].Add(cust);
                            if (worldEvent.RawName == "IAmLegendStory")
                            {
                                var talratha = new RemnantWorldEvent("TalRatha", "World_Nerud", "WorldBoss");
                                talratha.setMissingItems(character);
                                zoneEvents["World_Nerud"].Add(talratha);
                            }

                        }

                        if (worldEvent.RawName == "AsylumStory" && worldEvent.RawWorld == "World_Fae")
                        {
                            RemnantWorldEvent asylumhouse = new RemnantWorldEvent("AsylumHouse", "World_Fae", "Location");
                            asylumhouse.Locations.Add("Asylum");
                            asylumhouse.setMissingItems(character);
                            zoneEvents["World_Fae"].Add(asylumhouse);

                            RemnantWorldEvent abberation = new RemnantWorldEvent("AsylumChainsaw", "World_Fae", "Abberation");
                            asylumhouse.Locations.Add("Asylum");
                            abberation.setMissingItems(character);
                            zoneEvents["World_Fae"].Add(abberation);

                            RemnantWorldEvent nightweb = new RemnantWorldEvent("Nightweb", "World_Fae", "Point of Interest");
                            asylumhouse.Locations.Add("TormentedAsylum");
                            nightweb.setMissingItems(character);
                            zoneEvents["World_Fae"].Add(nightweb);

                        }
                        else if (worldEvent.RawName == "FaelinFaerlin")
                        {
                            RemnantWorldEvent faerlin = new RemnantWorldEvent("Faerin", "World_Fae", "WorldBoss");
                            faerlin.setMissingItems(character);
                            zoneEvents["World_Fae"].Add(faerlin);
                        }
                        // Abberation
                        else if (worldEvent.RawName == "HiddenMaze")
                        {
                            RemnantWorldEvent fester = new RemnantWorldEvent("Fester", worldEvent.Locations, "Abberation");
                            fester.setMissingItems(character);
                            zoneEvents[worldEvent.RawWorld].Add(fester);
                        }
                        else if (worldEvent.RawName == "TheLament")
                        {
                            RemnantWorldEvent wither = new RemnantWorldEvent("Wither", worldEvent.Locations, "Abberation");
                            wither.setMissingItems(character);
                            zoneEvents[worldEvent.RawWorld].Add(wither);
                        }
                        else if (worldEvent.RawName == "TheTangle")
                        {
                            RemnantWorldEvent mantagora = new RemnantWorldEvent("Mantagora", worldEvent.Locations, "Abberation");
                            mantagora.setMissingItems(character);
                            zoneEvents[worldEvent.RawWorld].Add(mantagora);
                        }
                        else if (worldEvent.RawName == "TheCustodian")
                        {
                            var drzyr = new RemnantWorldEvent("DrzyrReplicator", worldEvent.Locations, "Merchant");
                            drzyr.setMissingItems(character);
                            zoneEvents[worldEvent.RawWorld].Add(drzyr);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error parsing save event on {areaText}: {ex}");

                    }
                }
                //break;
            }
            //File.WriteAllText($"events{character.WorldIndex}-{eventsIndex}.txt", string.Join("\n", eventStrings.ToArray()));

            List<RemnantWorldEvent> eventList = character.CampaignEvents;
            if (mode == ProcessMode.Adventure)
            {
                eventList = character.AdventureEvents;
            }
            eventList.Clear();

            if (mode == ProcessMode.Campaign)
            {
                // Add Ward 13 events
                var ward13 = new RemnantWorldEvent("Ward13", "World_Earth", "Home");
                ward13.setMissingItems(character);
                if (ward13.MissingItems.Length > 0) eventList.Add(ward13);

                var cass = new RemnantWorldEvent("Cass", "World_Earth", "Home");
                cass.setMissingItems(character);
                if (cass.MissingItems.Length > 0) eventList.Add(cass);
                //Logger.Log(firstZone);
                // Add the first zone events
                for (int i = 0; firstZone != null && i < zoneEvents[firstZone].Count; i++)
                {
                    // Debug.WriteLine($"{mode.ToString() + " " + firstZone}:" + String.Join(",", zoneEvents[firstZone].Select(x => x.eventKey)));
                    eventList.Add(zoneEvents[firstZone][i]);
                }

                // Add the Labyrinth
                var lab = new RemnantWorldEvent("Labyrinth", "World_Labyrinth", "Location");
                lab.setMissingItems(character);
                if (lab.MissingItems.Length > 0) eventList.Add(lab);

                RemnantWorldEvent lab2 = new RemnantWorldEvent("LabyrinthBackrooms", "World_Labyrinth", "Location");
                lab2.setMissingItems(character);
                if (lab2.MissingItems.Length > 0) eventList.Add(lab2);
                //orderedEvents.Add(lab2);

                // Add other zones
                foreach (string s in zoneEvents.Keys.Except(new List<string>() { firstZone, "World_RootEarth" }))
                {
                    //Debug.WriteLine($"{mode.ToString() + " " + s}:" + String.Join(",", zoneEvents[s].Select(x => x.eventKey)));
                    eventList.AddRange(zoneEvents[s]);
                }

                // Add Root Earth
                RemnantWorldEvent RootEarth1 = new RemnantWorldEvent("AshenWasteland", "World_RootEarth", "Location");
                RootEarth1.setMissingItems(character);
                if (RootEarth1.MissingItems.Length > 0) eventList.Add(RootEarth1);

                RemnantWorldEvent RootEarth2 = new RemnantWorldEvent("CorruptedHarbor", "World_RootEarth", "Location");
                RootEarth2.setMissingItems(character);
                if (RootEarth2.MissingItems.Length > 0) eventList.Add(RootEarth2);

                // Add back in root earth events filtered out above?
            }
            else
            {
                foreach (string s in zoneEvents.Keys)
                {
                    if (zoneEvents[s].Exists(x => x._key.Contains("Story")))
                    {
                        //Debug.WriteLine($"Adventure Mode: {s}");
                        //Debug.WriteLine($"{mode.ToString() + " " + s}:" + String.Join(",", zoneEvents[s].Select(x => x.eventKey)));
                        eventList.AddRange(zoneEvents[s]);
                    }
                }

            }
        }
        static public void ProcessEventsAC(RemnantCharacter character, string saveText, ProcessMode mode)
        {
            var eventsText = "";
            var events = GetEventsFromSave(saveText);
            if (mode == ProcessMode.Campaign && events.ContainsKey(ProcessMode.Campaign))
            {
                eventsText = events[ProcessMode.Campaign];
            }
            if (mode == ProcessMode.Adventure && events.ContainsKey(ProcessMode.Adventure))
            {
                eventsText = events[ProcessMode.Adventure];
            }
            if (eventsText == "")
            {
                return;
            }
            Dictionary<string, List<RemnantWorldEvent>> zoneEvents = new Dictionary<string, List<RemnantWorldEvent>>();
            foreach (string z in GameInfo.Zones.Keys)
            {
                zoneEvents.Add(z, new List<RemnantWorldEvent>());
            }

            string zone = null;
            string currentMainLocation = null;
            string currentSublocation = null;

            string lastEventName = null;
            MatchCollection matches = Regex.Matches(eventsText, @"/\w+/(?:\w+)_(?<world>\w+)/(?:\w+/)?([a-zA-Z0-9]+_(?<eventType>[a-zA-Z0-9]+)_(?<eventName>[a-zA-Z0-9_]+))/(?:Q)");
            string firstZone = "";
            foreach (Match match in matches)
            {
                RemnantWorldEvent worldEvent = new RemnantWorldEvent(match);

                string textLine = match.Value;
                try
                {
                    zone = match.Groups["world"].Value;
                    if (mode == ProcessMode.Campaign && firstZone == "")
                    {
                        // Debug.Write("First Campaign Zone: "+zone);
                        firstZone = zone;
                    }
                    //Debug.WriteLine("Parse: " + textLine);
                   
                    if (textLine.Contains("Ring") || textLine.Contains("Amulet"))
                    {
                        //eventName = textLine.Split('/')[4].Split('_')[3];
                        currentSublocation = null;
                    }
                    else if (worldEvent.RawType.Contains("Injectable") || worldEvent.RawType.Contains("Abberation"))
                    {
                        if (GameInfo.SubLocations.ContainsKey(worldEvent.RawName))
                            currentSublocation = GameInfo.SubLocations[worldEvent.RawName];
                        else
                            currentSublocation = null;

                    }
                    if (textLine.Contains("OverworldPOI"))
                    {
                        //Debug.WriteLine(textLine);
                        currentSublocation = null;
                    }
                    else if (!textLine.Contains("Quest_Event"))
                    {
                        if (GameInfo.SubLocations.ContainsKey(worldEvent.RawName))
                        {
                            currentSublocation = GameInfo.SubLocations[worldEvent.RawName];
                        }
                        else
                        {
                            currentSublocation = null;
                        }
                    }
                    //Debug.WriteLine(eventType + ": " + eventName + ": " + currentSublocation);
                    if (mode == ProcessMode.Adventure) currentMainLocation = null;

                    if (!zoneEvents.ContainsKey(zone))
                    {
                        zoneEvents[zone] = new();
                    }

                    if (worldEvent.RawName != lastEventName)
                    {
                        var exists = zoneEvents[zone].Exists(ev => ev.RawType == worldEvent.RawType && ev.RawName == worldEvent.RawName);
                        if (zone != null && worldEvent.RawType != "")
                        {
                            if (zoneEvents[zone].Exists(ev => ev.RawType == worldEvent.RawType && ev.RawName == worldEvent.RawName) != true)
                            {
                                if (currentMainLocation != null)
                                {
                                    worldEvent.Locations.Add(currentMainLocation);
                                }
                                if (currentSublocation != null)
                                {
                                    worldEvent.Locations.Add(currentSublocation);
                                }
                                worldEvent.setMissingItems(character);

                                zoneEvents[zone].Add(worldEvent);

                                if (zone.Equals("Nerud") && worldEvent.RawName.Contains("Story"))
                                {
                                    var cust = new RemnantWorldEvent("TheCustodian", "Nerud", "Point of Interest");
                                    cust.setMissingItems(character);
                                    zoneEvents[zone].Add(cust);
                                    if (worldEvent.RawName.Equals("IAmLegendStory"))
                                    {
                                        var talratha = new RemnantWorldEvent("TalRatha", "Nerud", "WorldBoss");
                                        talratha.setMissingItems(character);
                                        zoneEvents[zone].Add(talratha);
                                    }

                                }

                                if (worldEvent.RawName.Equals("AsylumStory") && zone.Equals("Fae"))
                                {
                                    RemnantWorldEvent asylumhouse = new RemnantWorldEvent("AsylumHouse", "Fae", "Location");
                                    asylumhouse.Locations.Add("Asylum");
                                    asylumhouse.setMissingItems(character);
                                    zoneEvents[zone].Add(asylumhouse);

                                    RemnantWorldEvent abberation = new RemnantWorldEvent("AsylumChainsaw", "Fae", "Abberation");
                                    asylumhouse.Locations.Add("Asylum");
                                    abberation.setMissingItems(character);
                                    zoneEvents[zone].Add(abberation);

                                    RemnantWorldEvent nightweb = new RemnantWorldEvent("Nightweb", "Fae", "Point of Interest");
                                    asylumhouse.Locations.Add("TormentedAsylum");
                                    nightweb.setMissingItems(character);
                                    zoneEvents[zone].Add(nightweb);

                                }
                                else if (worldEvent.RawName.Equals("FaelinFaerlin"))
                                {
                                    RemnantWorldEvent faerlin = new RemnantWorldEvent("Faeron", "Fae", "WorldBoss");
                                    faerlin.setMissingItems(character);
                                    zoneEvents[zone].Add(faerlin);
                                }
                                // Abberation
                                else if (worldEvent.RawName.Equals("HiddenMaze"))
                                {
                                    RemnantWorldEvent fester = new RemnantWorldEvent("Fester", worldEvent.Locations, "Abberation");
                                    fester.setMissingItems(character);
                                    zoneEvents[zone].Add(fester);
                                }
                                else if (worldEvent.RawName.Equals("TheLament"))
                                {
                                    RemnantWorldEvent wither = new RemnantWorldEvent("Wither", worldEvent.Locations, "Abberation");
                                    wither.setMissingItems(character);
                                    zoneEvents[zone].Add(wither);
                                }
                                else if (worldEvent.RawName.Equals("TheTangle"))
                                {
                                    RemnantWorldEvent mantagora = new RemnantWorldEvent("Mantagora", worldEvent.Locations, "Abberation");
                                    mantagora.setMissingItems(character);
                                    zoneEvents[zone].Add(mantagora);
                                }
                            }
                        }

                    }
                    lastEventName = worldEvent.RawName;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error parsing save event:");
                    Console.WriteLine("\tLine: " + textLine);
                    Console.WriteLine("\tError: " + ex.ToString());
                }
            }
            List<RemnantWorldEvent> eventList = character.CampaignEvents;
            if (mode == ProcessMode.Adventure)
            {
                eventList = character.AdventureEvents;
            }
            eventList.Clear();

            if (mode == ProcessMode.Campaign)
            {
                // Add Ward 13 events
                var ward13 = new RemnantWorldEvent("Ward13", "Ward13", "Home");
                ward13.setMissingItems(character);
                if (ward13.MissingItems.Length > 0) eventList.Add(ward13);

                var cass = new RemnantWorldEvent("Cass", "Ward13", "Home");
                cass.setMissingItems(character);
                if (cass.MissingItems.Length > 0) eventList.Add(cass);

                // Add the first zone events
                for (int i = 0; firstZone != null && i < zoneEvents[firstZone].Count; i++)
                {
                    // Debug.WriteLine($"{mode.ToString() + " " + firstZone}:" + String.Join(",", zoneEvents[firstZone].Select(x => x.eventKey)));
                    eventList.Add(zoneEvents[firstZone][i]);
                }

                // Add the Labyrinth
                var lab = new RemnantWorldEvent("Labyrinth", "Labyrinth", "Location");
                lab.setMissingItems(character);
                if (lab.MissingItems.Length > 0) eventList.Add(lab);

                RemnantWorldEvent lab2 = new RemnantWorldEvent("LabyrinthBackrooms", "Labyrinth", "Location");
                lab2.setMissingItems(character);
                if (lab2.MissingItems.Length > 0) eventList.Add(lab2);
                //orderedEvents.Add(lab2);

                // Add other zones
                foreach (string s in zoneEvents.Keys.Except(new List<string>() { firstZone }))
                {
                    //Debug.WriteLine($"{mode.ToString() + " " + s}:" + String.Join(",", zoneEvents[s].Select(x => x.eventKey)));
                    eventList.AddRange(zoneEvents[s]);
                }

                // Add Root Earth
                RemnantWorldEvent RootEarth1 = new RemnantWorldEvent("RootEarthAshenWasteland", "RootEarth", "Location");
                RootEarth1.setMissingItems(character);
                if (RootEarth1.MissingItems.Length > 0) eventList.Add(RootEarth1);

                RemnantWorldEvent RootEarth2 = new RemnantWorldEvent("RootEarthCorruptedHarbor", "RootEarth", "Location");
                RootEarth2.setMissingItems(character);
                if (RootEarth2.MissingItems.Length > 0) eventList.Add(RootEarth2);
            }
            else
            {
                foreach (string s in zoneEvents.Keys)
                {
                    if (zoneEvents[s].Exists(x => x._key.Contains("Story")))
                    {
                        //Debug.WriteLine($"Adventure Mode: {s}");
                        //Debug.WriteLine($"{mode.ToString() + " " + s}:" + String.Join(",", zoneEvents[s].Select(x => x.eventKey)));
                        eventList.AddRange(zoneEvents[s]);
                    }
                }

            }
        }
        static private Dictionary<ProcessMode,string> GetEventsFromSave(string saveText)
        {
            var events = new Dictionary<ProcessMode,string>();
            var campaignStart = saveText.IndexOf("/Game/World_Base/Quests/Quest_Ward13/Quest_Ward13.Quest_Ward13_C");
            var campaignEnd = saveText.IndexOf("/Game/Campaign_Main/Quest_Campaign_Main.Quest_Campaign_Main_C");
            if (campaignStart == -1 || campaignEnd == -1)
            {
                return events;
            }
            events[ProcessMode.Campaign] = saveText[campaignStart..campaignEnd];
            var adventureMatch = Regex.Match(saveText, @"Quest_AdventureMode_(?<world>\w+)_C");
            if (adventureMatch.Success)
            {
                var adventureStart = campaignEnd;
                var adventureEnd = adventureMatch.Index;
                if (adventureEnd != -1 && adventureEnd < campaignStart)
                {
                    adventureStart = 0;
                }
                events[ProcessMode.Adventure] = saveText[adventureStart..adventureEnd];
            }
            return events;
        }
        static public void ProcessEvents(RemnantCharacter character, string saveText)
        {
            ProcessEvents(character, saveText, ProcessMode.Campaign);
            ProcessEvents(character, saveText, ProcessMode.Adventure);
        }
        static public void ProcessEvents(RemnantCharacter character)
        {
            var savetext = RemnantSave.DecompressSaveAsString(character.Save.WorldSaves[character.WorldIndex]);
            //File.WriteAllText(character.Save.WorldSaves[character.WorldIndex].Replace(".sav", ".txt"), savetext);
            ProcessEvents(character, savetext);
        }
    }
}
