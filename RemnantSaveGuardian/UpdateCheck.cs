using AutoUpdaterDotNET;
using System;
using System.Windows.Documents;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace RemnantSaveGuardian
{
    internal class UpdateCheck
    {
        private static string repo = "Razzmatazzz/RemnantSaveGuardian";
        private static readonly HttpClient client = new();
        private static DateTime lastUpdateCheck = DateTime.MinValue;

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
                JsonNode latestRelease = JsonNode.Parse(await response.Content.ReadAsStringAsync());

                Version remoteVersion = new Version(latestRelease["tag_name"].ToString());
                Version localVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (localVersion.CompareTo(remoteVersion) == -1)
                {
                    NewVersion?.Invoke(null, new() { Version = remoteVersion, Uri = new(latestRelease["html_url"].ToString()) });
                    var messageBox = new Wpf.Ui.Controls.MessageBox();
                    messageBox.Title = Loc.T("Update available");
                    Hyperlink hyperLink = new()
                    {
                        NavigateUri = new Uri($"https://github.com/Razzmatazzz/RemnantSaveGuardian/releases/tag/{remoteVersion}")
                    };
                    hyperLink.Inlines.Add(Loc.T("Changelog"));
                    hyperLink.RequestNavigate += (o, e) => Process.Start("explorer.exe", e.Uri.ToString());
                    var txtBlock = new TextBlock()
                    {
                        Text = Loc.T("The latest version of Remnant Save Guardian is {CurrentVersion}. You are using version {LocalVersion}. Do you want to upgrade the application now?",
                            new LocalizationOptions()
                            {
                                {
                                    "CurrentVersion", remoteVersion.ToString()
                                },
                                {
                                    "LocalVersion", localVersion.ToString()
                                }
                            }
                        ) + "\n",
                        TextWrapping = System.Windows.TextWrapping.WrapWithOverflow,
                    };
                    txtBlock.Inlines.Add(hyperLink);
                    messageBox.Content = txtBlock;
                    messageBox.ButtonLeftName = Loc.T("Update");
                    messageBox.ButtonLeftClick += (send, updatedEvent) => {
                        UpdateInfoEventArgs args = new()
                        {
                            InstalledVersion = localVersion,
                            CurrentVersion = remoteVersion.ToString(),
                            DownloadURL = $"https://github.com/Razzmatazzz/RemnantSaveGuardian/releases/download/{remoteVersion}/RemnantSaveGuardian.zip"
                        };
                        messageBox.Close();
                        AutoUpdater.DownloadUpdate(args);
                        Application.Current.Shutdown();
                    };
                    messageBox.ButtonRightName = Loc.T("Cancel");
                    messageBox.ButtonRightClick += (send, updatedEvent) => {
                        messageBox.Close();
                    };
                    messageBox.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"{Loc.T("Error checking for new version")}: {ex.Message}");
            }
        }
    }

    public class NewVersionEventArgs : EventArgs
    {
        public Version Version { get; set; }
        public Uri Uri { get; set; }
    }
    public class UpdateCheckErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
    }
}
