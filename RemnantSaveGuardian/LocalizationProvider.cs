using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WPFLocalizeExtension.Extensions;

namespace RemnantSaveGuardian
{
    internal class Loc
    {
        public static T GetLocalizedValue<T>(string key, string resourceFile)
        {
            return LocExtension.GetLocalizedValue<T>(Assembly.GetCallingAssembly().GetName().Name + $":{resourceFile}:" + key);
        }
        public static string T(string key, string resourceFile)
        {
            var val = GetLocalizedValue<string>(key, resourceFile);
            if (val == null)
            {
                return key;
                /*if (resourceFile != "GameStrings")
                {
                    return key;
                }
                if (key == null)
                {
                    return key;
                }
                return Regex.Replace(key.Replace("_", " "), "([A-Z0-9]+)", " $1").Trim();*/
            }
            var matches = new Regex(@"{(?<sub>\w+?)}").Matches(val);
            foreach (Match match in matches)
            {
                val = val.Replace(match.Value, T(match.Groups["sub"].Value, resourceFile));
            }
            return val;
        }
        public static string T(string key)
        {
            return T(key, "Strings");
        }
        public static string GameT(string key)
        {
            return T(key, "GameStrings");
        }
    }
}
