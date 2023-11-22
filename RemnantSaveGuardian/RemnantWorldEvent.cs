using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

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
                //return $"{_name}\n{_key}";
                //return Loc.GameT(_name)+"\n"+_name+"\n"+_key;
                return Loc.GameT(_name);
            }
        }
        public List<RemnantItem> MissingItems {
            get
            {
                return mItems;
            }
        }
        public List<RemnantItem> PossibleItems
        {
            get
            {
                return getPossibleItems();
            }
        }
        public string MissingItemsString
        {
            get
            {
                return string.Join("\n", mItems.Select(i => i.TypeName()));
            }
        }
        public string PossibleItemsString
        {
            get
            {
                return string.Join("\n", this.getPossibleItems().Select(i => i.TypeName()));
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
        public string TileSet { get; set; }
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
            if (_name.Contains("_Spawntable")) {
                _name = _name.Replace("_Spawntable", "");
            }
            _type = type;
            List<string> itemPrefixes = new() { "Ring", "Amulet", "Trait" };
            if (!itemPrefixes.TrueForAll(prefix => !name.StartsWith(prefix)))
            {
                _type = "Item";
            }
            if (name.ToLower().Contains("traitbook"))
            {
                _name = "TraitBook";
            }
            if (type == "Sewer")
            {
                _type = "Event";
            }
            if (!key.Contains("Quest_Event") && type == "Story")
            {
                _name += "Story";
            }
            if (type.Contains("Injectable") || type.Contains("Abberation"))
            {
                var nameSplit = _name.Split('_');
                _name = nameSplit.Last() == "DLC" ? nameSplit[nameSplit.Length-2] : nameSplit.Last();
            }
            if (type == "RootEarth")
            {
                //Logger.Log($"Locations: {string.Join(", ", _locations)}, type: {_type}, name: {_name}");
                _name = _locations.Last().Replace(" ", "");
                _type = "Location";
                _locations.Clear();
                _locations.Add("World_RootEarth");
            }
            TileSet = "";
        }
        public RemnantWorldEvent(string key, string world, string type) : this(key, key, null, type) {
            _locations.Add(world);
        }
        public RemnantWorldEvent(string key, List<string> locations, string type) : this(key, key, locations, type) { }
        public RemnantWorldEvent(Match match) : this(match.Value, match.Groups["eventName"].Value, new() { match.Groups["world"].Value }, match.Groups["eventType"].Value)
        { }
        public RemnantWorldEvent(Match match, string location) : this(match.Value, match.Groups["eventName"].Value, new() { match.Groups["world"].Value, location }, match.Groups["eventType"].Value) { }

        public List<RemnantItem> getPossibleItems()
        {
            if (GameInfo.EventItem.ContainsKey(this._name))
            {
                var possibleItems = GameInfo.EventItem[_name];
                if (!Properties.Settings.Default.ShowCoopItems)
                {
                    possibleItems = possibleItems.FindAll(item => item.Coop == false);
                }
                if (TileSet != null && TileSet.Length > 0)
                {
                    possibleItems = possibleItems.FindAll(item => item.TileSet.Length == 0 || TileSet.Contains(item.TileSet));
                }
                return possibleItems;
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

            List<RemnantWorldEvent> eventList = mode==ProcessMode.Adventure?character.AdventureEvents:character.CampaignEvents;
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
        static public void ProcessEvents_old_again(RemnantCharacter character, string savetext, ProcessMode mode)
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
            if (mode == ProcessMode.Adventure && eventLengths.Count == 1)
            {
                return;
            }
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
            var faelinCount = 0;
            Dictionary<string, RemnantWorldEvent> lastTemplates = new();
            RemnantWorldEvent lastTileInfo;
            RemnantWorldEvent lastEventTree;
            List<string> excludeTypes = new() { "Global", "Earth" };
            for (var areaIndex = 0; areaIndex < areas.Count; areaIndex++)
            {
                var currentArea = areas[areaIndex];
                var areaEndIndex = eventsText.Length;
                if (areaIndex + 1 < areas.Count)
                {
                    areaEndIndex = areas[areaIndex + 1].Index;
                }
                var areaText = eventsText[currentArea.Index..areaEndIndex];
                //MatchCollection eventMatches = Regex.Matches(areaText, @"/Game/(?<world>(?:World|Campaign)_\w+)/Quests/(?:\w+)/(?<eventDetails>(?:SpawnTable_)?(?:[a-zA-Z0-9]+_)?(?<eventType>[a-zA-Z0-9]+)_(?<eventName>\w+))\.\w+");
                MatchCollection eventMatches = Regex.Matches(areaText, @"/Game/(?<world>(?:World|Campaign)_\w+)/Quests/(?:Quest_)?(?<eventType>[a-zA-Z0-9]+)_(?<eventName>\w+)/(?<details>\w+)\.\w+");
                //MatchCollection eventMatches = Regex.Matches(eventsText, @"/\w+/(?:\w+)_(?<world>\w+)/(?:\w+/)?([a-zA-Z0-9]+_(?<eventType>[a-zA-Z0-9]+)_(?<eventName>[a-zA-Z0-9_]+))/(?:Q)");
                foreach (Match eventMatch in eventMatches)
                {
                    var lastTemplate = lastTemplates.ContainsKey(eventMatch.Groups["world"].Value) ? lastTemplates[eventMatch.Groups["world"].Value] : null;
                    if (lastTemplate != null)
                    {
                        currentMainLocation = lastTemplate.RawName;
                    } else
                    {
                        //Debug.WriteLine($"No last template for {eventMatch.Value}");
                        currentMainLocation = null;
                    }
                    try
                    {
                        if (eventMatch.Value.Contains("TileInfo") || eventMatch.Value.Contains("Template") || eventMatch.Value.Contains("EventTree") || eventMatch.Value.EndsWith("_Cxxx")) {
                            if (true || !eventMatch.Value.EndsWith("C"))
                            {
                                if (eventMatch.Value.Contains("Template"))
                                {
                                    lastTemplate = new RemnantWorldEvent(eventMatch.Value, eventMatch.Groups["details"].Value, new() { eventMatch.Groups["world"].Value }, "Template");
                                    var mainTemplateMatch = Regex.Match(eventMatch.Value, @"Quest_(Global|Story)_\w+_Template");
                                    if (mainTemplateMatch.Success)
                                    {
                                        lastTemplates[eventMatch.Groups["world"].Value] = lastTemplate;
                                    }
                                    /*if (!zoneEvents.ContainsKey(lastTemplate.RawWorld))
                                    {
                                        zoneEvents.Add(lastTemplate.RawWorld, new List<RemnantWorldEvent>());
                                    }
                                    zoneEvents[lastTemplate.RawWorld].Add(lastTemplate);*/
                                }
                                /*if (eventMatch.Value.Contains("TileInfo"))
                                {
                                    lastTileInfo = new RemnantWorldEvent(eventMatch.Value, eventMatch.Value, new() { eventMatch.Groups["world"].Value }, "TileInfo");
                                    if (!zoneEvents.ContainsKey(lastTileInfo.RawWorld))
                                    {
                                        zoneEvents.Add(lastTileInfo.RawWorld, new List<RemnantWorldEvent>());
                                    }
                                    zoneEvents[lastTileInfo.RawWorld].Add(lastTileInfo);
                                }*/
                                /*if (eventMatch.Value.Contains("EventTree"))
                                {
                                    lastEventTree = new RemnantWorldEvent(eventMatch.Value, eventMatch.Groups["eventName"].Value, new() { eventMatch.Groups["world"].Value }, "EventTree");
                                    if (!zoneEvents.ContainsKey(lastEventTree.RawWorld))
                                    {
                                        zoneEvents.Add(lastEventTree.RawWorld, new List<RemnantWorldEvent>());
                                    }
                                    zoneEvents[lastEventTree.RawWorld].Add(lastEventTree);
                                }*/
                                //Logger.Log(currentArea.Groups["location"]);
                                //Logger.Log(eventMatch.Value);
                            }
                            continue;
                        }
                        if (eventMatch.Value.EndsWith("_C"))
                        {
                            continue;
                        }
                        if (excludeTypes.Contains(eventMatch.Groups["eventType"].Value))
                        {
                            continue;
                        }
                        if (eventMatch.Groups["eventType"].Value == "Miniboss" && eventMatch.Groups["eventName"].Value.Contains("POI"))
                        {
                            continue;
                        }
                        if (eventMatch.Groups["details"].Value.Contains("SpawnTable_Quest_Global"))
                        {
                            continue;
                        }
                        if (eventMatch.Groups["details"].Value.Contains("SpawnTable_Quest_Story"))
                        {
                            continue;
                        }
                        if (eventMatch.Groups["details"].Value.Contains("SpawnTable_Miniboss"))
                        {
                            continue;
                        }
                        // determine location
                        if (eventMatch.Value.Contains("Ring") || eventMatch.Value.Contains("Amulet"))
                        {
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
                        var worldEvent = new RemnantWorldEvent(eventMatch);//, currentArea.Groups["location"].Value.Trim());
                        var subLoc = currentArea.Groups["location"].Value.Trim();
                        if (currentMainLocation != null)
                        {
                            //worldEvent.Locations.Add(currentMainLocation);
                        }
                        else if (true || mode == ProcessMode.Campaign)
                        {
                            //Debug.WriteLine("excluding due to no main location " + eventMatch.Value);
                            continue;
                        }
                        if (Loc.GameT(currentMainLocation) == Loc.GameT(subLoc))
                        {
                            subLoc = null;
                        }
                        if (worldEvent._key.Contains("OverworldPOI"))
                        {
                            subLoc = null;
                        }
                        if (subLoc != null)
                        {
                            worldEvent.Locations.Add(subLoc);
                        }
                        //worldEvent.Locations.Add(currentArea.Groups["location"].Value.Trim());
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
                        addEventToZones(zoneEvents, worldEvent);

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
                            RemnantWorldEvent faerlin = new RemnantWorldEvent("Faerin", "Faerin", new() { "World_Fae", "Malefic Gallery" }, "Boss");
                            faerlin.setMissingItems(character);
                            //zoneEvents["World_Fae"].Add(faerlin);
                            addEventToZones(zoneEvents, faerlin);
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
                List<string> ward13Events = new(){ "Ward13", "Cass", "Brabus", "Mudtooth", "Reggie" };
                foreach (var eName in ward13Events)
                {
                    var wardEvent = new RemnantWorldEvent(eName, "World_Earth", "Home");
                    wardEvent.setMissingItems(character);
                    if (wardEvent.MissingItems.Count > 0) eventList.Add(wardEvent);
                }

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
                if (lab.MissingItems.Count > 0) eventList.Add(lab);

                RemnantWorldEvent lab2 = new RemnantWorldEvent("LabyrinthBackrooms", "World_Labyrinth", "Location");
                lab2.setMissingItems(character);
                if (lab2.MissingItems.Count > 0) eventList.Add(lab2);
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
                if (RootEarth1.MissingItems.Count > 0) eventList.Add(RootEarth1);

                RemnantWorldEvent RootEarth2 = new RemnantWorldEvent("CorruptedHarbor", "World_RootEarth", "Location");
                RootEarth2.setMissingItems(character);
                if (RootEarth2.MissingItems.Count > 0) eventList.Add(RootEarth2);

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
        static public void ProcessEvents(RemnantCharacter character, List<Match> areas, Dictionary<ProcessMode, Dictionary<Match, Match>> allInjectables, ProcessMode mode)
        {
            Dictionary<string, Dictionary<string, string>> zones = new Dictionary<string, Dictionary<string, string>>();
            Dictionary<string, List<RemnantWorldEvent>> zoneEvents = new Dictionary<string, List<RemnantWorldEvent>>();
            List<RemnantWorldEvent> churchEvents = new List<RemnantWorldEvent>();

            //var eventStrings = new List<string>();
            string firstZone = "";
            string currentWorld = null;
            string currentMainLocation = null;
            string currentSublocation = null;
            var faelinCount = 0;
            Dictionary<string, RemnantWorldEvent> lastTemplates = new();
            RemnantWorldEvent lastTileInfo;
            RemnantWorldEvent lastEventTree;
            //List<string> excludeTypes = new() { "Global", "Earth" };
            List<string> excludeWorlds = new() { "World_Base", "World_Labyrinth" };
            List<string> excludeEventDetails = new() { "TheHunterDream", "Dranception" };
            var unknownAreaCount = 0;
            foreach (Match area in areas)
            {
                var areaEvents = new List<RemnantWorldEvent>();
                currentSublocation = null;
                string spawnTable = null;
                var spawnTableMatch = Regex.Match(area.Groups["spawnTable"].Value, @"SpawnTable_[a-zA-Z0-9]+_(?<name>\w+)\d?");
                if (spawnTableMatch.Success)
                {
                    spawnTable = spawnTableMatch.Groups["name"].Value;
                }
                var tileSets = string.Join(" ", area.Groups["tileSet"].Captures.Select(c => c.Value));
                MatchCollection eventMatches = Regex.Matches(area.Groups["events"].Value, @"/Game/(?<world>(?:World|Campaign)_\w+)/Quests/(?:Quest_)?(?<eventType>[a-zA-Z0-9]+)_(?<eventName>\w+)/(?<details>\w+)\.\w+");
                foreach (Match eventMatch in eventMatches)
                {
                    var prevWorld = currentWorld;
                    currentWorld = eventMatch.Groups["world"].Value;
                    if (currentWorld == null || excludeWorlds.Contains(currentWorld))
                    {
                        continue;
                    }

                    if (currentWorld == "World_DLC1" && prevWorld != null)
                    {
                        currentWorld = prevWorld;

                    }
                    var lastTemplate = lastTemplates.ContainsKey(currentWorld) ? lastTemplates[currentWorld] : null;
                    if (lastTemplate != null)
                    {
                        currentMainLocation = lastTemplate.RawName;
                    }
                    else
                    {
                        //Debug.WriteLine($"No last template for {eventMatch.Value}");
                        currentMainLocation = null;
                    }
                    try
                    {
                        if (eventMatch.Value.Contains("EventTree"))
                        {
                            //Debug.WriteLine(eventMatch.Value);
                            continue;
                        }
                        if (GameInfo.SubLocations.ContainsKey(eventMatch.Groups["eventName"].Value))
                        {
                            currentSublocation = GameInfo.SubLocations[eventMatch.Groups["eventName"].Value];
                        } else if (currentMainLocation == null)
                        {
                            currentSublocation = area.Groups["locationName"].Value;
                        }
                        if (currentSublocation == "Consecrated Throne")
                        {
                            continue;
                        }
                        if (currentSublocation == null && eventMatch.Groups["eventType"].Value != "OverworldPOI")
                        {
                            if (spawnTable != null)
                            {
                                currentSublocation = spawnTable;
                            }
                            else
                            {
                                unknownAreaCount++;
                                currentSublocation = Loc.GameT("Area {areaNumber}", new() { { "areaNumber", unknownAreaCount.ToString() } });
                            }
                        }
                        if (excludeEventDetails.Any(detailString => eventMatch.Groups["details"].Value.Contains(detailString)))
                        {
                            //Logger.Log(eventMatch.Value);
                            continue;
                        }

                        //eventStrings.Add(eventMatch.Value);
                        //var worldEvent = new RemnantWorldEvent(eventMatch);//, currentArea.Groups["location"].Value.Trim());
                        var worldEvent = new RemnantWorldEvent(eventMatch.Value, eventMatch.Groups["eventName"].Value, new() { currentWorld }, eventMatch.Groups["eventType"].Value);
                        worldEvent.TileSet = tileSets;
                        if (areaEvents.FindIndex(e => e.RawName == worldEvent.RawName) != -1)
                        {
                            continue;
                        }
                        if (currentMainLocation != null)
                        {
                            //worldEvent.Locations.Add(currentMainLocation);
                        }
                        else if (true || mode == ProcessMode.Campaign)
                        {
                            //Debug.WriteLine("excluding due to no main location " + eventMatch.Value);
                            //continue;
                        }
                        if (currentSublocation != null)
                        {
                            worldEvent.Locations.Add(currentSublocation);
                        }

                        worldEvent.setMissingItems(character);
                        areaEvents.Add(worldEvent);

                        // Add associated events
                        if (worldEvent.RawWorld == "World_Nerud" && worldEvent.RawName.Contains("Story"))
                        {
                            var cust = new RemnantWorldEvent("TheCustodian", "World_Nerud", "Point of Interest");
                            cust.setMissingItems(character);
                            areaEvents.Add(cust);
                            if (worldEvent.RawName == "IAmLegendStory")
                            {
                                var talratha = new RemnantWorldEvent("TalRatha", "World_Nerud", "WorldBoss");
                                talratha.setMissingItems(character);
                                areaEvents.Add(talratha);
                            }

                        }

                        if (worldEvent.RawName == "AsylumStory" && worldEvent.RawWorld == "World_Fae")
                        {
                            RemnantWorldEvent asylumhouse = new RemnantWorldEvent("AsylumHouse", "World_Fae", "Location");
                            asylumhouse.Locations.Add("Asylum");
                            asylumhouse.setMissingItems(character);
                            areaEvents.Add(asylumhouse);

                            RemnantWorldEvent abberation = new RemnantWorldEvent("AsylumChainsaw", "World_Fae", "Abberation");
                            abberation.Locations.Add("Asylum");
                            abberation.setMissingItems(character);
                            areaEvents.Add(abberation);

                            RemnantWorldEvent nightweb = new RemnantWorldEvent("Nightweb", "World_Fae", "Point of Interest");
                            nightweb.Locations.Add("TormentedAsylum");
                            nightweb.setMissingItems(character);
                            areaEvents.Add(nightweb);

                        }
                        else if (worldEvent.RawName == "FaelinFaerlin")
                        {
                            RemnantWorldEvent faerlin = new RemnantWorldEvent("Faerin", "Faerin", new() { "World_Fae", "Malefic Gallery" }, "Boss");
                            faerlin.setMissingItems(character);
                            areaEvents.Add(faerlin);
                            //addEventToZones(zoneEvents, faerlin);
                        }
                        // Abberation
                        else if (worldEvent.RawName == "HiddenMaze")
                        {
                            RemnantWorldEvent fester = new RemnantWorldEvent("Fester", worldEvent.Locations, "Abberation");
                            fester.setMissingItems(character);
                            areaEvents.Add(fester);
                        }
                        else if (worldEvent.RawName == "TheLament")
                        {
                            RemnantWorldEvent wither = new RemnantWorldEvent("Wither", worldEvent.Locations, "Abberation");
                            wither.setMissingItems(character);
                            areaEvents.Add(wither);
                        }
                        else if (worldEvent.RawName == "TheTangle")
                        {
                            RemnantWorldEvent mantagora = new RemnantWorldEvent("Mantagora", worldEvent.Locations, "Abberation");
                            mantagora.setMissingItems(character);
                            areaEvents.Add(mantagora);
                        }
                        else if (worldEvent.RawName == "TheCustodian")
                        {
                            var drzyr = new RemnantWorldEvent("DrzyrReplicator", worldEvent.Locations, "Merchant");
                            drzyr.setMissingItems(character);
                            areaEvents.Add(drzyr);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error parsing save event on {area.Groups["events"].Value}: {ex}");

                    }
                }
                if (areaEvents.Count == 0)
                {
                    continue;
                }
                var ignoreSoloEventTypes = new List<string>() {
                    "Injectable",
                    "OverworldPOI",
                    "Story"
                };
                if (areaEvents.Count == 1 && ignoreSoloEventTypes.Contains(areaEvents[0].RawType))
                {
                    //continue; // causes Eternal Empress to disappear
                }
                var invalidFirstZones = new List<string>() {
                    "World_RootEarth",
                    "World_Labyrinth"
                };
                if (firstZone.Length == 0 && !invalidFirstZones.Contains(currentWorld))
                {
                    firstZone = currentWorld;
                    //Logger.Log($"Setting first zone to {firstZone}");
                }
                if (!zoneEvents.ContainsKey(currentWorld))
                {
                    zoneEvents[currentWorld] = new();
                }
                var exclusiveTypes = new List<string>() {
                    "Boss",
                    "SideD",
                    "Miniboss",
                    "Point of Interest",
                    "OverworldPOI",
                    "Story"
                };
                var exclusiveEvents = areaEvents.FindAll(e => exclusiveTypes.Contains(e.RawType));
                if (exclusiveEvents.Count > 0)
                {
                    var previousIndex = zoneEvents[currentWorld].FindIndex(e => e._name == exclusiveEvents[0]._name);
                    if (previousIndex != -1)
                    {
                        areaEvents = areaEvents.FindAll(e => !exclusiveEvents.Contains(e) || !zoneEvents[currentWorld].Any(prev => prev._name == e._name));
                        if (areaEvents.Count > 0)
                        {
                            zoneEvents[currentWorld].InsertRange(previousIndex + 1, areaEvents);
                        }
                        continue;
                    }
                }
                if (areaEvents.Any(e => e._name == "EmpressStory"))
                {
                    var lastRedThrone = areaEvents.FindLastIndex(e => e.Locations.Contains("The Red Throne"));
                    RemnantWorldEvent widowsCourt = new RemnantWorldEvent("RedDoeStatue", new List<string>() { currentWorld, "Widow's Court" }, "OverworldPOI");
                    widowsCourt.setMissingItems(character);
                    areaEvents.Insert(lastRedThrone+1, widowsCourt);

                    var matriarch = new RemnantWorldEvent("Amulet_MatriarchsInsignia", new List<string>() { currentWorld, "Widow's Court" }, "Item");
                    matriarch.setMissingItems(character);
                    areaEvents.Insert(lastRedThrone+2, matriarch);
                }
                //zoneEvents[currentWorld].AddRange(areaEvents);
                foreach (var newEvent in areaEvents)
                {
                    var insertPosition = zoneEvents[currentWorld].Count;
                    var locationIndex = zoneEvents[currentWorld].FindLastIndex(e => e.Locations.Last() == newEvent.Locations.Last());
                    if (locationIndex != -1)
                    {
                        //Debug.WriteLine($"{newEvent.Locations.Last()} {newEvent.Name} = {insertPosition}, {locationIndex}; matched to {zoneEvents[currentWorld][locationIndex].Name}");
                        insertPosition = locationIndex + 1;
                    }
                    zoneEvents[currentWorld].Insert(insertPosition, newEvent);
                }
            }
            //File.WriteAllText($"events{character.WorldIndex}-{eventsIndex}.txt", string.Join("\n", eventStrings.ToArray()));

            // add in injectables
            foreach (var injectablePair in allInjectables[mode])
            {
                var injectable = new RemnantWorldEvent(injectablePair.Key);
                var parent = new RemnantWorldEvent(injectablePair.Value);
                var world = injectablePair.Key.Groups["world"].Value;
                if (!zoneEvents.ContainsKey(world))
                {
                    //Logger.Warn($"Injectable world {world} not found in {mode} events");
                    continue;
                }
                if (zoneEvents[world].Any(we => we._name == injectable._name))
                {
                    // injectable already exists
                    continue;
                }
                var parentIndex = zoneEvents[world].FindIndex(we => we._name == parent._name);
                RemnantWorldEvent parentEvent = null;
                if (parentIndex < 0)
                {
                    parentIndex = zoneEvents[world].Count - 1;
                    //Logger.Warn($"Could not find parent for {injectable._name}: {parent._key}");
                }
                else
                {
                    parentEvent = zoneEvents[world][parentIndex];
                }
                if (parentEvent != null && parentEvent.Locations.Count > 1)
                {
                    injectable.Locations.Clear();
                    injectable.Locations.AddRange(parentEvent.Locations);
                }

                injectable.setMissingItems(character);
                zoneEvents[world].Insert(parentIndex + 1, injectable);
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
                List<string> ward13Events = new() { "Ward13", "Cass", "Brabus", "Mudtooth", "Reggie", "Whispers", "McCabe", "Dwell" };
                foreach (var eName in ward13Events)
                {
                    var wardEvent = new RemnantWorldEvent(eName, "World_Earth", "Home");
                    wardEvent.setMissingItems(character);
                    if (wardEvent.MissingItems.Count > 0) eventList.Add(wardEvent);
                }

                //Logger.Log(firstZone);
                // Add the first zone events
                for (int i = 0; firstZone.Length != 0 && i < zoneEvents[firstZone].Count; i++)
                {
                    // Debug.WriteLine($"{mode.ToString() + " " + firstZone}:" + String.Join(",", zoneEvents[firstZone].Select(x => x.eventKey)));
                    eventList.Add(zoneEvents[firstZone][i]);
                }

                // Add the Labyrinth
                var lab = new RemnantWorldEvent("Labyrinth", "World_Labyrinth", "Location");
                lab.setMissingItems(character);
                if (lab.MissingItems.Count > 0) eventList.Add(lab);

                RemnantWorldEvent lab2 = new RemnantWorldEvent("LabyrinthBackrooms", "World_Labyrinth", "Location");
                lab2.setMissingItems(character);
                if (lab2.MissingItems.Count > 0) eventList.Add(lab2);
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
                if (RootEarth1.MissingItems.Count > 0) eventList.Add(RootEarth1);

                RemnantWorldEvent RootEarth2 = new RemnantWorldEvent("CorruptedHarbor", "World_RootEarth", "Location");
                RootEarth2.setMissingItems(character);
                if (RootEarth2.MissingItems.Count > 0) eventList.Add(RootEarth2);

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

                                    RemnantWorldEvent archersCrest = new RemnantWorldEvent("Ring_ArchersCrest", worldEvent.Location, "Item");
                                    archersCrest.setMissingItems(character);
                                    zoneEvents[zone].Add(archersCrest);
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
                if (ward13.MissingItems.Count > 0) eventList.Add(ward13);

                var cass = new RemnantWorldEvent("Cass", "Ward13", "Home");
                cass.setMissingItems(character);
                if (cass.MissingItems.Count > 0) eventList.Add(cass);

                // Add the first zone events
                for (int i = 0; firstZone != null && i < zoneEvents[firstZone].Count; i++)
                {
                    // Debug.WriteLine($"{mode.ToString() + " " + firstZone}:" + String.Join(",", zoneEvents[firstZone].Select(x => x.eventKey)));
                    eventList.Add(zoneEvents[firstZone][i]);
                }

                // Add the Labyrinth
                var lab = new RemnantWorldEvent("Labyrinth", "Labyrinth", "Location");
                lab.setMissingItems(character);
                if (lab.MissingItems.Count > 0) eventList.Add(lab);

                RemnantWorldEvent lab2 = new RemnantWorldEvent("LabyrinthBackrooms", "Labyrinth", "Location");
                lab2.setMissingItems(character);
                if (lab2.MissingItems.Count > 0) eventList.Add(lab2);
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
                if (RootEarth1.MissingItems.Count > 0) eventList.Add(RootEarth1);

                RemnantWorldEvent RootEarth2 = new RemnantWorldEvent("RootEarthCorruptedHarbor", "RootEarth", "Location");
                RootEarth2.setMissingItems(character);
                if (RootEarth2.MissingItems.Count > 0) eventList.Add(RootEarth2);
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
        static public void ProcessEvents_old(RemnantCharacter character, string saveText)
        {
            //ProcessEvents(character, saveText, ProcessMode.Campaign);
            //ProcessEvents(character, saveText, ProcessMode.Adventure);
        }
        static public Dictionary<ProcessMode, Dictionary<Match, Match>> GetInjectables(string saveText)
        {
            var injectables = new Dictionary<ProcessMode, Dictionary<Match, Match>>() { { ProcessMode.Campaign, new() }, { ProcessMode.Adventure, new() } };
            var eventBlobs = new Dictionary<ProcessMode, string>();
            string strCampaignEnd = "/Game/Campaign_Main/Quest_Campaign_Main.Quest_Campaign_Main_C";
            int campaignEnd = saveText.IndexOf(strCampaignEnd);
            string strCampaignStart = "/Game/World_Base/Quests/Quest_Ward13/Quest_Ward13.Quest_Ward13_C";
            int campaignStart = saveText.IndexOf(strCampaignStart);
            if (campaignStart != -1 && campaignEnd != -1)
            {
                var campaignBlob = saveText.Substring(0, campaignEnd);
                campaignStart = campaignBlob.LastIndexOf(strCampaignStart);
                eventBlobs[ProcessMode.Campaign] = campaignBlob.Substring(campaignStart);
            }
            var adventureMatch = Regex.Match(saveText, @"/Game/World_(?<world>\w+)/Quests/Quest_AdventureMode(_[a-zA-Z]+)?/Quest_AdventureMode(_[a-zA-Z]+)?_\w+.Quest_AdventureMode(_[a-zA-Z]+)?_\w+_C");
            if (adventureMatch.Success)
            {
                int adventureEnd = adventureMatch.Index;
                int adventureStart = campaignEnd;
                if (adventureStart > adventureEnd)
                {
                    adventureStart = 0;
                }
                eventBlobs[ProcessMode.Adventure] = saveText[adventureStart..adventureEnd];
            }
            foreach (var processMode in eventBlobs.Keys)
            {
                var events = Regex.Matches(eventBlobs[processMode], @"/Game/(?<world>(?:World|Campaign)_\w+)/Quests/(?:Quest_)?(?<eventType>[a-zA-Z0-9]+)_(?<eventName>\w+)/(?<details>\w+)\.\w+");
                for (var eventIndex = 0; eventIndex < events.Count; eventIndex++)
                {
                    if (eventIndex == 0)
                    {
                        continue;
                    }
                    var ev = events[eventIndex];
                    if (ev.Groups["eventType"].Value != "Injectable")
                    {
                        continue;
                    }

                    var eventNameSplitted = ev.Groups["eventName"].Value.Split("_");
                    var eventName = eventNameSplitted.Last() == "DLC" ? eventNameSplitted[eventNameSplitted.Length-2] : eventNameSplitted.Last();
                    Match parentEvent;
                    if (GameInfo.InjectableParents.ContainsKey(eventName))
                    {
                        var parentName = GameInfo.InjectableParents[eventName];
                        parentEvent = events.ToList().Find(e => (new RemnantWorldEvent(e)).RawName == parentName);
                        if (parentEvent != null)
                        {
                            //Debug.WriteLine($"found parent {parentEvent.Value}");
                            injectables[processMode].Add(ev, parentEvent);
                            continue;
                        }
                    }
                    parentEvent = events[eventIndex - 1];
                    if (ev.Groups["world"].Value != parentEvent.Groups["world"].Value || parentEvent.Groups["eventType"].Value == "Injectable")
                    {
                        parentEvent = null;
                        for (var parentIndex = eventIndex +1; parentIndex < events.Count; parentIndex++)
                        {
                            if (events[parentIndex].Groups["world"].Value == ev.Groups["world"].Value && events[parentIndex].Groups["eventType"].Value != "Injectable")
                            {
                                parentEvent = events[parentIndex];
                                break;
                            }
                        }
                    }
                    if (parentEvent == null)
                    {
                        continue;
                    }
                    injectables[processMode].Add(ev, parentEvent);
                }
            }
            return injectables;
        }
        static public void ProcessEvents(RemnantCharacter character, string saveText)
        {
            var injectables = GetInjectables(saveText);
            var eventStarts = Regex.Matches(saveText, @"/Game/World_Base/Quests/Quest_Global/Quest_Global\.Quest_Global_C");
            var eventEnds = Regex.Matches(saveText, @"/Game/World_Base/Quests/Quest_Global/Quest_Global.{5}Quest_Global_C");
            if (eventStarts.Count == 0 || eventEnds.Count == 0)
            {
                return;
            }
            var eventGroupMatches = new List<List<Match>>();
            for (var i = 0; i < eventStarts.Count; i++)
            {
                var eventText = saveText[eventStarts[i].Index..eventEnds[i].Index];
                //var matches = Regex.Matches(eventText, @"/Game/(?<world>[\w/]+)/SpawnTables/(?<spawnTable>[\w/]+)\.\w+(?<events>.+)MapGen[\w\W]+?/Script/Remnant\.ZoneActor.{10}(?:.\u0001....(?<tileSet>/.+?))+.{9}ID");
                var areas = Regex.Split(eventText, @"[A-Z0-9]{32}[\s\S]{5}");
                var matches = new List<Match>();
                foreach (var ar in areas)
                {
                    //var match = Regex.Match(ar, @"^(?<locationName>[a-zA-Z0-9 ']+)\x00.+?(?:\n.+?)?/Game/(?<world>[\w/]+)/SpawnTables/(?<spawnTable>[\w/]+)\.\w+.{5}(?<events>/.+).{20}MapGen[\w\W]+?/Script/Remnant\.ZoneActor.{10}(?:.\u0001....(?<tileSet>/.+?))+.{9}ID");
                    var match = Regex.Match(ar, @"^(?<locationName>[a-zA-Z0-9 ']+)[\x00 ][\s\S]+?/Game/(?<world>[\w/]+)/SpawnTables/(?<spawnTable>[\w/]+)\.\w+[\s\S]{5}(?<events>/[\s\S]+)[\s\S]{20}MapGen[\s\S]+?/Script/Remnant\.ZoneActor[\s\S]{10}(?:[\s\S]\u0001[\s\S]{4}(?<tileSet>/.+?))+[\s\S]{9}ID");
                    if (match.Success)
                    {
                        matches.Add(match);
                    }
                }
                //var matches = Regex.Matches(eventText, @"[A-Z0-9]{32}.{5}(?<locationName>[a-zA-Z0-9 ']+)\x00.+?(?:\n.+?)?/Game/(?<world>[\w/]+)/SpawnTables/(?<spawnTable>[\w/]+)\.\w+.{5}(?<events>/.+).{20}MapGen[\w\W]+?/Script/Remnant\.ZoneActor.{10}(?:.\u0001....(?<tileSet>/.+?))+.{9}ID");
                if (matches.Count > 7)
                {
                    eventGroupMatches.Add(matches);
                }
            }
            if (eventGroupMatches.Count > 2)
            {
                Logger.Warn(Loc.T("Unexpected_event_group_number_warning_{numberOfEventGroups}", new() { { "numberOfEventGroups", eventGroupMatches.Count.ToString() } }));
            }
            var campaignIndex = 0;
            var adventureIndex = -1;
            if (eventGroupMatches.Count > 1)
            {
                adventureIndex = 1;
                if (eventGroupMatches[0].Count < eventGroupMatches[1].Count)
                {
                    campaignIndex = 1;
                    adventureIndex = 0;
                }
            }
            ProcessEvents(character, eventGroupMatches[campaignIndex], injectables, ProcessMode.Campaign);
            //Logger.Log($"{mode}");
            if (adventureIndex != -1)
            {
                ProcessEvents(character, eventGroupMatches[adventureIndex], injectables, ProcessMode.Adventure);
            }
        }
        static public void ProcessEvents(RemnantCharacter character)
        {
            var savetext = RemnantSave.DecompressSaveAsString(character.Save.WorldSaves[character.WorldIndex]);
    #if DEBUG
            System.IO.File.WriteAllText(character.Save.WorldSaves[character.WorldIndex].Replace(".sav", ".txt"), savetext);
    #endif
            ProcessEvents(character, savetext);
        }

        private static void addEventToZones(Dictionary <string, List<RemnantWorldEvent>> zoneEvents, RemnantWorldEvent worldEvent)
        {
            var world = worldEvent.RawWorld;
            if (!zoneEvents.ContainsKey(world))
            {
                zoneEvents.Add(world, new List<RemnantWorldEvent>());
            }
            if (zoneEvents[world].Exists(ev => ev.Locations.Last() == worldEvent.Locations.Last() && ev.RawName == worldEvent.RawName))
            {
                return;
            }
            var insertAtIndex = zoneEvents[world].Count;
            if (zoneEvents[world].Count > 0 && worldEvent.Locations.Count > 1)
            {
                var lastIndex = zoneEvents[world].FindLastIndex(e => e.Locations.Count > 1 && e.Locations.Last() == worldEvent.Locations.Last());
                if (lastIndex != -1)
                {
                    if (zoneEvents[world][lastIndex].RawName == worldEvent.RawName)
                    {
                        return;
                    }
                    insertAtIndex = lastIndex + 1;
                }
            }
            if (worldEvent.RawName == "FaelinFaerlin" && worldEvent.Locations.Last() != "Beatific Gallery")
            {
                return;
            }
            zoneEvents[world].Insert(insertAtIndex, worldEvent);
        }
    }
}
