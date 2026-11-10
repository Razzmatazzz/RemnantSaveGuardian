using System.Text;
using System.Xml;
using System.Xml.Linq;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Localization;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using Microsoft.Win32;

// Regenerates GameStrings.<lang>.resx from Remnant 2's own localization data.
//
// For every entry in the neutral GameStrings.resx:
//   1. Find game assets whose file name equals the resx key (e.g. Amulet_AbrasiveWhetstone.uasset),
//      collect the localization keys referenced in their raw bytes, and pick the one whose English
//      text matches the resx English value (exact, or a near match to tolerate typos).
//   2. Otherwise, look up the resx English value in the English locres (exact match after normalizing).
//   3. Translate that localization key with each language's Remnant2.locres.

var opts = Options.Parse(args);
if (opts == null) return 1;

var paksDir = FindPaksDir(opts.GameDir);
if (paksDir == null)
{
    Console.Error.WriteLine("Could not find the Remnant 2 Paks folder. Pass --game <Remnant2 install folder>.");
    return 1;
}
var localesDir = opts.LocalesDir ?? FindLocalesDir();
if (localesDir == null || !File.Exists(Path.Combine(localesDir, "GameStrings.resx")))
{
    Console.Error.WriteLine("Could not find RemnantSaveGuardian/locales/GameStrings.resx. Pass --locales <folder>.");
    return 1;
}
Console.WriteLine($"Game paks: {paksDir}");
Console.WriteLine($"Locales:   {localesDir}");

// ---- Mount the game ----------------------------------------------------------------------------
using var provider = new DefaultFileProvider(paksDir, SearchOption.TopDirectoryOnly,
    new VersionContainer(EGame.GAME_UE5_2), StringComparer.OrdinalIgnoreCase);
provider.Initialize();
var aes = opts.AesKey.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? opts.AesKey : "0x" + opts.AesKey;
provider.SubmitKey(new FGuid(), new FAesKey(aes));
provider.PostMount();
var lockedPaks = provider.UnloadedVfs.Where(v => v.Name.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)).ToList();
if (lockedPaks.Count > 0)
{
    Console.Error.WriteLine($"AES key did not unlock: {string.Join(", ", lockedPaks.Select(v => v.Name))}. Is the key current for this game version?");
    return 1;
}

// ---- Load localization ---------------------------------------------------------------------------
const string locRoot = "Remnant2/Content/Localization/Remnant2/";
var locres = new Dictionary<string, Dictionary<(string Ns, string Key), string>>(StringComparer.OrdinalIgnoreCase);
foreach (var path in provider.Files.Keys.Where(k => k.StartsWith(locRoot, StringComparison.OrdinalIgnoreCase)
                                                    && k.EndsWith("/Remnant2.locres", StringComparison.OrdinalIgnoreCase)))
{
    var lang = path[locRoot.Length..].Split('/')[0];
    var res = new FTextLocalizationResource(provider.CreateReader(path));
    var table = new Dictionary<(string, string), string>();
    foreach (var ns in res.Entries)
        foreach (var e in ns.Value)
            table[(ns.Key.Str, e.Key.Str)] = e.Value.LocalizedString;
    locres[lang] = table;
}
if (!locres.TryGetValue("en", out var en))
{
    Console.Error.WriteLine("English locres not found in game data.");
    return 1;
}
Console.WriteLine($"Game languages: {string.Join(", ", locres.Keys.OrderBy(k => k))}");

var enNamespacesByKey = en.Keys.GroupBy(k => k.Key, StringComparer.Ordinal)
    .ToDictionary(g => g.Key, g => g.Select(k => k.Ns).ToList(), StringComparer.Ordinal);
var enByText = en.GroupBy(kv => Normalize(kv.Value))
    .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToList());

// ---- Index assets by file name -------------------------------------------------------------------
var assetsByName = provider.Files.Keys
    .Where(k => k.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) && k.StartsWith("Remnant2/Content/", StringComparison.OrdinalIgnoreCase))
    .GroupBy(k => Path.GetFileNameWithoutExtension(k), StringComparer.OrdinalIgnoreCase)
    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

// ---- Resolve each resx entry to localization keys ----------------------------------------------------
var neutralPath = Path.Combine(localesDir, "GameStrings.resx");
var neutral = ReadStrings(XDocument.Load(neutralPath));

// The app looks strings up by the game's asset names, and resx lookups are case-sensitive. If a key
// matches a game asset except for case (e.g. Ring_LighthouseKeepersRing vs the game's
// Ring_LightHouseKeepersRing), rename it to the game's casing in every file.
var renames = new Dictionary<string, string>(StringComparer.Ordinal);
if (!opts.SkipEnglish)
{
    var neutralNames = neutral.Select(n => n.Name).ToHashSet(StringComparer.Ordinal);
    foreach (var (name, _) in neutral)
    {
        if (!assetsByName.TryGetValue(name, out var paths)) continue;
        var assetNames = paths.Select(p => Path.GetFileNameWithoutExtension(p)).Distinct(StringComparer.Ordinal).ToList();
        if (assetNames.Contains(name, StringComparer.Ordinal) || assetNames.Count != 1) continue;
        if (!neutralNames.Contains(assetNames[0])) renames[name] = assetNames[0];
    }
    neutral = neutral.Select(n => (renames.GetValueOrDefault(n.Name, n.Name), n.Value)).ToList();
    foreach (var (from, to) in renames) Console.WriteLine($"Renaming key {from} -> {to} (game casing)");
}
// Canonical casing of every key, for collapsing case-variant duplicates in the language files.
var canonicalKeys = neutral.Select(n => n.Name).Distinct(StringComparer.OrdinalIgnoreCase)
    .ToDictionary(n => n, n => n, StringComparer.OrdinalIgnoreCase);

var resolved = new Dictionary<string, Resolution>();
var report = new List<string> { "key\tstatus\tmethod\tresx_english\tgame_english\tloc_keys" };

foreach (var (name, english) in neutral)
{
    if (english.Contains('{') && english.Contains('}'))
    {
        report.Add($"{name}\tskipped\tplaceholder\t{Tsv(english)}\t\t");
        continue;
    }
    var target = Normalize(english);
    if (target.Length == 0) continue;

    Resolution? r = null;

    // 1. Asset with the same name as the key.
    if (assetsByName.TryGetValue(name, out var assetPaths))
    {
        var candidates = new HashSet<(string, string)>();
        foreach (var path in assetPaths)
            if (provider.TrySaveAsset(path, out var bytes))
                candidates.UnionWith(FindLocKeys(bytes, enNamespacesByKey));

        var scored = candidates
            .Select(c => (Key: c, Dist: Levenshtein(Normalize(en[c]), target)))
            .Where(c => c.Dist <= Math.Max(1, target.Length / 8))
            .OrderBy(c => c.Dist)
            .ToList();
        if (scored.Count > 0)
        {
            var best = scored.Where(s => s.Dist == scored[0].Dist).Select(s => s.Key).ToList();
            r = new Resolution(best, scored[0].Dist == 0 ? "asset" : "asset~");
        }
    }

    // 2. The same English text anywhere in the game (ignoring case and whitespace only). Prefer keys
    //    whose text matches exactly, so e.g. "Armor" doesn't pick up the all-caps "ARMOR" label.
    if (r == null && enByText.TryGetValue(target, out var byText))
    {
        var exact = byText.Where(k => en[k].Trim() == english.Trim()).ToList();
        var loose = byText.Where(k => SoftNormalize(en[k]) == SoftNormalize(english)).ToList();
        if (exact.Count > 0) r = new Resolution(exact, "text");
        else if (loose.Count > 0) r = new Resolution(loose, "text~");
    }

    if (r == null)
    {
        report.Add($"{name}\tunresolved\t\t{Tsv(english)}\t\t");
        continue;
    }
    resolved[name] = r;
    report.Add($"{name}\tok\t{r.Method}\t{Tsv(english)}\t{Tsv(en[r.Keys[0]])}\t{string.Join(" ", r.Keys.Select(k => $"[{k.Item1}]{k.Item2}"))}");
}

// ---- Write each language's resx ------------------------------------------------------------------
var summary = new List<string>();
// The neutral GameStrings.resx is English; updating it brings names in line with the game's
// spelling and capitalization (matching only ever accepted small differences).
var resxFiles = Directory.GetFiles(localesDir, "GameStrings.*.resx").OrderBy(f => f).ToList();
if (!opts.SkipEnglish) resxFiles.Insert(0, neutralPath);
foreach (var file in resxFiles)
{
    var lang = file == neutralPath ? "en" : Path.GetFileNameWithoutExtension(file)["GameStrings.".Length..];
    if (!locres.TryGetValue(lang, out var table))
    {
        summary.Add($"{lang,-8} no game localization, left unchanged");
        continue;
    }

    var doc = XDocument.Load(file, LoadOptions.PreserveWhitespace);
    var renamed = 0;
    foreach (var d in doc.Root!.Elements("data"))
        if (renames.TryGetValue((string)d.Attribute("name")!, out var newName))
        {
            d.SetAttributeValue("name", newName);
            renamed++;
        }
    var removed = RemoveDuplicates(doc.Root!, canonicalKeys, ref renamed);
    var dataByName = doc.Root!.Elements("data").Where(d => d.Attribute("type") == null)
        .ToDictionary(d => (string)d.Attribute("name")!);
    int added = 0, changed = 0, same = 0, missing = 0, conflicts = 0;

    foreach (var (name, r) in resolved)
    {
        // Several loc keys can share one English text; take the most common translation.
        var votes = r.Keys.Select(k => table.TryGetValue(k, out var t) ? t.Trim() : null)
            .Where(t => !string.IsNullOrEmpty(t)).GroupBy(t => t!).OrderByDescending(g => g.Count()).ToList();
        if (votes.Count == 0) { missing++; continue; }
        if (votes.Count > 1)
        {
            conflicts++;
            if (votes[0].Count() == votes[1].Count()) continue; // genuinely ambiguous, keep what we have
        }
        var text = votes[0].Key;

        if (dataByName.TryGetValue(name, out var data))
        {
            var valueEl = data.Element("value");
            if (valueEl == null) { data.Add(new XElement("value", text)); changed++; continue; }
            if (valueEl.Value == text) { same++; continue; }
            if (opts.KeepExisting && !string.IsNullOrWhiteSpace(valueEl.Value)) { same++; continue; }
            valueEl.Value = text;
            changed++;
        }
        else
        {
            InsertSorted(doc.Root!, name, text);
            added++;
        }
    }

    summary.Add($"{lang,-8} added {added,4}, changed {changed,4}, unchanged {same,4}, not in game {missing,4}, conflicting {conflicts,3}, duplicates removed {removed,3}, keys renamed {renamed}");
    if ((added > 0 || changed > 0 || removed > 0 || renamed > 0) && !opts.DryRun) SaveResx(doc, file);
}

var ok = report.Count(l => l.Contains("\tok\t"));
var unresolved = report.Where(l => l.Contains("\tunresolved\t")).Select(l => l.Split('\t')[0]).ToList();
Console.WriteLine();
Console.WriteLine($"Resolved {ok} of {neutral.Count} entries ({report.Count(l => l.Contains("\tskipped\t"))} placeholders skipped, {unresolved.Count} unresolved).");
foreach (var s in summary) Console.WriteLine("  " + s);
if (unresolved.Count > 0) Console.WriteLine($"Unresolved: {string.Join(", ", unresolved)}");
if (opts.ReportPath != null)
{
    File.WriteAllLines(opts.ReportPath, report, new UTF8Encoding(false));
    Console.WriteLine($"Report written to {opts.ReportPath}");
}
if (opts.DryRun) Console.WriteLine("Dry run: no files written.");
return 0;

// ==================================================================================================

// Finds localization keys referenced in a cooked asset by scanning for serialized ASCII FStrings
// (int32 length incl. null terminator, then the bytes) that are keys in the English locres.
// The FString right before a key is its namespace when that pair exists.
static IEnumerable<(string Ns, string Key)> FindLocKeys(byte[] bytes, Dictionary<string, List<string>> nsByKey)
{
    var strings = new List<(int Start, int End, string Value)>();
    for (var p = 0; p + 4 < bytes.Length; p++)
    {
        var len = BitConverter.ToInt32(bytes, p);
        if (len < 1 || len > 1024 || p + 4 + len > bytes.Length || bytes[p + 4 + len - 1] != 0) continue;
        var ok = true;
        for (var i = p + 4; i < p + 4 + len - 1 && ok; i++) ok = bytes[i] >= 0x20 && bytes[i] < 0x7F;
        if (!ok) continue;
        strings.Add((p, p + 4 + len, Encoding.ASCII.GetString(bytes, p + 4, len - 1)));
        p += 4 + len - 1;
    }
    for (var i = 0; i < strings.Count; i++)
    {
        if (!nsByKey.TryGetValue(strings[i].Value, out var namespaces)) continue;
        var prev = i > 0 && strings[i - 1].End == strings[i].Start ? strings[i - 1].Value : null;
        if (prev != null && namespaces.Contains(prev)) yield return (prev, strings[i].Value);
        else foreach (var ns in namespaces) yield return (ns, strings[i].Value);
    }
}

static string Normalize(string s) =>
    new string(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

static string SoftNormalize(string s) =>
    System.Text.RegularExpressions.Regex.Replace(s.Trim().Replace('’', '\''), @"\s+", " ").ToLowerInvariant();

static int Levenshtein(string a, string b)
{
    if (Math.Abs(a.Length - b.Length) > 8) return int.MaxValue;
    var d = new int[b.Length + 1];
    for (var j = 0; j <= b.Length; j++) d[j] = j;
    for (var i = 1; i <= a.Length; i++)
    {
        var prevDiag = d[0];
        d[0] = i;
        for (var j = 1; j <= b.Length; j++)
        {
            var tmp = d[j];
            d[j] = Math.Min(Math.Min(d[j] + 1, d[j - 1] + 1), prevDiag + (a[i - 1] == b[j - 1] ? 0 : 1));
            prevDiag = tmp;
        }
    }
    return d[b.Length];
}

static string Tsv(string s) => s.Replace('\t', ' ').Replace("\r", "").Replace('\n', ' ');

static List<(string Name, string Value)> ReadStrings(XDocument doc) =>
    doc.Root!.Elements("data")
        .Where(d => d.Attribute("type") == null && d.Attribute("mimetype") == null)
        .Select(d => ((string)d.Attribute("name")!, (string?)d.Element("value") ?? ""))
        .ToList();

// Removes repeated <data> entries. Entries with the same name are duplicates, and so are names that
// differ only in case from a key in the neutral resx (only the neutral casing is ever looked up).
// Keeps the entry with the neutral casing that has a value, else the first with a value, else the
// first, and gives it the neutral casing. A lone entry whose name differs from a neutral key only in
// case is renamed too, since it would never be found. Returns how many entries were removed; adds
// the number of renamed entries to `renamed`.
static int RemoveDuplicates(XElement root, Dictionary<string, string> canonicalKeys, ref int renamed)
{
    var removed = 0;
    string GroupKey(XElement d)
    {
        var name = (string)d.Attribute("name")!;
        return canonicalKeys.TryGetValue(name, out var canonical) ? canonical : name;
    }
    static bool HasValue(XElement d) => !string.IsNullOrWhiteSpace((string?)d.Element("value"));
    var groups = root.Elements("data").Where(d => d.Attribute("type") == null)
        .GroupBy(GroupKey, StringComparer.Ordinal)
        .Where(g => g.Count() > 1 || (string)g.First().Attribute("name")! != g.Key).ToList();
    foreach (var g in groups)
    {
        var keep = g.FirstOrDefault(d => (string)d.Attribute("name")! == g.Key && HasValue(d))
                   ?? g.FirstOrDefault(HasValue) ?? g.First();
        if ((string)keep.Attribute("name")! != g.Key)
        {
            keep.SetAttributeValue("name", g.Key);
            renamed++;
        }
        foreach (var dup in g.Where(d => d != keep))
        {
            if (dup.PreviousNode is XText ws && string.IsNullOrWhiteSpace(ws.Value)) ws.Remove();
            dup.Remove();
            removed++;
        }
    }
    return removed;
}

// Inserts a new <data> entry before the first existing entry that sorts after it, reusing the
// indentation in front of the neighbouring entry so existing lines are left untouched.
static void InsertSorted(XElement root, string name, string text)
{
    var el = new XElement("data",
        new XAttribute("name", name),
        new XAttribute(XNamespace.Xml + "space", "preserve"),
        "\n    ", new XElement("value", text), "\n  ");
    var datas = root.Elements("data").Where(d => d.Attribute("type") == null).ToList();
    var next = datas.FirstOrDefault(d => string.Compare((string)d.Attribute("name")!, name, StringComparison.OrdinalIgnoreCase) > 0);
    var neighbour = next ?? datas.LastOrDefault();
    var indent = neighbour?.PreviousNode is XText ws && string.IsNullOrWhiteSpace(ws.Value) ? ws.Value : "\n  ";
    if (next != null) next.AddBeforeSelf(el, new XText(indent));
    else if (neighbour != null) neighbour.AddAfterSelf(new XText(indent), el);
    else root.Add(new XText(indent), el, new XText("\n"));
}

// Saves with the same encoding (BOM or not) and CRLF line endings the resx files already use.
static void SaveResx(XDocument doc, string path)
{
    var original = File.ReadAllBytes(path);
    var hasBom = original.Length >= 3 && original[0] == 0xEF && original[1] == 0xBB && original[2] == 0xBF;
    var settings = new XmlWriterSettings
    {
        Encoding = new UTF8Encoding(hasBom),
        NewLineHandling = NewLineHandling.Replace,
        NewLineChars = "\r\n",
        Indent = false,
    };
    using var ms = new MemoryStream();
    using (var writer = XmlWriter.Create(ms, settings)) doc.Save(writer);
    var text = new UTF8Encoding(false).GetString(ms.ToArray()).TrimStart('\uFEFF');
    // XDocument drops whitespace between the declaration and <root>.
    text = text.Replace("?><root>", "?>\r\n<root>");
    if (Encoding.UTF8.GetString(original).EndsWith("\n") && !text.EndsWith("\n")) text += "\r\n";
    File.WriteAllText(path, text, new UTF8Encoding(hasBom));
}

static string? FindPaksDir(string? gameDir)
{
    var roots = new List<string>();
    if (gameDir != null) roots.Add(gameDir);
    else
    {
        var steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        if (steam != null)
        {
            roots.Add(Path.Combine(steam, "steamapps", "common", "Remnant2"));
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
                foreach (var line in File.ReadLines(vdf))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(line, "^\\s*\"path\"\\s*\"(.+)\"");
                    if (m.Success) roots.Add(Path.Combine(m.Groups[1].Value.Replace(@"\\", @"\"), "steamapps", "common", "Remnant2"));
                }
        }
    }
    foreach (var root in roots)
        foreach (var candidate in new[] { root, Path.Combine(root, "Remnant2", "Content", "Paks"), Path.Combine(root, "Content", "Paks") })
            if (Directory.Exists(candidate) && Directory.EnumerateFiles(candidate, "*.utoc").Any())
                return candidate;
    return null;
}

static string? FindLocalesDir()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, "RemnantSaveGuardian", "locales");
        if (File.Exists(Path.Combine(candidate, "GameStrings.resx"))) return candidate;
    }
    return null;
}

record Resolution(List<(string, string)> Keys, string Method);

class Options
{
    public string AesKey = "";
    public string? GameDir, LocalesDir, ReportPath;
    public bool DryRun, KeepExisting, SkipEnglish;

    public static Options? Parse(string[] args)
    {
        var o = new Options { AesKey = Environment.GetEnvironmentVariable("REMNANT2_AES_KEY") ?? "" };
        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
            switch (args[i])
            {
                case "--aes": o.AesKey = Next(); break;
                case "--aes-file": o.AesKey = File.ReadLines(Next()).First().Trim(); break;
                case "--game": o.GameDir = Next(); break;
                case "--locales": o.LocalesDir = Next(); break;
                case "--report": o.ReportPath = Next(); break;
                case "--dry-run": o.DryRun = true; break;
                case "--keep-existing": o.KeepExisting = true; break;
                case "--skip-english": o.SkipEnglish = true; break;
                default:
                    Console.Error.WriteLine($"Unknown argument {args[i]}");
                    Usage();
                    return null;
            }
        }
        if (string.IsNullOrWhiteSpace(o.AesKey))
        {
            Console.Error.WriteLine("No AES key. Set REMNANT2_AES_KEY or pass --aes / --aes-file.");
            Usage();
            return null;
        }
        return o;
    }

    static void Usage() => Console.Error.WriteLine("""
        Usage: dotnet run --project tools/GameStringsGenerator -- [options]
          --aes <hex>          Remnant 2 pak AES key (or env REMNANT2_AES_KEY)
          --aes-file <path>    Read the AES key from the first line of a file
          --game <dir>         Remnant 2 install folder (default: found via Steam)
          --locales <dir>      Folder with GameStrings*.resx (default: RemnantSaveGuardian/locales)
          --report <file>      Write a TSV of how each entry was resolved
          --keep-existing      Only fill in missing translations; never overwrite
          --skip-english       Leave the neutral (English) GameStrings.resx alone
          --dry-run            Don't write any files
        """);
}
