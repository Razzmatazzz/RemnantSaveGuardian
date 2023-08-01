using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace RemnantSaveGuardian
{
    public class RemnantItem : IEquatable<Object>, IComparable
    {
        private static readonly List<string> _itemKeyPatterns = new() {
            @"/Game/\w+/Items/Trinkets/(?<itemType>\w+)/\w+/(?<itemName>\w+)(?:\.|$)", // rings and amulets
            @"/Game/\w+/Items/Mods/\w+/(?<itemType>\w+)(?:\.|$)", // weapon mods
            @"/Game/\w+/Items/(?<itemType>Archetypes)/\w+/(?<itemName>Archetype_\w+)(?:\.|$)", // archetypes
            @"/Game/\w+/Items/Archetypes/(?<archetypeName>\w+)/(?<itemType>\w+)/\w+/(?<itemName>\w+)(?:\.|$)", // perks and skills
            @"/Game/\w+/Items/(?<itemType>Traits)/(?<traitType>\w+)/\w+/(?<itemName>\w+)(?:\.|$)", // traits
            @"/Game/\w+/Items/(?<itemType>Armor)/\w+/(?<armorSet>\w+)/(?<itemName>\w+)(?:\.|$)", // armor
            @"/Game/\w+/Items/(?<itemType>Weapons)/(?:\w+/)+(?<itemName>\w+)(?:\.|$)", // weapons
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
                return Loc.GameT(_name);
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

        public RemnantItem(string name, string type)
        {
            this._name = name;
            this._type = type;
            this.ItemMode = RemnantItemMode.Normal;
            this.ItemNotes = "";
        }

        public RemnantItem(string name, string type, RemnantItemMode mode)
        {
            this._name = name;
            this._type = type;
            this.ItemMode = mode;
            this.ItemNotes = "";
        }
        public RemnantItem(string name)
        {
            this._key = name;
            this._name = name;
            this._type = "";
            this._set = "";
            this._part = "";
            this.ItemMode = RemnantItemMode.Normal;
            this.ItemNotes = "";
            foreach (string pattern in ItemKeyPatterns) { 
                var nameMatch = Regex.Match(name, pattern);
                if (!nameMatch.Success)
                {
                    continue;
                }
                this._key = this._key.Replace(".", "");
                this._type = nameMatch.Groups["itemType"].Value;
                this._name = nameMatch.Groups["itemName"].Value;
                if (nameMatch.Groups.ContainsKey("armorSet"))
                {
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
                    return this.ItemMode.CompareTo(rItem.ItemMode);
                }
                return this._name.CompareTo(rItem.Key);
            }
        }
    }
}
