using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using WPFLocalizeExtension.Extensions;

namespace RemnantSaveGuardian
{
    internal partial class Loc
    {
        public static T GetLocalizedValue<T>(string key, LocalizationOptions options)
        {
            string ns = "Strings";
            if (options.Has("namespace") && options["namespace"] != "")
            {
                ns = options["namespace"];
            }
            CultureInfo? currentCulture = WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.Culture;
            if (options.Has("locale") && options["locale"] != currentCulture.ToString())
            {
                WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.SetCurrentThreadCulture = false;
                WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.Culture = new CultureInfo(options["locale"]);
                T? translation = LocExtension.GetLocalizedValue<T>(Assembly.GetCallingAssembly().GetName().Name + $":{ns}:" + key);
                WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.Culture = currentCulture;
                return translation;
            }
            //Debug.WriteLine($"{ns}:{key}");
            return LocExtension.GetLocalizedValue<T>(Assembly.GetCallingAssembly().GetName().Name + $":{ns}:" + key);
        }
        public static string T(string key, LocalizationOptions options)
        {
            string val = GetLocalizedValue<string>(key, options);
            if (string.IsNullOrEmpty(val))
            {
                val = key;
                //return key;
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
            MatchCollection matches = LocalizationSubstitution().Matches(val);
            foreach (Match match in matches.Cast<Match>())
            {
                string valueToSub = match.Groups["sub"].Value;
                if (options.Has(valueToSub) && options[valueToSub] != "")
                {
                    valueToSub = options[valueToSub];
                } else
                {
                    LocalizationOptions optionsToUse = options;
                    if (match.Groups.ContainsKey("namespace") && match.Groups["namespace"].Value != "")
                    {
                        optionsToUse = new LocalizationOptions(options)
                        {
                            ["namespace"] = match.Groups["namespace"].Value
                        };
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
        //public static string GameT(string key, LocalizationOptions options)
        //{
        //    options["namespace"] = "GameStrings";
        //    return T(key, options);
        //}
        public static string GameT(string key)
        {
            return T(key, new LocalizationOptions { { "namespace", "GameStrings" } });
        }

        [GeneratedRegex(@"{(?:(?<namespace>\w+?):)?(?<sub>\w+?)}")]
        private static partial Regex LocalizationSubstitution();
        //public static bool Has(string key, LocalizationOptions options)
        //{
        //    string val = GetLocalizedValue<string>(key, options);
        //    if (val == "")
        //    {
        //        return false;
        //    }
        //    return true;
        //}
        //public static bool GameTHas(string key)
        //{
        //    return Has(key, new LocalizationOptions { { "namespace", "GameStrings" } });
        //}
    }

    public class LocalizationOptions : Dictionary<string, string>
    { 
        public LocalizationOptions(LocalizationOptions source)
        {
            foreach (KeyValuePair<string, string> kvp in source)
            {
                Add(kvp.Key, kvp.Value);
            }
        }
        public LocalizationOptions() { }

        public bool Has(string key)
        {
            if (!ContainsKey(key)) return false;
            return true;
        }
    }
}
