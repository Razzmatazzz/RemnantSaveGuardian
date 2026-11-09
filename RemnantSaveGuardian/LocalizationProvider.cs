using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;

namespace RemnantSaveGuardian
{
    internal class Loc
    {
        private static CultureInfo _culture = CultureInfo.CurrentUICulture;

        /// <summary>The culture used for all UI strings.</summary>
        public static CultureInfo Culture
        {
            get => _culture;
            set
            {
                _culture = value;
                locales.Strings.Culture = value;
                locales.GameStrings.Culture = value;
                CultureChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>Raised after <see cref="Culture"/> changes, so the UI can re-render.</summary>
        public static event EventHandler? CultureChanged;

        private static ResourceManager GetResourceManager(string ns)
        {
            return ns == "GameStrings" ? locales.GameStrings.ResourceManager : locales.Strings.ResourceManager;
        }

        public static T GetLocalizedValue<T>(string key, LocalizationOptions options)
        {
            var ns = "Strings";
            if (options.Has("namespace") && options["namespace"] != "")
            {
                ns = options["namespace"];
            }
            var culture = Culture;
            if (options.Has("locale") && options["locale"] != culture.ToString())
            {
                culture = new CultureInfo(options["locale"]);
            }
            if (key == null)
            {
                return default!;
            }
            object? value;
            try
            {
                value = GetResourceManager(ns).GetObject(key, culture);
            }
            catch (MissingManifestResourceException)
            {
                value = null;
            }
            return value is T typed ? typed : default!;
        }
        public static string T(string key, LocalizationOptions options)
        {
            var val = GetLocalizedValue<string>(key, options);
            if (val == null || val == "")
            {
                return key;
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
        public static bool Has(string key, LocalizationOptions options)
        {
            var val = GetLocalizedValue<string>(key, options);
            if (val == null || val == "")
            {
                return false;
            }
            return true;
        }
        public static bool GameTHas(string key)
        {
            return Has(key, new LocalizationOptions { { "namespace", "GameStrings" } });
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
