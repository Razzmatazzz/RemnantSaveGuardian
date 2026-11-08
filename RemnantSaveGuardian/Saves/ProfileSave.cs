using System.Collections.Generic;
using System.Linq;

namespace RemnantSaveGuardian.Saves
{
    /// <summary>A character stored in profile.sav.</summary>
    public sealed class SavedCharacterData
    {
        /// <summary>Slot in the profile's character list; world saves are numbered by slot (save_{slot}.sav).</summary>
        public int SlotIndex { get; init; }
        public string? ArchetypePath { get; init; }
        public string? SecondaryArchetypePath { get; init; }
        public int? PowerLevel { get; init; }
        public int? TraitRank { get; init; }
        /// <summary>Blueprint paths of every inventory item (weapons, armor, mods, rings, materials, ...).</summary>
        public List<string> InventoryItemPaths { get; } = new();
        /// <summary>Blueprint paths of unlocked traits.</summary>
        public List<string> TraitPaths { get; } = new();
        /// <summary>Blueprint paths of mods attached to items, including built-in weapon mods.</summary>
        public List<string> EquippedModPaths { get; } = new();

        public IEnumerable<string> AllItemPaths => InventoryItemPaths.Concat(TraitPaths).Concat(EquippedModPaths).Distinct();
    }

    /// <summary>Structured view of profile.sav.</summary>
    public sealed class ProfileSave
    {
        private ProfileSave(SaveArchive archive, List<SavedCharacterData> characters)
        {
            Archive = archive;
            Characters = characters;
        }

        public SaveArchive Archive { get; }
        /// <summary>Non-empty character slots in slot order.</summary>
        public IReadOnlyList<SavedCharacterData> Characters { get; }

        public static ProfileSave Load(string path) => FromArchive(SaveFile.Load(path));

        public static ProfileSave FromArchive(SaveArchive archive)
        {
            var root = archive.RootObject ?? throw new SaveFormatException("Profile has no root object.");
            var slots = root.Properties.GetValue<ArrayValue>("Characters")
                ?? throw new SaveFormatException("Profile has no Characters property.");

            var characters = new List<SavedCharacterData>();
            for (int slot = 0; slot < slots.Items.Count; slot++)
            {
                if (slots.Items[slot] is not ObjectReference reference || reference.IsNull)
                {
                    continue;
                }
                if (reference.Index >= archive.Objects.Count)
                {
                    throw new SaveFormatException($"Character slot {slot} references missing object {reference.Index}.");
                }
                characters.Add(ReadCharacter(slot, archive.Objects[reference.Index].Properties));
            }
            return new ProfileSave(archive, characters);
        }

        private static SavedCharacterData ReadCharacter(int slot, PropertyBag props)
        {
            var character = new SavedCharacterData
            {
                SlotIndex = slot,
                ArchetypePath = NullIfEmpty(props.GetString("Archetype")),
                SecondaryArchetypePath = NullIfEmpty(props.GetString("SecondaryArchetype")),
                PowerLevel = props.GetInt("PowerLevel"),
                TraitRank = props.GetInt("TraitRank"),
            };

            if (props.GetValue<StructValue>("CharacterData")?.Value is not byte[] blob)
            {
                throw new SaveFormatException($"Character slot {slot} has no CharacterData blob.");
            }
            // The character blob is a nested archive with package versions but no class path.
            var data = SaveArchive.Read(blob, 0, 0, hasPackageVersion: true, hasClassPath: false);
            var player = data.Objects.FirstOrDefault(o => o.IsActor && o.GetComponent("Inventory") != null)
                ?? throw new SaveFormatException($"Character slot {slot} has no inventory component.");

            var inventory = player.GetComponent("Inventory")!.Properties;
            foreach (var item in inventory.GetArray<StructValue>("Items").Select(s => s.Value).OfType<PropertyBag>())
            {
                AddPath(character.InventoryItemPaths, item.GetValue<ObjectReference>("ItemBP"));

                var instance = item.GetValue<ObjectReference>("InstanceData");
                if (instance != null && !instance.IsNull && instance.Index < data.Objects.Count)
                {
                    var mods = data.Objects[instance.Index].Properties.GetStruct("Mods");
                    foreach (var mod in mods?.GetArray<StructValue>("Mods").Select(s => s.Value).OfType<PropertyBag>() ?? Enumerable.Empty<PropertyBag>())
                    {
                        AddPath(character.EquippedModPaths, mod.GetValue<ObjectReference>("Mod"));
                    }
                }
            }

            var traits = player.GetComponent("Traits")?.Properties;
            foreach (var trait in traits?.GetArray<StructValue>("Traits").Select(s => s.Value).OfType<PropertyBag>() ?? Enumerable.Empty<PropertyBag>())
            {
                AddPath(character.TraitPaths, trait.GetValue<ObjectReference>("TraitBP"));
            }
            return character;
        }

        private static void AddPath(List<string> list, ObjectReference? reference)
        {
            if (reference?.Path is { Length: > 0 } path)
            {
                list.Add(path);
            }
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
