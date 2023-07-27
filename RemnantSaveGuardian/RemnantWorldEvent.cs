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

namespace RemnantSaveGuardian
{
    public class RemnantWorldEvent
    {
        private string _world;
        private string _location;
        private string _type;
        private string _name;
        private string _key;
        private List<RemnantItem> mItems;
        public string World
        {
            get
            {
                return Loc.GameT(_world);
            }
        }
        public string Location 
        { 
            get
            {
                return Loc.GameT(_location);
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
                var parsed = new Regex(@"^(Ring|Amulet)_").Replace(_name, "");
                if (parsed.Length < 3) {
                    parsed = _name;
                }
                return Loc.GameT(parsed);
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
        public enum ProcessMode { Campaign, Adventure };

        public RemnantWorldEvent()
        {
            mItems = new();
        }

        public RemnantWorldEvent(Match match)
        {
            mItems = new();


            _key = match.Value;
            _world = match.Groups["world"].Value;
            _type = match.Groups["eventType"].Value;
            _name = match.Groups["eventName"].Value;
            if (_name.Contains("TraitBook"))
            {
                _name = "TraitBook";
            }
        }

        public List<RemnantItem> getPossibleItems()
        {
            List<RemnantItem> items = new List<RemnantItem>();
            if (GameInfo.EventItem.ContainsKey(this._name))
            {
                items = new List<RemnantItem>(GameInfo.EventItem[this._name]);
            }
            return items;
        }

        public void setMissingItems(RemnantCharacter charData)
        {
            List<RemnantItem> missingItems = new List<RemnantItem>();
            List<RemnantItem> possibleItems = this.getPossibleItems();
            foreach (RemnantItem item in possibleItems)
            {
                if (!charData.Inventory.Contains(item.GetKey()))
                {
                    missingItems.Add(item);
                }
            }
            mItems = missingItems;

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
        static public void ProcessEvents(RemnantCharacter character, string eventsText, ProcessMode mode)
        {
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
            MatchCollection matches = Regex.Matches(eventsText, @"/Game/(?:World|Campaign)_(?<world>\w+)/Quests/(?:Quest_(?:(?<eventType>\w+?)_)?\w+/)?(?:Quest_(?:(?<eventType2>\w+?)_)?(?<eventName>\w+)|(?<eventName>\w+))\.");
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
                    Console.WriteLine("Error parsing save event:");
                    Console.WriteLine("\tLine: " + textLine);
                    Console.WriteLine("\tError: " + ex.ToString());
                }
            }

            List<RemnantWorldEvent> orderedEvents = new List<RemnantWorldEvent>();

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
                    orderedEvents.Add(worldEvent);
                }
            }

            if (mode == ProcessMode.Campaign)
            {
                character.CampaignEvents = orderedEvents;
            }
            else
            {
                character.AdventureEvents = orderedEvents;
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

        static private string getZone(string textLine)
        {
            string zone = null;
            if (textLine.Contains("World_City") || textLine.Contains("Quest_Church") || textLine.Contains("World_Rural"))
            {
                zone = "Earth";
            }
            else if (textLine.Contains("World_Wasteland"))
            {
                zone = "Rhom";
            }
            else if (textLine.Contains("World_Jungle"))
            {
                zone = "Yaesha";
            }
            else if (textLine.Contains("World_Swamp"))
            {
                zone = "Corsus";
            }
            else if (textLine.Contains("World_Snow") || textLine.Contains("Campaign_Clementine"))
            {
                zone = "Reisum";
            }
            return zone;
        }

        static private string getEventType(string textLine)
        {
            string eventType = null;
            if (textLine.Contains("SmallD"))
            {
                eventType = "Side Dungeon";
            }
            else if (textLine.Contains("Quest_Boss"))
            {
                eventType = "World Boss";
            }
            else if (textLine.Contains("Siege") || textLine.Contains("Quest_Church"))
            {
                eventType = "Siege";
            }
            else if (textLine.Contains("Mini"))
            {
                eventType = "Miniboss";
            }
            else if (textLine.Contains("Quest_Event"))
            {
                if (textLine.Contains("Nexus"))
                {
                    eventType = "Siege";
                }
                else if (textLine.Contains("Sketterling"))
                {
                    eventType = "Loot Beetle";
                }
                else
                {
                    eventType = "Item Drop";
                }
            }
            else if (textLine.Contains("OverworldPOI") || textLine.Contains("OverWorldPOI") || textLine.Contains("OverworlPOI"))
            {
                eventType = "Point of Interest";
            }
            return eventType;
        }
    }
}
