using RemnantSaveGuardian.locales;
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

namespace RemnantSaveGuardian.Views.Pages
{
    /// <summary>
    /// Interaction logic for WorldAnalyzerPage.xaml
    /// </summary>
    public partial class WorldAnalyzerPage : INavigableView<ViewModels.WorldAnalyzerViewModel>, INotifyPropertyChanged
    {
        public ViewModels.WorldAnalyzerViewModel ViewModel
        {
            get;
        }
        private RemnantSave _save;
        private List<WorldAnalyzerGridData> _filteredCampaign;
        private List<WorldAnalyzerGridData> _filteredAdventure;
        private ListViewItem _menuSrcItem;
        public WorldAnalyzerPage(ViewModels.WorldAnalyzerViewModel viewModel, string? pathToSaveFiles = null)
        {
            ViewModel = viewModel;

            InitializeComponent();
            EventTransfer.Event += ChangeGridVisibility;

            try
            {
                if (pathToSaveFiles == null)
                {
                    pathToSaveFiles = Properties.Settings.Default.SaveFolder;
                }

                Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;

                _save = new(pathToSaveFiles);
                if (pathToSaveFiles == Properties.Settings.Default.SaveFolder)
                {
                    SaveWatcher.SaveUpdated += (_, _) => {
                        Dispatcher.Invoke(() =>
                        {
                            int selectedIndex = CharacterControl.SelectedIndex;
                            _save.UpdateCharacters();
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
                    Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;
                    BackupsPage.BackupSaveRestored += BackupsPage_BackupSaveRestored;
                }
                CharacterControl.ItemsSource = _save.Dataset.Characters;

                //FontSizeSlider.Value = AdventureData.FontSize;
                //FontSizeSlider.Minimum = 2.0;
                //FontSizeSlider.Maximum = AdventureData.FontSize * 2;
                FontSizeSlider.Value = Properties.Settings.Default.AnalyzerFontSize;
                FontSizeSlider.ValueChanged += FontSizeSlider_ValueChanged;

                _filteredCampaign = new();
                _filteredAdventure = new();
                CampaignData.ItemsSource = _filteredCampaign;
                AdventureData.ItemsSource = _filteredAdventure;

                Task task = new(FirstLoad);
                task.Start();
            } catch (Exception ex) {
                Logger.Error($"Error initializing analzyer page: {ex}");
            }
        }
        private void ChangeGridVisibility(Object sender, EventTransfer.MessageArgs message)
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
            int selectedIndex = CharacterControl.SelectedIndex;
            _save.UpdateCharacters();
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

        #region INotifiedProperty Block
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;

            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Properties.Settings.Default.AnalyzerFontSize = (int)Math.Round(FontSizeSlider.Value);
            ReloadEventGrids();
        }

        private void GameType_CollapsedExpanded(object sender, PropertyChagedEventArgs e)
        {
            TreeListClass item = (TreeListClass)sender;
            Properties.Settings.Default[$"{item.Tag}_Expanded"] = item.IsExpanded;
        }

        private void SavePlaintextButton_Click(object sender, RoutedEventArgs e)
        {
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
                    Logger.Error(Loc.T("export_save_invalid_folder_error"));
                    return;
                }
                Analyzer.Export(openFolderDialog.SelectedPath, _save.SaveFolderPath, Properties.Settings.Default.ExportCopy, Properties.Settings.Default.ExportDecoded, Properties.Settings.Default.ExportJson);
                Logger.Success(Loc.T($"Exported save files successfully to {openFolderDialog.SelectedPath}"));
            } catch (Exception ex)
            {
                Logger.Error(Loc.T("Error exporting save files: {errorMessage}", new() { { "errorMessage", ex.Message } }));
            }
        }

        private void Default_PropertyChanged(object? sender, PropertyChangedEventArgs e)
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
                Dispatcher.Invoke(() => {
                    _save = new(Properties.Settings.Default.SaveFolder);
                    _save.UpdateCharacters();
                    ReloadPage();
                    CheckAdventureTab();
                });
            }
            if (e.PropertyName == "ShowCoopItems")
            {
                Dispatcher.Invoke(() =>
                {
                    _save.UpdateCharacters();
                    ReloadEventGrids();
                    CharacterControl_SelectionChanged(null, null);
                });
            }

            if(e.PropertyName == "Language")
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
            UIElement? ui = src.Parent as UIElement;
            ui.RaiseEvent(eBack);
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

            Style style = (Style)Resources[strStyle];
            listView.SetValue(StyleProperty, style);
            listView.SetValue(ContextMenuProperty, Resources["CommonContextMenu"]);
            listView.SetBinding(ItemsControl.ItemsSourceProperty,
                new Binding()
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
        #region missingItemsTextColor
        private Brush _missingItemsTextColor;
        public Brush MissingItemsTextColor
        {
            get { return _missingItemsTextColor; }
            set { _missingItemsTextColor = value; OnPropertyChanged("missingItemsTextColor"); }
        }
        #endregion
        private void Data_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            List<string> allowColumns = new() {
                "Location",
                "Type",
                "Name",
                "MissingItems",
            };
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

            e.Column.Header = Loc.T(e.Column.Header.ToString());
        }

        private static readonly string[] _modeTags = { "treeMissingNormal", "treeMissingHardcore", "treeMissingSurvival" };
        readonly List<TreeListClass> _itemModeNode = new();
        private void CharacterControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //if (CharacterControl.SelectedIndex == -1 && listCharacters.Count > 0) return;
            if (CharacterControl.Items.Count > 0 && CharacterControl.SelectedIndex > -1)
            {
                CheckAdventureTab();
                ApplyFilter();
                //txtMissingItems.Text = string.Join("\n", Save.Characters[CharacterControl.SelectedIndex].GetMissingItems());

                _itemModeNode.Clear();
                List<TreeListClass>[] itemNode = new List<TreeListClass>[3] { new(), new(), new() };
                List<TreeListClass>[] itemChild = new List<TreeListClass>[20];
                string[] modes = { Strings.Normal, Strings.Hardcore, Strings.Survival };
                for (int i = 0;i <= 2; i++)
                {
                    TreeListClass item = new() { Name = modes[i], Childnode = itemNode[i], Tag = _modeTags[i], IsExpanded = (bool)Properties.Settings.Default[$"{_modeTags[i]}_Expanded"] };
                    item.Expanded += GameType_CollapsedExpanded;
                    _itemModeNode.Add(item);
                }
                int idx = -1;
                string typeNodeTag = "";

                List<Dictionary<string, string>> missingItems = _save.Dataset.Characters[CharacterControl.SelectedIndex].Profile.MissingItems;
                if (!Properties.Settings.Default.ShowCoopItems)
                {
                    missingItems = missingItems.Where(x => x.ContainsKey("Coop") && x["Coop"] == "True").ToList();
                }
                missingItems.Sort(new SortCompare());
                foreach (Dictionary<string, string> rItem in missingItems)
                {
                    int itemMode = rItem.ContainsKey("Hardcore") && rItem["Hardcore"] == "True" ? 1 : 0;
                    string modeNode = "treeMissing" + (rItem.ContainsKey("Hardcore") && rItem["Hardcore"] == "True" ? "Hardcore" : "Normal");

                    string itemType = Regex.Replace(rItem["Type"], @"\b([a-z])", m => m.Value.ToUpper());

                    if (!typeNodeTag.Equals($"{modeNode}{itemType}"))
                    {
                        typeNodeTag = $"{modeNode}{itemType}";
                        idx++;
                        itemChild[idx] = new List<TreeListClass>();
                        bool isExpanded = true;
                        try
                        {
                            isExpanded = (bool)Properties.Settings.Default[$"{typeNodeTag}_Expanded"];
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"Not found properties: {typeNodeTag}_Expand");
                        }
                        TreeListClass item = new() { Name = itemType, Childnode = itemChild[idx], Tag = typeNodeTag, IsExpanded = isExpanded };
                        item.Expanded += GameType_CollapsedExpanded;
                        itemNode[itemMode].Add(item);
                    }

                    LootItem li = new() { Item = rItem };
                    itemChild[idx].Add(new TreeListClass() { Name = li.Name, Notes = rItem["Note"], Tag = rItem });
                }

                treeMissingItems.ItemsSource = null;
                treeMissingItems.ItemsSource = _itemModeNode;

                foreach (TreeListClass modeNode in _itemModeNode)
                {
                    if (modeNode != null)
                    {
                        modeNode.Visibility = (modeNode.Childnode.Count == 0 ? Visibility.Collapsed : Visibility.Visible);
                    }
                }
                foreach (List<TreeListClass> categoryNode in itemNode)
                {
                    if (categoryNode != null)
                    {
                        foreach (TreeListClass category in categoryNode)
                        {
                            category.Visibility = (category.Childnode.Count == 0 ? Visibility.Collapsed : Visibility.Visible);
                        }
                        categoryNode.Sort();
                    }
                }
                foreach (List<TreeListClass> typeNode in itemChild) {
                    if (typeNode != null) {
                        typeNode.Sort();
                    }
                }
            }
        }

        private void CheckAdventureTab()
        {
            Dispatcher.Invoke(() => {
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
            CharacterControl.ItemsSource = _save.Dataset.Characters;
            if (_filteredCampaign == null) _filteredCampaign = new();
            if (_filteredAdventure == null) _filteredAdventure = new();
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
            if (item == null)
            {
                return "";
            }
            if (item.Tag.GetType().ToString() == "System.Collections.Generic.Dictionary`2[System.String,System.String]") return item.Name;
            StringBuilder sb = new();
            sb.AppendLine(item.Name + ":");
            foreach (TreeListClass i in item.Childnode)
            {
                sb.AppendLine("\t- " + GetTreeListItem(i));
            }
            return sb.ToString();
        }

        private void CommonCopyItem_Click(object sender, RoutedEventArgs e)
        {
            if (_menuSrcItem == null) { return; }
            LootItem? item = _menuSrcItem.Content as LootItem;
            Clipboard.SetDataObject(item.Name);
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
            string itemname = item.Name;
            Process.Start("explorer.exe", $"https://remnant2.wiki.fextralife.com/{itemname}");
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
                List<TreeListClass>? child = item.Childnode;
                if (child != null && child.Count > 0)
                {
                    List<TreeListClass>? node = child[0].Childnode;
                    if (node != null && node.Count > 0) { CollapseExpandAllItems(child, bExpand); }
                }
            }
        }
        private void treeMissingItems_ContextMenuOpening(object sender, ContextMenuEventArgs e)
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

        private void treeMissingItems_MouseDown(object sender, MouseButtonEventArgs e)
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
        private void ApplyFilter()
        {
            if (CharacterControl.Items.Count == 0 || CharacterControl.SelectedIndex == -1)
            {
                return;
            }
            Character? character = _save.Dataset.Characters[CharacterControl.SelectedIndex];
            if (character == null)
            {
                return;
            }
            _filteredCampaign.Clear();
            _filteredCampaign.AddRange(FilterGridData(character.Profile, character.Save.Campaign));
            _filteredAdventure.Clear();
            _filteredAdventure.AddRange(FilterGridData(character.Profile, character.Save.Adventure));
            ReloadEventGrids();
        }
        private List<WorldAnalyzerGridData> FilterGridData(Profile profile, RolledWorld? world)
        {
            List<WorldAnalyzerGridData> result = new();
            if (world == null) return result;

            List<string> missingIds = profile.MissingItems.Select(x => x["Id"]).ToList();

            foreach (Zone zone in world.AllZones)
            {
                if (!Properties.Settings.Default.ShowWard13 && zone.Name == "Ward 13") continue;
                if (!Properties.Settings.Default.ShowShowLabyrinth && zone.Name == "Labyrinth") continue;
                if (!Properties.Settings.Default.ShowRootEarth && zone.Name == "Root Earth") continue;
                foreach (Location location in zone.Locations)
                {
                    string l = location.World == "Ward 13" ? location.World : $"{location.World}: {location.Name}";

                    if (Properties.Settings.Default.ShowConnections && location.Connections is { Count: > 0 })
                    {
                        WorldAnalyzerGridData newItem = new()
                        {
                            Location = Loc.GameT(l),
                            MissingItems = new(),
                            PossibleItems = new(),
                            Name = string.Join('\n', location.Connections.Select(Loc.GameT)),
                            Type = Loc.GameT("Connections")
                        };
                        if (EventPassesFilter(newItem))
                        {
                            result.Add(newItem);
                        }
                    }
                    if (Properties.Settings.Default.ShowWorldStones && location.WorldStones is { Count: > 0 })
                    {
                        WorldAnalyzerGridData newItem = new()
                        {
                            Location = Loc.GameT(l),
                            MissingItems = new(),
                            PossibleItems = new(),
                            Name = string.Join('\n', location.WorldStones.Select(Loc.GameT)),
                            Type = Loc.GameT("World Stones")
                        };
                        if (EventPassesFilter(newItem))
                        {
                            result.Add(newItem);
                        }
                    }
                    if (Properties.Settings.Default.ShowTomes && location.TraitBook)
                    {
                        //List<LocalisedLootItem> ll = new() { new LocalisedLootItem(new() { Item = new() { { "Name", Loc.GameT("TraitBook") } } }) };
                        WorldAnalyzerGridData newItem = new()
                        {
                            Location = Loc.GameT(l),
                            MissingItems = new(),
                            PossibleItems = location.TraitBookDeleted || Properties.Settings.Default.ShowLootedItems ? new() : new() { new LocalisedLootItem(new() { Item = new() { { "Name", Loc.GameT("TraitBook") }, {"Id", "Bogus"} } }) },
                            Name = Loc.GameT("TraitBook"),
                            Type = Loc.GameT("Item")
                        };
                        if (EventPassesFilter(newItem))
                        {
                            result.Add(newItem);
                        }

                    }
                    if (Properties.Settings.Default.ShowSimulacrums && location.Simulacrum)
                    {
                        WorldAnalyzerGridData newItem = new()
                        {
                            Location = Loc.GameT(l),
                            MissingItems = new(),
                            PossibleItems = location.SimulacrumDeleted || Properties.Settings.Default.ShowLootedItems ? new() : new() { new LocalisedLootItem(new() { Item = new() { { "Name", Loc.GameT("Simulacrum") }, { "Id", "Bogus" } } }) },
                            Name = Loc.GameT("Simulacrum"),
                            Type = Loc.GameT("Item")
                        };
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
                            items = items.Where(x => !x.IsDeleted).ToList();
                        }
                        WorldAnalyzerGridData newItem = new()
                        {
                            Location = Loc.GameT(l),
                            MissingItems = items.Where(x => missingIds.Contains(x.Item["Id"])).Select(x => new LocalisedLootItem(x)).ToList(),
                            PossibleItems = items.Select(x => new LocalisedLootItem(x)).ToList(),
                            Name = Loc.GameT(lg.Name),
                            Type = Loc.GameT(Regex.Replace(lg.Type, @"\b([a-z])", m => m.Value.ToUpper()))
                        };
                        if (EventPassesFilter(newItem))
                        {
                            result.Add(newItem);
                        }
                    }
                }
            }
            return result;
        }

        public class LocalisedLootItem : LootItem
        {
            [SetsRequiredMembers]
            public LocalisedLootItem(LootItem item)
            {
                Item = item.Item;
            }

            public override string Name => Item["Id"] == Loc.GameT(Item["Id"]) ? base.Name : Loc.GameT(Item["Id"]);
        }

        public class SortCompare : IComparer<Dictionary<string, string>>
        {
            public int Compare(Dictionary<string, string>? x, Dictionary<string, string>? y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                string xmode = x.ContainsKey("Hardcore") && x["Hardcore"] == "True" ? "Hardcore" : "Normal";
                string ymode = y.ContainsKey("Hardcore") && y["Hardcore"] == "True" ? "Hardcore" : "Normal";
                if (xmode != ymode)
                {
                    return xmode.CompareTo(ymode);
                }
                if (x["Type"] != y["Type"])
                {
                    return x["Type"].CompareTo(y["Type"]);
                }

                LootItem xi = new() { Item = x };
                LootItem yi = new() { Item = y };
                return xi.Name.CompareTo(yi.Name);
            }
        }
        public class TreeListClass : IComparable<TreeListClass>, INotifyPropertyChanged
        {
            public delegate void EventHandler(object sender, PropertyChagedEventArgs e);
            public event EventHandler Expanded;
            public event PropertyChangedEventHandler PropertyChanged;
            private List<TreeListClass> _childnode;
            private Object _tag;
            private bool _isselected;
            private bool _isexpanded;
            private Visibility _visibility;
            public String Name { get; set; }
            public String? Notes { get; set; }
            public Object Tag {
                get { return _tag == null ? new object() : _tag; }
                set { _tag = value; }
            }
            public List<TreeListClass> Childnode {
                get { return _childnode == null ? new List<TreeListClass>(0) : _childnode; }
                set { _childnode = value; }
            }
            public bool IsSelected {
                get { return _isselected; }
                set { _isselected = value; OnPropertyChanged(); }
            }
            public bool IsExpanded
            {
                get { return _isexpanded; }
                set
                {
                    if (value != _isexpanded)
                    {
                        _isexpanded = value;
                        OnPropertyChanged();
                        PropertyChagedEventArgs ev = new("IsExpanded", _isexpanded, value);
                        if (Expanded != null)
                        {
                            Expanded(this, ev);
                        }
                    }
                }
            }
            public Visibility Visibility
            {
                get { return _visibility; }
                set { _visibility = value; OnPropertyChanged(); }
            }
            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
            public int CompareTo(TreeListClass other)
            {
                return Name.CompareTo(other.Name);
            }
        }
        public class PropertyChagedEventArgs : EventArgs
        {
            public PropertyChagedEventArgs(string propertyName, object oldValue, object newValue)
            {
                PropertyName = propertyName;
                OldValue = oldValue;
                NewValue = newValue;
            }
            public string PropertyName { get; private set; }
            public object OldValue { get; private set; }
            public object NewValue { get; set; }
        }
        public class WorldAnalyzerGridData
        {
            public string Location { get; set; }
            public string Type { get; set; }
            public string Name { get; set; }
            public List<LocalisedLootItem> MissingItems { get; set; }
            public List<LocalisedLootItem> PossibleItems { get; set; }
            public string MissingItemsString => string.Join("\n", MissingItems.Select(x => x.Name));

            public string PossibleItemsString => string.Join("\n", PossibleItems.Select(x => x.Name));
        }

    }
}
