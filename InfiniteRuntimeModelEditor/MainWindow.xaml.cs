#region Using
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using InfiniteRuntimeModelEditor.Controls;
using HelixToolkit.Wpf;
using Memory;
using System.Numerics;
using Quaternion = System.Windows.Media.Media3D.Quaternion;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Microsoft.Win32;
using System.Text;
#endregion

// Logo design credit - cmumme
// I took the IRTV logo and changed it to IRME.

namespace InfiniteRuntimeModelEditor
{
    public partial class MainWindow : Window
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
            SystemCommands.CloseWindow(this);
        }
        // Move Window
        private void Move_Window(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
        #endregion


        #region Variables
        Mem m = new Mem();
        string renderAddress = string.Empty;
        string modelAddress = string.Empty;
        string selectedTag = string.Empty;
        string tagID = string.Empty;
        int nodeCount = 0;

        Dictionary<int, ModelMarker> markers = new Dictionary<int, ModelMarker>(); // int = Overall index of the marker, not the index of the marker group.
        Dictionary<int, ModelNode> nodes = new Dictionary<int, ModelNode>(); // int = Node index.
        Dictionary<string, string> hashNames = new Dictionary<string, string>();
        Dictionary<string, SaveChange> changes = new Dictionary<string, SaveChange>();
        List<Brush> materials = new List<Brush>() { Brushes.Aqua, Brushes.Blue, Brushes.Cyan, Brushes.Orange, Brushes.Purple, Brushes.Yellow, Brushes.Violet, Brushes.Pink, Brushes.MintCream, Brushes.PowderBlue, Brushes.Tan, Brushes.PaleTurquoise };
        int materialInd = 0;

        public class ModelMarker
        {
            public int id;
            public int groupID;
            public int groupInd;
            public int parentNode;
            public string groupName;
            public string address;

            public float scale;
            public Vector3 translation;
            public Vector3 direction;
            public Quaternion qRotation;
            public Vector3 rRotation;
            public Vector3 dRotation;

            public Quaternion oRot;
            public Vector3 oTran;
            public Vector3 oDir;

            public TreeViewItem item;
            public ArrowVisual3D arrow;
        }

        public class ModelNode
        {
            public int id;
            public int parentNode;
            public string name;
            public string nodeAddress;
            public string nodeAddress2;
            public string nodeInvAddress;

            public Vector3 translation;
            public Quaternion qRotation;
            public Vector3 rRotation;
            public Vector3 dRotation;
            public float scale;

            public Quaternion invQRotation;
            public Vector3 invRRotation;
            public Vector3 invDRotation;
            public Vector3 invForward;
            public Vector3 invLeft;
            public Vector3 invUp;
            public Vector3 invPosition;
            public float invScale;
            public float DFP;

            public Vector3 inverse;
            public Vector3 inverseV;
            public Quaternion oRot;
            public Vector3 oTran;
            public float oScale;
            public Quaternion oInvRotation;
            public Vector3 oInvForward;
            public Vector3 oInvLeft;
            public Vector3 oInvUp;
            public Vector3 oInvPosition;
            public float oInvScale;
            public float oDFP;

            public Brush material;
            public TreeViewItem item;
            public BoxVisual3D cube;
        }

        public class SaveChange
        {
            public string id;
            public string tagID;
            public string path;
            public string type;
            public string value;
        }
        
        #endregion


        #region Initialize
        public MainWindow()
        {
            InitializeComponent();
            inhale_tagnames();
            inhale_hashnames();
        }

        public void inhale_hashnames()
        {
            // Used to match names to node/marker hashes
            string filename = Directory.GetCurrentDirectory() + @"\files\hashes.txt";
            IEnumerable<string>? lines = System.IO.File.ReadLines(filename);
            foreach (string? line in lines)
            {
                string[] hexString = line.Split(" : ");
                if (!hashNames.ContainsKey(hexString[0]))
                {
                    hashNames.Add(hexString[0], hexString[1]);
                }
            }
        }
        #endregion


        #region Loading
        private void LoadModel()
        {
            // Clear for loading new models
            node_tree.Items.Clear();
            ModelViewer.Children.Clear();
            nodes.Clear();
            markers.Clear();
            hideMarker.IsChecked = false;
            hideNodes.IsChecked = false;

            // Create node/marker structs for the model
            try
            {
                // Attempt to open tag
                string tagDataNum = selectedTag;
                TagStruct selectedModel = TagsList[tagDataNum];
                modelAddress = selectedModel.TagData.ToString("X");
                
                // Get tag name
                string[] nameSegments = selectedModel.TagFullName.Split("\\");
                string name = nameSegments[nameSegments.Length - 2].Replace("_", " ");
                
                // Set display valuse
                modelName.Text = name;
                modelID.Text = selectedModel.ObjectId;
                modelAdd.Text = modelAddress;

                // Set model address
                modelAddress = selectedModel.TagData.ToString("X");
                
                // Get the render_model info from the model tag
                string renderDataNum = BitConverter.ToString(m.ReadBytes(modelAddress + "+0x28", 4)).Replace("-", string.Empty);
                string renderTagID = get_tagid_by_datnum(renderDataNum);
                tagID = renderTagID;

                // Load the model if TagsList contains it
                if (TagsList.ContainsKey(renderTagID))
                {
                    // Get the tag struct and set render model address
                    TagStruct renderModel = TagsList[renderTagID];
                    renderAddress = renderModel.TagData.ToString("X");

                    // Get the size of the node list
                    nodeCount = m.ReadInt(renderAddress + "+0x1FC");

                    // Create node classes
                    for (int i = 0; i < nodeCount; i++)
                    {
                        ModelNode newNode = new ModelNode();
                        TreeViewItem item = new TreeViewItem();
                        BoxVisual3D cube = new BoxVisual3D();
                        
                        // Get offsets of model
                        string nodeOffset = (i * 32).ToString("X");
                        string nodeModelOffset = (i * 92).ToString("X");
                        
                        // Get addresses
                        string nodeRuntimeAdd = m.Get64BitCode(renderAddress + "+0x1EC,0x" + nodeOffset).ToString("X");
                        string nodeModelAdd = m.Get64BitCode(modelAddress + "+0x240,0x" + nodeModelOffset).ToString("X");
                        string nodeAdd = m.Get64BitCode(renderAddress + "+0x40,0x0").ToString("X");
                        
                        // Node name
                        string nodeHash = BitConverter.ToString(m.ReadBytes(nodeAdd + "+0x" + (i * 124).ToString("X"), 4)).Replace("-", string.Empty);

                        // Node parent
                        int nodeParent = m.Read2Byte(nodeAdd + "+0x" + ((i * 124) + 4).ToString("X"));

                        // Runtime Node Values
                        Vector3 translation = new Vector3(m.ReadFloat(nodeRuntimeAdd + "+0x10", round: false), m.ReadFloat(nodeRuntimeAdd + "+0x14", round: false), m.ReadFloat(nodeRuntimeAdd + "+0x18", round: false));
                        Quaternion qRotation = new Quaternion(m.ReadFloat(nodeRuntimeAdd, round: false), m.ReadFloat(nodeRuntimeAdd + "+0x4", round: false), m.ReadFloat(nodeRuntimeAdd + "+0x8", round: false), m.ReadFloat(nodeRuntimeAdd + "+0xC", round: false));
                        Vector3 rRotation = ToEulerAngles(qRotation);
                        Vector3 dRotation = ToDegree(rRotation);
                        float scale = m.ReadFloat(nodeRuntimeAdd + "+0x1C");

                        // Node values
                        Quaternion invRotation = new Quaternion(m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 24).ToString("X"), round: false), m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 28).ToString("X"), round: false), m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 32).ToString("X"), round: false), m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 36).ToString("X"), round: false));
                        Vector3 invForward = new Vector3(m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 40).ToString("X"), round: false), m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 44).ToString("X"), round: false), m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 48).ToString("X"), round: false));
                        Vector3 invLeft = new Vector3(m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 52).ToString("X"), round: false), m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 56).ToString("X"), round: false), m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 60).ToString("X"), round: false));
                        Vector3 invUp = new Vector3(m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 64).ToString("X"), round: false), m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 68).ToString("X"), round: false), m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 72).ToString("X"), round: false));
                        Vector3 invPosition = new Vector3(m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 76).ToString("X"), round: false), m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 80).ToString("X"), round: false), m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 84).ToString("X"), round: false));
                        float invScale = m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 88).ToString("X"), round: false);
                        float DFP = m.ReadFloat(nodeAdd + "+0x" + ((i * 124) + 92).ToString("X"), round: false);
                        
                        // Model node values
                        Vector3 inverse = new Vector3(m.ReadFloat(nodeModelAdd + "+0x2C", round: false), m.ReadFloat(nodeModelAdd + "+0x3C", round: false), m.ReadFloat(nodeModelAdd + "+0x4C", round: false));
                        Vector3 inverseV = new Vector3(1, 1, 1);
                        if (inverse.X < 0)
                            inverseV.X = -1;
                        if (inverse.Y < 0)
                            inverseV.Y = -1;
                        if (inverse.Z < 0)
                            inverseV.Z = -1;

                        
                        

                        // Create struct
                        newNode.id = i;
                        newNode.parentNode = nodeParent;
                        newNode.name = nodeHash;
                        
                        newNode.nodeAddress = nodeRuntimeAdd;
                        newNode.nodeAddress2 = nodeAdd;
                        newNode.nodeInvAddress = nodeModelAdd;
                        
                        newNode.translation = translation; 
                        newNode.qRotation = qRotation;
                        newNode.rRotation = rRotation;
                        newNode.dRotation = dRotation;
                        newNode.scale = scale;

                        newNode.invQRotation = invRotation;
                        newNode.invRRotation = ToEulerAngles(invRotation);
                        newNode.invDRotation = ToDegree(newNode.invRRotation);
                        newNode.invForward = invForward;
                        newNode.invLeft = invLeft;
                        newNode.invUp = invUp;
                        newNode.invPosition = invPosition;
                        newNode.invScale = invScale;
                        newNode.DFP = DFP;

                        newNode.inverse = inverse;
                        newNode.inverseV = inverseV;

                        newNode.oRot = qRotation;
                        newNode.oTran = translation;
                        newNode.oScale = scale;

                        newNode.oInvRotation = invRotation;
                        newNode.oInvForward = invForward;
                        newNode.oInvLeft = invLeft;
                        newNode.oInvUp = invUp;
                        newNode.oInvPosition = invPosition;
                        newNode.oInvScale = invScale;
                        newNode.oDFP = DFP;
                        
                        newNode.material = materials[materialInd];
                        newNode.item = item;
                        newNode.cube = cube;

                        // Set Item data
                        if (hashNames.ContainsKey(nodeHash))
                        {
                            item.Header = "Node: " + i + " (" + nodeHash + ") - " + hashNames[nodeHash];
                        }
                        else
                        {
                            item.Header = "Node: " + i + " (" + nodeHash + ")";
                        }

                        item.Tag = newNode;
                        item.Selected += LoadProperties;
                        item.Style = Resources["TreeViewItemStyle"] as Style;

                        // Set cube data
                        cube.SetName(i.ToString());
                        cube.Material = new DiffuseMaterial(newNode.material);

                        // Add to Dictionary
                        nodes.Add(i, newNode);
                        
                        // Reset materials
                        
                        if (materialInd < materials.Count - 1)
                        {
                            materialInd++;
                        }
                        else
                        {
                            materialInd = 0;
                        } 
                    }

                    // Assign nodes to proper parents
                    foreach (KeyValuePair<int, ModelNode> nodeKVP in nodes)
                    {
                        // Check if node has a parent
                        if (nodeKVP.Value.parentNode.ToString("X") == "FFFF")
                        {
                            node_tree.Items.Add(nodeKVP.Value.item);
                        }
                        else
                        {
                            nodes[nodeKVP.Value.parentNode].item.Items.Add(nodeKVP.Value.item);
                        }
                    }

                    int markerInd = 0; // overall index
                    int markerGroupsSize = m.ReadInt(renderAddress + "+0x78");

                    // Create marker structs
                    for (int i = 0; i < markerGroupsSize; i++)
                    {
                        // Get group address and hash
                        string markerGroupAdd = m.Get64BitCode(renderAddress + "+0x68,0x" + (i*24).ToString("X")).ToString("X");
                        string markerName = BitConverter.ToString(m.ReadBytes(markerGroupAdd, 4)).Replace("-", string.Empty);;
                        
                        // Iterate through marker section
                        int markerSize = m.ReadInt(markerGroupAdd + "+0x14");
                        for (int j = 0; j < markerSize; j++)
                        {
                            // Get marker address
                            string markerAdd = m.Get64BitCode(markerGroupAdd + "+0x4,0x" + (j * 56).ToString("X")).ToString("X");

                            // Get values
                            Quaternion markerRotation = new Quaternion(m.ReadFloat(markerAdd + "+0x18", round: false), m.ReadFloat(markerAdd + "+0x1C", round: false), m.ReadFloat(markerAdd + "+0x20", round: false), m.ReadFloat(markerAdd + "+0x24", round: false));
                            Vector3 markerTrans = new Vector3(m.ReadFloat(markerAdd + "+0xC", round: false), m.ReadFloat(markerAdd + "+0x10", round: false), m.ReadFloat(markerAdd + "+0x14", round: false));
                            Vector3 markerDirection = new Vector3(m.ReadFloat(markerAdd + "+0x2C", round: false), m.ReadFloat(markerAdd + "+0x30", round: false), m.ReadFloat(markerAdd + "+0x34", round: false));
                            float markerScale = m.ReadFloat(markerAdd + "+0x28", round: false);
                            int parentNode = m.Read2Byte(markerAdd + "+0x08");
                            
                            // Create tree view item and arrow
                            TreeViewItem markerItem = new TreeViewItem();
                            ArrowVisual3D arrow = new ArrowVisual3D();
                            arrow.SetName(markerInd.ToString());
                            arrow.Material = new DiffuseMaterial(Brushes.OrangeRed);

                            // Create marker struct
                            ModelMarker newMarker = new ModelMarker();
                            newMarker.id = markerInd;
                            newMarker.groupID = i;
                            newMarker.groupInd = j;
                            newMarker.parentNode = parentNode;
                            newMarker.groupName = markerName;
                            newMarker.address = markerAdd;
                            newMarker.scale = markerScale;
                            newMarker.translation = markerTrans;
                            newMarker.direction = markerDirection;
                            newMarker.qRotation = markerRotation;
                            newMarker.rRotation = ToEulerAngles(markerRotation);
                            newMarker.dRotation = ToDegree(newMarker.rRotation);
                            newMarker.oTran = markerTrans;
                            newMarker.oDir = markerDirection;
                            newMarker.oRot = markerRotation;
                            newMarker.item = markerItem;
                            newMarker.arrow = arrow;

                            markerItem.Tag = newMarker;

                            if (hashNames.ContainsKey(markerName))
                            {
                                markerItem.Header = "Marker: " + markerInd + " (" + markerName + ") - " + hashNames[markerName];
                            }
                            else
                            {
                                markerItem.Header = "Marker: " + markerInd + " (" + markerName + ")";
                            }

                            markerItem.Header = "Marker: " + markerInd + " (" + markerName + ")";
                            markerItem.Selected += LoadProperties;
                            markerItem.Style = Resources["TreeViewItemStyle"] as Style;
                            markers.Add(markerInd, newMarker);

                            markerInd++;
                        }
                    }

                    // Assign parents to markers
                    foreach (KeyValuePair<int, ModelMarker> markerKVP in markers)
                    {
                        // Probably not needed here, but checks just in case.
                        if (markerKVP.Value.parentNode.ToString("X") == "FFFF")
                        {
                            node_tree.Items.Add(markerKVP.Value.item);
                        }
                        else
                        {
                            nodes[markerKVP.Value.parentNode].item.Items.Add(markerKVP.Value.item);
                        }
                    }
                }
                else
                {
                    statusText.Text = "Render model not found!";
                }
            }
            catch (Exception ex)
            {
                statusText.Text = "Error loading nodes and markers!";
                Debug.WriteLine(ex.Message);
            }
            
            // Set parents and load the model through updates
            try
            {
                foreach (ModelNode node in nodes.Values)
                {
                    if (node.parentNode.ToString("X") != "FFFF")
                    {
                        ModelNode parentNode = nodes[node.parentNode];
                        parentNode.cube.Children.Add(node.cube);
                    }
                }
                foreach (ModelMarker marker in markers.Values)
                {
                    ModelNode parentNode = nodes[marker.parentNode];
                    parentNode.cube.Children.Add(marker.arrow);
                }

                // Add lights and grid lines
                DefaultLights defaultLights = new DefaultLights();
                GridLinesVisual3D gridLinesVisual3D = new GridLinesVisual3D();

                gridLinesVisual3D.Material = new DiffuseMaterial(Brushes.WhiteSmoke);
                gridLinesVisual3D.Width = 100;
                gridLinesVisual3D.Length = 100;
                ModelViewer.Children.Add(defaultLights);
                ModelViewer.Children.Add(gridLinesVisual3D);
                ModelViewer.Children.Add(nodes[0].cube);

                // Set transform / Update model
                foreach (var child1 in nodes[0].cube.Children)
                {
                    Type t = child1.GetType();
                    if (t.Equals(typeof(BoxVisual3D)))
                    {
                        BoxVisual3D child = child1 as BoxVisual3D;
                        int childInd = int.Parse(child.GetName());
                        ModelNode childStruct = nodes[childInd];

                        UpdateNodes(childStruct);
                    }
                    else
                    {
                        ArrowVisual3D child = child1 as ArrowVisual3D;
                        int childInd = int.Parse(child.GetName());
                        ModelMarker childStruct = markers[childInd];

                        UpdateMarkers(childStruct);
                    }
                }

                statusText.Text = "Model Loaded!";
            }
            catch (Exception ex)
            {
                statusText.Text = "Error loading model!";
                Debug.WriteLine(ex.Message);
            }
        }

        private void LoadProperties(object sender, RoutedEventArgs e)
        {
            // When node is clicked parents are selected too
            // This Checks which node was the original source
            var hit = e.OriginalSource as DependencyObject;
            while (hit != null && !(hit is TreeViewItem))
                hit = VisualTreeHelper.GetParent(hit);

            // If sender is the original source, do this
            if (hit != null || hit != sender)
            {
                try
                {
                    propTab.Children.Clear();
                    otherPropTab.Children.Clear();
                    TreeViewItem item = hit as TreeViewItem;

                    // Get type of item hit
                    var t = item.Tag.GetType();

                    if (t.Equals(typeof(ModelNode)))
                    {
                        ModelNode node = (ModelNode)item.Tag;

                        // Create value blocks for node
                        CreateHashBlock(node.name, node.id);
                        CreateValueBlock("translation", node.translation.X * node.inverseV.X * node.inverseV.X, node.translation.Y * node.inverseV.Y * node.inverseV.Y, node.translation.Z * node.inverseV.Z * node.inverseV.Z, "n", node.id);
                        CreateValueBlock("rotation", node.dRotation.X, node.dRotation.Y, node.dRotation.Z, "n", node.id);
                        CreateScaleBlock(node.scale, "n", node.id);

                        CreateInvBlock("Rotations", node.invDRotation.X, node.invDRotation.Y, node.invDRotation.Z, node.id);
                        CreateInvBlock("Dimensions", node.invForward.Y, node.invLeft.Z, node.invPosition.X, node.id);
                        CreateInvBlock("Translations", node.invPosition.Y, node.invPosition.Z, node.scale, node.id);
                        CreateInvBlock("Skew From Center", node.invUp.Y, node.invUp.Z, node.invLeft.X, node.id);
                        CreateInvBlock("Inverse Skew", node.invLeft.Y, node.invForward.Z, node.invUp.X, node.id);
                        CreateSingleInvBlock("Scale", node.invForward.X, node.id, "invForwardX");
                        CreateSingleInvBlock("Distance From Parent", node.DFP, node.id, "dfp");                        

                        // Create undo button for properties tab
                        Button undoButton = new Button();
                        undoButton.Content = "Undo";
                        undoButton.Tag = node;
                        undoButton.Click += UndoObjChanges;
                        undoButton.Width = 100;
                        undoButton.Margin = new Thickness(5);
                        propTab.Children.Add(undoButton);

                        // Create undo button for other properties tab
                        Button undoButton1 = new Button();
                        undoButton1.Content = "Undo";
                        undoButton1.Tag = node;
                        undoButton1.Click += UndoObjChanges;
                        undoButton1.Width = 100;
                        undoButton1.Margin = new Thickness(5);
                        otherPropTab.Children.Add(undoButton1);
                        
                        ShowSelection(node, new ModelMarker());
                        
                        statusText.Text = "Node properties loaded...";
                    }
                    else if (t.Equals(typeof(ModelMarker)))
                    {
                        ModelMarker marker = (ModelMarker)item.Tag;

                        // Create value blocks for node
                        CreateHashBlock(marker.groupName, marker.id);
                        CreateValueBlock("translation", marker.translation.X, marker.translation.Y, marker.translation.Z, "m", marker.id);
                        CreateValueBlock("rotation", marker.dRotation.X, marker.dRotation.Y, marker.dRotation.Z, "m", marker.id);
                        CreateValueBlock("direction", marker.direction.X, marker.direction.Y, marker.direction.Z, "m", marker.id);
                        
                        // Create Undo Button
                        Button undoButton = new Button();
                        undoButton.Content = "Undo";
                        undoButton.Tag = marker;
                        undoButton.Click += UndoObjChanges;
                        undoButton.Margin = new Thickness(5);
                        undoButton.Width = 100;
                        propTab.Children.Add(undoButton);
                        
                        ShowSelection(new ModelNode(), marker);

                        statusText.Text = "Marker properties loaded...";
                    }
                }
                catch (Exception ex)
                {
                    statusText.Text = "Error loading properties!";
                }
            }
        }
        #endregion


        #region Save
        private void SaveFile(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create save file dialog and set filters
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "IRTV Files | *.irtv";
                saveFileDialog.DefaultExt = "irtv";
                if (saveFileDialog.ShowDialog() == true)
                {
                    // Write mod description
                    FileStream fs = File.OpenWrite(saveFileDialog.FileName);
                    byte[] des1 = new UTF8Encoding(true).GetBytes("^" + Environment.NewLine);
                    byte[] des2 = new UTF8Encoding(true).GetBytes("-IRME Model File-" + Environment.NewLine);
                    byte[] des3 = new UTF8Encoding(true).GetBytes("^" + Environment.NewLine);

                    fs.Write(des1, 0, des1.Length);
                    fs.Write(des2, 0, des2.Length);
                    fs.Write(des3, 0, des3.Length);

                    // Write every change to file
                    foreach (SaveChange change in changes.Values)
                    {
                        string tag = change.tagID;
                        string path = change.path;
                        string value = change.value;
                        string newLine = tag + ":," + path + ";Float;" + value;
                        byte[] line = new UTF8Encoding(true).GetBytes(newLine + Environment.NewLine);

                        fs.Write(line, 0, line.Length);
                    }
                    fs.Close();
                    statusText.Text = "File saved...";
                }
            }
            catch (Exception ex)
            {
                statusText.Text = "Error saving file!";
            }
        }

        private void AddNewChange(string objType, string objID, string updateType, string path, string value)
        {
            // Prevents the same value from being added twice
            if (changes.ContainsKey(objID + ":" + tagID + ":" + objType + ":" + updateType))
            {
                changes.Remove(objID + ":" + tagID + ":" + objType + ":" + updateType);
            }

            // Creates a new change struct
            SaveChange newSave = new SaveChange();
            newSave.id = objID;
            newSave.tagID = tagID;
            newSave.path = path;
            newSave.type = "float";
            newSave.value = value;

            // Add new change to dictionary
            changes.Add(objID + ":" + tagID + ":" + objType + ":" + updateType, newSave);
        }

        private void ClearChanges(object sender, RoutedEventArgs e)
        {
            changes.Clear();
            statusText.Text = "Changes cleared...";
        }
        #endregion


        #region Changes and Updates
        private void SliderChange(object sender, RoutedEventArgs e)
        {
            try
            {
                // Reflect change from slider to the value box
                Slider slider = sender as Slider;
                StackPanel parent = slider.Parent as StackPanel;
                TextBox textBox = parent.Children[2] as TextBox;
                textBox.Text = slider.Value.ToString("0.000");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void ValueChange(object sender, RoutedEventArgs e)
        {
            try
            {
                // Reflect change from value box onto slider
                TextBox textBox = sender as TextBox;
                StackPanel parent = textBox.Parent as StackPanel;
                Slider slider = parent.Children[1] as Slider;
                slider.Value = float.Parse(textBox.Text);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void Update(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            
            try
            {
                // Prevent invalide entrys from passing through here
                if (!string.IsNullOrEmpty(textBox.Text) && float.TryParse(textBox.Text, out float value))
                {
                    // Set variables
                    string offset = "";
                    string objType = textBox.Tag.ToString().Split(":")[0];
                    string updateType = textBox.Tag.ToString().Split(":")[1];
                    int index = int.Parse(textBox.Tag.ToString().Split(":")[2]);

                    if (objType == "n")
                    {
                        // Get current node
                        ModelNode node = nodes[index];
                        float newValue = float.Parse(textBox.Text);

                        // Rotation
                        if (updateType == "rotX" || updateType == "rotY" || updateType == "rotZ")
                        {
                            Vector3 dRotNew = new Vector3();
                            switch (updateType)
                            {
                                case "rotX":
                                    dRotNew = new Vector3(newValue, node.dRotation.Y, node.dRotation.Z);
                                    break;
                                case "rotY":
                                    dRotNew = new Vector3(node.dRotation.X, newValue, node.dRotation.Z);
                                    break;
                                case "rotZ":
                                    dRotNew = new Vector3(node.dRotation.X, node.dRotation.Y, newValue);
                                    break;
                            }
                            Vector3 rRotNew = ToRadian(dRotNew);
                            Quaternion newValues = ToQuaternion(rRotNew);

                            // Generates a new Quaternion, so all rotation values need updating.
                            m.WriteMemory(node.nodeAddress, "float", newValues.X.ToString("0.00000"));
                            m.WriteMemory(node.nodeAddress + "+0x4", "float", newValues.Y.ToString("0.00000"));
                            m.WriteMemory(node.nodeAddress + "+0x8", "float", newValues.Z.ToString("0.00000"));
                            m.WriteMemory(node.nodeAddress + "+0xC", "float", newValues.W.ToString("0.00000"));

                            // Add changes made to change list
                            AddNewChange("n", node.id.ToString(), "rotX", "492," + (node.id * 32), newValues.X.ToString("0.00000"));
                            AddNewChange("n", node.id.ToString(), "rotY", "492," + ((node.id * 32) + 4), newValues.Y.ToString("0.00000"));
                            AddNewChange("n", node.id.ToString(), "rotZ", "492," + ((node.id * 32) + 8), newValues.Z.ToString("0.00000"));
                            AddNewChange("n", node.id.ToString(), "rotW", "492," + ((node.id * 32) + 12), newValues.W.ToString("0.00000"));
                            
                        }
                        // Translation
                        else if (updateType == "transX" || updateType == "transY" || updateType == "transZ")
                        {
                            switch (updateType)
                            {
                                case "transX":
                                    newValue = newValue * node.inverseV.X * node.inverseV.X;
                                    m.WriteMemory(node.nodeAddress + "+0x10", "float", newValue.ToString());
                                    AddNewChange("n", node.id.ToString(), "transX", "492," + ((node.id * 32) + 16), newValue.ToString("0.00"));
                                    break;
                                case "transY":
                                    newValue = newValue * node.inverseV.Y * node.inverseV.Y;
                                    m.WriteMemory(node.nodeAddress + "+0x14", "float", newValue.ToString());
                                    AddNewChange("n", node.id.ToString(), "transY", "492," + ((node.id * 32) + 20), newValue.ToString("0.00"));
                                    break;
                                case "transZ":
                                    newValue = newValue * node.inverseV.Z * node.inverseV.Z;
                                    m.WriteMemory(node.nodeAddress + "+0x18", "float", newValue.ToString());
                                    AddNewChange("n", node.id.ToString(), "transZ", "492," + ((node.id * 32) + 24), newValue.ToString("0.00"));
                                    break;
                            }
                        }
                        // Scale
                        else if (updateType == "scale")
                        {
                            m.WriteMemory(node.nodeAddress + "+0x1C", "float", textBox.Text);
                            AddNewChange("n", node.id.ToString(), "scale", "492," + ((node.id * 32) + 28), newValue.ToString("0.00"));
                        }
                        // Inverse Forward
                        else if (updateType == "invForwardX" || updateType == "invForwardY" || updateType == "invForwardZ")
                        {
                            switch (updateType)
                            {
                                case "invForwardX":
                                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 40).ToString("X"), "float", newValue.ToString());
                                    AddNewChange("n", node.id.ToString(), "invForwardX", "64," + ((node.id * 124) + 40), newValue.ToString("0.00"));
                                    break;
                                case "invForwardY":
                                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 44).ToString("X"), "float", newValue.ToString());
                                    AddNewChange("n", node.id.ToString(), "invForwardY", "64," + ((node.id * 124) + 44), newValue.ToString("0.00"));
                                    break;
                                case "invForwardZ":
                                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 48).ToString("X"), "float", newValue.ToString());
                                    AddNewChange("n", node.id.ToString(), "invForwardZ", "64," + ((node.id * 124) + 48), newValue.ToString("0.00"));
                                    break;
                            }
                        }
                        // Inverse Left
                        else if (updateType == "invLeftX" || updateType == "invLeftY" || updateType == "invLeftZ")
                        {
                            switch (updateType)
                            {
                                case "invLeftX":
                                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 52).ToString("X"), "float", newValue.ToString());
                                    AddNewChange("n", node.id.ToString(), "invLeftX", "64," + ((node.id * 124) + 52), newValue.ToString("0.00"));
                                    break;
                                case "invLeftY":
                                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 56).ToString("X"), "float", newValue.ToString());
                                    AddNewChange("n", node.id.ToString(), "invLeftY", "64," + ((node.id * 124) + 56), newValue.ToString("0.00"));
                                    break;
                                case "invLeftZ":
                                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 60).ToString("X"), "float", newValue.ToString());
                                    AddNewChange("n", node.id.ToString(), "invLeftZ", "64," + ((node.id * 124) + 60), newValue.ToString("0.00"));
                                    break;
                            }
                        }
                        // Inverse Up
                        else if (updateType == "invUpX" || updateType == "invUpY" || updateType == "invUpZ")
                        {
                            switch (updateType)
                            {
                                case "invUpX":
                                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 64).ToString("X"), "float", newValue.ToString());
                                    AddNewChange("n", node.id.ToString(), "invUpX", "64," + ((node.id * 124) + 64), newValue.ToString("0.00"));
                                    break;
                                case "invUpY":
                                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 68).ToString("X"), "float", newValue.ToString());
                                    AddNewChange("n", node.id.ToString(), "invUpY", "64," + ((node.id * 124) + 68), newValue.ToString("0.00"));
                                    break;
                                case "invUpZ":
                                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 72).ToString("X"), "float", newValue.ToString());
                                    AddNewChange("n", node.id.ToString(), "invUpZ", "64," + ((node.id * 124) + 72), newValue.ToString("0.00"));
                                    break;
                            }
                        }
                        // Inverse Position
                        else if (updateType == "invPositionX" || updateType == "invPositionY" || updateType == "invPositionZ")
                        {
                            switch (updateType)
                            {
                                case "invPositionX":
                                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 76).ToString("X"), "float", newValue.ToString());
                                    AddNewChange("n", node.id.ToString(), "invPositionX", "64," + ((node.id * 124) + 76), newValue.ToString("0.00"));
                                    break;
                                case "invPositionY":
                                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 80).ToString("X"), "float", newValue.ToString());
                                    AddNewChange("n", node.id.ToString(), "invPositionY", "64," + ((node.id * 124) + 80), newValue.ToString("0.00"));
                                    break;
                                case "invPositionZ":
                                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 84).ToString("X"), "float", newValue.ToString());
                                    AddNewChange("n", node.id.ToString(), "invPositionZ", "64," + ((node.id * 124) + 84), newValue.ToString("0.00"));
                                    break;
                            }
                        }
                        // Inverse Scale
                        else if (updateType == "invScale")
                        {
                            m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 88).ToString("X"), "float", newValue.ToString());
                            AddNewChange("n", node.id.ToString(), "invScale", "64," + ((node.id * 124) + 88), newValue.ToString("0.00"));
                        }
                        // Distance From Parent
                        else if (updateType == "dfp")
                        {
                            m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 92).ToString("X"), "float", newValue.ToString());
                            AddNewChange("n", node.id.ToString(), "dfp", "64," + ((node.id * 124) + 92), newValue.ToString("0.00"));
                        }
                        else if (updateType == "invRotationsX" || updateType == "invRotationsY" || updateType == "invRotationsZ")
                        {
                            Vector3 dRotNew = new Vector3();
                            switch (updateType)
                            {
                                case "invRotationsX":
                                    dRotNew = new Vector3(newValue, node.invDRotation.Y, node.invDRotation.Z);
                                    break;
                                case "invRotationsY":
                                    dRotNew = new Vector3(node.invDRotation.X, newValue, node.invDRotation.Z);
                                    break;
                                case "invRotationsZ":
                                    dRotNew = new Vector3(node.invDRotation.X, node.invDRotation.Y, newValue);
                                    break;
                            }
                            Vector3 rRotNew = ToRadian(dRotNew);
                            Quaternion newValues = ToQuaternion(rRotNew);

                            // Generates a new Quaternion, so all rotation values need updating.
                            m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 24).ToString("X"), "float", newValues.X.ToString());
                            m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 28).ToString("X"), "float", newValues.Y.ToString());
                            m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 32).ToString("X"), "float", newValues.Z.ToString());
                            m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 36).ToString("X"), "float", newValues.W.ToString());

                            // Add changes made to change list
                            AddNewChange("n", node.id.ToString(), "invRotationsX", "64," + ((node.id * 124) + 24), newValues.X.ToString("0.0000"));
                            AddNewChange("n", node.id.ToString(), "invRotationsY", "64," + ((node.id * 124) + 28), newValues.Y.ToString("0.0000"));
                            AddNewChange("n", node.id.ToString(), "invRotationsZ", "64," + ((node.id * 124) + 32), newValues.Z.ToString("0.0000"));
                            AddNewChange("n", node.id.ToString(), "invRotationsW", "64," + ((node.id * 124) + 36), newValues.W.ToString("0.0000"));

                        }

                        UpdateNode(node);
                        UpdateNodes(node);
                    }
                    else if (objType == "m")
                    {
                        ModelMarker marker = markers[index];
                        string markerAdd = marker.address;
                        float newValue = float.Parse(textBox.Text);

                        // Translation
                        if (updateType == "transX" || updateType == "transY" || updateType == "transZ")
                        {
                            switch (updateType)
                            {
                                case "transX":
                                    m.WriteMemory(markerAdd + "+0xC", "float", newValue.ToString());
                                    AddNewChange("m", marker.id.ToString(), "transX", "104," + ((marker.groupID * 24) + 4) + "," + ((marker.groupInd * 56) + 12), newValue.ToString("0.00"));
                                    break;
                                case "transY":
                                    m.WriteMemory(markerAdd + "+0x10", "float", newValue.ToString());
                                    AddNewChange("m", marker.id.ToString(), "transY", "104," + ((marker.groupID * 24) + 4) + "," + ((marker.groupInd * 56) + 16), newValue.ToString("0.00"));
                                    break;
                                case "transZ":
                                    m.WriteMemory(markerAdd + "+0x14", "float", newValue.ToString());
                                    AddNewChange("m", marker.id.ToString(), "transZ", "104," + ((marker.groupID * 24) + 4) + "," + ((marker.groupInd * 56) + 20), newValue.ToString("0.00"));
                                    break;
                            }
                        }
                        // Direction
                        else if (updateType == "dirX" || updateType == "dirY" || updateType == "dirZ")
                        {
                            switch (updateType)
                            {
                                case "dirX":
                                    m.WriteMemory(markerAdd + "+0x2C", "float", newValue.ToString());
                                    AddNewChange("m", marker.id.ToString(), "dirX", "104," + ((marker.groupID * 24) + 4) + "," + ((marker.groupInd * 56) + 44), newValue.ToString("0.00"));
                                    break;
                                case "dirY":
                                    m.WriteMemory(markerAdd + "+0x30", "float", newValue.ToString());
                                    AddNewChange("m", marker.id.ToString(), "dirY", "104," + ((marker.groupID * 24) + 4) + "," + ((marker.groupInd * 56) + 48), newValue.ToString("0.00"));
                                    break;
                                case "dirZ":
                                    m.WriteMemory(markerAdd + "+0x34", "float", newValue.ToString());
                                    AddNewChange("m", marker.id.ToString(), "dirZ", "104," + ((marker.groupID * 24) + 4) + "," + ((marker.groupInd * 56) + 52), newValue.ToString("0.00"));
                                    break;
                            }
                        }
                        // Rotation
                        else if (updateType == "rotX" || updateType == "rotY" || updateType == "rotZ")
                        {
                            Vector3 dRotNew = new Vector3();
                            switch (updateType)
                            {
                                case "rotX":
                                    dRotNew = new Vector3(newValue, marker.dRotation.Y, marker.dRotation.Z);
                                    break;
                                case "rotY":
                                    dRotNew = new Vector3(marker.dRotation.X, newValue, marker.dRotation.Z);
                                    break;
                                case "rotZ":
                                    dRotNew = new Vector3(marker.dRotation.X, marker.dRotation.Y, newValue);
                                    break;
                            }
                            Vector3 rRotNew = ToRadian(dRotNew);
                            Quaternion newValues = ToQuaternion(rRotNew);

                            Debug.WriteLine("-START-");
                            Debug.WriteLine("Degrees - X:{0}, Y:{1}, Z:{2}", dRotNew.X.ToString("0.00"), dRotNew.Y.ToString("0.00"), dRotNew.Z.ToString("0.00"));
                            Debug.WriteLine("Radians - X:{0}, Y:{1}, Z:{2}", rRotNew.X.ToString("0.00"), rRotNew.Y.ToString("0.00"), rRotNew.Z.ToString("0.00"));
                            Debug.WriteLine("Quaternions - X:{0}, Y:{1}, Z:{2}, W:{3}", newValues.X.ToString("0.00"), newValues.Y.ToString("0.00"), newValues.Z.ToString("0.00"), newValues.W.ToString("0.00"));
                            Debug.WriteLine("-END-");

                            m.WriteMemory(markerAdd + "+0x18", "float", newValues.X.ToString());
                            m.WriteMemory(markerAdd + "+0x1C", "float", newValues.Y.ToString());
                            m.WriteMemory(markerAdd + "+0x20", "float", newValues.Z.ToString());
                            m.WriteMemory(markerAdd + "+0x24", "float", newValues.W.ToString());

                            AddNewChange("m", marker.id.ToString(), "rotX", "104," + ((marker.groupID * 24) + 4) + "," + ((marker.groupInd * 56) + 24), newValues.X.ToString("0.00"));
                            AddNewChange("m", marker.id.ToString(), "rotY", "104," + ((marker.groupID * 24) + 4) + "," + ((marker.groupInd * 56) + 28), newValues.Y.ToString("0.00"));
                            AddNewChange("m", marker.id.ToString(), "rotZ", "104," + ((marker.groupID * 24) + 4) + "," + ((marker.groupInd * 56) + 32), newValues.Z.ToString("0.00"));
                            AddNewChange("m", marker.id.ToString(), "rotW", "104," + ((marker.groupID * 24) + 4) + "," + ((marker.groupInd * 56) + 36), newValues.W.ToString("0.00"));
                        }

                        UpdateMarker(marker);
                        UpdateMarkers(marker);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void UpdateNodes(ModelNode node)
        {
            if (node.parentNode.ToString("X") == "FFFF")
            {
                node.cube.Width = node.scale;
                node.cube.Length = node.scale;
                node.cube.Height = node.scale;
                node.cube.Center = new Point3D(node.translation.X, node.translation.Y, node.translation.Z);
            }
            else
            {
                ModelNode parentNode = nodes[node.parentNode];

                // Cube scale
                node.cube.Width = node.scale * parentNode.cube.Width;
                node.cube.Length = node.scale * parentNode.cube.Length;
                node.cube.Height = node.scale * parentNode.cube.Height;

                // Cube center
                node.cube.Center = new Point3D()
                {
                    X = parentNode.cube.Center.X,
                    Y = parentNode.cube.Center.Y,
                    Z = parentNode.cube.Center.Z
                };

                // Cube rotation
                QuaternionRotation3D qCubeRotate = new QuaternionRotation3D(node.qRotation);
                RotateTransform3D cubeRotate = new RotateTransform3D();
                cubeRotate.Rotation = qCubeRotate;

                // Cube Translation
                // Don't ask about that math, I spent a lot of time figuring out how to translate things properly.
                // When I made the equation for it, it made sense to me.
                // However, I forgot why or how it works.
                TranslateTransform3D cubeTranslate = new TranslateTransform3D()
                {
                    OffsetX = (node.cube.Center.X + node.translation.X) * (parentNode.cube.Width * 10) * 1.5,
                    OffsetY = (node.cube.Center.Y + node.translation.Y) * (parentNode.cube.Length * 10) * 1.5,
                    OffsetZ = (node.cube.Center.Z + node.translation.Z) * (parentNode.cube.Height * 10) * 1.5,
                };

                Transform3DGroup cubeTransform = new Transform3DGroup();
                cubeTransform.Children.Add(cubeRotate);
                cubeTransform.Children.Add(cubeTranslate);
                node.cube.Transform = cubeTransform;

                foreach (var child1 in node.cube.Children)
                {
                    Type t = child1.GetType();
                    if (t.Equals(typeof(BoxVisual3D)))
                    {
                        BoxVisual3D child = child1 as BoxVisual3D;
                        int childInd = int.Parse(child.GetName());
                        ModelNode childStruct = nodes[childInd];

                        UpdateNodes(childStruct);
                    }
                    else
                    {
                        ArrowVisual3D child = child1 as ArrowVisual3D;
                        int childInd = int.Parse(child.GetName());
                        ModelMarker childStruct = markers[childInd];

                        UpdateMarkers(childStruct);
                    }
                }
            }
        }

        private void UpdateMarkers(ModelMarker marker)
        {
            ModelNode parentNode = nodes[marker.parentNode];
            marker.arrow.Direction = new Vector3D()
            {
                X = (marker.direction.X + marker.arrow.Origin.X) * parentNode.inverseV.X,
                Y = (marker.direction.Y + marker.arrow.Origin.Y) * parentNode.inverseV.Y,
                Z = (marker.direction.Z + marker.arrow.Origin.Z) * parentNode.inverseV.Z
            };

            marker.arrow.Origin = new Point3D()
            {
                X = (parentNode.cube.Center.X + marker.translation.X) * (parentNode.cube.Width * 10) * 1.5,
                Y = (parentNode.cube.Center.Y + marker.translation.Y) * (parentNode.cube.Width * 10) * 1.5,
                Z = (parentNode.cube.Center.Z + marker.translation.Z) * (parentNode.cube.Width * 10) * 1.5
            };

            marker.arrow.HeadLength = 5;
            marker.arrow.ThetaDiv = 5;

            RotateTransform3D arrowRotate = new RotateTransform3D();
            arrowRotate.Rotation = new QuaternionRotation3D(marker.qRotation);

            ScaleTransform3D arrowScale = new ScaleTransform3D()
            {
                CenterX = (parentNode.cube.Center.X + marker.translation.X) * (parentNode.cube.Width * 10) * 1.5,
                CenterY = (parentNode.cube.Center.Y + marker.translation.Y) * (parentNode.cube.Width * 10) * 1.5,
                CenterZ = (parentNode.cube.Center.Z + marker.translation.Z) * (parentNode.cube.Width * 10) * 1.5,
                ScaleX = 0.075,
                ScaleY = 0.075,
                ScaleZ = 0.075,
            };

            Transform3DGroup arrowTransform = new Transform3DGroup();
            arrowTransform.Children.Add(arrowRotate);
            arrowTransform.Children.Add(arrowScale);
            marker.arrow.Transform = arrowTransform;
        }

        private void UpdateNode(ModelNode node)
        {
            string nodeOffset = (node.id * 32).ToString("X");
            string nodeAdd = m.Get64BitCode(renderAddress + "+0x1EC,0x" + nodeOffset).ToString("X");

            string nodeInvOff = (node.id * 92).ToString("X");
            string nodeInvAdd = m.Get64BitCode(modelAddress + "+0x240,0x" + nodeInvOff).ToString("X");

            float scale = m.ReadFloat(nodeAdd + "+0x1C");
            Vector3 translation = new Vector3(m.ReadFloat(nodeAdd + "+0x10", round: false), m.ReadFloat(nodeAdd + "+0x14", round: false), m.ReadFloat(nodeAdd + "+0x18", round: false));
            Vector3 inverse = new Vector3(m.ReadFloat(nodeInvAdd + "+0x2C", round: false), m.ReadFloat(nodeInvAdd + "+0x3C", round: false), m.ReadFloat(nodeInvAdd + "+0x4C", round: false));
            Vector3 inverseV = new Vector3(1, 1, 1);
            if (inverse.X < 0)
                inverseV.X = -1;
            if (inverse.Y < 0)
                inverseV.Y = -1;
            if (inverse.Z < 0)
                inverseV.Z = -1;
            Quaternion qRotation = new Quaternion(m.ReadFloat(nodeAdd, round: false), m.ReadFloat(nodeAdd + "+0x4", round: false), m.ReadFloat(nodeAdd + "+0x8", round: false), m.ReadFloat(nodeAdd + "+0xC", round: false));
            Vector3 rRotation = ToEulerAngles(qRotation);
            Vector3 dRotation = ToDegree(rRotation);

            Quaternion invRotation = new Quaternion(m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 24).ToString("X"), round: false), m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 28).ToString("X"), round: false), m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 32).ToString("X"), round: false), m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 36).ToString("X"), round: false));
            Vector3 invForward = new Vector3(m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 40).ToString("X"), round: false), m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 44).ToString("X"), round: false), m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 48).ToString("X"), round: false));
            Vector3 invLeft = new Vector3(m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 52).ToString("X"), round: false), m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 56).ToString("X"), round: false), m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 60).ToString("X"), round: false));
            Vector3 invUp = new Vector3(m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 64).ToString("X"), round: false), m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 68).ToString("X"), round: false), m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 72).ToString("X"), round: false));
            Vector3 invPosition = new Vector3(m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 76).ToString("X"), round: false), m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 80).ToString("X"), round: false), m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 84).ToString("X"), round: false));
            float invScale = m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 88).ToString("X"), round: false);
            float DFP = m.ReadFloat(node.nodeAddress2 + "+0x" + ((node.id * 124) + 92).ToString("X"), round: false);

            node.nodeAddress = nodeAdd;
            node.nodeInvAddress = nodeInvAdd;
            node.translation = translation;
            node.qRotation = qRotation;
            node.rRotation = rRotation;
            node.dRotation = dRotation;
            node.scale = scale;
            node.invQRotation = invRotation;
            node.invRRotation = ToEulerAngles(invRotation);
            node.invDRotation = ToDegree(node.invRRotation);
            node.invForward = invForward;
            node.invLeft = invLeft;
            node.invUp = invUp;
            node.invPosition = invPosition;
            node.invScale = invScale;
            node.DFP = DFP;
            node.inverse = inverse;
            node.inverseV = inverseV;
        }

        private void UpdateMarker(ModelMarker marker)
        {
            Vector3 markerTrans = new Vector3(m.ReadFloat(marker.address + "+0xC"), m.ReadFloat(marker.address + "+0x10"), m.ReadFloat(marker.address + "+0x14"));
            Quaternion markerRotation = new Quaternion(m.ReadFloat(marker.address + "+0x18"), m.ReadFloat(marker.address + "+0x1C"), m.ReadFloat(marker.address + "+0x20"), m.ReadFloat(marker.address + "+0x24"));
            Vector3 markerDirection = new Vector3(m.ReadFloat(marker.address + "+0x2C"), m.ReadFloat(marker.address + "+0x30"), m.ReadFloat(marker.address + "+0x34"));

            marker.translation = markerTrans;
            marker.direction = markerDirection;
            marker.qRotation = markerRotation;
            marker.rRotation = ToEulerAngles(markerRotation);
            marker.dRotation = ToDegree(marker.rRotation);
        }
        #endregion


        #region Undo
        public void UndoObjChanges(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var t = button.Tag.GetType();
            try
            {
                if (t.Equals(typeof(ModelNode)))
                {
                    ModelNode node = (ModelNode)button.Tag;

                    m.WriteMemory(node.nodeAddress, "float", node.oRot.X.ToString());
                    m.WriteMemory(node.nodeAddress + "+0x4", "float", node.oRot.Y.ToString());
                    m.WriteMemory(node.nodeAddress + "+0x8", "float", node.oRot.Z.ToString());
                    m.WriteMemory(node.nodeAddress + "+0xC", "float", node.oRot.W.ToString());
                    m.WriteMemory(node.nodeAddress + "+0x10", "float", node.oTran.X.ToString());
                    m.WriteMemory(node.nodeAddress + "+0x14", "float", node.oTran.Y.ToString());
                    m.WriteMemory(node.nodeAddress + "+0x18", "float", node.oTran.Z.ToString());
                    m.WriteMemory(node.nodeAddress + "+0x1C", "float", node.oScale.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 40).ToString("X"), "float", node.oInvForward.X.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 44).ToString("X"), "float", node.oInvForward.Y.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 48).ToString("X"), "float", node.oInvForward.Z.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 52).ToString("X"), "float", node.oInvLeft.X.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 56).ToString("X"), "float", node.oInvLeft.Y.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 60).ToString("X"), "float", node.oInvLeft.Z.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 64).ToString("X"), "float", node.oInvUp.X.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 68).ToString("X"), "float", node.oInvUp.Y.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 72).ToString("X"), "float", node.oInvUp.Z.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 76).ToString("X"), "float", node.oInvPosition.X.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 80).ToString("X"), "float", node.oInvPosition.Y.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 84).ToString("X"), "float", node.oInvPosition.Z.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 88).ToString("X"), "float", node.oInvScale.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 92).ToString("X"), "float", node.oDFP.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 24).ToString("X"), "float", node.oInvRotation.X.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 28).ToString("X"), "float", node.oInvRotation.Y.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 32).ToString("X"), "float", node.oInvRotation.Z.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 36).ToString("X"), "float", node.oInvRotation.W.ToString());
                    UpdateNode(node);
                    UpdateNodes(node);

                    node.item.RaiseEvent(new RoutedEventArgs(TreeViewItem.SelectedEvent));
                    statusText.Text = "Node reset...";
                }
                else if (t.Equals(typeof(ModelMarker)))
                {
                    ModelMarker marker = (ModelMarker)button.Tag;

                    m.WriteMemory(marker.address + "+0xC", "float", marker.oTran.X.ToString());
                    m.WriteMemory(marker.address + "+0x10", "float", marker.oTran.Y.ToString());
                    m.WriteMemory(marker.address + "+0x14", "float", marker.oTran.Z.ToString());
                    m.WriteMemory(marker.address + "+0x18", "float", marker.oRot.X.ToString());
                    m.WriteMemory(marker.address + "+0x1C", "float", marker.oRot.Y.ToString());
                    m.WriteMemory(marker.address + "+0x20", "float", marker.oRot.Z.ToString());
                    m.WriteMemory(marker.address + "+0x24", "float", marker.oRot.W.ToString());
                    m.WriteMemory(marker.address + "+0x2C", "float", marker.oDir.X.ToString());
                    m.WriteMemory(marker.address + "+0x30", "float", marker.oDir.Y.ToString());
                    m.WriteMemory(marker.address + "+0x34", "float", marker.oDir.Z.ToString());

                    UpdateMarker(marker);
                    UpdateNodes(nodes[marker.parentNode]);
                    marker.item.RaiseEvent(new RoutedEventArgs(TreeViewItem.SelectedEvent));
                    statusText.Text = "Marker reset...";
                }
            }
            catch (Exception ex)
            {
                statusText.Text = "Error undoing node changes!";
                Debug.WriteLine(ex.Message);
            }
        }

        private void UndoAllChanges(object sender, RoutedEventArgs e)
        {
            propTab.Children.Clear();
            try
            {
                foreach (ModelNode node in nodes.Values)
                {
                    m.WriteMemory(node.nodeAddress, "float", node.oRot.X.ToString());
                    m.WriteMemory(node.nodeAddress + "+0x4", "float", node.oRot.Y.ToString());
                    m.WriteMemory(node.nodeAddress + "+0x8", "float", node.oRot.Z.ToString());
                    m.WriteMemory(node.nodeAddress + "+0xC", "float", node.oRot.W.ToString());
                    m.WriteMemory(node.nodeAddress + "+0x10", "float", node.oTran.X.ToString());
                    m.WriteMemory(node.nodeAddress + "+0x14", "float", node.oTran.Y.ToString());
                    m.WriteMemory(node.nodeAddress + "+0x18", "float", node.oTran.Z.ToString());
                    m.WriteMemory(node.nodeAddress + "+0x1C", "float", node.oScale.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 40).ToString("X"), "float", node.oInvForward.X.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 44).ToString("X"), "float", node.oInvForward.Y.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 48).ToString("X"), "float", node.oInvForward.Z.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 52).ToString("X"), "float", node.oInvLeft.X.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 56).ToString("X"), "float", node.oInvLeft.Y.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 60).ToString("X"), "float", node.oInvLeft.Z.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 64).ToString("X"), "float", node.oInvUp.X.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 68).ToString("X"), "float", node.oInvUp.Y.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 72).ToString("X"), "float", node.oInvUp.Z.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 76).ToString("X"), "float", node.oInvPosition.X.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 80).ToString("X"), "float", node.oInvPosition.Y.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 84).ToString("X"), "float", node.oInvPosition.Z.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 88).ToString("X"), "float", node.oInvScale.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 92).ToString("X"), "float", node.oDFP.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 24).ToString("X"), "float", node.oInvRotation.X.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 28).ToString("X"), "float", node.oInvRotation.Y.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 32).ToString("X"), "float", node.oInvRotation.Z.ToString());
                    m.WriteMemory(node.nodeAddress2 + "+0x" + ((node.id * 124) + 36).ToString("X"), "float", node.oInvRotation.W.ToString());
                }

                foreach (ModelMarker marker in markers.Values)
                {
                    m.WriteMemory(marker.address + "+0xC", "float", marker.oTran.X.ToString());
                    m.WriteMemory(marker.address + "+0x10", "float", marker.oTran.Y.ToString());
                    m.WriteMemory(marker.address + "+0x14", "float", marker.oTran.Z.ToString());
                    m.WriteMemory(marker.address + "+0x18", "float", marker.oRot.X.ToString());
                    m.WriteMemory(marker.address + "+0x1C", "float", marker.oRot.Y.ToString());
                    m.WriteMemory(marker.address + "+0x20", "float", marker.oRot.Z.ToString());
                    m.WriteMemory(marker.address + "+0x24", "float", marker.oRot.W.ToString());
                    m.WriteMemory(marker.address + "+0x2C", "float", marker.oDir.X.ToString());
                    m.WriteMemory(marker.address + "+0x30", "float", marker.oDir.Y.ToString());
                    m.WriteMemory(marker.address + "+0x34", "float", marker.oDir.Z.ToString());
                }


                LoadModel();
                statusText.Text = "Model reset...";
            }
            catch (Exception ex)
            {
                statusText.Text = "Error resetting model...";
                Debug.WriteLine(ex.Message);
            }

        }
        #endregion


        #region Hide Objects
        private void NodeHideCheck(object sender, RoutedEventArgs e)
        {
            MenuItem mItem = sender as MenuItem;
            try
            {
                if (mItem.IsChecked)
                {
                    foreach (ModelNode node in nodes.Values)
                    {
                        node.cube.Visible = false;
                    }
                    statusText.Text = "Nodes are now invisible...";
                }
                else
                {
                    foreach (ModelNode node in nodes.Values)
                    {
                        node.cube.Visible = true;
                    }
                    statusText.Text = "Nodes are now visible...";
                }
            }
            catch (Exception ex)
            {
                statusText.Text = "Error hiding nodes!";
                Debug.WriteLine(ex.Message);
            }
        }

        private void MarkerHideCheck(object sender, RoutedEventArgs e)
        {
            MenuItem mItem = sender as MenuItem;
            try
            {
                if (mItem.IsChecked)
                {
                    foreach (ModelMarker marker in markers.Values)
                    {
                        marker.arrow.Visible = false;
                    }
                    statusText.Text = "Nodes are now invisible...";
                }
                else
                {
                    foreach (ModelMarker marker in markers.Values)
                    {
                        marker.arrow.Visible = true;
                    }
                    statusText.Text = "Markers are now visible...";
                }
            }
            catch (Exception ex)
            {
                statusText.Text = "Error hiding nodes!";
                Debug.WriteLine(ex.Message);
            }
}
        #endregion


        #region Create Controls
        private void CreateValueBlock(string blockType, float xValue, float yValue, float zValue, string objType, int index)
        {
            if (blockType == "translation")
            {
                translationBlock transBlock = new translationBlock();
                transBlock.xValue.Tag = objType + ":transX:" + index;
                transBlock.yValue.Tag = objType + ":transY:" + index;
                transBlock.zValue.Tag = objType + ":transZ:" + index;
                transBlock.xValue.Text = xValue.ToString("0.000");
                transBlock.yValue.Text = yValue.ToString("0.000");
                transBlock.zValue.Text = zValue.ToString("0.000");
                transBlock.xSlider.Value = xValue;
                transBlock.ySlider.Value = yValue;
                transBlock.zSlider.Value = zValue;
                transBlock.xValue.TextChanged += Update;
                transBlock.yValue.TextChanged += Update;
                transBlock.zValue.TextChanged += Update;
                transBlock.xValue.LostFocus += ValueChange;
                transBlock.yValue.LostFocus += ValueChange;
                transBlock.zValue.LostFocus += ValueChange;
                transBlock.xSlider.ValueChanged += SliderChange;
                transBlock.ySlider.ValueChanged += SliderChange;
                transBlock.zSlider.ValueChanged += SliderChange;
                propTab.Children.Add(transBlock);
            }
            else if (blockType == "rotation")
            {
                translationBlock rotBlock = new translationBlock();
                rotBlock.title.Text = "Rotation";
                rotBlock.xValue.Tag = objType + ":rotX:" + index;
                rotBlock.yValue.Tag = objType + ":rotY:" + index;
                rotBlock.zValue.Tag = objType + ":rotZ:" + index;
                rotBlock.xValue.Text = xValue.ToString("0.000");
                rotBlock.yValue.Text = yValue.ToString("0.000");
                rotBlock.zValue.Text = zValue.ToString("0.000");
                rotBlock.xSlider.Value = xValue;
                rotBlock.ySlider.Value = yValue;
                rotBlock.zSlider.Value = zValue;
                rotBlock.xSlider.Minimum = -180;
                rotBlock.ySlider.Minimum = -90;
                rotBlock.zSlider.Minimum = -180;
                rotBlock.xSlider.Maximum = 180;
                rotBlock.ySlider.Maximum = 90;
                rotBlock.zSlider.Maximum = 180;
                rotBlock.xValue.TextChanged += Update;
                rotBlock.yValue.TextChanged += Update;
                rotBlock.zValue.TextChanged += Update;
                rotBlock.xValue.LostFocus += ValueChange;
                rotBlock.yValue.LostFocus += ValueChange;
                rotBlock.zValue.LostFocus += ValueChange;
                rotBlock.xSlider.ValueChanged += SliderChange;
                rotBlock.ySlider.ValueChanged += SliderChange;
                rotBlock.zSlider.ValueChanged += SliderChange;
                propTab.Children.Add(rotBlock);
            }
            else if (blockType == "direction")
            {
                translationBlock dirBlock = new translationBlock();
                dirBlock.title.Text = "Direction";
                dirBlock.xValue.Tag = objType + ":dirX:" + index;
                dirBlock.yValue.Tag = objType + ":dirY:" + index;
                dirBlock.zValue.Tag = objType + ":dirZ:" + index;
                dirBlock.xValue.Text = xValue.ToString("0.000");
                dirBlock.yValue.Text = yValue.ToString("0.000");
                dirBlock.zValue.Text = zValue.ToString("0.000");
                dirBlock.xSlider.Value = xValue;
                dirBlock.ySlider.Value = yValue;
                dirBlock.zSlider.Value = zValue;
                dirBlock.xValue.TextChanged += Update;
                dirBlock.yValue.TextChanged += Update;
                dirBlock.zValue.TextChanged += Update;
                dirBlock.xValue.LostFocus += ValueChange;
                dirBlock.yValue.LostFocus += ValueChange;
                dirBlock.zValue.LostFocus += ValueChange;
                dirBlock.xSlider.ValueChanged += SliderChange;
                dirBlock.ySlider.ValueChanged += SliderChange;
                dirBlock.zSlider.ValueChanged += SliderChange;
                propTab.Children.Add(dirBlock);
            }
        }

        private void CreateScaleBlock(float value, string objType, int index)
        {
            scaleBlock scale = new scaleBlock();
            scale.scaleValue.Text = value.ToString("0.000");
            scale.scaleSlider.Value = value;
            scale.scaleValue.TextChanged += Update;
            scale.scaleValue.LostFocus += ValueChange;
            scale.scaleSlider.ValueChanged += SliderChange;
            scale.scaleValue.Tag = objType + ":scale:" + index;
            propTab.Children.Add(scale);
        }

        private void CreateHashBlock(string value, int index)
        {
            hashBlock hash = new hashBlock();
            hash.hashValue.Text = value.ToString();
            hash.Tag = index;
            propTab.Children.Add(hash);
        }

        private void CreateInvBlock(string description, float Value1, float Value2, float Value3, int index)
        {
            if (description == "Dimensions")
            {
                translationBlock newBlock = new translationBlock();
                newBlock.title.Text = description;
                newBlock.xValue.Tag = "n:invForwardY:" + index;
                newBlock.yValue.Tag = "n:invLeftZ:" + index;
                newBlock.zValue.Tag = "n:invPositionX:" + index;
                newBlock.value1.Text = "L";
                newBlock.value2.Text = "W";
                newBlock.value3.Text = "H";
                newBlock.xValue.Text = Value1.ToString("0.000");
                newBlock.yValue.Text = Value2.ToString("0.000");
                newBlock.zValue.Text = Value3.ToString("0.000");
                newBlock.xSlider.Value = Value1;
                newBlock.ySlider.Value = Value2;
                newBlock.zSlider.Value = Value3;
                newBlock.xValue.TextChanged += Update;
                newBlock.yValue.TextChanged += Update;
                newBlock.zValue.TextChanged += Update;
                newBlock.xValue.LostFocus += ValueChange;
                newBlock.yValue.LostFocus += ValueChange;
                newBlock.zValue.LostFocus += ValueChange;
                newBlock.xSlider.ValueChanged += SliderChange;
                newBlock.ySlider.ValueChanged += SliderChange;
                newBlock.zSlider.ValueChanged += SliderChange;

                otherPropTab.Children.Add(newBlock);
            }
            else if (description == "Translations")
            {
                translationBlock newBlock = new translationBlock();
                newBlock.title.Text = description;
                newBlock.xValue.Tag = "n:invPositionY:" + index;
                newBlock.yValue.Tag = "n:invPositionZ:" + index;
                newBlock.zValue.Tag = "n:invScale:" + index;
                newBlock.value1.Text = "X";
                newBlock.value2.Text = "Y";
                newBlock.value3.Text = "Z";
                newBlock.xValue.Text = Value1.ToString("0.000");
                newBlock.yValue.Text = Value2.ToString("0.000");
                newBlock.zValue.Text = Value3.ToString("0.000");
                newBlock.xSlider.Value = Value1;
                newBlock.ySlider.Value = Value2;
                newBlock.zSlider.Value = Value3;
                newBlock.xValue.TextChanged += Update;
                newBlock.yValue.TextChanged += Update;
                newBlock.zValue.TextChanged += Update;
                newBlock.xValue.LostFocus += ValueChange;
                newBlock.yValue.LostFocus += ValueChange;
                newBlock.zValue.LostFocus += ValueChange;
                newBlock.xSlider.ValueChanged += SliderChange;
                newBlock.ySlider.ValueChanged += SliderChange;
                newBlock.zSlider.ValueChanged += SliderChange;

                otherPropTab.Children.Add(newBlock);
            }
            else if (description == "Skew From Center")
            {
                translationBlock newBlock = new translationBlock();
                newBlock.title.Text = description;
                newBlock.xValue.Tag = "n:invUpY:" + index;
                newBlock.yValue.Tag = "n:invUpZ:" + index;
                newBlock.zValue.Tag = "n:invLeftX:" + index;
                newBlock.value1.Text = "X";
                newBlock.value2.Text = "Y";
                newBlock.value3.Text = "Z";
                newBlock.xValue.Text = Value1.ToString("0.000");
                newBlock.yValue.Text = Value2.ToString("0.000");
                newBlock.zValue.Text = Value3.ToString("0.000");
                newBlock.xSlider.Value = Value1;
                newBlock.ySlider.Value = Value2;
                newBlock.zSlider.Value = Value3;
                newBlock.xValue.TextChanged += Update;
                newBlock.yValue.TextChanged += Update;
                newBlock.zValue.TextChanged += Update;
                newBlock.xValue.LostFocus += ValueChange;
                newBlock.yValue.LostFocus += ValueChange;
                newBlock.zValue.LostFocus += ValueChange;
                newBlock.xSlider.ValueChanged += SliderChange;
                newBlock.ySlider.ValueChanged += SliderChange;
                newBlock.zSlider.ValueChanged += SliderChange;

                otherPropTab.Children.Add(newBlock);
            }
            else if (description == "Inverse Skew")
            {
                translationBlock newBlock = new translationBlock();
                newBlock.title.Text = description;
                newBlock.xValue.Tag = "n:invLeftY:" + index;
                newBlock.yValue.Tag = "n:invForwardZ:" + index;
                newBlock.zValue.Tag = "n:invUpX:" + index;
                newBlock.value1.Text = "X";
                newBlock.value2.Text = "Y";
                newBlock.value3.Text = "Z";
                newBlock.xValue.Text = Value1.ToString("0.000");
                newBlock.yValue.Text = Value2.ToString("0.000");
                newBlock.zValue.Text = Value3.ToString("0.000");
                newBlock.xSlider.Value = Value1;
                newBlock.ySlider.Value = Value2;
                newBlock.zSlider.Value = Value3;
                newBlock.xValue.TextChanged += Update;
                newBlock.yValue.TextChanged += Update;
                newBlock.zValue.TextChanged += Update;
                newBlock.xValue.LostFocus += ValueChange;
                newBlock.yValue.LostFocus += ValueChange;
                newBlock.zValue.LostFocus += ValueChange;
                newBlock.xSlider.ValueChanged += SliderChange;
                newBlock.ySlider.ValueChanged += SliderChange;
                newBlock.zSlider.ValueChanged += SliderChange;

                otherPropTab.Children.Add(newBlock);
            }
            else if (description == "Rotations")
            {
                translationBlock newBlock = new translationBlock();
                newBlock.title.Text = description;
                newBlock.xValue.Tag = "n:invRotationsX:" + index;
                newBlock.yValue.Tag = "n:invRotationsY:" + index;
                newBlock.zValue.Tag = "n:invRotationsZ:" + index;
                newBlock.value1.Text = "X";
                newBlock.value2.Text = "Y";
                newBlock.value3.Text = "Z";
                newBlock.xValue.Text = Value1.ToString("0.000");
                newBlock.yValue.Text = Value2.ToString("0.000");
                newBlock.zValue.Text = Value3.ToString("0.000");
                newBlock.xSlider.Value = Value1;
                newBlock.ySlider.Value = Value2;
                newBlock.zSlider.Value = Value3;
                newBlock.xSlider.Minimum = -180;
                newBlock.xSlider.Maximum = 180;
                newBlock.ySlider.Minimum = -90;
                newBlock.ySlider.Maximum = 90;
                newBlock.zSlider.Minimum = -180;
                newBlock.zSlider.Maximum = 180;
                newBlock.xValue.TextChanged += Update;
                newBlock.yValue.TextChanged += Update;
                newBlock.zValue.TextChanged += Update;
                newBlock.xValue.LostFocus += ValueChange;
                newBlock.yValue.LostFocus += ValueChange;
                newBlock.zValue.LostFocus += ValueChange;
                newBlock.xSlider.ValueChanged += SliderChange;
                newBlock.ySlider.ValueChanged += SliderChange;
                newBlock.zSlider.ValueChanged += SliderChange;

                otherPropTab.Children.Add(newBlock);
            }
        }

        private void CreateSingleInvBlock(string title, float value, int index, string tagType)
        {
            scaleBlock singleBlock = new scaleBlock();
            singleBlock.title.Text = title;
            singleBlock.scaleValue.Text = value.ToString("0.000");
            singleBlock.scaleSlider.Value = value;
            singleBlock.scaleValue.TextChanged += Update;
            singleBlock.scaleValue.LostFocus += ValueChange;
            singleBlock.scaleSlider.ValueChanged += SliderChange;
            singleBlock.scaleValue.Tag = "n:" + tagType + ":" + index;
            otherPropTab.Children.Add(singleBlock);
        }
        #endregion


        #region Selections
        private void ShowSelection(ModelNode node, ModelMarker marker)
        {
            foreach (ModelNode nodeStruct in nodes.Values)
            {
                nodeStruct.cube.Material = new DiffuseMaterial(nodeStruct.material);
            }
            foreach (ModelMarker markerStruct in markers.Values)
            {
                markerStruct.arrow.Material = new DiffuseMaterial(Brushes.Red);
            }

            if (marker.arrow == null)
            {
                node.cube.Material = new DiffuseMaterial(Brushes.Green);
                if (node.cube.Children.Count > 0)
                {
                    ShowSelectionChild(node);
                }
            }
            else if (node.cube == null)
            {
                marker.arrow.Material = new DiffuseMaterial(Brushes.Green);
            }
        }
        
        private void ShowSelectionChild(ModelNode node)
        {
            BoxVisual3D parentCube = node.cube;

            foreach (var child in parentCube.Children)
            {
                Type t = child.GetType();
                if (t == typeof(BoxVisual3D))
                {
                    BoxVisual3D childCube = child as BoxVisual3D;
                    childCube.Material = new DiffuseMaterial(Brushes.YellowGreen);

                    if (childCube.Children.Count > 0)
                    {
                        ShowSelectionChild(nodes[int.Parse(childCube.GetName())]);
                    }
                }
                else if (t == typeof(ArrowVisual3D))
                {
                    ArrowVisual3D childArrow = child as ArrowVisual3D;
                    childArrow.Material = new DiffuseMaterial(Brushes.YellowGreen);
                }
            }
        }
        #endregion


        #region Converters
        public static Vector3 ToEulerAngles(Quaternion q)
        {
            // Convert X Angle
            float sinr_cosp = (float)(2 * (-q.W * q.X + q.Y * q.Z));
            float cosr_cosp = (float)(1 - 2 * (q.X * q.X + q.Y * q.Y));
            float xAngle = (float)Math.Atan2(sinr_cosp, cosr_cosp) * -1;

            // Convert Y Angle
            float sinp = (float)(2 * (-q.W * q.Y - q.Z * q.X));
            float yAngle;
            if (Math.Abs(sinp) >= 1)
                yAngle = (float)(Math.PI / 2 * Math.Sign(sinp)) * -1;
            else
                yAngle = (float)Math.Asin(sinp) * -1;

            // Convert Z Angle
            float siny_cosp = (float)(2 * (-q.W * q.Z + q.X * q.Y));
            float cosy_cosp = (float)(1 - 2 * (q.Y * q.Y + q.Z * q.Z));
            float zAngle = (float)Math.Atan2(siny_cosp, cosy_cosp) * -1;
           
            return new Vector3((float)Math.Round(xAngle, 2), (float)Math.Round(yAngle, 2), (float)Math.Round(zAngle, 2));
        }

        public static Vector3 ToDegree(Vector3 r)
        {
            float xDeg = (float)(r.X * 180 / Math.PI);
            float yDeg = (float)(r.Y * 180 / Math.PI);
            float zDeg = (float)(r.Z * 180 / Math.PI);

            return new Vector3(xDeg, yDeg, zDeg);
        }

        public static Vector3 ToRadian(Vector3 d)
        {
            float xRad = (float)(d.X * Math.PI / 180);
            float yRad = (float)(d.Y * Math.PI / 180);
            float zRad = (float)(d.Z * Math.PI / 180);

            return new Vector3((float)Math.Round(xRad, 2), (float)Math.Round(yRad, 2), (float)Math.Round(zRad, 2));
        }

        public static Quaternion ToQuaternion(Vector3 e)
        {
            double cy = Math.Cos(e.Z * 0.5);
            double sy = Math.Sin(e.Z * 0.5);
            double cp = Math.Cos(e.Y * 0.5);
            double sp = Math.Sin(e.Y * 0.5);
            double cr = Math.Cos(e.X * 0.5);
            double sr = Math.Sin(e.X * 0.5);

            Quaternion q = new Quaternion
            {
                W = cr * cp * cy + sr * sp * sy * -1,
                X = sr * cp * cy - cr * sp * sy * -1,
                Y = cr * sp * cy + sr * cp * sy * -1,
                Z = cr * cp * sy - sr * sp * cy * -1 
            };

            return q;
        }
        #endregion


        #region Modified IRTV Tag Loader & Other stuff
        // Credit for the majority of this section goes to Gamergotten and all those who've contributed to the github.
        // https://github.com/Gamergotten/Infinite-runtime-tagviewer

        public bool loadedTags = false;
        public bool hooked = false;
        private int TagCount = -1;
        private long BaseAddress = -1;
        public long aobStart;
        private readonly long AOBScanStartAddr = Convert.ToInt64("0000010000000000", 16);
        private readonly long AOBScanEndAddr = Convert.ToInt64("000003ffffffffff", 16);
        public string ScanMemAOBBaseAddr = "HaloInfinite.exe+0x3612B08";
        public string AOBScanTagStr = "74 61 67 20 69 6E 73 74 61 6E 63 65 73";
        public Dictionary<string, string> InhaledTagnames = new();
        public Dictionary<string, TagStruct> TagsList { get; set; } = new();
        public class TagStruct
        {
            public string Datnum;
            public string ObjectId;
            public string TagGroup;
            public long TagData;
            public string TagTypeDesc;
            public string TagFullName;
            public string TagFile;
            public bool unloaded;
        }

        private void BtnLoadTags_Click(object sender, RoutedEventArgs e)
        {
            renderAddress = string.Empty;
            modelAddress = string.Empty;
            selectedTag = string.Empty;
            tagID = string.Empty;
            nodeCount = 0;
            ModelViewer.Children.Clear();
            node_tree.Items.Clear();
            markers.Clear();
            nodes.Clear();
            changes.Clear();
            materialInd = 0;
            TagsTree.Items.Clear();
            propTab.Children.Clear();
            nodes.Clear();
            markers.Clear();
            otherPropTab.Children.Clear();

            HookAndLoad();
        }

        private void Select_Tag_click(object sender, RoutedEventArgs e)
        {
            TreeViewItem? item = sender as TreeViewItem;
            selectedTag = item.Tag.ToString();
            LoadModel();
        }

        public async void HookAndLoad()
        {
            try
            {
                await HookProcessAsync();
            }
            catch (System.ArgumentNullException)
            {

            }
            if (BaseAddress != -1 && BaseAddress != 0)
            {
                await LoadTagsMem(false);


                if (hooked == true)
                {
                    Searchbox_TextChanged(null, null);

                    System.Diagnostics.Debugger.Log(0, "DBGTIMING", "Done loading tags");

                }
            }
        }

        private async Task HookProcessAsync()
        {
            try
            {
                if (!hooked)
                {
                    m.OpenProcess("HaloInfinite.exe");
                    BaseAddress = m.ReadLong("HaloInfinite.exe+0x41A79E8");

                    string validtest = m.ReadString(BaseAddress.ToString("X"));
                    Debug.WriteLine(validtest);
                    if (validtest == "tag instances")
                    {
                        statusText.Text = "Process Hooked: " + m.mProc.Process.Id;
                        hooked = true;
                    }
                    else
                    {
                        statusText.Text = "Offset failed, scanning...";
                        await ScanMem();
                    }
                }
            }
            catch (Exception ex)
            {
                statusText.Text = "Error hooking to halo infinite!";
                Debug.WriteLine(ex.Message);
            }
        }

        public async Task LoadTagsMem(bool is_silent)
        {
            await Task.Run((Action)(() =>
            {
                if (TagCount != -1)
                {
                    TagCount = -1;
                    TagsList.Clear();
                }
                TagCount = this.m.ReadInt((BaseAddress + 0x6C).ToString("X"));
                long tagsStart = this.m.ReadLong((BaseAddress + 0x78).ToString("X"));

                TagsList = new Dictionary<string, TagStruct>();
                for (int tagIndex = 0; tagIndex < TagCount; tagIndex++)
                {
                    TagStruct currentTag = new();
                    long tagAddress = tagsStart + (tagIndex * 52);

                    byte[] test1 = this.m.ReadBytes(tagAddress.ToString("X"), 4);
                    try
                    {
                        currentTag.Datnum = BitConverter.ToString(test1).Replace("-", string.Empty);
                        loadedTags = false;
                    }
                    catch (System.ArgumentNullException)
                    {
                        hooked = false;
                        return;
                    }
                    byte[] test = (this.m.ReadBytes((tagAddress + 4).ToString("X"), 4));

                    currentTag.ObjectId = BitConverter.ToString(test).Replace("-", string.Empty);
                    currentTag.TagData = this.m.ReadLong((tagAddress + 0x10).ToString("X"));
                    currentTag.TagFullName = convert_ID_to_tag_name(currentTag.ObjectId).Trim();
                    currentTag.TagFile = currentTag.TagFullName.Split('\\').Last<string>().Trim();

                    if (!TagsList.ContainsKey(currentTag.ObjectId))
                    {
                        TagsList.Add(currentTag.ObjectId, currentTag);
                    }
                }
            }));
            if (!is_silent)
                await Loadtags();
        }

        private void Searchbox_TextChanged(object? sender, TextChangedEventArgs? e)
        {
            string search = Searchbox.Text;
            foreach (TreeViewItem? tv in TagsTree.Items)
            {
                if (!tv.Header.ToString().Contains(search.ToLower()))
                {
                    tv.Visibility = Visibility.Collapsed;
                    foreach (TreeViewItem tc in tv.Items)
                    {
                        if (tc.Header.ToString().Contains(search))
                        {
                            tc.Visibility = Visibility.Visible;
                            tv.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            tc.Visibility = Visibility.Collapsed;
                        }
                    }
                }
                else
                {
                    tv.Visibility = Visibility.Visible;
                    foreach (TreeViewItem tc in tv.Items)
                    {
                        tc.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void Searchbox2_TextChanged(object? sender, TextChangedEventArgs? e)
        {
            string search = Searchbox2.Text;
            foreach (TreeViewItem tc in node_tree.Items)
            {
                SearchBox2Search(tc, search);
            }
        }

        private void SearchBox2Search(TreeViewItem tv, string search)
        {
            if (string.IsNullOrEmpty(search) || string.IsNullOrWhiteSpace(search))
            {
                foreach (TreeViewItem tc in tv.Items)
                {
                    tc.Visibility = Visibility.Visible;
                    SearchBox2Search(tc, search);
                }
            }
            else if (!tv.Header.ToString().ToLower().Contains(search.ToLower()))
            {
                if (tv.HasItems)
                {
                    tv.IsExpanded = true;
                }
                else
                {
                    tv.Visibility = Visibility.Collapsed;
                }
                foreach (TreeViewItem tc in tv.Items)
                {
                    if (tc.Header.ToString().ToLower().Contains(search.ToLower()))
                    {
                        HideAllItems(tc);
                    }
                    if (tc.HasItems)
                    {
                        SearchBox2Search(tc, search);
                    }
                }
            }
            
        }

        private void HideAllItems(TreeViewItem ignore)
        {
            var tpTest = ignore.Parent;
            Type t = tpTest.GetType();
            if (t.Equals(typeof(TreeViewItem)))
            {
                TreeViewItem tp = tpTest as TreeViewItem;
                foreach (TreeViewItem tc in tp.Items)
                {
                    tc.Visibility = Visibility.Collapsed;
                    HideAllItems(tp);
                }
                tp.Visibility = Visibility.Visible;
            }
            ignore.Visibility = Visibility.Visible;
        }

        public async Task ScanMem()
        {
            BaseAddress = m.ReadLong(ScanMemAOBBaseAddr);
            string validtest = m.ReadString(BaseAddress.ToString("X"));

            if (validtest == "tag instances")
            {
                statusText.Text = "Process Hooked: " + m.mProc.Process.Id;
                hooked = true;
            }
            else
            {
                statusText.Text = "Offset failed, scanning...";
                try
                {
                    long? aobScan = (await m.AoBScan(AOBScanStartAddr, AOBScanEndAddr, AOBScanTagStr, true))
                        .First();

                    long haloInfinite = 0;
                    if (aobScan != null)
                    {
                        foreach (Process process in Process.GetProcessesByName("HaloInfinite"))
                        {
                            haloInfinite = (long)process.MainModule.BaseAddress;
                        }
                        string aobHex = aobScan.Value.ToString("X");
                        IEnumerable<string> aobStr = SplitThis("0" + aobHex, 2);
                        IEnumerable<string> aobReversed = aobStr.Reverse().ToArray();
                        string aobSingle = string.Join("", aobReversed);
                        aobSingle = Regex.Replace(aobSingle, ".{2}", "$0 ");
                        aobSingle = aobSingle.TrimEnd();
                        Debugger.Log(0, "DBGTIMING", "AOB: " + aobSingle);
                        long pointer = (await m.AoBScan(haloInfinite, 140737488289791, aobSingle + " 00 00", true, true, true)).First();
                    }

                    if (aobScan == null || aobScan == 0)
                    {
                        BaseAddress = -1;
                        loadedTags = false;
                        statusText.Text = "Failed to locate base tag address";
                    }
                    else
                    {
                        BaseAddress = aobScan.Value;
                        statusText.Text = "Process Hooked: " + m.mProc.Process.Id + " (AOB)";
                        hooked = true;
                    }
                }
                catch (Exception)
                {
                    statusText.Text = "Cant find HaloInfinite.exe";
                }
            }
        }

        public string convert_ID_to_tag_name(string value)
        {
            _ = InhaledTagnames.TryGetValue(value, value: out string? potentialName);

            return potentialName ??= "None";
        }

        public async Task Loadtags()
        {
            Dictionary<string, TreeViewItem> groups_headers_diff = new();

            await Task.Run(async () =>
            {
                loadedTags = true;

                Dictionary<string, TreeViewItem> tags_headers_diff = new();

                int iteration = 0;
                foreach (KeyValuePair<string, TagStruct> curr_tag in TagsList.OrderBy(key => key.Value.TagFullName))
                {
                    iteration += 1;
                    if (!curr_tag.Value.unloaded)
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                                string tagName = convert_ID_to_tag_name(curr_tag.Key);

                                if (tagName != "None" && (tagName.EndsWith(".model ") || tagName.EndsWith(".model")))
                                {
                                    TreeViewItem t = new();
                                    TagStruct tag = curr_tag.Value;

                                    string[] nameSegments = tagName.Split("\\");
                                    string nameSpaces = nameSegments[nameSegments.Length - 2].Replace("_", " ");

                                    string newName = nameSpaces;

                                    t.Header = newName;

                                    t.Tag = curr_tag.Key;

                                    t.Selected += Select_Tag_click;
                                    t.Style = Resources["TreeViewItemStyle"] as Style;
                                    TagsTree.Items.Add(t);

                                    tags_headers_diff.Add(curr_tag.Key, t);
                                }
                        }));
                        if (iteration > 200)
                        {
                            Thread.Sleep(1);
                            iteration = 0;
                        }
                    }
                }

                if (TagsTree.Items.Count < 1)
                {
                    loadedTags = false;
                }
                Dispatcher.Invoke(new Action(() =>
                {
                    statusText.Text = "Models loaded";
                    TagsTree.Items.SortDescriptions.Add(new SortDescription("Header", ListSortDirection.Ascending));
                }));
            });
        }

        public static IEnumerable<string> SplitThis(string str, int n)
        {
            return Enumerable.Range(0, str.Length / n)
                            .Select(i => str.Substring(i * n, n));
        }

        public static string ReverseString(string myStr)
        {
            char[] myArr = myStr.ToCharArray();
            Array.Reverse(myArr);
            return new string(myArr);
        }

        public void inhale_tagnames()
        {
            string filename = Directory.GetCurrentDirectory() + @"\files\tagnames.txt";
            IEnumerable<string>? lines = System.IO.File.ReadLines(filename);
            foreach (string? line in lines)
            {
                string[] hexString = line.Split(" : ");
                if (!InhaledTagnames.ContainsKey(hexString[0]))
                {
                    InhaledTagnames.Add(hexString[0], hexString[1]);
                }
            }
        }

        public string get_tagid_by_datnum(string datnum)
        {
            foreach (KeyValuePair<string, TagStruct> t in TagsList)
            {
                if (t.Value.Datnum == datnum)
                {
                    return t.Value.ObjectId;
                }
            }

            return "Tag not present(" + datnum + ")";
        }
        #endregion
    }
}