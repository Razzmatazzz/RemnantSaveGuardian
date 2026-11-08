using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RemnantSaveGuardian.Saves
{
    /// <summary>
    /// A quest asset path split into the parts used to identify world events, e.g.
    /// /Game/World_Nerud/Quests/Quest_SideD_TowerHeist/Quest_SideD_TowerHeists.Quest_SideD_TowerHeists_C
    /// → world World_Nerud, type SideD, name TowerHeist, details Quest_SideD_TowerHeists.
    /// </summary>
    public sealed class QuestRef
    {
        private static readonly Regex PathPattern = new(@"/Game/(?<world>(?:World|Campaign)_\w+)/Quests/(?:Quest_)?(?<eventType>[a-zA-Z0-9]+)_(?<eventName>\w+)/(?<details>\w+)\.\w+", RegexOptions.Compiled);

        private QuestRef(string value, string world, string eventType, string eventName, string details)
        {
            Value = value;
            World = world;
            EventType = eventType;
            EventName = eventName;
            Details = details;
        }

        public string Value { get; }
        public string World { get; }
        public string EventType { get; }
        public string EventName { get; }
        public string Details { get; }
        /// <summary>The quest this path was read from, when it came from a structured world save.</summary>
        public QuestInfo? Quest { get; private init; }

        public QuestRef WithQuest(QuestInfo quest) => new(Value, World, EventType, EventName, Details) { Quest = quest };

        public static QuestRef? TryParse(string path)
        {
            var match = PathPattern.Match(path);
            return match.Success ? FromMatch(match) : null;
        }

        public static QuestRef FromMatch(Match match) => new(
            match.Value,
            match.Groups["world"].Value,
            match.Groups["eventType"].Value,
            match.Groups["eventName"].Value,
            match.Groups["details"].Value);

        public static List<QuestRef> ParseAll(string text) => PathPattern.Matches(text).Select(FromMatch).ToList();

        public override string ToString() => Value;
    }

    /// <summary>A named area of a world together with the quests (events) found in it.</summary>
    public sealed class SaveArea
    {
        public string LocationName { get; init; } = "";
        public string SpawnTable { get; init; } = "";
        /// <summary>Space-separated tile asset paths, used to filter tile-specific loot.</summary>
        public string TileSets { get; init; } = "";
        public List<QuestRef> Events { get; init; } = new();

        public override string ToString() => $"{LocationName} ({Events.Count} events)";

        /// <summary>
        /// Groups a mode's quests by the zone they appear in. A quest that generated zones of its own
        /// (bosses, dungeons, story areas) belongs to those zones; any other quest belongs to the zone
        /// it was placed in, or failing that, to its parent quest's zone.
        /// </summary>
        public static List<SaveArea> FromQuestTree(QuestTree tree)
        {
            var ownedZones = tree.Zones.GroupBy(z => z.QuestId).ToDictionary(g => g.Key, g => g.Select(z => z.Id).ToList());
            var zoneOrder = tree.Zones.GroupBy(z => z.Id).Select(g => g.First()).ToList();
            var owners = zoneOrder.ToDictionary(z => z.Id, _ => new List<QuestRef>());
            var placed = zoneOrder.ToDictionary(z => z.Id, _ => new List<QuestRef>());

            foreach (var quest in tree.Quests)
            {
                if (IsGlobalQuest(tree, quest))
                {
                    continue;
                }
                var questRef = QuestRef.TryParse(quest.ClassPath)?.WithQuest(quest);
                if (questRef == null)
                {
                    continue;
                }
                if (ownedZones.TryGetValue(quest.Id, out var zoneIds))
                {
                    foreach (var zoneId in zoneIds)
                    {
                        owners[zoneId].Add(questRef);
                    }
                }
                else if (FindPlacementZone(tree, quest, ownedZones) is int zoneId && placed.ContainsKey(zoneId))
                {
                    placed[zoneId].Add(questRef);
                }
            }

            return zoneOrder.Select(zone => new SaveArea
            {
                LocationName = zone.Label ?? "",
                SpawnTable = zone.DynamicResources.FirstOrDefault(r => r.Contains("/SpawnTables/")) ?? "",
                TileSets = string.Join(" ", zone.TilePaths),
                Events = owners[zone.Id].Concat(placed[zone.Id]).ToList(),
            }).Where(area => area.Events.Count > 0).ToList();
        }

        private static int? FindPlacementZone(QuestTree tree, QuestInfo quest, Dictionary<int, List<int>> ownedZones)
        {
            var visited = new HashSet<int>();
            for (var current = quest; current != null && visited.Add(current.Id);)
            {
                if (current.ZoneId is int zoneId)
                {
                    return zoneId;
                }
                if (current != quest && ownedZones.TryGetValue(current.Id, out var zones) && zones.Count > 0)
                {
                    return zones[0];
                }
                current = current.ParentId is int parentId && tree.QuestsById.TryGetValue(parentId, out var parent) ? parent : null;
            }
            return null;
        }

        /// <summary>
        /// Quests under Quest_Global (dream sequences and similar) exist in every playthrough and are not
        /// part of the rolled world.
        /// </summary>
        private static bool IsGlobalQuest(QuestTree tree, QuestInfo quest)
        {
            var visited = new HashSet<int>();
            for (var current = quest; current != null && visited.Add(current.Id);)
            {
                if (current.ClassPath.EndsWith(".Quest_Global_C"))
                {
                    return true;
                }
                current = current.ParentId is int parentId && tree.QuestsById.TryGetValue(parentId, out var parent) ? parent : null;
            }
            return false;
        }
    }
}
