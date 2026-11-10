# GameStringsGenerator

Dev-only tool that fills in `RemnantSaveGuardian/locales/GameStrings.<lang>.resx` with the
translations Remnant 2 itself uses. It is not part of the solution or the shipped app.

## Requirements

- .NET 10 SDK
- Remnant 2 installed (found automatically through Steam, or pass `--game`)
- The game's pak AES key. The `.locres` translation files live in the encrypted `.pak` files.
  Never commit the key: pass it on the command line, through `REMNANT2_AES_KEY`, or from a file
  named `aes.key` (ignored by git).

## Usage

From the repository root:

```bash
dotnet run --project tools/GameStringsGenerator -- --aes-file aes.key --report gamestrings-report.tsv
```

| Option | Meaning |
|---|---|
| `--aes <hex>` / `--aes-file <path>` | AES key (or set `REMNANT2_AES_KEY`) |
| `--game <dir>` | Remnant 2 install folder, if it isn't found via Steam |
| `--locales <dir>` | Folder containing `GameStrings*.resx` (default: `RemnantSaveGuardian/locales`) |
| `--report <file>` | TSV showing how each entry was resolved, and which weren't |
| `--keep-existing` | Only add missing translations; never overwrite existing ones |
| `--skip-english` | Don't touch the neutral (English) `GameStrings.resx` |
| `--dry-run` | Print the summary without writing files |

Review the result with `git diff` before committing.

## How it works

The neutral `GameStrings.resx` (English) is the list of entries. For each one:

1. **Asset match.** If a game asset has the same file name as the resx key (for example
   `Amulet_AbrasiveWhetstone.uasset`), the tool scans its raw bytes for localization keys and
   picks the one whose English text matches the resx value. A small edit distance is allowed here,
   so typos in the resx English (such as "Rupture Canon") still match. Report method: `asset` / `asset~`.
2. **Text match.** Otherwise it looks up the resx English value in the game's English `.locres`,
   preferring exact matches and otherwise ignoring only case and whitespace. Method: `text` / `text~`.
   This is less certain than an asset match: the same English word can mean different things.
3. The matched key is translated with each language's `Remnant2.locres`. If several keys share the
   same English text, the most common translation wins; ties are left alone.
4. The neutral `GameStrings.resx` is updated too, with the game's own English text. Because
   matching only allows small differences, this fixes spelling, capitalization and punctuation
   (e.g. "Rupture Canon" → "Rupture Cannon") without renaming anything. The English names are
   also used to build wiki links, so check that changed names still match wiki page titles.

Entries whose English value contains `{placeholders}` are skipped. Entries the game has no text for,
or that can't be matched (mostly location and event names the app made up), keep their
hand-written translations. The game's Traditional Chinese (`zh-Hant`) localization is only partial,
so most of that file stays hand-maintained.

No `.usmap` mappings file is needed, because assets are scanned as raw bytes rather than fully
parsed.
