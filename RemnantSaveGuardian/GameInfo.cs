using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Net;
using System.IO;
using System.Text.Json.Nodes;

namespace RemnantSaveGuardian
{
    class GameInfo
    {
        public static event EventHandler<GameInfoUpdateEventArgs> GameInfoUpdate;
        private static Dictionary<string, string> zones = new Dictionary<string, string>();
        private static Dictionary<string, string> events = new Dictionary<string, string>();
        private static Dictionary<string, RemnantItem[]> eventItem = new Dictionary<string, RemnantItem[]>();
        private static Dictionary<string, string> subLocations = new Dictionary<string, string>();
        private static Dictionary<string, string> mainLocations = new Dictionary<string, string>();
        private static Dictionary<string, string> archetypes = new Dictionary<string, string>();
        public static Dictionary<string, string> Events
        {
            get
            {
                if (events.Count == 0)
                {
                    RefreshGameInfo();
                }

                return events;
            }
        }
        public static Dictionary<string, RemnantItem[]> EventItem
        {
            get
            {
                if (eventItem.Count == 0)
                {
                    RefreshGameInfo();
                }

                return eventItem;
            }
        }
        public static Dictionary<string, string> Zones
        {
            get
            {
                if (zones.Count == 0)
                {
                    RefreshGameInfo();
                }

                return zones;
            }
        }
        public static Dictionary<string, string> SubLocations
        {
            get
            {
                if (subLocations.Count == 0)
                {
                    RefreshGameInfo();
                }

                return subLocations;
            }
        }
        public static Dictionary<string, string> MainLocations
        {
            get
            {
                if (mainLocations.Count == 0)
                {
                    RefreshGameInfo();
                }

                return mainLocations;
            }
        }

        public static Dictionary<string, string> Archetypes
        {
            get
            {
                if (archetypes.Count == 0)
                {
                    RefreshGameInfo();
                }

                return archetypes;
            }
        }

        public static void RefreshGameInfo()
        {
            zones.Clear();
            events.Clear();
            eventItem.Clear();
            subLocations.Clear();
            mainLocations.Clear();
            archetypes.Clear();
            var json = JsonNode.Parse(File.ReadAllText("game.json"));
            var gameEvents = json["events"].AsObject();
            foreach (var kvp in gameEvents.AsEnumerable())
            {
                List<RemnantItem> eventItems = new List<RemnantItem>();
                foreach (var item in kvp.Value["items"].AsArray())
                {
                    RemnantItem rItem = new RemnantItem(item.ToString());
                    eventItems.Add(rItem);
                }
                eventItem.Add(kvp.Key, eventItems.ToArray());
            }
        }

        public static void CheckForNewGameInfo()
        {
            GameInfoUpdateEventArgs args = new GameInfoUpdateEventArgs();
            try
            {
                WebClient client = new WebClient();
                client.DownloadFile("https://raw.githubusercontent.com/Razzmatazzz/RemnantSaveGuardian/master/game.json", "tempgame.json");

                XmlTextReader reader = new XmlTextReader("TempGameInfo.xml");
                reader.WhitespaceHandling = WhitespaceHandling.None;
                int remoteversion = 0;
                int localversion = 0;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name.Equals("GameInfo"))
                        {
                            remoteversion = int.Parse(reader.GetAttribute("version"));
                            break;
                        }
                    }
                }
                args.RemoteVersion = remoteversion;
                reader.Close();
                if (File.Exists("GameInfo.xml"))
                {
                    reader = new XmlTextReader("GameInfo.xml");
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            if (reader.Name.Equals("GameInfo"))
                            {
                                localversion = int.Parse(reader.GetAttribute("version"));
                                break;
                            }
                        }
                    }
                    reader.Close();
                    args.LocalVersion = localversion;

                    if (remoteversion > localversion)
                    {
                        File.Delete("GameInfo.xml");
                        File.Move("TempGameInfo.xml", "GameInfo.xml");
                        RefreshGameInfo();
                        args.Result = GameInfoUpdateResult.Updated;
                        args.Message = "Game info updated from v" + localversion + " to v" + remoteversion + ".";
                    }
                    else
                    {
                        File.Delete("TempGameInfo.xml");
                    }
                }
                else
                {
                    File.Move("TempGameInfo.xml", "GameInfo.xml");
                    RefreshGameInfo();
                    args.Result = GameInfoUpdateResult.Updated;
                    args.Message = "No local game info found; updated to v" + remoteversion + ".";
                }
            }
            catch (Exception ex)
            {
                args.Result = GameInfoUpdateResult.Failed;
                args.Message = "Error checking for new game info: " + ex.Message;
            }

            OnGameInfoUpdate(args);
        }

        protected static void OnGameInfoUpdate(GameInfoUpdateEventArgs e)
        {
            EventHandler<GameInfoUpdateEventArgs> handler = GameInfoUpdate;
            handler?.Invoke(typeof(GameInfo), e);
        }
    }
    public class GameInfoUpdateEventArgs : EventArgs
    {
        public int LocalVersion { get; set; }
        public int RemoteVersion { get; set; }
        public string Message { get; set; }
        public GameInfoUpdateResult Result { get; set; }

        public GameInfoUpdateEventArgs()
        {
            this.LocalVersion = 0;
            this.RemoteVersion = 0;
            this.Message = "No new game info found.";
            this.Result = GameInfoUpdateResult.NoUpdate;
        }
    }

    public enum GameInfoUpdateResult
    {
        Updated,
        Failed,
        NoUpdate
    }
}
