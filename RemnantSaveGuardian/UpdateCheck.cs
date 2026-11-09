using AutoUpdaterDotNET;
using System;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows;

namespace RemnantSaveGuardian
{
    internal class UpdateCheck
    {
        private static string repo = "Razzmatazzz/RemnantSaveGuardian";
        private static readonly HttpClient client = new();
        private static DateTime lastUpdateCheck = DateTime.MinValue;

        /// <summary>
        /// Raised when a newer release is available. The UI shows the update dialog in response
        /// and calls <see cref="ApplyUpdate"/> if the user accepts.
        /// </summary>
        public static event EventHandler<NewVersionEventArgs>? NewVersion;

        public static async void CheckForNewVersion()
        {
            try
            {
                if (lastUpdateCheck.AddMinutes(5) > DateTime.Now)
                {
                    Logger.Warn(Loc.T("You must wait 5 minutes between update checks"));
                    return;
                }
                lastUpdateCheck = DateTime.Now;
                GameInfo.CheckForNewGameInfo();
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases/latest");
                request.Headers.Add("user-agent", "remnant-save-guardian");
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                JsonNode latestRelease = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

                Version remoteVersion = new Version(latestRelease["tag_name"]!.ToString());
                Version localVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!;
                localVersion = new Version(localVersion.Major, localVersion.Minor, localVersion.Build);
                if (localVersion.CompareTo(remoteVersion) == -1)
                {
                    NewVersion?.Invoke(null, new()
                    {
                        Version = remoteVersion,
                        LocalVersion = localVersion,
                        Uri = new(latestRelease["html_url"]!.ToString()),
                        ChangelogUri = new Uri($"https://github.com/{repo}/releases/tag/{remoteVersion}"),
                        DownloadUrl = latestRelease["assets"]!.AsArray()[0]!["browser_download_url"]!.ToString(),
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"{Loc.T("Error checking for new version")}: {ex.Message}");
            }
        }

        /// <summary>Downloads and installs the release described by <paramref name="update"/>, then exits.</summary>
        public static void ApplyUpdate(NewVersionEventArgs update)
        {
            UpdateInfoEventArgs args = new()
            {
                InstalledVersion = update.LocalVersion,
                CurrentVersion = update.Version.ToString(),
                DownloadURL = update.DownloadUrl,
            };
            AutoUpdater.DownloadUpdate(args);
            Application.Current.Shutdown();
        }
    }

    public class NewVersionEventArgs : EventArgs
    {
        public Version Version { get; set; } = new();
        public Version LocalVersion { get; set; } = new();
        public Uri Uri { get; set; } = null!;
        public Uri ChangelogUri { get; set; } = null!;
        public string DownloadUrl { get; set; } = "";
    }
    public class UpdateCheckErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; } = null!;
    }
}
