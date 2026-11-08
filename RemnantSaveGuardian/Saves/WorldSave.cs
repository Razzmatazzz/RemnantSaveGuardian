using System.Collections.Generic;
using System.Linq;

namespace RemnantSaveGuardian.Saves
{
    public enum LootState
    {
        /// <summary>The area holding the item has not been generated yet (not visited).</summary>
        NotSpawned,
        /// <summary>The item is in the world and has not been picked up.</summary>
        InWorld,
        /// <summary>The item has been picked up.</summary>
        Collected,
    }

    /// <summary>An item or actor a quest spawns into one of its zones (from a quest component's Spawns list).</summary>
    public sealed class LootSpawn
    {
        public string Component { get; init; } = "";
        /// <summary>Blueprint path of the spawned item or actor.</summary>
        public string ActorPath { get; init; } = "";
        /// <summary>Level the spawn was placed in, or null if it has not been spawned yet.</summary>
        public string? ContainerKey { get; init; }
        /// <summary>Set by the game once the spawned actor is gone (for items: picked up).</summary>
        public bool Destroyed { get; init; }

        public LootState State => Destroyed ? LootState.Collected : ContainerKey == null ? LootState.NotSpawned : LootState.InWorld;
        public override string ToString() => $"{Component}: {ActorPath} ({State})";
    }

    /// <summary>A quest actor (story, boss, dungeon, event, injectable, ...).</summary>
    public sealed class QuestInfo
    {
        public int Id { get; init; }
        public int? ParentId { get; init; }
        /// <summary>The zone the quest was placed in, if it was placed inside another quest's zone.</summary>
        public int? ZoneId { get; init; }
        public string ClassPath { get; init; } = "";
        /// <summary>Child quests referenced by this quest's components.</summary>
        public List<int> ChildQuestIds { get; } = new();
        /// <summary>Items and actors the quest's components spawned (or will spawn).</summary>
        public List<LootSpawn> Spawns { get; } = new();
        public override string ToString() => $"{Id}: {ClassPath}";
    }

    /// <summary>A generated zone (an area of the map with its own name).</summary>
    public sealed class ZoneInfo
    {
        public int Id { get; init; }
        /// <summary>Quest that generated this zone.</summary>
        public int QuestId { get; init; }
        public string? Label { get; init; }
        public string? NameId { get; init; }
        /// <summary>Tileset and tile info assets used to build the zone.</summary>
        public List<string> TilePaths { get; } = new();
        public List<string> DynamicResources { get; } = new();
        public override string ToString() => $"{Id}: {Label}";
    }

    /// <summary>All quests and zones belonging to one game mode (campaign or adventure).</summary>
    public sealed class QuestTree
    {
        public QuestTree(QuestInfo root, List<QuestInfo> quests, List<ZoneInfo> zones)
        {
            Root = root;
            Quests = quests;
            Zones = zones;
            QuestsById = quests.GroupBy(q => q.Id).ToDictionary(g => g.Key, g => g.First());
        }

        public QuestInfo Root { get; }
        /// <summary>Quests in save order (excluding the root).</summary>
        public IReadOnlyList<QuestInfo> Quests { get; }
        /// <summary>Zones in save order.</summary>
        public IReadOnlyList<ZoneInfo> Zones { get; }
        public IReadOnlyDictionary<int, QuestInfo> QuestsById { get; }
    }

    /// <summary>Structured view of a world save (save_N.sav).</summary>
    public sealed class WorldSave
    {
        private const string MainContainerKey = "/Game/Maps/Main.Main:PersistentLevel";
        private const string ZoneActorClass = "/Script/Remnant.ZoneActor";

        private WorldSave(SaveArchive archive, Dictionary<string, byte[]> containers)
        {
            Archive = archive;
            ContainerBlobs = containers;
        }

        public SaveArchive Archive { get; }
        /// <summary>Raw persistence container blobs keyed by level path.</summary>
        public IReadOnlyDictionary<string, byte[]> ContainerBlobs { get; }
        public QuestTree? Campaign { get; private set; }
        public QuestTree? Adventure { get; private set; }

        public static WorldSave Load(string path) => FromArchive(SaveFile.Load(path));

        public static WorldSave FromArchive(SaveArchive archive)
        {
            var root = archive.RootObject ?? throw new SaveFormatException("World save has no root object.");
            var containers = new Dictionary<string, byte[]>();
            foreach (var reference in root.Properties.GetArray<ObjectReference>("Containers"))
            {
                if (reference.IsNull || reference.Index >= archive.Objects.Count)
                {
                    continue;
                }
                var props = archive.Objects[reference.Index].Properties;
                if (props.GetString("Key") is string key && props.GetValue<StructValue>("Blob")?.Value is byte[] blob)
                {
                    containers[key] = blob;
                }
            }

            var save = new WorldSave(archive, containers);
            if (!containers.TryGetValue(MainContainerKey, out var mainBlob))
            {
                throw new SaveFormatException("World save has no main level container.");
            }

            // The main level holds the root quests; each root quest's content lives in its own container.
            var mainQuests = PersistenceContainer.Read(mainBlob).Actors
                .Select(a => (actor: a, id: a.Root?.Properties.GetInt("ID")))
                .Where(x => x.id != null)
                .ToList();
            var campaignRoot = mainQuests.LastOrDefault(x => x.actor.ClassPath.EndsWith(".Quest_Campaign_Main_C"));
            var adventureRoot = mainQuests.LastOrDefault(x => x.actor.ClassPath.Contains("/Quest_AdventureMode_"));
            if (campaignRoot.actor != null)
            {
                save.Campaign = save.ReadQuestTree(campaignRoot.actor, campaignRoot.id!.Value);
            }
            if (adventureRoot.actor != null)
            {
                save.Adventure = save.ReadQuestTree(adventureRoot.actor, adventureRoot.id!.Value);
            }
            return save;
        }

        private QuestTree? ReadQuestTree(PersistenceActor rootActor, int rootId)
        {
            var rootQuest = ReadQuest(rootActor, rootActor.Root!);
            string prefix = $"/Game/Quest_{rootId}_Container.";
            var blob = ContainerBlobs.FirstOrDefault(kv => kv.Key.StartsWith(prefix)).Value;
            if (blob == null)
            {
                return null;
            }

            var quests = new List<QuestInfo>();
            var zones = new List<ZoneInfo>();
            foreach (var actor in PersistenceContainer.Read(blob).Actors)
            {
                var obj = actor.Root;
                if (obj == null)
                {
                    continue;
                }
                if (actor.ClassPath == ZoneActorClass)
                {
                    zones.Add(ReadZone(actor.Archive, obj));
                }
                else if (obj.Properties.GetInt("ID") != null)
                {
                    quests.Add(ReadQuest(actor, obj));
                }
            }
            return new QuestTree(rootQuest, quests, zones);
        }

        private static QuestInfo ReadQuest(PersistenceActor actor, UObject obj)
        {
            var quest = new QuestInfo
            {
                Id = obj.Properties.GetInt("ID")!.Value,
                ParentId = obj.Properties.GetInt("ParentQuestID"),
                ZoneId = obj.Properties.GetInt("ZoneID"),
                ClassPath = actor.ClassPath,
            };
            foreach (var component in obj.Components)
            {
                if (component.Properties.GetInt("QuestID") is int childId)
                {
                    quest.ChildQuestIds.Add(childId);
                }
                foreach (var spawn in component.Properties.GetArray<StructValue>("Spawns"))
                {
                    if (spawn.Value is PropertyBag props && ReadSpawn(component.Key, props) is LootSpawn loot)
                    {
                        quest.Spawns.Add(loot);
                    }
                }
            }
            return quest;
        }

        private static LootSpawn? ReadSpawn(string component, PropertyBag props)
        {
            var actorPath = PathValue(props.GetStruct("SpawnEntry")?.Get("ActorBP")?.Value);
            if (string.IsNullOrEmpty(actorPath))
            {
                return null;
            }
            var containerKey = props.GetStruct("Key")?.GetString("ContainerKey");
            return new LootSpawn
            {
                Component = component,
                ActorPath = actorPath,
                // Unspawned entries have an empty key ("None").
                ContainerKey = string.IsNullOrEmpty(containerKey) || containerKey == "None" ? null : containerKey,
                Destroyed = props.GetValue<bool>("Destroyed"),
            };
        }

        private static string? PathValue(object? value) => value switch
        {
            string s => s,
            StructValue { Value: string s } => s,
            ObjectReference r => r.Path,
            _ => null,
        };

        private static ZoneInfo ReadZone(SaveArchive archive, UObject obj)
        {
            var props = obj.Properties;
            var zone = new ZoneInfo
            {
                Id = props.GetInt("ID") ?? -1,
                QuestId = props.GetInt("QuestID") ?? -1,
                Label = props.GetString("Label"),
                NameId = props.GetString("NameID"),
            };
            // Tile assets are the loaded package objects referenced by the zone (tileset + tile infos).
            zone.TilePaths.AddRange(archive.Objects.Where(o => o.WasLoadedFromPackage && o.ObjectPath.StartsWith("/Game/")).Select(o => o.ObjectPath));
            zone.DynamicResources.AddRange(props.GetArray<StructValue>("DynamicResources").Select(s => s.Value).OfType<string>());
            return zone;
        }
    }
}
