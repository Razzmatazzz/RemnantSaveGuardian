using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;
using Wpf.Ui.Common.Interfaces;

namespace RemnantSaveGuardian.Views.Pages
{
    /// <summary>
    /// Interaction logic for WorldAnalyzerPage.xaml
    /// </summary>
    public partial class WorldAnalyzerPage : INavigableView<ViewModels.WorldAnalyzerViewModel>
    {
        public ViewModels.WorldAnalyzerViewModel ViewModel
        {
            get;
        }
        private RemnantSave Save;
        private List<RemnantWorldEvent> filteredCampaign;
        private List<RemnantWorldEvent> filteredAdventure;
        private double midFontSize = 14;
        public WorldAnalyzerPage(ViewModels.WorldAnalyzerViewModel viewModel, string? pathToSaveFiles = null)
        {
            ViewModel = viewModel;

            InitializeComponent();

            try
            {
                if (pathToSaveFiles == null)
                {
                    pathToSaveFiles = Properties.Settings.Default.SaveFolder;
                }

                Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;

                foreach (TreeViewItem treeViewItem in treeMissingItems.Items)
                {
                    treeViewItem.IsExpanded = (bool)Properties.Settings.Default[$"{treeViewItem.Tag}_Expanded"];
                    treeViewItem.Expanded += GameType_CollapsedExpanded;
                    treeViewItem.Collapsed += GameType_CollapsedExpanded;
                }

                Save = new(pathToSaveFiles);
                if (pathToSaveFiles == Properties.Settings.Default.SaveFolder)
                {
                    SaveWatcher.SaveUpdated += (sender, eventArgs) => {
                        Dispatcher.Invoke(() =>
                        {
                            var selectedIndex = CharacterControl.SelectedIndex;
                            Save.UpdateCharacters();
                            CharacterControl.Items.Refresh();
                            if (selectedIndex >= CharacterControl.Items.Count)
                            {
                                selectedIndex = 0;
                            }
                            CharacterControl.SelectedIndex = selectedIndex;
                            //CharacterControl_SelectionChanged(null, null);
                        });
                    };
                    Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;
                    BackupsPage.BackupSaveRestored += BackupsPage_BackupSaveRestored;
                }
                CharacterControl.ItemsSource = Save.Characters;
                Save.UpdateCharacters();

                //FontSizeSlider.Value = AdventureData.FontSize;
                //FontSizeSlider.Minimum = 2.0;
                //FontSizeSlider.Maximum = AdventureData.FontSize * 2;
                FontSizeSlider.Value = Properties.Settings.Default.AnalyzerFontSize;
                FontSizeSlider.ValueChanged += FontSizeSlider_ValueChanged;

                filteredCampaign = new();
                filteredAdventure = new();
                CampaignData.ItemsSource = filteredCampaign;
                AdventureData.ItemsSource = filteredAdventure;

                CharacterControl.SelectedIndex = 0;
                checkAdventureTab();
            } catch (Exception ex) {
                Logger.Error($"Error initializing analzyer page: {ex}");
            }

        }

        private void BackupsPage_BackupSaveRestored(object? sender, EventArgs e)
        {
            var selectedIndex = CharacterControl.SelectedIndex;
            Save.UpdateCharacters();
            CharacterControl.Items.Refresh();
            if (selectedIndex >= CharacterControl.Items.Count)
            {
                selectedIndex = 0;
            }
            CharacterControl.SelectedIndex = selectedIndex;
        }

        public WorldAnalyzerPage(ViewModels.WorldAnalyzerViewModel viewModel) : this(viewModel, null)
        {
            
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Properties.Settings.Default.AnalyzerFontSize = (int)Math.Round(FontSizeSlider.Value);
            reloadEventGrids();
        }

        private void GameType_CollapsedExpanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem modeItem = (TreeViewItem)sender;
            Properties.Settings.Default[$"{modeItem.Tag}_Expanded"] = modeItem.IsExpanded;
        }

        private void SavePlaintextButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Forms.FolderBrowserDialog openFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
                openFolderDialog.Description = Loc.T("Export save files as plaintext");
                openFolderDialog.UseDescriptionForTitle = true;
                System.Windows.Forms.DialogResult result = openFolderDialog.ShowDialog();
                if (result != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                File.WriteAllText($@"{openFolderDialog.SelectedPath}\profile.txt", Save.GetProfileData());
                File.Copy(Save.SaveProfilePath, $@"{openFolderDialog.SelectedPath}\profile.sav", true);
                foreach (var filePath in Save.WorldSaves)
                {
                    File.WriteAllText($@"{openFolderDialog.SelectedPath}\{filePath.Substring(filePath.LastIndexOf(@"\")).Replace(".sav", ".txt")}", RemnantSave.DecompressSaveAsString(filePath));
                    File.Copy(filePath, $@"{openFolderDialog.SelectedPath}\{filePath.Substring(filePath.LastIndexOf(@"\"))}", true);
                }
                Logger.Success(Loc.T($"Exported save files successfully to {openFolderDialog.SelectedPath}"));
            } catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private void Default_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ShowPossibleItems" || e.PropertyName == "MissingItemColor")
            {
                Dispatcher.Invoke(() =>
                {
                    reloadEventGrids();
                });
            }
            if (e.PropertyName == "SaveFolder")
            {
                Dispatcher.Invoke(() => {
                    Save = new(Properties.Settings.Default.SaveFolder);
                    Save.UpdateCharacters();
                    checkAdventureTab();
                });
            }
            if (e.PropertyName == "ShowCoopItems")
            {
                Dispatcher.Invoke(() =>
                {
                    Save.UpdateCharacters();
                    reloadEventGrids();
                    CharacterControl_SelectionChanged(null, null);
                });
            }
        }

        private void Data_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var cancelColumns = new List<string>() {
                "RawName",
                "RawLocation",
                "RawWorld",
                "RawType",
                "Locations",
                "World",
                "MissingItems",
                "PossibleItems"
            };
            if (cancelColumns.Contains(e.Column.Header))
            {
                e.Cancel = true;
                return;
            }
            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(FontSizeProperty, FontSizeSlider.Value));
            if (e.Column.Header.Equals("MissingItemsString"))
            {
                e.Column.Header = "Missing Items";
                
                if (Properties.Settings.Default.MissingItemColor == "Highlight")
                {
                    var highlight = System.Drawing.SystemColors.Highlight;
                    cellStyle.Setters.Add(new Setter(ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(highlight.R, highlight.G, highlight.B))));
                }
            }
            else if (e.Column.Header.Equals("PossibleItemsString"))
            {
                e.Column.Header = "Possible Items";

                if (!Properties.Settings.Default.ShowPossibleItems)
                {
                    e.Cancel = true;
                    return;
                }
            }
            e.Column.CellStyle = cellStyle;
            e.Column.Header = Loc.T(e.Column.Header.ToString());
        }

        private void CharacterControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //if (CharacterControl.SelectedIndex == -1 && listCharacters.Count > 0) return;
            if (CharacterControl.Items.Count > 0 && CharacterControl.SelectedIndex > -1)
            {
                checkAdventureTab();
                applyFilter();
                //txtMissingItems.Text = string.Join("\n", Save.Characters[CharacterControl.SelectedIndex].GetMissingItems());

                foreach (TreeViewItem item in treeMissingItems.Items)
                {
                    item.Items.Clear();
                }
                foreach (RemnantItem rItem in Save.Characters[CharacterControl.SelectedIndex].GetMissingItems())
                {
                    var item = new TreeViewItem();
                    item.Header = rItem.Name;
                    if (!rItem.ItemNotes.Equals("")) item.ToolTip = rItem.ItemNotes;
                    item.ContextMenu = treeMissingItems.Resources["ItemContext"] as System.Windows.Controls.ContextMenu;
                    item.Tag = rItem;
                    TreeViewItem modeNode = ((TreeViewItem)treeMissingItems.Items[(int)rItem.ItemMode]);
                    TreeViewItem? itemTypeNode = null;
                    foreach (TreeViewItem typeNode in modeNode.Items)
                    {
                        if (typeNode.Tag.ToString().Equals($"{modeNode.Tag}{rItem.RawType}"))
                        {
                            itemTypeNode = typeNode;
                            break;
                        }
                    }
                    if (itemTypeNode == null)
                    {
                        itemTypeNode = new();
                        itemTypeNode.Header = rItem.Type;
                        itemTypeNode.IsExpanded = true;
                        itemTypeNode.ContextMenu = treeMissingItems.Resources["ItemGroupContext"] as System.Windows.Controls.ContextMenu;
                        itemTypeNode.Tag = $"{modeNode.Tag}{rItem.RawType}";
                        modeNode.Items.Add(itemTypeNode);
                    }
                    itemTypeNode.Items.Add(item);
                }
                foreach (TreeViewItem categoryNode in treeMissingItems.Items)
                {
                    categoryNode.Visibility = categoryNode.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                    categoryNode.Items.SortDescriptions.Add(new("Header", System.ComponentModel.ListSortDirection.Ascending));
                    foreach (TreeViewItem typeNode in categoryNode.Items)
                    {
                        typeNode.Items.SortDescriptions.Add(new("Header", System.ComponentModel.ListSortDirection.Ascending));
                    }
                }
            }
        }

        private void checkAdventureTab()
        {
            Dispatcher.Invoke(() => {
                if (Save.Characters[CharacterControl.SelectedIndex].AdventureEvents.Count > 0)
                {
                    tabAdventure.IsEnabled = true;
                }
                else
                {
                    tabAdventure.IsEnabled = false;
                    if (tabAnalyzer.SelectedIndex == 1)
                    {
                        tabAnalyzer.SelectedIndex = 0;
                    }
                }
            });
        }

        private void reloadEventGrids()
        {
            var tempData = filteredCampaign;
            CampaignData.ItemsSource = null;
            CampaignData.ItemsSource = tempData;

            tempData = filteredAdventure;
            AdventureData.ItemsSource = null;
            AdventureData.ItemsSource = tempData;
        }

        private string GetTreeItem(TreeViewItem item)
        {
            if (item == null)
            {
                return "";
            }
            if (item.Tag.GetType().ToString() == "RemnantSaveGuardian.RemnantItem") return item.Header.ToString();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(item.Header.ToString() + ":");
            foreach (TreeViewItem i in item.Items)
            {
                sb.AppendLine("\t- " + GetTreeItem(i));
            }
            return sb.ToString();
        }

        private void CopyItem_Click(object sender, RoutedEventArgs e)
        {
            if (treeMissingItems.SelectedItem == null)
            {
                return;
            }
            Clipboard.SetDataObject(GetTreeItem((TreeViewItem)treeMissingItems.SelectedItem));
        }

        private void SearchItem_Click(object sender, RoutedEventArgs e)
        {
            var treeItem = (TreeViewItem)treeMissingItems.SelectedItem;
            var item = (RemnantItem)treeItem.Tag;

            var itemname = treeItem?.Header.ToString();
            if (!WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.Culture.ToString().StartsWith("en"))
            {
                itemname = item.RawName;
                var setFound = false;
                if (item.RawType == "Armor")
                {
                    var armorMatch = Regex.Match(itemname, @"\w+_(?<armorPart>(?:Head|Body|Gloves|Legs))_\w+");
                    if (armorMatch.Success)
                    {
                        itemname = itemname.Replace($"{armorMatch.Groups["armorPart"].Value}_", "");
                        setFound = true;
                    }
                }
                itemname = Loc.GameT(itemname, new() { { "locale", "en-US" } });
                if (setFound)
                {
                    itemname += " (";
                }
            } 

            if (item.RawType == "Armor")
            {
                itemname = itemname?.Substring(0, itemname.IndexOf("(")) + "Set";
            }
            Process.Start("explorer.exe", $"https://remnant2.wiki.fextralife.com/{itemname}");
        }

        private void treeMissingItems_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var item = (TreeViewItem)e.Source;
            menuMissingItemOpenWiki.Visibility = item.Items.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void treeMissingItems_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var item = e.Source as TreeViewItem;
            if (item != null) { item.IsSelected = true; }
        }

        private void WorldAnalyzerFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            applyFilter();
        }
        private bool eventPassesFilter(RemnantWorldEvent e)
        {
            var filter = WorldAnalyzerFilter.Text.ToLower();
            if (filter.Length == 0)
            {
                return true;
            }
            if (e.MissingItemsString.ToLower().Contains(filter))
            {
                return true;
            }
            if (Properties.Settings.Default.ShowPossibleItems && e.PossibleItemsString.ToLower().Contains(filter))
            {
                return true;
            }
            if (e.Name.ToLower().Contains(filter))
            {
                return true;
            }
            return false;
        }
        private void applyFilter()
        {
            if (CharacterControl.Items.Count == 0 || CharacterControl.SelectedIndex == -1)
            {
                return;
            }
            var character = Save.Characters[CharacterControl.SelectedIndex];
            if (character == null)
            {
                return;
            }
            filteredCampaign.Clear();
            filteredCampaign.AddRange(character.CampaignEvents.FindAll(e => eventPassesFilter(e)));
            filteredAdventure.Clear();
            filteredAdventure.AddRange(character.AdventureEvents.FindAll(e => eventPassesFilter(e)));
            reloadEventGrids();
        }
    }
}
