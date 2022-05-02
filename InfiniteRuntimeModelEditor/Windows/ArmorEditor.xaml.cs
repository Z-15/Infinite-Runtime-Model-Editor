using InfiniteRuntimeModelEditor.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Memory;
using InfiniteRuntimeModelEditor;
using static InfiniteRuntimeModelEditor.MainWindow;
using System.Diagnostics;

namespace InfiniteRuntimeModelEditor.Windows
{
    public partial class ArmorEditor : Window
    {
        #region Control Buttons
        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        // Minimize
        private void CommandBinding_Executed_Minimize(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }
        // Maximize
        private void CommandBinding_Executed_Maximize(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MaximizeWindow(this);
            RestoreButton.Visibility = Visibility.Visible;
            MaximizeButton.Visibility = Visibility.Collapsed;
        }
        // Restore
        private void CommandBinding_Executed_Restore(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.RestoreWindow(this);
            RestoreButton.Visibility = Visibility.Collapsed;
            MaximizeButton.Visibility = Visibility.Visible;
        }
        // Close
        private void CommandBinding_Executed_Close(object sender, ExecutedRoutedEventArgs e)
        {
            MainWindow.DestroyArmorEditor();
        }
        // Move Window
        private void Move_Window(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
        #endregion

        #region Variables
        Mem m = new Mem();
        List<string> armorRegions = new List<string>();
        List<string> armorVariants = new List<string>();
        Dictionary<string, SaveChange> changes = new Dictionary<string, SaveChange>();
        Dictionary<int, RegionData> regions = new Dictionary<int, RegionData>();
        string regionAddress;

        private class RegionData
        {
            public int Index;
            public string RegionName;
            public string RegionAddress;
            public Dictionary<int, string> KnownVariations;
            public ArmorRegions RegionItem;
        }
        #endregion
        public ArmorEditor(Mem M, List<string> regions, List<string> variants, Dictionary<string, SaveChange> saveChanges, string address)
        {
            InitializeComponent();
            m = M;
            armorRegions = regions;
            armorVariants = variants;
            changes = saveChanges;
            regionAddress = address;

            Populate();
        }

        private void Populate()
        {
            foreach (string region in armorRegions)
            {
                int index = int.Parse(region.Split(":")[0]);
                string name = region.Split(":")[1];
                string address = m.Get64BitCode(regionAddress + "+0x" + (32 * index + 8).ToString("X") + ",0x4").ToString("X");
                RegionData data = new RegionData();
                ArmorRegions newRegion = new ArmorRegions();

                newRegion.Title.Text = index.ToString() + ": " + name;
                newRegion.Values.SelectionChanged += SelectionUpdate;
                newRegion.Values.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                    new System.Windows.Controls.TextChangedEventHandler(TextUpdate));
                newRegion.Values.Tag = data;
                content.Children.Add(newRegion);

                data.Index = index;
                data.RegionName = name;
                data.RegionAddress = address;
                data.KnownVariations = new Dictionary<int, string>();
                data.RegionItem = newRegion;
                regions.Add(index, data);
            }

            foreach (string variant in armorVariants)
            {
                int region = int.Parse(variant.Split(":")[0]);
                int variation = int.Parse(variant.Split(":")[1]);
                string name = variant.Split(":")[2];

                RegionData data = regions[region];
                ArmorRegions item = data.RegionItem;

                item.Values.Items.Add(variation + ": " + name);
                data.KnownVariations.Add(variation, name);
            }

            foreach (KeyValuePair<int, RegionData> region in regions)
            {
                int curVariation = m.ReadInt(region.Value.RegionAddress);

                if (region.Value.KnownVariations.ContainsKey(curVariation))
                {
                    region.Value.RegionItem.Values.SelectedItem = curVariation + ": " + region.Value.KnownVariations[curVariation];
                }
                else
                {
                    region.Value.RegionItem.Values.Text = curVariation.ToString();
                }
            }
        }

        private void SelectionUpdate(object sender, RoutedEventArgs e)
        {
            ComboBox CB = sender as ComboBox;
            RegionData data = CB.Tag as RegionData;
            CB.Text = CB.SelectedItem.ToString();
            string newValue = CB.Text.Split(":")[0];
            string curVariation = m.ReadInt(data.RegionAddress).ToString();
            if (int.TryParse(newValue, out int actualValue) && newValue != curVariation)
            {
                m.WriteMemory(data.RegionAddress, "int", actualValue.ToString());
                MainWindow.AddNewChange("a", data.Index.ToString(), data.RegionName, "244,1620," + (32 * data.Index + 8) + ",4", actualValue.ToString());
            }
        }

        private void TextUpdate(object sender, RoutedEventArgs e)
        {
            ComboBox CB = sender as ComboBox;
            RegionData data = CB.Tag as RegionData;
            string newValue = CB.Text;
            string curVariation = m.ReadInt(data.RegionAddress).ToString();
            if (int.TryParse(newValue, out int actualValue) && newValue != curVariation)
            {
                m.WriteMemory(data.RegionAddress, "int", actualValue.ToString());
                MainWindow.AddNewChange("a", data.Index.ToString(), data.RegionName, "244,1620," + (32 * data.Index + 8) + ",4", actualValue.ToString());
            }
        }
    }
}
