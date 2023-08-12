using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
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
            var currentCulture = WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.Culture;
            if (options.Has("locale") && options["locale"] != currentCulture.ToString())
            {
                WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.SetCurrentThreadCulture = false;
                WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.Culture = new CultureInfo(options["locale"]);
                var translation = LocExtension.GetLocalizedValue<T>(Assembly.GetCallingAssembly().GetName().Name + $":{ns}:" + key);
                WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.Culture = currentCulture;
                return translation;
            }
            //Debug.WriteLine($"{ns}:{key}");
            return LocExtension.GetLocalizedValue<T>(Assembly.GetCallingAssembly().GetName().Name + $":{ns}:" + key);
        }
        public static string T(string key, LocalizationOptions options)
        {
            var val = GetLocalizedValue<string>(key, options);
            if (val == null || val == "")
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
                var valueToSub = match.Groups["sub"].Value;
                if (options.Has(valueToSub) && options[valueToSub] != "")
                {
                    valueToSub = options[valueToSub];
                } else
                {
                    var optionsToUse = options;
                    if (match.Groups.ContainsKey("namespace") && match.Groups["namespace"].Value != "")
                    {
                        optionsToUse = new LocalizationOptions(options);
                        optionsToUse["namespace"] = match.Groups["namespace"].Value;
                    }
                    valueToSub = T(valueToSub, optionsToUse);
                }
                val = val.Replace(match.Value, valueToSub);
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
            if (!ContainsKey(key)) return false;
            return this[key] != null;
        }
    }
}
