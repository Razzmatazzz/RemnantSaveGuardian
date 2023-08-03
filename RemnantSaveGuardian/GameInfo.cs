using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Net;
using System.IO;
using System.Text.Json.Nodes;
using System.Net.Http;
using System.Diagnostics;

namespace RemnantSaveGuardian
{
    class GameInfo
    {
        public static event EventHandler<GameInfoUpdateEventArgs> GameInfoUpdate;
        private static Dictionary<string, string> zones = new Dictionary<string, string>();
        private static Dictionary<string, string> events = new Dictionary<string, string>();
        private static Dictionary<string, List<RemnantItem>> eventItem = new Dictionary<string, List<RemnantItem>>();
        private static Dictionary<string, string> subLocations = new Dictionary<string, string>();
        private static Dictionary<string, string> injectables = new Dictionary<string, string>();
        private static List<string> mainLocations = new List<string>();
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
        public static Dictionary<string, List<RemnantItem>> EventItem
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
        public static Dictionary<string, string> Injectables
        {
            get
            {
                if (injectables.Count == 0)
                {
                    RefreshGameInfo();
                }

                return injectables;
            }
        }
        public static List<string> MainLocations
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
            injectables.Clear();
            mainLocations.Clear();
            archetypes.Clear();
            var json = JsonNode.Parse(File.ReadAllText("game.json"));
            var gameEvents = json["events"].AsObject();
            foreach (var worldkvp in gameEvents.AsEnumerable())
            {
                foreach (var kvp in worldkvp.Value.AsObject().AsEnumerable())
                {
                    List<RemnantItem> eventItems = new List<RemnantItem>();
                    if (kvp.Value == null)
                    {
                        Logger.Warn($"Event {kvp.Key} has no items");
                        continue;
                    }
                    foreach (var item in kvp.Value.AsArray())
                    {
                        RemnantItem rItem = new RemnantItem(item["name"].ToString());
                        if (item["notes"] != null)
                        {
                            rItem.ItemNotes = item["notes"].ToString();
                        }
                        if (item["mode"] != null)
                        {
                            rItem.ItemMode = (RemnantItem.RemnantItemMode)Enum.Parse(typeof(RemnantItem.RemnantItemMode), item["mode"].ToString(), true);
                        }
                        eventItems.Add(rItem);
                    }
                    eventItem.Add(kvp.Key, eventItems);
                }
            }
            var locations = json["mainLocations"].AsArray();
            foreach (var location in locations)
            {
                mainLocations.Add(location.ToString());
            }
            var subLocs = json["subLocations"].AsObject();
            foreach (var worldkvp in subLocs.AsEnumerable())
            {
                foreach (var kvp in worldkvp.Value.AsObject().AsEnumerable())
                {
                    subLocations.Add(kvp.Key, kvp.Value.ToString());
                }
            }
            var injects = json["injectables"].AsObject();
            foreach (var worldkvp in injects.AsEnumerable())
            {
                foreach (var kvp in worldkvp.Value.AsObject().AsEnumerable())
                {
                    injectables.Add(kvp.Key, kvp.Value.ToString());
                }
            }
        }

        public static async void CheckForNewGameInfo()
        {
            GameInfoUpdateEventArgs args = new GameInfoUpdateEventArgs()
            {
                Result = GameInfoUpdateResult.NoUpdate,
            };
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://raw.githubusercontent.com/Razzmatazzz/RemnantSaveGuardian/main/RemnantSaveGuardian/game.json");
                request.Headers.Add("user-agent", "remnant-save-guardian");
                HttpClient client = new();
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                JsonNode gameJson = JsonNode.Parse(await response.Content.ReadAsStringAsync());
                args.RemoteVersion = int.Parse(gameJson["version"].ToString());

                var json = JsonNode.Parse(File.ReadAllText("game.json"));
                args.LocalVersion = int.Parse(json["version"].ToString());

                if (args.RemoteVersion > args.LocalVersion)
                {
                    File.WriteAllText("game.json", gameJson.ToJsonString());
                    try {
                        RefreshGameInfo();
                    } catch (Exception ex) {
                        File.WriteAllText("game.json", json.ToString());
                        Logger.Error(Loc.T("Could not parse updated game data; check for new version of this app"));
                    }
                    args.Result = GameInfoUpdateResult.Updated;
                    args.Message = Loc.T("Game info updated.");
                }
            }
            catch (Exception ex)
            {
                args.Result = GameInfoUpdateResult.Failed;
                args.Message = $"{Loc.T("Error checking for new game info")}: {ex.Message}";
            }

            GameInfoUpdate?.Invoke(typeof(GameInfo), args);
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
