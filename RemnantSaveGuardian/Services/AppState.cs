using System;
using System.Collections.Generic;
using System.Linq;

namespace RemnantSaveGuardian.Services
{
    /// <summary>A page the user can navigate to from the side menu.</summary>
    public class NavEntry
    {
        public required string Key { get; init; }
        /// <summary>Resource key for the title; translated when rendered.</summary>
        public required string TitleKey { get; init; }
        /// <summary>Text appended to the translated title (used for backup analyzers).</summary>
        public string? TitleSuffix { get; init; }
        public required string Icon { get; init; }
        /// <summary>Save folder for a World Analyzer page; null for the live save folder.</summary>
        public string? SavePath { get; init; }
        public bool Closable { get; init; }

        public string Title => TitleSuffix == null ? Loc.T(TitleKey) : $"{Loc.T(TitleKey)} - {TitleSuffix}";
    }

    /// <summary>
    /// UI state shared between the WPF window and the Blazor components:
    /// navigation, whether the navigation/title bar is visible, and app-wide events.
    /// </summary>
    public class AppState
    {
        public const string BackupsKey = "backups";
        public const string WorldAnalyzerKey = "world-analyzer";
        public const string LogKey = "log";
        public const string SettingsKey = "settings";

        public List<NavEntry> MainItems { get; } = new()
        {
            new() { Key = BackupsKey, TitleKey = "Save Backups", Icon = MudBlazor.Icons.Material.Filled.Storage },
            new() { Key = WorldAnalyzerKey, TitleKey = "World Analyzer", Icon = MudBlazor.Icons.Material.Filled.Public },
        };
        public List<NavEntry> BackupAnalyzers { get; } = new();
        public List<NavEntry> FooterItems { get; } = new()
        {
            new() { Key = LogKey, TitleKey = "Log", Icon = MudBlazor.Icons.Material.Filled.Article },
            new() { Key = SettingsKey, TitleKey = "Settings", Icon = MudBlazor.Icons.Material.Filled.Settings },
        };

        public IEnumerable<NavEntry> AllItems => MainItems.Concat(BackupAnalyzers).Concat(FooterItems);

        /// <summary>Pages that have been opened at least once; they stay alive so their state is kept.</summary>
        public HashSet<string> Visited { get; } = new();

        public string ActiveKey { get; private set; } = BackupsKey;

        public NavEntry? ActiveEntry => AllItems.FirstOrDefault(e => e.Key == ActiveKey);

        /// <summary>False while the window is inactive and "auto hide navigation" is on.</summary>
        public bool ChromeVisible { get; private set; } = true;

        /// <summary>
        /// Window commands for the app's own title bar (the app bar), provided by the main window,
        /// which has no system title bar.
        /// </summary>
        public Action? DragWindow { get; set; }
        public Action? MinimizeWindow { get; set; }
        public Action? ToggleMaximizeWindow { get; set; }
        public Action? CloseWindow { get; set; }
        /// <summary>Opens the window's system menu (Restore, Move, Size, ...) at the mouse pointer.</summary>
        public Action? ShowSystemMenu { get; set; }

        public bool WindowMaximized { get; private set; }

        public void SetWindowMaximized(bool maximized)
        {
            if (WindowMaximized == maximized)
            {
                return;
            }
            WindowMaximized = maximized;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? Changed;

        /// <summary>Raised after a backup has been restored into the live save folder.</summary>
        public event EventHandler? BackupSaveRestored;

        public AppState()
        {
            var start = Properties.Settings.Default.StartPage;
            if (!MainItems.Any(e => e.Key == start))
            {
                Properties.Settings.Default.StartPage = BackupsKey;
                start = BackupsKey;
            }
            Navigate(start);
        }

        public void Navigate(string key)
        {
            if (!AllItems.Any(e => e.Key == key))
            {
                key = BackupsKey;
            }
            ActiveKey = key;
            Visited.Add(key);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void OpenBackupAnalyzer(SaveBackup backup)
        {
            var key = $"world-analyzer-{backup.SaveDate.Ticks}";
            if (!BackupAnalyzers.Any(e => e.Key == key))
            {
                BackupAnalyzers.Add(new()
                {
                    Key = key,
                    TitleKey = "World Analyzer",
                    TitleSuffix = backup.Name,
                    Icon = MudBlazor.Icons.Material.Filled.TravelExplore,
                    SavePath = backup.Save.SaveFolderPath,
                    Closable = true,
                });
            }
            Navigate(key);
        }

        public void CloseBackupAnalyzer(string key)
        {
            BackupAnalyzers.RemoveAll(e => e.Key == key);
            Visited.Remove(key);
            if (ActiveKey == key)
            {
                Navigate(BackupsKey);
                return;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void SetChromeVisible(bool visible)
        {
            if (ChromeVisible == visible)
            {
                return;
            }
            ChromeVisible = visible;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseBackupSaveRestored()
        {
            BackupSaveRestored?.Invoke(this, EventArgs.Empty);
        }
    }
}
