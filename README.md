# MasterEMV
C# code used in Master Thesis: Circular Design with Reclaimed Wood: What’s Missing in Automation for a Fully Digital Structural Workflow, NTNU Trondheim.

Used to create components in Grasshopper.

### Matching   
DESCRIPTION

### ParametricDesign  
DESCRIPTION

### DigitalMaterialBank  
  
Consists of two folders and one file.  

"BeamFromScan" contains the general code for creating "BeamFromPts" and "BeamFromMesh". To make the code less cluttered, additional files ("ConvexHullMethods", DelauneyMethods" and "GetCrossSection") have been created, containing the developed methods called upon in the general code. 

"PackingAlgorithms" contains the general code for the different packing algorithms (Packing, PackingOptimized and GridPacking). To make the code less cluttered, some methods have been developed in the file called "PackingMethods".

"Methods.cs" contains some general quality-of-life methods, not available in RhinoAPI, and were therefore created in a separate file so they could be used when needed.
