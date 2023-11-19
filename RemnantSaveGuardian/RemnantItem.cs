using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RemnantSaveGuardian
{
    public class RemnantItem : IEquatable<Object>, IComparable
    {
        private static readonly List<string> _itemKeyPatterns = new() {
            @"/Items/Trinkets/(?<itemType>\w+)/(?:\w+/)+(?<itemName>\w+)(?:\.|$)", // rings and amulets
            @"/Items/(?<itemType>Mods)/\w+/(?<itemName>\w+)(?:\.|$)", // weapon mods
            @"/Items/(?<itemType>Archetypes)/\w+/(?<itemName>Archetype_\w+)(?:\.|$)", // archetypes
            @"/Items/Archetypes/(?<archetypeName>\w+)/(?<itemType>\w+)/\w+/(?<itemName>\w+)(?:\.|$)", // perks and skills
            @"/Items/(?<itemType>Traits)/(?<traitType>\w+?/)?\w+?/(?<itemName>\w+)(?:\.|$)", // traits
            @"/Items/Archetypes/(?<armorSet>\w+)/(?<itemType>Armor)/(?<itemName>\w+)(?:\.|$)", // armors
            @"/Items/(?<itemType>Armor)/(?:\w+/)?(?:(?<armorSet>\w+)/)?(?<itemName>\w+)(?:\.|$)", // armor
            @"/Items/(?<itemType>Weapons)/(?:\w+/)+(?<itemName>\w+)(?:\.|$)", // weapons
            @"/Items/(?<itemType>Gems)/(?:\w+/)+(?<itemName>\w+)(?:\.|$)", // gems
            @"/Items/Armor/(?:\w+/)?(?<itemType>Relic)Testing/(?:\w+/)+(?<itemName>\w+)(?:\.|$)", // relics
            @"/Items/(?<itemType>Relic)s/(?:\w+/)+(?<itemName>\w+)(?:\.|$)", // relics
            @"/Items/Materials/(?<itemType>Engrams)/(?<itemName>\w+)(?:\.|$)", // engrams
            @"/(?<itemType>Quests)/Quest_\w+/Items/(?<itemName>\w+)(?:\.|$)", // quest items
            @"/Items/(?<itemType>Materials)/World/\w+/(?<itemName>\w+)(?:\.|$)", // materials
        };
        public static List<string> ItemKeyPatterns { get { return _itemKeyPatterns; } }
        public enum RemnantItemMode
        {
            Normal,
            Hardcore,
            Survival
        }

        private string _key;
        private string _name;
        private string _type;
        private string _set;
        private string _part;
        public string Name {
            get
            {
                if (this._set != "" && this._part != "")
                {
                    return $"{Loc.GameT($"Armor_{this._set}")} ({Loc.GameT($"Armor_{this._part}")})";
                }
                if (_type == "Armor")
                {
                    var armorMatch = Regex.Match(_name, @"\w+_(?<armorPart>(?:Head|Body|Gloves|Legs))_\w+");
                    if (armorMatch.Success)
                    {
                        return $"{Loc.GameT(_name.Replace($"{armorMatch.Groups["armorPart"].Value}_", ""))} ({Loc.GameT($"Armor_{armorMatch.Groups["armorPart"].Value}")})";
                    }
                }
                return Loc.GameT(_name);
            }
        }
        public string RawName
        {
            get
            {
                return _name;
            }
        }
        public string Type
        {
            get
            {
                return Loc.GameT(_type);
            }
        }
        public string RawType
        {
            get
            {
                return _type;
            }
        }
        public string Key
        {
            get
            {
                return _key;
            }
        }
        public RemnantItemMode ItemMode { get; set; }
        public string ItemNotes { get; set; }
        public bool Coop { get; set;  }
        public string TileSet { get; set; }
        public bool IsArmorSet { get; set; }
        public RemnantItem(string nameOrKey)
        {
            this._key = nameOrKey;
            this._name = nameOrKey;
            this._type = "Unknown";
            this._set = "";
            this._part = "";
            this.ItemMode = RemnantItemMode.Normal;
            this.ItemNotes = "";
            this.Coop = false;
            TileSet = "";
            IsArmorSet = true;
            foreach (string pattern in ItemKeyPatterns) {
                var nameMatch = Regex.Match(nameOrKey, pattern);
                if (!nameMatch.Success)
                {
                    continue;
                }
                this._key = this._key.Replace(".", "");
                this._type = nameMatch.Groups["itemType"].Value;
                this._name = nameMatch.Groups["itemName"].Value;
                if (nameMatch.Groups.ContainsKey("armorSet"))
                {
                    //this._type = "Armor";
                    this._set = nameMatch.Groups["armorSet"].Value;
                    var armorMatch = Regex.Match(this._name, @"Armor_(?<armorPart>\w+)_\w+");
                    if (armorMatch.Success)
                    {
                        this._part = armorMatch.Groups["armorPart"].Value;
                    }
                }
                break;
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public string TypeName()
        {
            if (_type == "")
            {
                return Name;
            }
            return Type + ": " + Name;
        }

        public override bool Equals(Object? obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null))
            {
                return false;
            }
            else if (!this.GetType().Equals(obj.GetType()))
            {
                if (obj.GetType() == typeof(string))
                {
                    return (this.Key.Equals(obj));
                }
                return false;
            }
            else
            {
                RemnantItem rItem = (RemnantItem)obj;
                return (this.Key.Equals(rItem.Key) && this.ItemMode == rItem.ItemMode);
            }
        }

        public override int GetHashCode()
        {
            return this._name.GetHashCode();
        }

        public int CompareTo(Object? obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null))
            {
                return 1;
            }
            else if (!this.GetType().Equals(obj.GetType()))
            {
                if (obj.GetType() == typeof(string))
                {
                    return (this.Key.CompareTo(obj));
                }
                return this.ToString().CompareTo(obj.ToString());
            }
            else
            {
                RemnantItem rItem = (RemnantItem)obj;
                if (this.ItemMode != rItem.ItemMode)
                {
                    var modeCompare = this.ItemMode.CompareTo(rItem.ItemMode);
                    if (modeCompare != 0)
                    {
                        return modeCompare;
                    }
                }
                return this._key.CompareTo(rItem.Key);
            }
        }
    }
}
