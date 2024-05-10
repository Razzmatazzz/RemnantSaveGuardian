using AutoUpdaterDotNET;
using System;
using System.Windows.Documents;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using Remnant2SaveAnalyzer.Logging;

namespace Remnant2SaveAnalyzer
{
    internal class UpdateCheck
    {
        private static readonly string Repo = "AndrewSav/Remnant2SaveAnalyzer";
        private static readonly HttpClient Client = new();
        private static DateTime _lastUpdateCheck = DateTime.MinValue;

        public static event EventHandler<NewVersionEventArgs>? NewVersion;

        public static async void CheckForNewVersion()
        {
            try
            {
                if (_lastUpdateCheck.AddMinutes(5) > DateTime.Now)
                {
                    Notifications.Warn(Loc.T("You must wait 5 minutes between update checks"));
                    return;
                }
                _lastUpdateCheck = DateTime.Now;
                HttpRequestMessage request = new(HttpMethod.Get, $"https://api.github.com/repos/{Repo}/releases/latest");
                request.Headers.Add("user-agent", "remnant2-save-analyzer");
                HttpResponseMessage response = await Client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseString = await response.Content.ReadAsStringAsync();
                JsonNode latestRelease = JsonNode.Parse(responseString) ?? 
                    throw new ApplicationException($"Could not parse GitHub releases response string as json: {responseString}");

                JsonNode tagName = latestRelease["tag_name"] ?? 
                    throw new ApplicationException($"'tag_name' is not found in GitHub releases json: {responseString}");

                JsonNode htmlUrl = latestRelease["html_url"] ??
                                    throw new ApplicationException($"'html_url' is not found in GitHub releases json: {responseString}");

                Version remoteVersion = new(tagName.GetValue<string>());
                Version? localVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

                Debug.Assert(localVersion != null, nameof(localVersion) + " != null");

                if (localVersion.CompareTo(remoteVersion) == -1)
                {
                    NewVersion?.Invoke(null, new(remoteVersion, new(htmlUrl.GetValue<string>())));
                    MessageBox messageBox = new()
                    {
                        Title = Loc.T("Update available")
                    };
                    Hyperlink hyperLink = new()
                    {
                        NavigateUri = new Uri($"https://github.com/{Repo}/releases/tag/{remoteVersion}")
                    };
                    hyperLink.Inlines.Add(Loc.T("Changelog"));
                    hyperLink.RequestNavigate += (_, e) => Process.Start("explorer.exe", e.Uri.ToString());
                    TextBlock txtBlock = new()
                    {
                        Text = Loc.T("The latest version of Remnant 2 Save Analyzer is {CurrentVersion}. You are using version {LocalVersion}. Do you want to upgrade the application now?",
                            new LocalizationOptions
                            {
                                {
                                    "CurrentVersion", remoteVersion.ToString()
                                },
                                {
                                    "LocalVersion", localVersion.ToString()
                                }
                            }
                        ) + "\n",
                        TextWrapping = TextWrapping.WrapWithOverflow,
                    };
                    txtBlock.Inlines.Add(hyperLink);
                    messageBox.Content = txtBlock;
                    messageBox.ButtonLeftName = Loc.T("Update");
                    messageBox.ButtonLeftClick += (_, _) => {
                        UpdateInfoEventArgs args = new()
                        {
                            InstalledVersion = localVersion,
                            CurrentVersion = remoteVersion.ToString(),
                            DownloadURL = $"https://github.com/{Repo}/releases/download/{remoteVersion}/Remnant2SaveAnalyzer-{remoteVersion}.zip"
                        };
                        messageBox.Close();
                        AutoUpdater.DownloadUpdate(args);
                        Application.Current.Shutdown();
                    };
                    messageBox.ButtonRightName = Loc.T("Cancel");
                    messageBox.ButtonRightClick += (_, _) => {
                        messageBox.Close();
                    };
                    messageBox.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                Notifications.Error($"{Loc.T("Error checking for new version")}: {ex.Message}");
            }
        }
    }

    public class NewVersionEventArgs(Version version, Uri uri) : EventArgs
    {
        public Version Version { get; set; } = version;
        public Uri Uri { get; set; } = uri;
    }
}
