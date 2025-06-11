# MasterEMV
C# code used in Master Thesis: Circular Design with Reclaimed Wood: What’s Missing in Automation for a Fully Digital Structural Workflow, NTNU Trondheim.

Used to create components in Grasshopper.

### Matching   
DESCRIPTION

### ParametricDesign  
Consists of five folders, where each of the folders and important code files will be described below.

**Classes:** Contains classes with different functions for the creation of the components. There are purely logistical classes namely: "BeamTree.cs", "GHTreeMaker.cs", "GH_Tree" and "TreeElement.cs". These are used to create trees and convert geoemtry to be compatible as outputs in the Grasshopper environment. 

"DowelsSteelplates.cs" is a file used to create the coordinates of the dowels related to the beam element, to vizualise the placement of the dowels and the calculate the eccentricity of the connection based on the dowel coordinates. Used to find the most loaded row for example. 

"MaterialList.cs" is a component used to fetch the custom material properties for both solid timber elements of the class Cxx, and glulam elements of the class GLxxc.

**Geometry:** Contains components used to create the geometry of the constructions in Grasshopper. "TrussMaker.cs" and "ColumnMaker.cs" are the most simple, and are used to further create "3DStructureMaker.cs". "ComplexStructureMaker.cs" is a component used to create multiple story constructions based on a 3D list of points.

**Miscellaneous:** Contains various components which are created trough the master thesis, and which does not contribute as components in the final product. Several different variations are created here, and the relevant ones are merged together for the final products. These are included to show the progression of the creation of the finished components.

**Properties:** Default options used to run Rhino when code is run.

**Testing (Karamba3D):** Contains all of the analysis components for the geometries created in "Geometry". They are all allocated a suitable name to easily identify which geometry components they are created to analyse. "JointCheckerList.cs" is a different analysis which bases the analysis on the joint forces from the "TesterList (Karamba3D).cs" component.

### DigitalMaterialBank  
  
Consists of two folders and one file.  

"BeamFromScan" contains the general code for creating "BeamFromPts" and "BeamFromMesh". To make the code less cluttered, additional files ("ConvexHullMethods", DelauneyMethods" and "GetCrossSection") have been created, containing the developed methods called upon in the general code. 

"PackingAlgorithms" contains the general code for the different packing algorithms (Packing, PackingOptimized and GridPacking). To make the code less cluttered, some methods have been developed in the file called "PackingMethods".

"Methods.cs" contains some general quality-of-life methods, not available in RhinoAPI, and were therefore created in a separate file so they could be used when needed.
