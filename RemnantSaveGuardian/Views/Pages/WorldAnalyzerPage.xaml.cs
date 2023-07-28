using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
        public WorldAnalyzerPage(ViewModels.WorldAnalyzerViewModel viewModel, string pathToSaveFiles)
        {
            ViewModel = viewModel;

            InitializeComponent();

            SavePlaintextButton.Click += SavePlaintextButton_Click;

            CampaignData.AutoGeneratingColumn += Data_AutoGeneratingColumn;
            AdventureData.AutoGeneratingColumn += Data_AutoGeneratingColumn;

            Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;

            TreeViewItem nodeNormal = new TreeViewItem();
            nodeNormal.Header = Loc.T("Normal");
            nodeNormal.Foreground = treeMissingItems.Foreground;
            nodeNormal.IsExpanded = Properties.Settings.Default.NormalExpanded;
            nodeNormal.Expanded += GameType_CollapsedExpanded;
            nodeNormal.Collapsed += GameType_CollapsedExpanded;
            nodeNormal.Tag = "mode-normal";
            TreeViewItem nodeHardcore = new TreeViewItem();
            nodeHardcore.Header = Loc.T("Hardcore");
            nodeHardcore.Foreground = treeMissingItems.Foreground;
            nodeHardcore.IsExpanded = Properties.Settings.Default.HardcoreExpanded;
            nodeHardcore.Expanded += GameType_CollapsedExpanded;
            nodeHardcore.Collapsed += GameType_CollapsedExpanded;
            nodeHardcore.Tag = "mode-hardcore";
            TreeViewItem nodeSurvival = new TreeViewItem();
            nodeSurvival.Header = Loc.T("Survival");
            nodeSurvival.Foreground = treeMissingItems.Foreground;
            nodeSurvival.IsExpanded = Properties.Settings.Default.SurvivalExpanded;
            nodeSurvival.Expanded += GameType_CollapsedExpanded;
            nodeSurvival.Collapsed += GameType_CollapsedExpanded;
            nodeSurvival.Tag = "mode-survival";
            treeMissingItems.Items.Add(nodeNormal);
            treeMissingItems.Items.Add(nodeHardcore);
            treeMissingItems.Items.Add(nodeSurvival);

            Save = new(pathToSaveFiles);
            if (pathToSaveFiles == Properties.Settings.Default.SaveFolder)
            {
                SaveWatcher.SaveUpdated += (sender, eventArgs) => {
                    Save.UpdateCharacters();
                    checkAdventureTab();
                };
            }
            CharacterControl.ItemsSource = Save.Characters;
            CharacterControl.SelectionChanged += CharacterControl_SelectionChanged;
            Save.UpdateCharacters();
            CharacterControl.SelectedIndex = 0;
            checkAdventureTab();
        }
        public WorldAnalyzerPage(ViewModels.WorldAnalyzerViewModel viewModel) : this(viewModel, Properties.Settings.Default.SaveFolder)
        {

        }

        private void GameType_CollapsedExpanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem modeItem = (TreeViewItem)sender;
            if (modeItem.Tag.ToString().Contains("normal"))
            {
                Properties.Settings.Default.NormalExpanded = modeItem.IsExpanded;
            }
            else if (modeItem.Tag.ToString().Contains("hardcore"))
            {
                Properties.Settings.Default.HardcoreExpanded = modeItem.IsExpanded;
            }
            else if (modeItem.Tag.ToString().Contains("survival"))
            {
                Properties.Settings.Default.SurvivalExpanded = modeItem.IsExpanded;
            }
        }

        private void SavePlaintextButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
            //openFolderDialog.SelectedPath = Properties.Settings.Default.GameFolder;
            openFolderDialog.Description = Loc.T("Save plaintext data");
            System.Windows.Forms.DialogResult result = openFolderDialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            File.WriteAllText($@"{openFolderDialog.SelectedPath}\profile.txt", Save.GetProfileData());
            foreach (var filePath in Save.WorldSaves)
            {
                File.WriteAllText($@"{openFolderDialog.SelectedPath}\{filePath.Substring(filePath.LastIndexOf(@"\")).Replace(".sav", ".txt")}", RemnantSave.DecompressSaveAsString(filePath));
            }
        }

        private void Default_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ShowPossibleItems")
            {
                reloadEventGrids();
            }
        }

        private void Data_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.Column.Header.Equals("MissingItems"))
            {
                // todo: set missing item color?
            }
            else if (e.Column.Header.Equals("PossibleItems"))
            {
                if (!Properties.Settings.Default.ShowPossibleItems)
                {
                    e.Cancel = true;
                    return;
                }
            }
            e.Column.Header = Loc.T(e.Column.Header.ToString());
        }

        private void CharacterControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //if (CharacterControl.SelectedIndex == -1 && listCharacters.Count > 0) return;
            if (CharacterControl.Items.Count > 0 && CharacterControl.SelectedIndex > -1)
            {
                CampaignData.ItemsSource = Save.Characters[CharacterControl.SelectedIndex].CampaignEvents;
                AdventureData.ItemsSource = Save.Characters[CharacterControl.SelectedIndex].AdventureEvents;
                checkAdventureTab();
                //txtMissingItems.Text = string.Join("\n", Save.Characters[CharacterControl.SelectedIndex].GetMissingItems());

                foreach (TreeViewItem item in treeMissingItems.Items)
                {
                    item.Items.Clear();
                }
                foreach (RemnantItem rItem in Save.Characters[CharacterControl.SelectedIndex].GetMissingItems())
                {
                    TreeViewItem item = new TreeViewItem();
                    item.Header = rItem.Name;
                    if (!rItem.ItemNotes.Equals("")) item.ToolTip = rItem.ItemNotes;
                    item.Foreground = treeMissingItems.Foreground;
                    item.ContextMenu = this.treeMissingItems.Resources["ItemContext"] as System.Windows.Controls.ContextMenu;
                    item.Tag = "item";
                    TreeViewItem modeNode = ((TreeViewItem)treeMissingItems.Items[(int)rItem.ItemMode]);
                    TreeViewItem itemTypeNode = null;
                    foreach (TreeViewItem typeNode in modeNode.Items)
                    {
                        if (typeNode.Tag.ToString().Equals($"type-{rItem.RawType}"))
                        {
                            itemTypeNode = typeNode;
                            break;
                        }
                    }
                    if (itemTypeNode == null)
                    {
                        itemTypeNode = new TreeViewItem();
                        itemTypeNode.Header = rItem.Type;
                        itemTypeNode.Foreground = treeMissingItems.Foreground;
                        itemTypeNode.IsExpanded = true;
                        itemTypeNode.ContextMenu = this.treeMissingItems.Resources["ItemGroupContext"] as System.Windows.Controls.ContextMenu;
                        itemTypeNode.Tag = $"type-{rItem.RawType}";
                        ((TreeViewItem)treeMissingItems.Items[(int)rItem.ItemMode]).Items.Add(itemTypeNode);
                    }
                    itemTypeNode.Items.Add(item);
                }
            }
        }

        private void checkAdventureTab()
        {
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
        }

        private void reloadEventGrids()
        {
            var tempData = CampaignData.ItemsSource;
            CampaignData.ItemsSource = null;
            CampaignData.ItemsSource = tempData;

            tempData = AdventureData.ItemsSource;
            AdventureData.ItemsSource = null;
            AdventureData.ItemsSource = tempData;
        }
    }
}
