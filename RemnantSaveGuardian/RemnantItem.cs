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
        public enum RemnantItemMode
        {
            Normal,
            Hardcore,
            Survival
        }

        private string _name;
        private string _type;
        public string Name { 
            get
            {
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
            this._name = name;
            this._type = "";
            this.ItemMode = RemnantItemMode.Normal;
            this.ItemNotes = "";
        }

        public string GetKey()
        {
            return this._name;
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
                    return (this.GetKey().Equals(obj));
                }
                return false;
            }
            else
            {
                RemnantItem rItem = (RemnantItem)obj;
                return (this.GetKey().Equals(rItem.GetKey()) && this.ItemMode == rItem.ItemMode);
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
                    return (this.GetKey().CompareTo(obj));
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
                return this._name.CompareTo(rItem.GetKey());
            }
        }
    }
}
