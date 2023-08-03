using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WPFLocalizeExtension.Extensions;

namespace RemnantSaveGuardian
{
    internal class Loc
    {
        public static T GetLocalizedValue<T>(string key, LocalizationOptions options)
        {
            var ns = "Strings";
            if (options.Has("namespace") && options["namespace"] != "")
            {
                ns = options["namespace"];
            }
            //Debug.WriteLine($"{ns}:{key}");
            return LocExtension.GetLocalizedValue<T>(Assembly.GetCallingAssembly().GetName().Name + $":{ns}:" + key);
        }
        public static string T(string key, LocalizationOptions options)
        {
            var val = GetLocalizedValue<string>(key, options);
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
            var matches = new Regex(@"{(?:(?<namespace>\w+?):)?(?<sub>\w+?)}").Matches(val);
            foreach (Match match in matches)
            {
                var optionsToUse = options;
                if (match.Groups.ContainsKey("namespace") && match.Groups["namespace"].Value != "")
                {
                    optionsToUse = new LocalizationOptions(options);
                    optionsToUse["namespace"] = match.Groups["namespace"].Value;
                }
                val = val.Replace(match.Value, T(match.Groups["sub"].Value, optionsToUse));
            }
            return val;
        }
        public static string T(string key)
        {
            return T(key, new LocalizationOptions { { "namespace", "Strings" } });
        }
        public static string GameT(string key, LocalizationOptions options)
        {
            options["namespace"] = "GameStrings";
            return T(key, options);
        }
        public static string GameT(string key)
        {
            return T(key, new LocalizationOptions { { "namespace", "GameStrings" } });
        }
    }

    public class LocalizationOptions : Dictionary<string, string>
    { 
        public LocalizationOptions(LocalizationOptions source)
        {
            foreach (var kvp in source)
            {
                this.Add(kvp.Key, kvp.Value);
            }
        }
        public LocalizationOptions() { }

        public bool Has(string key)
        {
            return this[key] != null;
        }
    }
}
