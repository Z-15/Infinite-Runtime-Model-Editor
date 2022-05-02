# Infinite-Runtime-Model-Editor

# Download: 

Latest Build: 

[![Download latest build](https://github.com/Z-15/Infinite-Runtime-Model-Editor/actions/workflows/dotnet.yml/badge.svg)](https://nightly.link/Z-15/Infinite-Runtime-Model-Editor/workflows/dotnet/master/IRME.zip)

.Net 5.0 Runtime:

https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-5.0.16-windows-x64-installer

# Read Me

**Current Capabilities:**

1. Loads all available models using a modified version of the IRTV tag loader.
2. Visually shows the location and orientation of both nodes and markers.
3. Changing values for runtime nodes (Item Properties Tab) will update the model in IRME.
4. Changing values in the Other Properties Tab will not, due to complexity.
5. Models can be saved as IRTV Mod files and can be loaded in IRTV.
6. Armor Editor - Located in tools menu.
7. Other (smaller) features:
  a. Ability to undo changes on individual nodes.
  b. Ability to revert an entire model (as long as it stays loaded).
  c. Ability to hide marker and nodes.
  d. Expand/Collapse all Nodes.
  e. Search node tree for specific markers or nodes.

**Fixes:**

1. Rotations no longer glitch out.
  - Details: Quaternions convert improperly if the Y-Axis is above 90 degrees or below -90 Degrees.

**Known Issues:**

1. Marker Names don't show in the node tree
2. Marker index/group index need to be shown in a more understandable fashion. 

**Other:**

1. If you want to name hashes, just follow the format in the files/hashes.txt and add onto it. 
2. Please report any bugs or issues you find.
3. If there are any additional features you want to request, please do so.
