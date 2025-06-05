using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using KarambaCommon;
using Karamba.Geometry;
using Karamba.Exporters;
using Karamba.CrossSections;
using Karamba.Utilities;
using Karamba.Elements;
using Karamba.Supports;
using Karamba.Loads;
using Karamba.Joints;
using Karamba.Models;
using Karamba.Materials;
using System.Linq;
using UnitsNet;
using Grasshopper;
using Karamba.Results;
using static System.Net.WebRequestMethods;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using System.DirectoryServices.ActiveDirectory;
using Karamba.Algorithms;
using System.Diagnostics;
using System.Threading;
using System.Runtime.Intrinsics.X86;
using Rhino.FileIO;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Text;
using System.Drawing.Printing;
using System.Reflection.Metadata.Ecma335;

namespace Masterv2
{
    public class ComplexTester__Karamba3D_ : GH_Component
    {

        public ComplexTester__Karamba3D_()
          : base("ComplexTester (Karamba3D)", "Nickname",
              "Description",
              "Master", "Karamba3D")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Supports", "sup", "Supports of the structure", GH_ParamAccess.list);
            pManager.AddPointParameter("Floorpoints", "fpts", "Points of the floor to apply point load", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Columns", "col", "Columns of the structure", GH_ParamAccess.list);
            pManager.AddCurveParameter("Beams", "beam", "Beams of the structure", GH_ParamAccess.tree);
            pManager.AddMeshParameter("MeshFloors", "mFloors", "Floors of the structure as a mesh", GH_ParamAccess.list);
            pManager.AddCurveParameter("Bracing", "br", "Bracing of the structure", GH_ParamAccess.list);
            pManager.AddNumberParameter("Cross-Sections", "cs", "Cross sections of the structure [h_c, b_c, h_b, b_b] [cm]", GH_ParamAccess.list);
            pManager.AddTextParameter("Material", "mat", "Material of the beams", GH_ParamAccess.item);
            pManager.AddCurveParameter("FLoorBracing", "fBr", "", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Displacement", "t1", "", GH_ParamAccess.item);
            pManager.AddGenericParameter("Utilization", "t2", "", GH_ParamAccess.item);
            pManager.AddGenericParameter("Breps", "t3", "", GH_ParamAccess.list);
            pManager.AddGenericParameter("WallMesh", "t4", "", GH_ParamAccess.item);
            pManager.AddGenericParameter("LineModel", "t5", "", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Input
            var colcrvs = new List<Curve>();
            var beamcrvstree = new GH_Structure<GH_Curve>();
            var supports3d = new List<Point3d>();
            var floors = new List<Mesh>();
            var dimlist = new List<double>();
            var material = "";
            var floorpointstree = new GH_Structure<GH_Point>();
            var bracingcrvs = new List<Curve>();
            var floorbracingcrv = new List<Curve>();

            DA.GetDataList(2, colcrvs);
            DA.GetDataTree(3, out beamcrvstree);
            DA.GetDataList(0, supports3d);
            DA.GetDataList(4, floors);
            DA.GetDataList(6, dimlist);
            DA.GetData(7, ref material);
            DA.GetDataTree(1, out floorpointstree);
            DA.GetDataList(5, bracingcrvs);
            DA.GetDataList(8, floorbracingcrv);

            //Converting tree inputs to list within lists to make it easier to work with
            var beamcrvs = new List<List<Curve>>();
            foreach (var path in beamcrvstree.Paths)
            {
                var crv = new List<Curve>();
                var branch = beamcrvstree.get_Branch(path);
                foreach (GH_Curve c in branch)
                    crv.Add(c.Value);
                beamcrvs.Add(crv);
            }

            var floorpoints = new List<List<Point3d>>();
            foreach (var path in floorpointstree.Paths)
            {
                var pt = new List<Point3d>();
                var branch = floorpointstree.get_Branch(path);
                foreach (GH_Point p in branch)
                    pt.Add(p.Value);
                floorpoints.Add(pt);
            }

            //Converting data to Karamba geometry
            var columns3 = ConvertToLine3(colcrvs);
            var beamcrvslist = new List<Curve>();
            foreach (var blist in beamcrvs)
                beamcrvslist.AddRange(blist);
            var beams3 = ConvertToLine3(beamcrvslist);
            var supports3 = ConvertToPoint3(supports3d);
            var floorbracings3 = ConvertToLine3(floorbracingcrv);
            var bracings3 = ConvertToLine3(bracingcrvs);
            foreach (var f in floors)
            {
                f.Faces.ConvertQuadsToTriangles();
                f.Normals.ComputeNormals();
                f.Compact();
            }
            var vertices = new List<List<Point3d>>();
            List<IReadOnlyList<Point3>> vertices3 = new List<IReadOnlyList<Point3>>();
            foreach (var f in floors)
            {
                var verticelist = new List<Point3d>();
                for (int i = 0; i < f.Vertices.Count; i++)
                    verticelist.Add(new Point3d(f.Vertices[i]));
                vertices.Add(verticelist);
            }

            foreach (var vlist in vertices)
            {
                var pt3list = ConvertToPoint3(vlist);
                vertices3.Add(pt3list);
            }

            var faces = new List<List<MeshFace>>();
            List<IReadOnlyList<Face3>> faces3 = new List<IReadOnlyList<Face3>>();
            foreach (var f in floors)
            {
                var facelist = new List<MeshFace>();
                for (int i = 0; i < f.Faces.Count; i++)
                    facelist.Add(f.Faces[i]);
                faces.Add(facelist);
            }

            foreach (var flist in faces)
            {
                var f3list = ConvertToFace3(flist);
                faces3.Add(f3list);
            }

            var floors3 = new List<Mesh3>();
            for (int i = 0; i < faces3.Count; i++)
            {
                var mesh3 = new Mesh3(vertices3[i], faces3[i]);
                floors3.Add(mesh3);
            }
            /*
            //For the concrete
            var concretes3 = new List<Mesh3>();
            var cvertices = new List<List<Point3d>>();
            List<IReadOnlyList<Point3>> cvertices3 = new List<IReadOnlyList<Point3>>();
            var cfaces = new List<List<MeshFace>>();
            List<IReadOnlyList<Face3>> cfaces3 = new List<IReadOnlyList<Face3>>();
            foreach (var f in concretemsh)
            {
                f.Normals.ComputeNormals();
                f.Compact();
            }
            foreach (var f in concretemsh)
            {
                var verticelist = new List<Point3d>();
                for (int i = 0; i < f.Vertices.Count; i++)
                    verticelist.Add(new Point3d(f.Vertices[i]));
                cvertices.Add(verticelist);
            }
            foreach (var vlist in cvertices)
            {
                var pt3list = ConvertToPoint3(vlist);
                cvertices3.Add(pt3list);
            }
            foreach (var f in concretemsh)
            {
                var facelist = new List<MeshFace>();
                for (int i = 0; i < f.Faces.Count; i++)
                    facelist.Add(f.Faces[i]);
                cfaces.Add(facelist);
            }
            foreach (var flist in cfaces)
            {
                var f3list = ConvertToFace3(flist);
                cfaces3.Add(f3list);
            }
            for (int i = 0; i < cfaces3.Count; i++)
            {
                var mesh3 = new Mesh3(vertices3[i], faces3[i]);
                concretes3.Add(mesh3);
            }
            */

            //KarambaCommon Toolkit for operations
            var k3d = new KarambaCommon.Toolkit();

            //Defining the material from the input and class (MaterialList.GetMaterial())
            string family = GetString(material, @"Material:\s+(\w+)"); //GlulamTimber
            string name = GetString(material, @"Material:\s+\S+\s+'([^']+)'"); //GLXXx
            var matr = MaterialList.GetMaterialGlulam(name);
            double E0 = GetDouble(material, @"E1:(-?\d+(\.\d+)?(E-?\d+)?)")*10;
            double E90 = GetDouble(material, @"E2:(-?\d+(\.\d+)?(E-?\d+)?)") * 10;
            double G12 = GetDouble(material, @"G12:(-?\d+(\.\d+)?(E-?\d+)?)")*10;
            double G31 = GetDouble(material, @"G31:(-?\d+(\.\d+)?(E-?\d+)?)")*10;
            double G32 = GetDouble(material, @"G32:(-?\d+(\.\d+)?(E-?\d+)?)") * 10;
            double gamma = GetDouble(material, @"gamma:(-?\d+(\.\d+)?(E-?\d+)?)");
            double ft0 = GetDouble(material, @"ft1:(-?\d+(\.\d+)?(E-?\d+)?)")*10;
            double ft90 = GetDouble(material, @"ft2:(-?\d+(\.\d+)?(E-?\d+)?)") * 10;
            double fc0 = GetDouble(material, @"fc1:(-?\d+(\.\d+)?(E-?\d+)?)") * 10;
            double fc90 = GetDouble(material, @"fc2:(-?\d+(\.\d+)?(E-?\d+)?)") * 10;
            double fm = matr[0];
            double fv = matr[5];
            double alphaT1 = GetDouble(material, @"alphaT1:(-?\d+(\.\d+)?(E-?\d+)?)");
            double alphaT2 = GetDouble(material, @"alphaT2:(-?\d+(\.\d+)?(E-?\d+)?)");

            var mat = k3d.Material.IsotropicMaterial(family, name, E0 / 10, G12 / 10, G31 / 10, gamma, ft0 / 10, fc0 / 10, 
                FemMaterial.FlowHypothesis.rankine, alphaT1);

            
            //Creating cross sections
            var hc = dimlist[0];
            var bc = dimlist[1];
            var hb = dimlist[2];
            var bb = dimlist[3];
            var hbr = dimlist[4];
            var bbr = dimlist[5];

            var rectcol = k3d.CroSec.Trapezoid(hc, bc, bc, mat, name, $"{name}:{hc}x{bc}");
            var rectbeam = k3d.CroSec.Trapezoid(hb, bb, bb, mat, name, $"{name}:{hb}x{bb}");
            var rectbrac = k3d.CroSec.Trapezoid(hbr, bbr, bbr, mat, name, $"{name}:{hbr}x{bbr}");
            //var rectconc = k3d.CroSec.ShellConst(0.2, 0, FemMaterial.defaultConcrete());

            //Creating columns and beams
            var allelements = new List<BuilderElement>();
            var logger = new MessageLogger();
            var columns = new List<BuilderBeam>();
            var beams = new List<BuilderBeam>();
            var bracings = new List<BuilderBeam>();
            var floorbracings = new List<BuilderBeam>();

            var columnsperfloor = columns3.Count / floors.Count;
            for (int i = 0; i < columns3.Count; i++) //Creating beams for each line and adding to list
            {
                var l = columns3[i];
                var floor = i / columnsperfloor;
                List<BuilderBeam> bm = k3d.Part.LineToBeam(l, $"Column on {floor} floor", rectcol, logger, out _);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklY, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklZ, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklLT, l.Length);
                columns.Add(bm[0]);
                allelements.Add(bm[0]);
            }

            var beamsperfloor = beams3.Count / beamcrvs.Count;
            for (int i = 0; i < beams3.Count; i++) //Creating beams for each line and adding to list
            {
                var l = beams3[i];
                var floor = i / beamsperfloor + 1;
                List<BuilderBeam> bm = k3d.Part.LineToBeam(l, $"Beam on {floor} floor", rectbeam, logger, out _, false);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklY, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklZ, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklLT, l.Length);
                beams.Add(bm[0]);
                allelements.Add(bm[0]);
            }

            if (bracings3 != null)
            {
                foreach (var b in bracings3)
                {
                    List<BuilderBeam> bm = k3d.Part.LineToBeam(b, $"Bracing", rectbrac, logger, out _, false);
                    bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklY, b.Length);
                    bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklZ, b.Length);
                    bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklLT, b.Length);
                    bracings.Add(bm[0]);
                    allelements.Add(bm[0]);
                }
            }
            for (int i = 0; i < floorbracings3.Count; i++) //Creating beams for each line and adding to list
            {
                var l = floorbracings3[i];
                List<BuilderBeam> bm = k3d.Part.LineToBeam(l, $"Floorbracing", rectbeam, logger, out _, false);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklY, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklZ, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklLT, l.Length);
                floorbracings.Add(bm[0]);
                allelements.Add(bm[0]);
            }
            /*
            //Creating concrete floors
            var crosecs = new List<CroSec>();
            var concreteIDs = new List<string>();
            for (int i = 0; i < concretes3.Count; i++)
            {
                crosecs.Add(rectconc);
                concreteIDs.Add("Concrete");
            }
            var concretefloors = k3d.Part.MeshToShell(concretes3, concreteIDs, crosecs, logger, out _);
            foreach (var c in concretefloors)
                allelements.Add(c);
            */
            //Creating supports
            var supports = new List<Support>();
            foreach (Point3 p3 in supports3)
            {
                Support s = k3d.Support.Support(p3, k3d.Support.SupportFixedConditions);
                supports.Add(s);
            }

            //Creating loads
            var loads = new List<Load>();
            
            loads.Add(k3d.Load.GravityLoad(new Vector3(0, 0, 1), "LC0"));

            for (int i = 0; i < faces.Count; i++)
            {
                var vectors = new List<Vector3>();
                for (int j = 0; j < faces[i].Count; j++)
                {
                    vectors.Add(new Vector3(0, 0, 4));
                }
                var ids = new List<string>();
                for (int j = 0; j < beamsperfloor - 1; j++)
                    ids.Add($"Beam on {i + 1} floor");
                loads.Add(k3d.Load.MeshLoad(vectors, floors3[i], LoadOrientation.local, true, true, ConvertToPoint3(floorpoints[i+1]),
                    ids));
            }
            
            //Wind load
            var westwall = new List<Point3d>();
            westwall.Add(floorpoints[0][0]);
            westwall.Add(floorpoints[floorpoints.Count - 1][0]);
            westwall.Add(floorpoints[floorpoints.Count - 1].Where(p => p.Y == floorpoints[0].Max(p => p.Y)).OrderBy(p => p.X).First());
            westwall.Add(floorpoints[0].Where(p => p.Y == floorpoints[0].Max(p => p.Y)).OrderBy(p => p.X).First());

            var westwallsurf = NurbsSurface.CreateFromCorners(westwall[0], westwall[1], westwall[2], westwall[3]);
            var meshparam = new MeshingParameters
            {
                GridMinCount = 50,
                GridMaxCount = 100,
                RefineGrid = true,
                JaggedSeams = false,
            };
            var westwallmesh = Mesh.CreateFromSurface(westwallsurf, meshparam); //where the wind load is placed
            List<Face3> westwallfaces3 = new List<Face3>();
            foreach (var face in westwallmesh.Faces)
            {
                westwallfaces3.Add(new Face3(face.A, face.B, face.C));
                if (face.IsQuad)
                    westwallfaces3.Add(new Face3(face.A, face.C, face.D));
            }
            var westwallvertices3d = new List<Point3d>();
            foreach (var vertice in westwallmesh.Vertices)
                westwallvertices3d.Add(new Point3d(vertice));
            var westwallvertices = ConvertToPoint3(westwallvertices3d);

            var westwallmesh3 = new Mesh3(westwallvertices.AsReadOnly(), westwallfaces3.AsReadOnly());

            var westwallpoint = new List<Point3d>();
            for (int f = 0; f < floorpoints.Count; f++)
            {
                for (int i = 0; i < floorpoints[f].Count; i++)
                {
                    if (floorpoints[f][i].X == 0)
                        westwallpoint.Add(floorpoints[f][i]);
                }
            }
            var westwallpoint3 = ConvertToPoint3(westwallpoint);

            var westwallelements = new List<string>();
            for (int i = 0; i < columns.Count; i++)
            {
                if (colcrvs[i].PointAtStart.X == 0 && colcrvs[i].PointAtEnd.X == 0)
                    westwallelements.Add(columns[i].id);
            }

            for (int i = 0; i < beams.Count; i++)
            {
                if (beamcrvslist[i].PointAtStart.X == 0 && beamcrvslist[i].PointAtEnd.X == 0)
                    westwallelements.Add(beams[i].id );
            }
            /*
            for (int i = 0; i < bracings.Count; i++)
            {
                if (bracingcrvs[i].PointAtStart.X == 0 && bracingcrvs[i].PointAtEnd.X == 0)
                    westwallelements.Add(bracings[i].id);
            }
            */
            var westwallwind = new List<Vector3>();
            for (int i = 0; i < westwallfaces3.Count; i++)
                westwallwind.Add(new Vector3(0, 0, 1)); //0,0,0.3
            
            loads.Add(k3d.Load.MeshLoad(westwallwind, westwallmesh3, LoadOrientation.local, true, true,
                westwallpoint3, westwallelements));
            //Assembling model
            model = k3d.Model.AssembleModel(allelements, supports, loads, out _, out _, out _, out _, out _);

            //Analyzing model
            IReadOnlyList<string> LCs = new List<string> { "LC0" };

            Amodel = k3d.Algorithms.Analyze(model, LCs,
                out IReadOnlyList<double> maxD,
                out _, out _, out string w);
            double maxDisp = maxD[0]/100;

            //BESO beam
            var bracingIDs = new List<string>();
            var maxDBESO = new double();

            foreach (var b in bracings)
                bracingIDs.Add(b.id);
            if (bracings.Count >= 1)
            {
                Karamba.Algorithms.BESOBeam.solve(model, bracingIDs, new List<string>() { }, 
                    new List<string>() { "LC0" }, dimlist[6], 10, 20, 1, 1, 0, 0, 0, 0, 0, 0, 
                    p => Debug.WriteLine(p), CancellationToken.None, out _, out _, out _, 
                    out maxDBESO, out BESOmodelA);
            }



            var BESOlines = new List<Line3>();
            var BESOelements = new List<BuilderBeam>();
            BESOmodelA.Disassemble(out _, out BESOlines, out _, out BESOelements, out _, out _, out _, out _, out _,
                out _, out _);
            var BESOcurves = ConvertToCurve(BESOlines);

            //Getting the internal forces
            var elemIDs = new List<string>();
            var elemGUIDs = new List<Guid>();
            foreach (var element in allelements)
            {
                elemIDs.Add(element.id);
                elemGUIDs.Add(element.guid);
            }
                
            Karamba.Results.BeamForces.solve(Amodel, elemIDs, elemGUIDs,
                "LC0", new List<double> { 0, 0.5, 1 },
                out List<List<List<Vector3>>> forces,
                out List<List<List<Vector3>>> moments, out _, out _, out _);

            var forcestree = intvectors(forces);
            var momentstree = intvectors(moments);

            List<double> Nlist = new List<double>(); //Creating a list for the N values
            for (int i = 0; i < forces.Count / 2; i++)
                for (int j = 0; j < forces[i].Count; j++)
                    Nlist.Add(forces[i][j][0][0]);

            List<double> Mlist = new List<double>(); //Creating a list for the M values
            for (int i = 0; i < moments.Count / 2; i++)
                for (int j = 0; j < moments[i].Count; j++)
                    Mlist.Add(moments[i][j][0][1]);

            List<double> Vlist = new List<double>(); //Creating a list for the N values
            for (int i = 0; i < forces.Count / 2; i++)
                for (int j = 0; j < forces[i].Count; j++)
                    Vlist.Add(forces[i][j][0][2]);

            //Getting the internal forces for the BESO model
            var elemIDsB = new List<string>();
            var elemGUIDsB = new List<Guid>();
            foreach (var element in BESOelements)
            {
                elemIDsB.Add(element.id);
                elemGUIDsB.Add(element.guid);
            }
            Karamba.Results.BeamForces.solve(BESOmodelA, elemIDsB, elemGUIDsB,
                "LC0", new List<double> { 0, 0.5, 1 },
                out List<List<List<Vector3>>> forcesB,
                out List<List<List<Vector3>>> momentsB, out _, out _, out _);

            var forcestreeB = intvectors(forcesB);
            var momentstreeB = intvectors(momentsB);

            List<double> NlistB = new List<double>(); //Creating a list for the N values
            for (int i = 0; i < forcesB.Count / 2; i++)
                for (int j = 0; j < forcesB[i].Count; j++)
                    NlistB.Add(forcesB[i][j][0][0]);

            List<double> MlistB = new List<double>(); //Creating a list for the M values
            for (int i = 0; i < momentsB.Count / 2; i++)
                for (int j = 0; j < momentsB[i].Count; j++)
                    MlistB.Add(momentsB[i][j][0][1]);

            List<double> VlistB = new List<double>(); //Creating a list for the N values
            for (int i = 0; i < forcesB.Count / 2; i++)
                for (int j = 0; j < forcesB[i].Count; j++)
                    VlistB.Add(forcesB[i][j][0][2]);

            var maxNB = MaxInternalForce(NlistB);
            var maxMB = MaxInternalForce(MlistB);
            var maxVB = MaxInternalForce(VlistB);


            //Utilization
            var utillist = new List<double>();
            double kmod = 0.9;
            double g_m = 1.25;
            double fmd = fm * kmod / g_m;
            double fc0d = Math.Abs(fc0) * kmod / (g_m);
            double fc90d = Math.Abs(fc90) * kmod / (g_m);
            double ft0d = Math.Abs(ft0) * kmod / (g_m);
            double ft90d = Math.Abs(ft90) * kmod / (g_m);

            for (int i = 0; i < maxNB.Count; i++)
            {
                if (BESOelements[i].id.StartsWith("Column")) //columns
                {
                    double A = hc * bc * 100; //Area in mm^2
                    double I = ((bc * Math.Pow(hc, 3)) / 12) * 10000;
                    var N = maxNB[i];
                    var M = maxMB[i];
                    var u = MNUtilization(N, M, hc, bc, A, I, fc0d, ft0d, fmd);
                    utillist.Add(u);
                }
                if (BESOelements[i].id.StartsWith("Beam")) //beams
                {
                    double A = hb * bb * 100; //Area in mm^2
                    double I = ((bb * Math.Pow(hb, 3)) / 12) * 10000;
                    var N = maxNB[i];
                    var M = maxMB[i];
                    var u = MNUtilization(N, M, hb, bb, A, I, fc0d, ft0d, fmd);
                    utillist.Add(u);
                }
                if (BESOelements[i].id.StartsWith("Bracing")) //bracings
                {
                    double A = hbr * bbr * 100; //Area in mm^2
                    double I = ((bbr * Math.Pow(hbr, 3)) / 12) * 10000;
                    var N = maxNB[i];
                    var M = maxMB[i];
                    var u = MNUtilization(N, M, hbr, bbr, A, I, fc0d, ft0d, fmd);
                    utillist.Add(u);
                }
            }

            //Shear check
            var fvd = fv * kmod / g_m;
            var k_cr = 0.67;
            var shearlist = new List<double>();

            for (int i = 0; i < maxVB.Count; i++)
            {
                if (BESOelements[i].id.StartsWith("Column")) //columns
                {
                    var S = (bc * 10 * Math.Pow(hc * 10, 2)) / 6;
                    double I = ((bc * Math.Pow(hc, 3)) / 12) * 10000;
                    var tau = Math.Abs((maxVB[i] * 1000 * S) / (I * bc * 10 * k_cr));
                    shearlist.Add(tau / fvd);
                }
                if (BESOelements[i].id.StartsWith("Beam")) //beams
                {
                    var S = (bb * 10 * Math.Pow(hb * 10, 2)) / 6;
                    double I = ((bb * Math.Pow(hb, 3)) / 12) * 10000;
                    var tau = Math.Abs((maxVB[i] * 1000 * S) / (I * bb * 10 * k_cr));
                    shearlist.Add(tau / fvd);
                }
                if (BESOelements[i].id.StartsWith("Bracing")) //bracings
                {
                    var S = (bbr * 10 * Math.Pow(hbr * 10, 2)) / 6;
                    double I = ((bbr * Math.Pow(hbr, 3)) / 12) * 10000;
                    var tau = Math.Abs((maxVB[i] * 1000 * S) / (I * bbr * 10 * k_cr));
                    shearlist.Add(tau / fvd);
                }
            }

            //Creating Breps of the beams
            var columnBreps = new List<Brep>();
            var beamBreps = new List<Brep>();
            var bracingBreps = new List<Brep>();
            var allBreps = new List<Brep>();
            foreach (var c in BESOcurves)
            {
                var pt1 = c.PointAtStart;
                var pt2 = c.PointAtEnd;
                if (pt1.X == pt2.X && pt1.Y == pt2.Y) //columns
                {
                    var pl = new Plane(pt1, new Vector3d(0, 0, 1));
                    var shape = new Rectangle3d(pl, new Interval(-bc / 200, bc / 200), new Interval(-hc / 200, hc / 200));
                    var brep = Brep.CreateFromSweep(c, shape.ToNurbsCurve(), true, 0.01)[0].CapPlanarHoles(0.01);
                    columnBreps.Add(brep);
                    allBreps.Add(brep);
                }
                if (pt1.Z == pt2.Z) //beams
                {
                    var beamdir = new Vector3d(pt2.X - pt1.X, pt2.Y - pt1.Y, 0);
                    var xdir = Vector3d.CrossProduct(Vector3d.ZAxis, beamdir);
                    var pl = new Plane(pt1, xdir, Vector3d.CrossProduct(beamdir, xdir));
                    var shape = new Rectangle3d(pl, new Interval(-bb / 200, bb / 200), new Interval(-hb / 200, hb / 200));
                    var brep = Brep.CreateFromSweep(c, shape.ToNurbsCurve(), true, 0.01)[0].CapPlanarHoles(0.01);
                    beamBreps.Add(brep);
                    allBreps.Add(brep);
                }
                if (pt1.X == pt2.X && pt1.Z != pt2.Z && pt1.Y != pt2.Y || pt1.Y == pt2.Y && pt1.Z != pt2.Z && pt1.X != pt2.X) //bracing
                {
                    var beamdir = new Vector3d(pt2.X - pt1.X, pt2.Y - pt1.Y, pt2.Z - pt1.Z);
                    var xdir = Vector3d.CrossProduct(Vector3d.ZAxis, beamdir);
                    var pl = new Plane(pt1, xdir, Vector3d.CrossProduct(beamdir, xdir));
                    var shape = new Rectangle3d(pl, new Interval(-bbr / 200, bbr / 200), new Interval(-hbr / 200, hbr / 200));
                    var brep = Brep.CreateFromSweep(c, shape.ToNurbsCurve(), true, 0.01)[0].CapPlanarHoles(0.01);
                    bracingBreps.Add(brep);
                    allBreps.Add(brep);
                }
            }

            //Buckling checks
            var buckly = new List<double>();
            var bucklz = new List<double>();
            var kczlist = new List<double>();
            var E005 = matr[6]; //kN/mm^2
            var beta_c = 0.1; //for Glulam
            var G005 = matr[7]; //kN/mm^2 //For the LTB buckling checks
            var LTB = new List<double>();
            
            for (int i = 0; i < BESOcurves.Count; i++)
            {
                double I_y = 0, I_z = 0, A = 0, h = 0, b = 0, lk = 0, lef = 0;
                if (BESOelements[i].id.StartsWith("Column")) //columns
                {
                    I_y = ((bc * Math.Pow(hc, 3)) / 12) * 10000;
                    I_z = ((hc * Math.Pow(bc, 3)) / 12) * 10000;
                    A = hc * bc * 100;
                    h = hc;
                    b = bc;
                    lk = BESOlines[i].Length * 0.5 * 1000;
                    lef = BESOlines[i].Length * 1000;
                }
                if (BESOelements[i].id.StartsWith("Beam")) //beams
                {
                    I_y = ((bb * Math.Pow(hb, 3)) / 12) * 10000;
                    I_z = ((hb * Math.Pow(bb, 3)) / 12) * 10000;
                    A = hb * bb * 100;
                    h = hb;
                    b = bb;
                    lk = BESOlines[i].Length * 0.5 * 1000;
                    lef = BESOlines[i].Length * 1000 * 0.9;
                }
                if (BESOelements[i].id.StartsWith("Bracing")) //bracings
                {
                    I_y = ((bbr * Math.Pow(hbr, 3)) / 12) * 10000;
                    I_z = ((hbr * Math.Pow(bbr, 3)) / 12) * 10000;
                    A = hbr * bbr * 100;
                    h = hbr;
                    b = bbr;
                    lk = BESOlines[i].Length * 1000;
                    lef = BESOlines[i].Length * 1000;
                }

                //Buckling checks as columns

                //Buckling in Y direction
                var gamma_y = lk / (Math.Sqrt(I_y / A));
                var gamma_rely = (gamma_y / Math.PI) * Math.Sqrt(Math.Abs(fc0) / E005);
                var k_y = 0.5 * (1 + beta_c * (gamma_rely - 0.3) + Math.Pow(gamma_rely, 2));
                var k_cy = 1 / (k_y + Math.Sqrt(Math.Pow(k_y, 2) - Math.Pow(gamma_rely, 2)));

                //Buckling in Z direction
                var gamma_z = lk / (Math.Sqrt(I_z / A));
                var gamma_relz = (gamma_z / Math.PI) * Math.Sqrt(Math.Abs(fc0) / (E005));
                var k_z = 0.5 * (1 + beta_c * (gamma_relz - 0.3) + Math.Pow(gamma_relz, 2));
                var k_cz = 1 / (k_z + Math.Sqrt(Math.Pow(k_z, 2) - Math.Pow(gamma_relz, 2)));
                kczlist.Add(k_cz);

                //Buckling checks
                if (maxNB[i] >= 0)
                {
                    var bchecky = Math.Abs((maxNB[i] * 1000 / A) / (k_cy * fc0d)) + Math.Abs(((maxMB[i] * 10000000 * h / 2) / (I_y)) / (fmd));
                    var bcheckz = (maxNB[i] * 1000 / A) / (k_cz * fc0d) + 0.7 * Math.Abs(((maxMB[i] * 10000000 * h / 2) / (I_y)) / (fmd));
                    buckly.Add(bchecky);
                    bucklz.Add(bcheckz);
                }
                if (maxNB[i] < 0)
                {
                    var bchecky = 0;
                    var bcheckz = 0;
                    buckly.Add(bchecky);
                    bucklz.Add(bcheckz);
                }

                //LTB check (as beams)
                var I_tor = (1.0 / 3) * (1 - 0.63 * b / h) * h * Math.Pow(b, 3) * 10000;
                var W_y = (b * Math.Pow(h, 2) * 1000) / (6);
                var sig_mcrit = (Math.PI * Math.Sqrt(E005 * I_z * G005 * I_tor)) / (lef * W_y);
                var lambda_relm = Math.Sqrt(fm / sig_mcrit);
                double kcrit = 0;
                if (lambda_relm <= 0.75)
                    kcrit = 1.0;
                else if (lambda_relm > 0.75 && lambda_relm <= 1.4)
                    kcrit = 1.56 - 0.75 * lambda_relm;
                else
                    kcrit = 1 / (Math.Pow(lambda_relm, 2));

                if (kcrit == 1.0)
                    LTB.Add(0);
                else
                {
                    var LTBcheck = Math.Abs(Math.Pow((((maxMB[i] * 10000000 * h / 2) / (I_y)) / (kcrit * fmd)), 2)) + 
                        Math.Abs((maxNB[i] * 1000 / A) / (kczlist[i] * fc0d));
                    LTB.Add(LTBcheck);
                }
            }

            //Finding the total max utilization from N+M and V and Buckling Y and Z
            var bucklingY = "No Buckling";
            if (buckly.Max() > 1.0)
                bucklingY = "Buckling";
            var bucklingZ = "No Buckling";
            if (bucklz.Max() > 1.0)
                bucklingZ = "Buckling";
            var bucklingLTB = "No Buckling";
            if (LTB.Max() > 1.0)
                bucklingLTB = $"Buckling";

            var totalmaxutil = $"N+M Utilization: {Math.Round(utillist.Max(), 4)} at {BESOelements[utillist.IndexOf(utillist.Max())].id}, " +
                $"V Utilization: {Math.Round(shearlist.Max(), 4)} at {BESOelements[shearlist.IndexOf(shearlist.Max())].id}, " +
                $"Buckling Y: {Math.Round(buckly.Max(), 4)} -> {bucklingY} at {BESOelements[buckly.IndexOf(buckly.Max())].id}, " +
                $"Buckling Z: {Math.Round(bucklz.Max(), 4)} -> {bucklingZ} at {BESOelements[bucklz.IndexOf(bucklz.Max())].id}, " +
                $"Buckling LTB: {Math.Round(LTB.Max(), 4)} -> {bucklingLTB} at {BESOelements[LTB.IndexOf(LTB.Max())].id}";

            var utilopt = Math.Abs(new List<double>() { utillist.Max(), buckly.Max(), bucklz.Max() }.Max() - 0.95);
            var displacement = new double();
            if (maxDBESO == null)
                displacement = maxDisp;
            if (maxDBESO != null)
                displacement = maxDBESO/100;
            //Output
            DA.SetData(0, maxDBESO/100);
            DA.SetData(1, totalmaxutil);
            DA.SetDataList(2, allBreps);
            DA.SetData(3, westwallmesh);
            DA.SetDataList(4, ConvertToLine(BESOlines));
        }
        //Different functions used to shorten the main code
        private Karamba.Models.Model model;
        private Karamba.Models.Model besomodel;
        private Karamba.Models.Model Amodel;
        private Karamba.Models.Model BESOmodelA;

        List<Point3> ConvertToPoint3(List<Point3d> pts3d)
        {
            List<Point3> pts3 = new List<Point3>();

            foreach (Point3d p in pts3d)
            {
                Point3 p3 = new Point3(p.X, p.Y, p.Z);
                pts3.Add(p3);
            }

            return pts3;
        }

        List<Line3> ConvertToLine3(List<Curve> crvs)
        {
            List<Line3> lines3 = new List<Line3>();

            foreach (Curve c in crvs)
            {
                Point3d pt1 = c.PointAtStart;
                Point3d pt2 = c.PointAtEnd;
                Point3 p1 = new Point3(pt1.X, pt1.Y, pt1.Z);
                Point3 p2 = new Point3(pt2.X, pt2.Y, pt2.Z);
                Line3 l3 = new Line3(p1, p2);
                lines3.Add(l3);
            }
            return lines3;
        }
        List<Curve> ConvertToCurve(List<Line3> crvs)
        {
            List<Curve> lines3 = new List<Curve>();

            foreach (Line3 c in crvs)
            {
                Point3 pt1 = c.PointAtStart;
                Point3 pt2 = c.PointAtEnd;
                Point3d p1 = new Point3d(pt1.X, pt1.Y, pt1.Z);
                Point3d p2 = new Point3d(pt2.X, pt2.Y, pt2.Z);
                Line l3 = new Line(p1, p2);
                lines3.Add(l3.ToNurbsCurve());
            }
            return lines3;
        }

        string GetString(string text, string pattern)
        {
            var s = Regex.Match(text, pattern);
            return s.Groups[1].Value;
        }
        
        double GetDouble(string text, string pattern)
        {
            var s = Regex.Match(text, pattern);
            return double.Parse(s.Groups[1].Value, CultureInfo.InvariantCulture);
        }
        
        List<double> ListFrom0To1(double div)
        {
            List<double> list = new List<double>();
            double step = 1 / (div - 1);
            for (int i = 0; i < div; i++)
                list.Add(i * step);
            return list;
        }
        List<Line> ConvertToLine(List<Line3> lines3)
        {
            List<Line> lines = new List<Line>();

            foreach (Line3 l3 in lines3)
            {
                Point3 pt1 = l3.PointAtStart;
                Point3 pt2 = l3.PointAtEnd;
                Point3d p1 = new Point3d(pt1.X, pt1.Y, pt1.Z);
                Point3d p2 = new Point3d(pt2.X, pt2.Y, pt2.Z);
                Line l = new Line(p1, p2);
                lines.Add(l);
            }
            return lines;
        }
        
        List<Face3> ConvertToFace3(List<MeshFace> meshfaces)
        {
            var faces3 = new List<Face3>();

            for (int i = 0; i < meshfaces.Count; i++)
            {
                var f = meshfaces[i];
                faces3.Add(new Face3(f.A, f.B, f.C));
            }

            return faces3;
        }

        DataTree<Vector3> intvectors(List<List<List<Vector3>>> intlist)
        {
            DataTree<Vector3> forcestree = new DataTree<Vector3>();
            for (int i = 0; i < intlist.Count; i++)
            {
                for (int j = 0; j < intlist[i].Count; j++)
                {
                    for (int k = 0; k < intlist[i][j].Count; k++)
                    {
                        forcestree.Add(intlist[i][j][k], new Grasshopper.Kernel.Data.GH_Path(i, j));
                    }
                }
            }

            return forcestree;
        }
        
        List<double> MaxInternalForce(List<double> Ilist)
        {
            var Maxlist = new List<double>();

            for (int i = 0; i <Ilist.Count; i = i + 3)
            {
                var values = Ilist.Skip(i).Take(3).ToList();
                var max = values.OrderByDescending(x => Math.Abs(x)).First();
                Maxlist.Add(max);
            }

            return Maxlist;
        }

        double MNUtilization(double N, double M, double h, double b, double A, double I, double fcd, double ftd, double fmd)
        {
            var u = new double();

            if (N > 0) //compression
            {
                u = Math.Abs(Math.Pow(((N * 1000 / A) / fcd), 2)) +
                    Math.Abs(((M * 10000000 * h / 2) / (I)) / fmd);
            }
            if (N <= 0) //tension
            {
                u = Math.Abs((N / A) / (ftd)) +
                    Math.Abs(((M * 10000000 * h / 2) / (I)) / (fmd));
            }
            return u;
        }
        
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("A3FA5D2C-FBD9-4610-A49D-0888993113EF"); }
        }
    }
}