using Remnant2SaveAnalyzer.locales;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using lib.remnant2.analyzer.Model;
using Wpf.Ui.Common.Interfaces;
using System.Diagnostics.CodeAnalysis;
using lib.remnant2.analyzer;
using Clipboard = System.Windows.Clipboard;
using Remnant2SaveAnalyzer.Logging;

namespace Remnant2SaveAnalyzer.Views.Pages
{
    /// <summary>
    /// Interaction logic for WorldAnalyzerPage.xaml
    /// </summary>
    public partial class WorldAnalyzerPage : INavigableView<ViewModels.WorldAnalyzerViewModel>, INotifyPropertyChanged
    {
        public WorldAnalyzerPage(ViewModels.WorldAnalyzerViewModel viewModel) : this(viewModel, null)
        {

        }

        public ViewModels.WorldAnalyzerViewModel ViewModel
        {
            get;
        }
        private RemnantSave _save;
        private readonly List<WorldAnalyzerGridData> _filteredCampaign;
        private readonly List<WorldAnalyzerGridData> _filteredAdventure;
        private ListViewItem? _menuSrcItem;

        public WorldAnalyzerPage(ViewModels.WorldAnalyzerViewModel viewModel, string? pathToSaveFiles = null)
        {
            ViewModel = viewModel;

            InitializeComponent();
            EventTransfer.Event += ChangeGridVisibility;

            pathToSaveFiles ??= Properties.Settings.Default.SaveFolder;

            Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;

            _save = new(pathToSaveFiles);
            if (pathToSaveFiles == Properties.Settings.Default.SaveFolder)
            {
                SaveWatcher.SaveUpdated += (_, _) =>
                {
                    _save.UpdateCharacters();
                    Dispatcher.Invoke(() =>
                    {
                        int selectedIndex = CharacterControl.SelectedIndex;
                        ApplyFilter();
                        CharacterControl.Items.Refresh();
                        if (selectedIndex >= CharacterControl.Items.Count)
                        {
                            selectedIndex = 0;
                        }

                        CharacterControl.SelectedIndex = selectedIndex;
                        //CharacterControl_SelectionChanged(null, null);
                    });
                };
                BackupsPage.BackupSaveRestored += BackupsPage_BackupSaveRestored;
            }

            _filteredCampaign = [];
            _filteredAdventure = [];
            if (_save.Dataset == null) return;

            Debug.Assert(_save.Dataset != null, "_save.Dataset != null");
            CharacterControl.ItemsSource = _save.Dataset.Characters;

            //FontSizeSlider.Value = AdventureData.FontSize;
            //FontSizeSlider.Minimum = 2.0;
            //FontSizeSlider.Maximum = AdventureData.FontSize * 2;
            FontSizeSlider.Value = Properties.Settings.Default.AnalyzerFontSize;
            FontSizeSlider.ValueChanged += FontSizeSlider_ValueChanged;

            CampaignData.ItemsSource = _filteredCampaign;
            AdventureData.ItemsSource = _filteredAdventure;

            Task task = new(FirstLoad);
            task.Start();
        }

        private void ChangeGridVisibility(object? sender, EventTransfer.MessageArgs message)
        {
            OptionGrid.Visibility = (Visibility)message.Message;
            if (message.Message is Visibility.Visible)
            {
                tabGrid.Margin = new Thickness(23, 0, 23, 23);
                tabCampaign.Visibility = Visibility.Visible;
                tabAdventure.Visibility = Visibility.Visible;
                tabMissing.Visibility = Visibility.Visible;
            }
            else if (message.Message is Visibility.Collapsed)
            {
                tabGrid.Margin = new Thickness(4, 0, 4, 4);
                tabCampaign.Visibility = Visibility.Collapsed;
                tabAdventure.Visibility = Visibility.Collapsed;
                tabMissing.Visibility = Visibility.Collapsed;
            }
        }

        private void FirstLoad()
        {
            System.Threading.Thread.Sleep(500); //Wait for UI render first
            _save.UpdateCharacters();
            Dispatcher.Invoke(() => { 
                CharacterControl.SelectedIndex = 0;
                CheckAdventureTab();
                progressRing.Visibility = Visibility.Collapsed;
            });
        }

        private void BackupsPage_BackupSaveRestored(object? sender, EventArgs e)
        {
            _save.UpdateCharacters();
            Dispatcher.Invoke(() =>
            {
                int selectedIndex = CharacterControl.SelectedIndex;
                CharacterControl.Items.Refresh();
                if (selectedIndex >= CharacterControl.Items.Count)
                {
                    selectedIndex = 0;
                }

                CharacterControl.SelectedIndex = selectedIndex;
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Properties.Settings.Default.AnalyzerFontSize = (int)Math.Round(FontSizeSlider.Value);
            ReloadEventGrids();
        }

        private void GameType_CollapsedExpanded(object sender, PropertyChangedEventArgs e)
        {
            TreeListClass item = (TreeListClass)sender;
            Properties.Settings.Default[$"{item.Tag}_Expanded"] = item.IsExpanded;
        }

        private void SavePlaintextButton_Click(object sender, RoutedEventArgs e)
        {

            if (!Properties.Settings.Default.ExportCopy && 
                !Properties.Settings.Default.ExportDecoded &&
                !Properties.Settings.Default.ExportJson)
            {
                Notifications.Error(Loc.T("Please select at least one export options in profile"));
                return;
            }

            try
            {
                System.Windows.Forms.FolderBrowserDialog openFolderDialog = new()
                {
                    Description = Loc.T("Export save files as plaintext"),
                    UseDescriptionForTitle = true
                };
                System.Windows.Forms.DialogResult result = openFolderDialog.ShowDialog();
                if (result != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                if (openFolderDialog.SelectedPath == Properties.Settings.Default.SaveFolder || openFolderDialog.SelectedPath == _save.SaveFolderPath)
                {
                    Notifications.Error(Loc.T("export_save_invalid_folder_error"));
                    return;
                }
                Exporter.Export(openFolderDialog.SelectedPath, _save.SaveFolderPath, Properties.Settings.Default.ExportCopy, Properties.Settings.Default.ExportDecoded, Properties.Settings.Default.ExportJson);
                Notifications.Success(Loc.T($"Exported save files successfully to {openFolderDialog.SelectedPath}"));
            } catch (Exception ex)
            {
                Notifications.Error(Loc.T("Error exporting save files: {errorMessage}", new() { { "errorMessage", ex.Message } }));
            }
        }

        private void Default_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ShowPossibleItems" 
                || e.PropertyName == "MissingItemColor"
                || e.PropertyName == "ShowWard13"
                || e.PropertyName == "ShowShowLabyrinth"
                || e.PropertyName == "ShowRootEarth"
                || e.PropertyName == "ShowConnections"
                || e.PropertyName == "ShowWorldStones"
                || e.PropertyName == "ShowTomes"
                || e.PropertyName == "ShowSimulacrums"
                || e.PropertyName == "ShowCoopItems"
                || e.PropertyName == "ShowDlc1"
                || e.PropertyName == "ShowDlc2"
                || e.PropertyName == "ShowItemsWithNoPrerequisites"
                || e.PropertyName == "ShowLootedItems"
                )
            {
                Dispatcher.Invoke(() =>
                {
                    ReloadEventGrids();
                    CharacterControl_SelectionChanged(null, null);
                });
            }
            if (e.PropertyName == "SaveFolder")
            {
                Task.Run(() =>
                {
                    _save = new(Properties.Settings.Default.SaveFolder);
                    _save.UpdateCharacters();
                    Dispatcher.Invoke(() =>
                    {
                        ReloadPage();
                        CheckAdventureTab();
                    });
                });
            }

            if (e.PropertyName == "Language")
            {
                Dispatcher.Invoke(() =>
                {
                    ReloadEventGrids();
                    CharacterControl_SelectionChanged(null, null);
                });
            }
        }
        
        private void ListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            MouseWheelEventArgs eBack = new(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = MouseWheelEvent
            };

            ListView? src = e.Source as ListView;
            UIElement? ui = src?.Parent as UIElement;
            ui?.RaiseEvent(eBack);
        }
        private void ListViewItem_Selected(object sender, RoutedEventArgs e)
        {
            ListViewItem? item = (ListViewItem)e.Source;
            if (_menuSrcItem != null && !item.Equals(_menuSrcItem))
            {
                _menuSrcItem.IsSelected = false;
            }
            _menuSrcItem = item;
        }
        private DataGridTemplateColumn GeneratingColumn(string strHeader, string strProperty, string strStyle, string strSortBy)
        {
            FrameworkElementFactory stackPanelFactory = new(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            FrameworkElementFactory listView = new(typeof(ListView));

            Style style = (Style)Resources[strStyle]!;
            listView.SetValue(StyleProperty, style);
            listView.SetValue(ContextMenuProperty, Resources["CommonContextMenu"]);
            listView.SetBinding(ItemsControl.ItemsSourceProperty,
                new Binding
                {
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    Path = new PropertyPath(strProperty),
                    Mode = BindingMode.OneWay
                }
                );
            listView.AddHandler(MouseWheelEvent, new MouseWheelEventHandler(ListView_PreviewMouseWheel), true);

            stackPanelFactory.AppendChild(listView);

            DataTemplate dataTemplate = new()
            {
                VisualTree = stackPanelFactory
            };
            DataGridTemplateColumn templateColumn = new()
            {
                Header = strHeader,
                CanUserSort = true,
                SortMemberPath = strSortBy,
                CellTemplate = dataTemplate
            };
            return templateColumn;
        }

        private Brush? _missingItemsTextColor;
        public Brush? MissingItemsTextColor
        {
            get => _missingItemsTextColor;
            set { _missingItemsTextColor = value; OnPropertyChanged("missingItemsTextColor"); }
        }

        private void Data_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            List<string> allowColumns = [
                "Location",
                "Type",
                "Name",
                "MissingItems",
            ];
            if (Properties.Settings.Default.ShowPossibleItems)
            {
                allowColumns.Add("PossibleItems");
            }
            if (!allowColumns.Contains(e.Column.Header))
            {
                e.Cancel = true;
                return;
            }

            //Replace the MissingItems column with a custom template column.
            if (e.PropertyName == "MissingItems")
            {
                e.Column = GeneratingColumn("Missing Items", "MissingItems", "lvMissingItemsStyle", "MissingItemsString");
                if (Properties.Settings.Default.MissingItemColor == "Highlight")
                {
                    SolidColorBrush highlight = Brushes.RoyalBlue;
                    MissingItemsTextColor = highlight;
                } else {
                    Brush brush = (Brush)FindResource("TextFillColorPrimaryBrush");
                    MissingItemsTextColor = brush;
                }
            } else if (e.PropertyName == "PossibleItems") {
                e.Column = GeneratingColumn("Possible Items", "PossibleItems", "lvPossibleItemsStyle", "PossibleItemsString");
            }

            string? header = e.Column.Header.ToString();
            Debug.Assert(header != null, nameof(header) + " != null");
            e.Column.Header = Loc.T(header);
        }

        private static readonly string[] ModeTags = ["treeMissingNormal", "treeMissingHardcore", "treeMissingSurvival"];
        readonly List<TreeListClass> _itemModeNode = [];
        private void CharacterControl_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
        {
            //if (CharacterControl.SelectedIndex == -1 && listCharacters.Count > 0) return;
            if (CharacterControl.Items.Count > 0 && CharacterControl.SelectedIndex > -1)
            {
                CheckAdventureTab();
                ApplyFilter();
                //txtMissingItems.Text = string.Join("\n", Save.Characters[CharacterControl.SelectedIndex].GetMissingItems());

                _itemModeNode.Clear();
                List<TreeListClass>[] itemNode = [[], [], []];
                List<TreeListClass>?[] itemChild = new List<TreeListClass>[20];
                string[] modes = [Strings.Normal, Strings.Hardcore, Strings.Survival];
                for (int i = 0;i <= 2; i++)
                {
                    TreeListClass item = new() { Name = modes[i], ChildNode = itemNode[i], Tag = ModeTags[i], IsExpanded = (bool)Properties.Settings.Default[$"{ModeTags[i]}_Expanded"] };
                    item.Expanded += GameType_CollapsedExpanded;
                    _itemModeNode.Add(item);
                }
                int idx = -1;
                string typeNodeTag = "";

                Debug.Assert(_save.Dataset != null, "_save.Dataset != null");
                List<Dictionary<string, string>> missingItems = _save.Dataset.Characters[CharacterControl.SelectedIndex].Profile.MissingItems;
                if (!Properties.Settings.Default.ShowCoopItems)
                {
                    missingItems = missingItems.Where(x => x.ContainsKey("Coop") && x["Coop"] == "True").ToList();
                }
                
                missingItems = FilterAllDlcItems(missingItems, x=>x);

                missingItems.Sort(new SortCompare());
                foreach (Dictionary<string, string> rItem in missingItems)
                {
                    int itemMode = rItem.TryGetValue("Hardcore", out string? hardcore) && hardcore == "True" ? 1 : 0;
                    string modeNode = "treeMissing" + (rItem.TryGetValue("Hardcore", out string? treeMissing) && treeMissing == "True" ? "Hardcore" : "Normal");

                    string itemType = Capitalize().Replace(rItem["Type"], m => m.Value.ToUpper());

                    if (!typeNodeTag.Equals($"{modeNode}{itemType}"))
                    {
                        typeNodeTag = $"{modeNode}{itemType}";
                        idx++;
                        itemChild[idx] = [];
                        bool isExpanded = true;
                        try
                        {
                            isExpanded = (bool)Properties.Settings.Default[$"{typeNodeTag}_Expanded"];
                        }
                        catch (Exception ex)
                        {
                            Notifications.Warn($"Not found properties: {typeNodeTag}_Expand; {ex}");
                        }
                        TreeListClass item = new() { Name = itemType, ChildNode = itemChild[idx] ?? [], Tag = typeNodeTag, IsExpanded = isExpanded };
                        item.Expanded += GameType_CollapsedExpanded;
                        itemNode[itemMode].Add(item);
                    }

                    LootItem li = new() { Item = rItem };
                    List<TreeListClass>? treeItem = itemChild[idx];
                    Debug.Assert(treeItem != null, nameof(treeItem) + " != null");
                    treeItem.Add(new TreeListClass { Name = li.Name, Notes = rItem["Note"], Tag = rItem });
                }

                treeMissingItems.ItemsSource = null;
                treeMissingItems.ItemsSource = _itemModeNode;

                foreach (TreeListClass modeNode in _itemModeNode)
                {
                    modeNode.Visibility = modeNode.ChildNode.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                }
                foreach (List<TreeListClass> categoryNode in itemNode)
                {
                    foreach (TreeListClass category in categoryNode)
                    {
                        category.Visibility = category.ChildNode.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                    }
                    categoryNode.Sort();
                }
                foreach (List<TreeListClass>? typeNode in itemChild)
                {
                    typeNode?.Sort();
                }
            }
        }

        private static List<T> FilterAllDlcItems<T>(IEnumerable<T> missingItems, Func<T, Dictionary<string, string>> getDbItem)
        {
            for (int i = 0; i < 2; i++)
            {
                missingItems = FilterDlcItems(missingItems, getDbItem, i);
            }
            
            return missingItems.ToList();
        }

        private static IEnumerable<T> FilterDlcItems<T>(IEnumerable<T> items, Func<T, Dictionary<string, string>> getDbItem, int dlcId)
        {
            var dlcs = new[]
            {
                new { DlcId = 0, ProfileIdSubstring = "/World_DLC1/", WorldValue = "World_DLC1", ControlValue = Properties.Settings.Default.ShowDlc1 },
                new { DlcId = 1, ProfileIdSubstring = "/World_DLC2/", WorldValue = "World_DLC2", ControlValue = Properties.Settings.Default.ShowDlc2 }

            };

            var dlc = dlcs.Single(x => x.DlcId == dlcId);

            if (!dlc.ControlValue)
            {
                items = items.Where(y =>
                {
                    Dictionary<string, string> x = getDbItem(y);
                    return ((!x.ContainsKey("DropType") || x["DropType"] == "Event" || x["DropType"] == "Location") && !x["ProfileId"].Contains(dlc.ProfileIdSubstring)) ||
                           ((!x.ContainsKey("DropType") || x["DropType"] == "Vendor") && x["World"] != dlc.WorldValue );
                });
            }

            return items;
        }

        private void CheckAdventureTab()
        {
            Dispatcher.Invoke(() =>
            {
                Debug.Assert(_save.Dataset != null, "_save.Dataset != null");
                if (CharacterControl.SelectedIndex > -1 && _save.Dataset.Characters[CharacterControl.SelectedIndex].Save.Adventure != null)
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
        private void ReloadPage()
        {
            CharacterControl.ItemsSource = null;
            Debug.Assert(_save.Dataset != null, "_save.Dataset != null");
            CharacterControl.ItemsSource = _save.Dataset.Characters;
            CharacterControl.SelectedIndex = 0;
            CharacterControl.Items.Refresh();
        }
        private void ReloadEventGrids()
        {
            List<WorldAnalyzerGridData> tempDataC = _filteredCampaign;
            CampaignData.ItemsSource = null;
            CampaignData.ItemsSource = tempDataC;

            List<WorldAnalyzerGridData> tempDataA = _filteredAdventure;
            AdventureData.ItemsSource = null;
            AdventureData.ItemsSource = tempDataA;
        }

        private static string GetTreeListItem(TreeListClass item)
        {
            if (item.Tag.GetType().ToString() == "System.Collections.Generic.Dictionary`2[System.String,System.String]") return item.Name ?? "";
            StringBuilder sb = new();
            sb.AppendLine(item.Name + ":");
            foreach (TreeListClass i in item.ChildNode)
            {
                sb.AppendLine("\t- " + GetTreeListItem(i));
            }
            return sb.ToString();
        }

        private void CommonCopyItem_Click(object sender, RoutedEventArgs e)
        {
            if (_menuSrcItem == null) { return; }
            if (_menuSrcItem.Content is LootItem item)
            {
                Clipboard.SetDataObject(item.Name);
            }
        }

        private void CommonSearchItem_Click(object sender, RoutedEventArgs e)
        {
            if (_menuSrcItem == null) { return; }

            if (_menuSrcItem.Content is not LootItem lstItem) { return; }
            SearchItem(lstItem);
        }

        private void CopyItem_Click(object sender, RoutedEventArgs e)
        {
            if (treeMissingItems.SelectedItem == null)
            {
                return;
            }
            Clipboard.SetDataObject(GetTreeListItem((TreeListClass)treeMissingItems.SelectedItem));
        }
        private void SearchItem_Click(object sender, RoutedEventArgs e)
        {
            TreeListClass? treeItem = (TreeListClass)treeMissingItems.SelectedItem;
            LootItem item = new() { Item = (Dictionary<string, string>)treeItem.Tag };
            SearchItem(item);
        }
        private static void SearchItem(LootItem item)
        {
            string wikiQuery = Properties.Settings.Default.Wiki == "remwiki" ? 
                $"https://remnant.wiki/{item.Name}" : 
                $"https://remnant2.wiki.fextralife.com/{item.Name}";
            Process.Start("explorer.exe", wikiQuery);
        }
        private void ExpandAllItem_Click(object sender, RoutedEventArgs e)
        {
            CollapseExpandAllItems(_itemModeNode, true);
        }
        private void CollapseAllItem_Click(object sender, RoutedEventArgs e)
        {
            CollapseExpandAllItems(_itemModeNode, false);
        }
        private static void CollapseExpandAllItems(List<TreeListClass> lstItems, bool bExpand)
        {
            foreach (TreeListClass item in lstItems)
            {
                item.IsExpanded = bExpand;
                List<TreeListClass> child = item.ChildNode;
                if (child.Count > 0)
                {
                    List<TreeListClass> node = child[0].ChildNode;
                    if (node.Count > 0) { CollapseExpandAllItems(child, bExpand); }
                }
            }
        }
        private void TreeMissingItems_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            TreeListClass? item = (TreeListClass)treeMissingItems.SelectedItem;
            if (item != null)
            {
                menuMissingItemOpenWiki.Visibility = item.Tag.GetType().ToString() != "System.Collections.Generic.Dictionary`2[System.String,System.String]" ? Visibility.Collapsed : Visibility.Visible;
                menuMissingItemCopy.Visibility = Visibility.Visible;
            } else {
                menuMissingItemOpenWiki.Visibility = Visibility.Collapsed;
                menuMissingItemCopy.Visibility = Visibility.Collapsed;
            }
        }

        private void TreeMissingItems_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                TreeListClass? node = (TreeListClass)item.Header;
                if (node != null) {
                    node.IsSelected = true;
                    e.Handled = true;
                }
            }
        }

        private void WorldAnalyzerFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }
        private bool EventPassesFilter(WorldAnalyzerGridData e)
        {
            string filter = WorldAnalyzerFilter.Text.ToLower();
            if (filter.Length == 0)
            {
                return true;
            }
            if (e.MissingItemsString.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }
            if (Properties.Settings.Default.ShowPossibleItems && e.PossibleItemsString.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }
            if (e.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }
            return false;
        }
        private void ApplyFilter()
        {
            if (CharacterControl.Items.Count == 0 || CharacterControl.SelectedIndex == -1)
            {
                return;
            }

            Debug.Assert(_save.Dataset != null, "_save.Dataset != null");
            Character character = _save.Dataset.Characters[CharacterControl.SelectedIndex];
            _filteredCampaign.Clear();
            _filteredCampaign.AddRange(FilterGridData(character.Profile, character.Save.Campaign));
            _filteredAdventure.Clear();
            _filteredAdventure.AddRange(FilterGridData(character.Profile, character.Save.Adventure));
            ReloadEventGrids();
        }
        private List<WorldAnalyzerGridData> FilterGridData(Profile profile, RolledWorld? world)
        {
            List<WorldAnalyzerGridData> result = [];
            if (world == null) return result;

            List<string> missingIds = profile.MissingItems.Select(x => x["Id"]).ToList();

            foreach (Zone zone in world.AllZones)
            {
                if (!Properties.Settings.Default.ShowWard13 && zone.Name == "Ward 13") continue;
                if (!Properties.Settings.Default.ShowShowLabyrinth && zone.Name == "Labyrinth") continue;
                if (!Properties.Settings.Default.ShowRootEarth && zone.Name == "Root Earth") continue;
                foreach (Location location in zone.Locations)
                {
                    string l = location.World == "Ward 13" ? Loc.GameT(location.World) : $"{Loc.GameT(location.World)}: {Loc.GameT(location.Name)}";

                    if (Properties.Settings.Default.ShowConnections && location.Connections is { Count: > 0 })
                    {
                        WorldAnalyzerGridData newItem = new(
                            location: l,
                            missingItems: [],
                            possibleItems: [],
                            name: string.Join('\n', location.Connections.Select(Loc.GameT)),
                            type: Loc.T("Connections")
                        );
                        if (EventPassesFilter(newItem))
                        {
                            result.Add(newItem);
                        }
                    }
                    if (Properties.Settings.Default.ShowWorldStones && location.WorldStones is { Count: > 0 })
                    {
                        WorldAnalyzerGridData newItem = new(
                            location: l,
                            missingItems: [],
                            possibleItems: [],
                            name: string.Join('\n', location.WorldStones.Select(Loc.GameT)),
                            type: Loc.T("World Stones")
                        );
                        if (EventPassesFilter(newItem))
                        {
                            result.Add(newItem);
                        }
                    }
                    if (Properties.Settings.Default.ShowTomes && location.TraitBook && (Properties.Settings.Default.ShowLootedItems || !location.TraitBookLooted))
                    {
                        //List<LocalisedLootItem> ll = new() { new LocalisedLootItem(new() { Item = new() { { "Name", Loc.GameT("TraitBook") } } }) };
                        WorldAnalyzerGridData newItem = new(
                            location: l,
                            missingItems: [],
                            possibleItems: 
                                
                                [
                                    new LocalisedLootItem(new()
                                    {
                                        Item = new() { { "Name", Loc.GameT("TraitBook") }, { "Id", "Bogus" }, {"Type", "Tome Of Knowledge"} },
                                        IsLooted = location.TraitBookLooted
                                    }, this)
                                ],
                            name: Loc.GameT("TraitBook"),
                            type: Loc.T("Item")
                        );
                        if (EventPassesFilter(newItem))
                        {
                            result.Add(newItem);
                        }

                    }
                    if (Properties.Settings.Default.ShowSimulacrums && location.Simulacrum && (Properties.Settings.Default.ShowLootedItems || !location.SimulacrumLooted))
                    {
                        WorldAnalyzerGridData newItem = new(
                            location: l,
                            missingItems: [],
                            possibleItems: 
                                [
                                    new LocalisedLootItem(new()
                                    {
                                        Item = new() { { "Name", Loc.GameT("Simulacrum") }, { "Id", "Bogus" }, {"Type", "Simulacrum"} },
                                        IsLooted = location.SimulacrumLooted,
                                    }, this)
                                ],
                            name: Loc.GameT("Simulacrum"),
                            type: Loc.T("Item")
                        );
                        if (EventPassesFilter(newItem))
                        {
                            result.Add(newItem);
                        }
                    }
                    
                    foreach (LootGroup lg in location.LootGroups)
                    {

                        List<LootItem> items = lg.Items;
                        if (!Properties.Settings.Default.ShowCoopItems)
                        {
                            items = items.Where(x => x.Item.ContainsKey("Coop") && x.Item["Coop"] == "True").ToList();
                        }
                        if (!Properties.Settings.Default.ShowLootedItems)
                        {
                            items = items.Where(x => !x.IsLooted).ToList();
                        }

                        WorldAnalyzerGridData newItem = new(
                            location: l,
                            missingItems: FilterAllDlcItems(items
                                .Where( x => !x.IsPrerequisiteMissing || Properties.Settings.Default.ShowItemsWithNoPrerequisites)
                                .Where(x => missingIds.Contains(x.Item["Id"])), 
                                x=>x.Item).Select(x => new LocalisedLootItem(x, this)).ToList(),
                            possibleItems: FilterAllDlcItems(items
                                .Where(x => !x.IsPrerequisiteMissing || Properties.Settings.Default.ShowItemsWithNoPrerequisites), 
                                x=>x.Item).Select(x => new LocalisedLootItem(x, this)).ToList(),
                            name: Loc.GameT(lg.Name ?? ""),
                            type: Loc.T(Capitalize().Replace(lg.Type, m => m.Value.ToUpper()))
                        ){Unknown = lg.UnknownMarker};
                        if (newItem.Type == "Dungeon" || newItem.Type == "Location")
                        {
                            newItem.Name = Loc.GameT(location.Name);
                        }
                        if (EventPassesFilter(newItem))
                        {
                            result.Add(newItem);
                        }
                    }
                }
            }

            result = result.Where(x=>x.Type == "Connections"
                || x.Type == "World Stones"
                || x.MissingItems.Count > 0
                || Properties.Settings.Default.ShowPossibleItems && x.PossibleItems.Count > 0
            ).ToList();

            return result;
        }

        public class LocalisedLootItem : LootItem
        {
            private readonly WorldAnalyzerPage _parent;
            [SetsRequiredMembers]
            public LocalisedLootItem(LootItem item, WorldAnalyzerPage parent)
            {
                IsLooted = item.IsLooted;
                Item = item.Item;
                _parent = parent;
            }

            public override string Name => Item["Id"] == Loc.GameT(Item["Id"]) ? base.Name : Loc.GameT(Item["Id"]);

            // ReSharper disable once UnusedMember.Global
            public Brush? LootedItemsTextColor => Properties.Settings.Default.LootedItemColor == "Dim" && IsLooted 
                ? Brushes.DarkGray 
                : (Brush)_parent.FindResource("TextFillColorPrimaryBrush");
        }

        public class SortCompare : IComparer<Dictionary<string, string>>
        {
            public int Compare(Dictionary<string, string>? x, Dictionary<string, string>? y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                string xMode = x.TryGetValue("Hardcore", out string? xHardcore) && xHardcore == "True" ? "Hardcore" : "Normal";
                string yMode = y.TryGetValue("Hardcore", out string? yHardcore) && yHardcore == "True" ? "Hardcore" : "Normal";
                if (xMode != yMode)
                {
                    return string.Compare(xMode, yMode, StringComparison.InvariantCulture);
                }
                if (x["Type"] != y["Type"])
                {
                    return string.Compare(x["Type"], y["Type"], StringComparison.InvariantCulture);
                }
                LootItem xi = new() { Item = x };
                LootItem yi = new() { Item = y };
                return string.Compare(xi.Name, yi.Name, StringComparison.InvariantCulture);
            }
        }
        public class TreeListClass : IComparable<TreeListClass>, INotifyPropertyChanged
        {
            public delegate void EventHandler(object sender, PropertyChangedEventArgs e);
            public event EventHandler? Expanded;
            public event PropertyChangedEventHandler? PropertyChanged;
            private List<TreeListClass>? _childNode;
            private object? _tag;
            private bool _isSelected;
            private bool _isExpanded;
            private Visibility _visibility;
            public string? Name { get; set; }
            public string? Notes { get; set; }
            public object Tag {
                get => _tag ?? new object();
                set => _tag = value;
            }
            public List<TreeListClass> ChildNode {
                get => _childNode ?? [];
                set => _childNode = value;
            }
            public bool IsSelected {
                get => _isSelected;
                set { _isSelected = value; OnPropertyChanged(); }
            }
            public bool IsExpanded
            {
                get => _isExpanded;
                set
                {
                    if (value != _isExpanded)
                    {
                        _isExpanded = value;
                        OnPropertyChanged();
                        PropertyChangedEventArgs ev = new(nameof(IsExpanded), _isExpanded, value);
                        Expanded?.Invoke(this, ev);
                    }
                }
            }
            public Visibility Visibility
            {
                get => _visibility;
                set { _visibility = value; OnPropertyChanged(); }
            }
            protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
            public int CompareTo(TreeListClass? other)
            {
                return string.Compare(Name, other?.Name, StringComparison.InvariantCulture);
            }
        }
        public class PropertyChangedEventArgs(string propertyName, object oldValue, object newValue) : EventArgs
        {
            // ReSharper disable UnusedMember.Global
            public string PropertyName { get; private set; } = propertyName;
            public object OldValue { get; private set; } = oldValue;
            public object NewValue { get; set; } = newValue;
            // ReSharper restore UnusedMember.Global
        }
        public class WorldAnalyzerGridData(
            string location,
            string type,
            string name,
            List<LocalisedLootItem> missingItems,
            List<LocalisedLootItem> possibleItems)
        {
            // ReSharper disable once UnusedMember.Global
            public string Location { get; set; } = location;
            public string Type { get; set; } = type;
            public string Name { get; set; } = name;
            public List<LocalisedLootItem> MissingItems { get; set; } = missingItems;
            public List<LocalisedLootItem> PossibleItems { get; set; } = possibleItems;
            public string MissingItemsString => string.Join("\n", MissingItems.Select(x => x.Name));
            public UnknownData Unknown { get; set; }
            public string PossibleItemsString => string.Join("\n", PossibleItems.Select(x => x.Name));
        }

        [GeneratedRegex(@"\b([a-z])")]
        private static partial Regex Capitalize();
    }
}
