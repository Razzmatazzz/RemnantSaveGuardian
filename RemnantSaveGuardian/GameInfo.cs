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
                    RefreshGameInfo();
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
