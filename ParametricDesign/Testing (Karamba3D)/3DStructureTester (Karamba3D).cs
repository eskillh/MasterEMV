using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

using Grasshopper.Kernel;
using Rhino.Geometry;

using KarambaCommon;
using Karamba.Geometry;
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
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System.Collections;

namespace Masterv2
{
    public class _3DStructureTester__Karamba3D_ : GH_Component
    {

        public _3DStructureTester__Karamba3D_()
          : base("3DStructureTester (Karamba3D)", "Nickname",
              "Description",
              "Master", "Karamba3D")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("TopCurves", "topC", "", GH_ParamAccess.tree);
            pManager.AddCurveParameter("TrussCurves", "trussC", "", GH_ParamAccess.tree);
            pManager.AddCurveParameter("BottomCurves", "botC", "", GH_ParamAccess.tree);
            pManager.AddCurveParameter("ColumnCurves", "colC", "", GH_ParamAccess.tree);
            pManager.AddPointParameter("Supports", "sPts", "", GH_ParamAccess.list);
            pManager.AddMeshParameter("RoofMeshes", "roofMsh", "", GH_ParamAccess.list);
            pManager.AddPointParameter("RoofPoints", "rPts", "", GH_ParamAccess.tree);
            pManager.AddCurveParameter("PurlinCurves", "purC", "", GH_ParamAccess.tree);
            pManager.AddTextParameter("Material", "mat", "", GH_ParamAccess.item);
            pManager.AddNumberParameter("CrossSections", "cs", "[htop, btop, htruss, btruss," +
                "hbottom, bbottom, hcol, bcol, hpur, bpur] [cm]", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Displacement", "", "", GH_ParamAccess.item);
            pManager.AddGenericParameter("Utilization", "", "", GH_ParamAccess.item);
            pManager.AddGenericParameter("Jointforces", "", "", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Breps", "", "", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Input
            var topcrvstree = new GH_Structure<GH_Curve>();
            var trusscrvstree = new GH_Structure<GH_Curve>();
            var bottomcrvstree = new GH_Structure<GH_Curve>();
            var colcrvstree = new GH_Structure<GH_Curve>();
            var supports3d = new List<Point3d>();
            var roofmesh = new List<Mesh>();
            var roofpoints3d = new GH_Structure<GH_Point>();
            var purlincrvstree = new GH_Structure<GH_Curve>();
            var material = "";
            var dimlist = new List<double>();

            DA.GetDataTree(0, out topcrvstree);
            DA.GetDataTree(1, out trusscrvstree);
            DA.GetDataTree(2, out bottomcrvstree);
            DA.GetDataTree(3, out colcrvstree);
            DA.GetDataList(4, supports3d);
            DA.GetDataList(5, roofmesh);
            DA.GetDataTree(6, out roofpoints3d);
            DA.GetDataTree(7, out purlincrvstree);
            DA.GetData(8, ref material);
            DA.GetDataList(9, dimlist);

            var topcrvs = Curvelist(topcrvstree);
            var trusscrvs = Curvelist(trusscrvstree);
            var bottomcrvs = Curvelist(bottomcrvstree);
            var colcrvs = Curvelist(colcrvstree);
            var purlincrvs = Curvelist(purlincrvstree);
            var roofpts3d = Pointlist(roofpoints3d);

            var leftroofpoints = new List<Point3d>();
            for (int i = 0; i < roofpts3d.Count; i++)
            {
                for (int j = 0; j < roofpts3d[i].Count/2 + 1; j++)
                    leftroofpoints.Add(roofpts3d[i][j]);
            }
            var rightroofpoints = new List<Point3d>();
            for (int i = 0; i < roofpts3d.Count; i++)
            {
                for (int j = roofpts3d[i].Count/2; j < roofpts3d[i].Count; j++)
                    rightroofpoints.Add(roofpts3d[i][j]);
            }

            var leftroofpurlins = new List<Curve>();
            for (int i = 0; i < purlincrvs.Count; i++)
            {
                for (int j = 0; j < purlincrvs[i].Count/2 + 1; j++)
                    leftroofpurlins.Add(purlincrvs[i][j]);
            }
            var rightroofpurlins = new List<Curve>();
            for (int i = 0; i < purlincrvs.Count; i++)
            {
                for (int j = purlincrvs[i].Count / 2 ; j < purlincrvs[i].Count; j++)
                    rightroofpurlins.Add(purlincrvs[i][j]);
            }

            //Converting geometry to Karamba geometry
            var top3 = ConvertToLine3Alt(topcrvs); //Converts and flattens the List<List<Curve>> to List<Line3>
            var truss3 = ConvertToLine3Alt(trusscrvs);
            var bottom3 = ConvertToLine3Alt(bottomcrvs);
            var col3 = ConvertToLine3Alt(colcrvs);
            var purlin3 = ConvertToLine3Alt(purlincrvs);

            var supports3 = ConvertToPoint3(supports3d);
            var lpts3 = ConvertToPoint3(leftroofpoints);
            var rpts3 = ConvertToPoint3(rightroofpoints);

            foreach (var m in roofmesh)
            {
                m.Faces.ConvertQuadsToTriangles();
                m.Normals.ComputeNormals();
                m.Compact();
            }
            var vertices = new List<List<Point3d>>();
            List<IReadOnlyList<Point3>> vertices3 = new List<IReadOnlyList<Point3>>();
            foreach (var m in roofmesh)
            {
                var verticelist = new List<Point3d>();
                for (int i = 0; i < m.Vertices.Count; i++)
                    verticelist.Add(new Point3d(m.Vertices[i]));
                vertices.Add(verticelist);
            }

            foreach (var vlist in vertices)
            {
                var pt3list = ConvertToPoint3(vlist);
                vertices3.Add(pt3list);
            }

            var faces = new List<List<MeshFace>>();
            List<IReadOnlyList<Face3>> faces3 = new List<IReadOnlyList<Face3>>();
            foreach (var m in roofmesh)
            {
                var facelist = new List<MeshFace>();
                for (int i = 0; i < m.Faces.Count; i++)
                    facelist.Add(m.Faces[i]);
                faces.Add(facelist);
            }

            foreach (var flist in faces)
            {
                var f3list = ConvertToFace3(flist);
                faces3.Add(f3list);
            }

            var roofmesh3 = new List<Mesh3>();
            for (int i = 0; i < faces3.Count; i++)
            {
                var mesh3 = new Mesh3(vertices3[i], faces3[i]);
                roofmesh3.Add(mesh3);
            }

            //KarambaCommon Toolkit for operations
            var k3d = new KarambaCommon.Toolkit();

            //Defining the material from the input and class (MaterialList.GetMaterial())
            string family = GetString(material, @"Material:\s+(\w+)");
            string name = GetString(material, @"Material:\s+\S+\s+'([^']+)'");
            var matr = MaterialList.GetMaterial(GetString(name, @"(C\d+)"));
            double E = GetDouble(material, @"E:(-?\d+(\.\d+)?(E-?\d+)?)");
            double Gip = GetDouble(material, @"G12:(-?\d+(\.\d+)?(E-?\d+)?)");
            double Gtr = GetDouble(material, @"G3:(-?\d+(\.\d+)?(E-?\d+)?)");
            double gamma = GetDouble(material, @"gamma:(-?\d+(\.\d+)?(E-?\d+)?)") / (100 * 100);
            double ft = matr[0] * 100 * 100;//GetDouble(material, @"ft:(-?\d+(\.\d+)?(E-?\d+)?)")
            double fc = matr[1] * 100 * 100;//GetDouble(material, @"fc:(-?\d+(\.\d+)?(E-?\d+)?)");
            double fm = matr[2];
            double fv = matr[3];
            double alphaT = GetDouble(material, @"alphaT:(-?\d+(\.\d+)?(E-?\d+)?)") / (100 * 100);

            FemMaterial mat = k3d.Material.IsotropicMaterial(family,
                name, E, Gip, Gtr, gamma, ft, fc,
                FemMaterial.FlowHypothesis.rankine, alphaT);

            //Creating cross sections
            var htop = dimlist[0];
            var btop = dimlist[1];
            var htruss = dimlist[2];
            var btruss = dimlist[3];
            var hbottom = dimlist[4];
            var bbottom = dimlist[5];
            var hcol = dimlist[6];
            var bcol = dimlist[7];
            var hpur = dimlist[8];
            var bpur = dimlist[9];

            CroSec recttop = k3d.CroSec.Trapezoid(htop, btop, btop, mat, name, $"{name}:{htop}x{btop}");
            CroSec recttruss = k3d.CroSec.Trapezoid(htruss, btruss, btruss, mat, name, $"{name}:{htruss}x{btruss}");
            CroSec rectbottom = k3d.CroSec.Trapezoid(hbottom, bbottom, bbottom, mat, name, $"{name}:{hbottom}x{bbottom}");
            CroSec rectcol = k3d.CroSec.Trapezoid(hcol, bcol, bcol, mat, name, $"{name}:{hcol}x{bcol}");
            CroSec rectpur = k3d.CroSec.Trapezoid(hpur, bpur, bpur, mat, name, $"{name}:{hpur}x{bpur}");

            //Creating beams
            var logger = new MessageLogger();
            List<BuilderBeam> beams = new List<BuilderBeam>();

            List<BuilderBeam> topbeams = new List<BuilderBeam>();
            for (int i = 0; i < top3.Count; i++) //Creating beams for each line and adding to list
            {
                var l = top3[i];
                List<BuilderBeam> bm = k3d.Part.LineToBeam(l, $"Top Beam {i}", recttop, logger, out _);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklY, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklZ, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklLT, l.Length);
                topbeams.Add(bm[0]);
                beams.Add(bm[0]);
            }
            List<BuilderBeam> trussbeams = new List<BuilderBeam>();
            for (int i = 0; i < truss3.Count; i++)
            {
                var l = truss3[i];
                List<BuilderBeam> bm = k3d.Part.LineToBeam(l, $"Truss {i}", recttruss, logger, out _, false);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklY, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklZ, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklLT, l.Length);
                trussbeams.Add(bm[0]);
                beams.Add(bm[0]);
            }
            List<BuilderBeam> bottombeams = new List<BuilderBeam>();
            for (int i = 0; i < bottom3.Count; i++)
            {
                var l = bottom3[i];
                List<BuilderBeam> bm = k3d.Part.LineToBeam(l, $"Bottom Beam {i}", rectbottom, logger, out _);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklY, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklZ, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklLT, l.Length);
                bottombeams.Add(bm[0]);
                beams.Add(bm[0]);
            }
            List<BuilderBeam> colbeams = new List<BuilderBeam>();
            for (int i = 0; i < col3.Count; i++) //Creating beams for each line and adding to list
            {
                var l = col3[i];
                List<BuilderBeam> bm = k3d.Part.LineToBeam(l, $"Column {i}", rectcol, logger, out _);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklY, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklZ, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklLT, l.Length);
                colbeams.Add(bm[0]);
                beams.Add(bm[0]);
            }
            List<BuilderBeam> purbeams = new List<BuilderBeam>();
            for (int i = 0; i < purlin3.Count; i++) //Creating beams for each line and adding to list
            {
                var l = purlin3[i];
                List<BuilderBeam> bm = k3d.Part.LineToBeam(l, $"Purlin {i}", rectpur, logger, out _);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklY, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklZ, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklLT, l.Length);
                purbeams.Add(bm[0]);
                beams.Add(bm[0]);
            }

            //Creating supports
            var supports = new List<Support>();
            foreach (Point3 p3 in supports3)
            {
                Support s = k3d.Support.Support(p3, k3d.Support.SupportHingedConditions);
                supports.Add(s);
            }

            //Creating loads
            var loads = new List<Load>();
            loads.Add(k3d.Load.GravityLoad(new Vector3(0, 0, 1), "LC0"));

            var middleX = topcrvs[0][topcrvs[0].Count / 2].PointAtStart.X;
            var leftroofpurlinIDs = new List<string>();
            var rightroofpurlinIDs = new List<string>();
            for (int i = 0; i < purbeams.Count; i++)
            {
                if (purlin3[i].PointAtStart.X <= middleX)
                    leftroofpurlinIDs.Add(purbeams[i].id);
                if (purlin3[i].PointAtStart.X >= middleX)
                    rightroofpurlinIDs.Add(purbeams[i].id);
            }

            var vector3s = new List<List<Vector3>>();
            foreach (var mesh in roofmesh3)
            {
                var vector3 = new List<Vector3>();
                for (int i = 0; i < mesh.Faces.Count; i++)
                    vector3.Add(new Vector3(0, 0, 10));
                vector3s.Add(vector3);
            }

            var roofpurlinIDs = new List<List<string>>() { leftroofpurlinIDs, rightroofpurlinIDs };
            var roofpoints = new List<List<Point3>>() { lpts3, rpts3 };
            for (int i = 0; i < roofmesh3.Count; i++)
            {
                loads.Add(k3d.Load.MeshLoad(vector3s[i], roofmesh3[i], LoadOrientation.global, true, true,
                    roofpoints[i], roofpurlinIDs[i]));
            }
            //Assembling model
            model = k3d.Model.AssembleModel(
                beams, supports, loads,
                out _, out _, out _, out _, out _);

            model.Disassemble(out _, out List<Line3> allines, out _, out _, out _, out _, out _, out _, out _, out _, out _);
            var allcurves = ConvertToCurve(allines);
                
            //Analyzing model
            IReadOnlyList<string> LCs = new List<string> { "LC0" };

            Amodel = k3d.Algorithms.Analyze(model, LCs,
                out IReadOnlyList<double> maxD,
                out _, out _, out string w);

            double maxDisp = maxD[0] * 100;

            //Getting the internal forces
            var elemIDs = new List<string>();
            var elemGUIDs = new List<Guid>();
            foreach (var element in beams)
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

            var maxN = MaxInternalForce(Nlist);
            var maxM = MaxInternalForce(Mlist);
            var maxV = MaxInternalForce(Vlist);

            //Utilization
            var utillist = new List<double>();
            double kmod = 0.9;
            double g_m = 1.3;
            double fmd = fm * kmod / g_m;
            double fcd = Math.Abs(fc) * kmod / (g_m * 1000);
            double ftd = Math.Abs(ft) * kmod / (g_m * 1000);

            for (int i = 0; i < maxN.Count; i++)
            {
                if (beams[i].id.StartsWith("Top")) //Top Beams
                {
                    double A = htop * btop * 100; //Area in mm^2
                    double I = ((btop * Math.Pow(htop, 3)) / 12) * 10000;
                    var N = maxN[i];
                    var M = maxM[i];
                    var u = MNUtilization(N, M, htop, btop, A, I, fcd, ftd, fmd);
                    utillist.Add(u);
                }
                if (beams[i].id.StartsWith("Truss")) //Truss beams
                {
                    double A = htruss * btruss * 100; //Area in mm^2
                    double I = ((btruss * Math.Pow(htruss, 3)) / 12) * 10000;
                    var N = maxN[i];
                    var M = maxM[i];
                    var u = MNUtilization(N, M, htruss, btruss, A, I, fcd, ftd, fmd);
                    utillist.Add(u);
                }
                if (beams[i].id.StartsWith("Bottom")) //Bottom beams
                {
                    double A = hbottom * bbottom * 100; //Area in mm^2
                    double I = ((bbottom * Math.Pow(hbottom, 3)) / 12) * 10000;
                    var N = maxN[i];
                    var M = maxM[i];
                    var u = MNUtilization(N, M, hbottom, bbottom, A, I, fcd, ftd, fmd);
                    utillist.Add(u);
                }
                if (beams[i].id.StartsWith("Column")) //Columns
                {
                    double A = hcol * bcol * 100; //Area in mm^2
                    double I = ((bcol * Math.Pow(hcol, 3)) / 12) * 10000;
                    var N = maxN[i];
                    var M = maxM[i];
                    var u = MNUtilization(N, M, hcol, bcol, A, I, fcd, ftd, fmd);
                    utillist.Add(u);
                }
                if (beams[i].id.StartsWith("Purlin")) //Purlin beams
                {
                    double A = hpur * bpur * 100; //Area in mm^2
                    double I = ((bpur * Math.Pow(hpur, 3)) / 12) * 10000;
                    var N = maxN[i];
                    var M = maxM[i];
                    var u = MNUtilization(N, M, hpur, bpur, A, I, fcd, ftd, fmd);
                    utillist.Add(u);
                }
            }

            //Shear check
            double fvd = fv * kmod / g_m;
            double k_cr = 0.67;
            var shearlist = new List<double>();
            for (int i = 0; i < maxV.Count; i++)
            {
                if (beams[i].id.StartsWith("Top")) //Top Beams
                {
                    var h = htop;
                    var b = btop;
                    var S = (b * 10 * Math.Pow(h * 10, 2)) / 6;
                    double I = ((b * Math.Pow(h, 3)) / 12) * 10000;
                    var tau = Math.Abs((maxV[i] * 1000 * S) / (I * b * 10 * k_cr));
                    shearlist.Add(tau / fvd);
                }
                if (beams[i].id.StartsWith("Truss")) //Truss beams
                {
                    var h = htruss;
                    var b = btruss;
                    var S = (b * 10 * Math.Pow(h * 10, 2)) / 6;
                    double I = ((b * Math.Pow(h, 3)) / 12) * 10000;
                    var tau = Math.Abs((maxV[i] * 1000 * S) / (I * b * 10 * k_cr));
                    shearlist.Add(tau / fvd);
                }
                if (beams[i].id.StartsWith("Bottom")) //Bottom beams
                {
                    var h = hbottom;
                    var b = bbottom;
                    var S = (b * 10 * Math.Pow(h * 10, 2)) / 6;
                    double I = ((b * Math.Pow(h, 3)) / 12) * 10000;
                    var tau = Math.Abs((maxV[i] * 1000 * S) / (I * b * 10 * k_cr));
                    shearlist.Add(tau / fvd);
                }
                if (beams[i].id.StartsWith("Column")) //Columns
                {
                    var h = hcol;
                    var b = bcol;
                    var S = (b * 10 * Math.Pow(h * 10, 2)) / 6;
                    double I = ((b * Math.Pow(h, 3)) / 12) * 10000;
                    var tau = Math.Abs((maxV[i] * 1000 * S) / (I * b * 10 * k_cr));
                    shearlist.Add(tau / fvd);
                }
                if (beams[i].id.StartsWith("Purlin")) //Purlin beams
                {
                    var h = hpur;
                    var b = bpur;
                    var S = (b * 10 * Math.Pow(h * 10, 2)) / 6;
                    double I = ((b * Math.Pow(h, 3)) / 12) * 10000;
                    var tau = Math.Abs((maxV[i] * 1000 * S) / (I * b * 10 * k_cr));
                    shearlist.Add(tau / fvd);
                }
            }

            //Buckling checks
            var buckly = new List<double>();
            var bucklz = new List<double>();
            var kczlist = new List<double>();
            var E005 = matr[4] *1000; //N/mm^2
            var beta_c = 0.2; //for solid timber (konstruksjonstre)
            var testc = new List<double>();
            var Gmean = matr[5] *1000; //N/mm^2 //For the LTB buckling checks
            var LTB = new List<double>();

            for (int i = 0; i < beams.Count; i++)
            {
                double I_y = 0, I_z = 0, A = 0, h = 0, b = 0, lk = 0, lef = 0;
                if (beams[i].id.StartsWith("Top")) //Top Beams
                {
                    h = htop;
                    b = btop;
                    lk = allines[i].Length * 0.5 * 1000;
                    lef = allines[i].Length * 1000 - 2*htop*10;
                }
                if (beams[i].id.StartsWith("Truss")) //Truss beams
                {
                    h = htruss;
                    b = btruss;
                    lk = allines[i].Length * 1.0 * 1000;
                    lef = allines[i].Length * 1000;
                }
                if (beams[i].id.StartsWith("Bottom")) //Bottom beams
                {
                    h = hbottom;
                    b = bbottom;
                    lk = allines[i].Length * 0.5 * 1000;
                    lef = allines[i].Length * 1000;
                }
                if (beams[i].id.StartsWith("Column")) //Columns
                {
                    h = hcol;
                    b = bcol;
                    lk = allines[i].Length * 0.7 * 1000;
                    lef = allines[i].Length * 1000;
                }
                if (beams[i].id.StartsWith("Purlin")) //Purlin beams
                {
                    h = hpur;
                    b = bpur;
                    lk = allines[i].Length * 0.5 * 1000;
                    lef = allines[i].Length * 1000*0.9 - 2*hpur*10;
                }
                I_y = ((b * Math.Pow(h, 3)) / 12) * 10000;
                I_z = ((h * Math.Pow(b, 3)) / 12) * 10000;
                A = h * b * 100;

                //Buckling checks as columns

                //Buckling in Y direction
                var gamma_y = lk / (Math.Sqrt(I_y / A));
                var gamma_rely = (gamma_y / Math.PI) * Math.Sqrt(Math.Abs(fcd) / E005);
                var k_y = 0.5 * (1 + beta_c * (gamma_rely - 0.3) + Math.Pow(gamma_rely, 2));
                var k_cy = 1 / (k_y + Math.Sqrt(Math.Pow(k_y, 2) - Math.Pow(gamma_rely, 2)));

                //Buckling in Z direction
                var gamma_z = lk / (Math.Sqrt(I_z / A));
                var gamma_relz = (gamma_z / Math.PI) * Math.Sqrt(Math.Abs(fcd) / (E005));
                var k_z = 0.5 * (1 + beta_c * (gamma_relz - 0.3) + Math.Pow(gamma_relz, 2));
                var k_cz = 1 / (k_z + Math.Sqrt(Math.Pow(k_z, 2) - Math.Pow(gamma_relz, 2)));
                kczlist.Add(k_cz);

                //Buckling checks
                if (maxN[i] >= 0)
                {
                    var bchecky = Math.Abs((maxN[i] * 1000 / A) / (k_cy * fcd)) + Math.Abs(((maxM[i] * 10000000 * h / 2) / (I_y)) / (fmd));
                    var bcheckz = (maxN[i] * 1000 / A) / (k_cz * fcd) + 0.7 * Math.Abs(((maxM[i] * 10000000 * h / 2) / (I_y)) / (fmd));
                    buckly.Add(bchecky);
                    bucklz.Add(bcheckz);
                }
                if (maxN[i] < 0)
                {
                    var bchecky = 0;
                    var bcheckz = 0;
                    buckly.Add(bchecky);
                    bucklz.Add(bcheckz);
                }

                //LTB check (as beams)
                var I_tor = (1.0 / 3) * (1 - 0.63 * b / h) * h * Math.Pow(b, 3) * 10000;
                var W_y = (b * Math.Pow(h, 2) * 1000) / (6);
                var sig_mcrit = (Math.PI * Math.Sqrt(E005 * I_z * Gmean * I_tor)) / (lef * W_y);
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
                    var LTBcheck = Math.Abs(Math.Pow((((maxM[i] * 10000000 * h / 2) / (I_y)) / (kcrit * fmd)), 2)) +
                        Math.Abs((maxN[i] * 1000 / A) / (kczlist[i] * fcd));
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

            var totalmaxutil = $"N+M Utilization: {Math.Round(utillist.Max(), 4)} at {beams[utillist.IndexOf(utillist.Max())].id}, " +
                $"V Utilization: {Math.Round(shearlist.Max(), 4)} at {beams[shearlist.IndexOf(shearlist.Max())].id}, " +
                $"Buckling Y: {Math.Round(buckly.Max(), 4)} -> {bucklingY} at {beams[buckly.IndexOf(buckly.Max())].id}, " +
                $"Buckling Z: {Math.Round(bucklz.Max(), 4)} -> {bucklingZ} at {beams[bucklz.IndexOf(bucklz.Max())].id}, " +
                $"Buckling LTB: {Math.Round(LTB.Max(), 4)} -> {bucklingLTB} at {beams[LTB.IndexOf(LTB.Max())].id}";

            //Merging the top beams and bottom beams to continous beams
            var topcrvslong = new List<Curve>();
            var bottomcrvslong = new List<Curve>();
            for (int i = 0; i < roofpts3d.Count; i++)
            {
                var topline = new Line(roofpts3d[i][0], roofpts3d[i][roofpts3d[i].Count / 2]);
                var topline2 = new Line(roofpts3d[i][roofpts3d[i].Count / 2], roofpts3d[i][roofpts3d[i].Count - 1]);
                var bottomline = new Line(roofpts3d[i][0], roofpts3d[i][roofpts3d[i].Count - 1]);
                topcrvslong.Add(topline.ToNurbsCurve());
                topcrvslong.Add(topline2.ToNurbsCurve());
                bottomcrvslong.Add(bottomline.ToNurbsCurve());
            }
            //Creating breps of the beams
            var allbreps = new List<Brep>();
            var allbrepsIDs = new List<string>();
            var beamdims = new List<double>();
            foreach (var c in topcrvslong)
            {
                var pt1 = c.PointAtStart;
                var pt2 = c.PointAtEnd;
                var beamdir = new Vector3d(pt2.X - pt1.X, pt2.Y - pt1.Y, pt2.Z - pt1.Z);
                var xdir = Vector3d.CrossProduct(Vector3d.ZAxis, beamdir);
                var pl = new Plane(pt1, xdir, Vector3d.CrossProduct(beamdir, xdir));
                var shape = new Rectangle3d(pl, new Interval(-btop / 200, btop / 200), new Interval(-htop / 200, htop / 200));
                var brep = Brep.CreateFromSweep(c, shape.ToNurbsCurve(), true, 0.01)[0].CapPlanarHoles(0.01);
                allbreps.Add(brep);
                allbrepsIDs.Add("Top Beam");
                beamdims.Add(new Line(pt1, pt2).Length);
            }
            for (int i = 0; i < allcurves.Count; i++)
            {
                var pt1 = allcurves[i].PointAtStart;
                var pt2 = allcurves[i].PointAtEnd;
                if (beams[i].id.StartsWith("Truss")) //Truss beams
                {
                    if (Math.Abs(pt1.X - pt2.X) + 0.1 > 0.2)
                    {
                        var beamdir = new Vector3d(pt2.X - pt1.X, pt2.Y - pt1.Y, pt2.Z - pt1.Z);
                        var xdir = Vector3d.CrossProduct(Vector3d.ZAxis, beamdir);
                        var pl = new Plane(pt1, xdir, Vector3d.CrossProduct(beamdir, xdir));
                        var shape = new Rectangle3d(pl, new Interval(-btruss / 200, btruss / 200), new Interval(-htruss / 200, htruss / 200));
                        var brep = Brep.CreateFromSweep(allcurves[i], shape.ToNurbsCurve(), true, 0.01)[0].CapPlanarHoles(0.01);
                        allbreps.Add(brep);
                        allbrepsIDs.Add("Truss Beam");
                        beamdims.Add(new Line(pt1, pt2).Length);
                    }
                    if (Math.Abs(pt1.X - pt2.X) + 0.1 <= 0.2)
                    {
                        var pl = new Plane(pt1, new Vector3d(0, 0, 1));
                        var shape = new Rectangle3d(pl, new Interval(-btruss / 200, btruss / 200), new Interval(-htruss / 200, htruss / 200));
                        var brep = Brep.CreateFromSweep(allcurves[i], shape.ToNurbsCurve(), true, 0.01)[0].CapPlanarHoles(0.01);
                        allbreps.Add(brep);
                        allbrepsIDs.Add("Truss Beam");
                        beamdims.Add(new Line(pt1, pt2).Length);
                    }
                }
                if (beams[i].id.StartsWith("Column")) //Columns
                {
                    var pl = new Plane(pt1, new Vector3d(0, 0, 1));
                    var shape = new Rectangle3d(pl, new Interval(-bcol / 200, bcol / 200), new Interval(-hcol / 200, hcol / 200));
                    var brep = Brep.CreateFromSweep(allcurves[i], shape.ToNurbsCurve(), true, 0.01)[0].CapPlanarHoles(0.01);
                    allbreps.Add(brep);
                    allbrepsIDs.Add("Column");
                    beamdims.Add(new Line(pt1, pt2).Length);
                }
                if (beams[i].id.StartsWith("Purlin")) //Purlin beams
                {
                    var beamdir = new Vector3d(pt2.X - pt1.X, pt2.Y - pt1.Y, 0);
                    var xdir = Vector3d.CrossProduct(Vector3d.ZAxis, beamdir);
                    var pl = new Plane(pt1, xdir, Vector3d.CrossProduct(beamdir, xdir));
                    var shape = new Rectangle3d(pl, new Interval(-bpur / 200, bpur / 200), new Interval(-hpur / 200, hpur / 200));
                    var brep = Brep.CreateFromSweep(allcurves[i], shape.ToNurbsCurve(), true, 0.01)[0].CapPlanarHoles(0.01);
                    allbreps.Add(brep);
                    allbrepsIDs.Add("Purlin");
                    beamdims.Add(new Line(pt1, pt2).Length);
                }
            }

            foreach (var c in bottomcrvslong)
            {
                var pt1 = c.PointAtStart;
                var pt2 = c.PointAtEnd;
                var beamdir = new Vector3d(pt2.X - pt1.X, pt2.Y - pt1.Y, 0);
                var xdir = Vector3d.CrossProduct(Vector3d.ZAxis, beamdir);
                var pl = new Plane(pt1, xdir, Vector3d.CrossProduct(beamdir, xdir));
                var shape = new Rectangle3d(pl, new Interval(-bbottom / 200, bbottom / 200), new Interval(-hbottom / 200, hbottom / 200));
                var brep = Brep.CreateFromSweep(c, shape.ToNurbsCurve(), true, 0.01)[0].CapPlanarHoles(0.01);
                allbreps.Add(brep);
                allbrepsIDs.Add("Bottom Beam");
                beamdims.Add(new Line(pt1, pt2).Length);
            }

            //Getting info about the dimensions of the beams
            var beamgeom = new List<List<string>>();

            for (int i = 0; i < beamdims.Count; i++)
            {
                var geom = new List<String>();
                if (allbrepsIDs[i].StartsWith("Top Beam"))
                {
                    geom.Add($"{htop}");
                    geom.Add($"{btop}");
                    geom.Add($"{beamdims[i]}");
                }
                if (allbrepsIDs[i].StartsWith("Truss"))
                {
                    geom.Add($"{htruss}");
                    geom.Add($"{btruss}");
                    geom.Add($"{beamdims[i]}");
                }
                if (allbrepsIDs[i].StartsWith("Column"))
                {
                    geom.Add($"{hcol}");
                    geom.Add($"{bcol}");
                    geom.Add($"{beamdims[i]}");
                }
                if (allbrepsIDs[i].StartsWith("Purlin"))
                {
                    geom.Add($"{hpur}");
                    geom.Add($"{bpur}");
                    geom.Add($"{beamdims[i]}");
                }
                if (allbrepsIDs[i].StartsWith("Bottom"))
                {
                    geom.Add($"{hbottom}");
                    geom.Add($"{bbottom}");
                    geom.Add($"{beamdims[i]}");
                }
                beamgeom.Add(geom);
            }
            var beamgeomtree = GHTreeMaker.StringTree(beamgeom); //Converting to tree

            //For optimization
            var utilopt = Math.Abs(new List<double>() { utillist.Max(), buckly.Max(), bucklz.Max() }.Max() - 0.95);
            //Output
            DA.SetData(0, utilopt);
            DA.SetData(1, totalmaxutil);
            DA.SetDataTree(2, beamgeomtree);
            DA.SetDataList(3, allbreps);  
        }
        private Karamba.Models.Model model;
        private Karamba.Models.Model Amodel;
        List<Line3> ConvertToLine3Alt(List<List<Curve>> crvslistlist)
        {
            List<Line3> lines3 = new List<Line3>();

            foreach (var crvs in crvslistlist)
            {
                foreach(Curve c in crvs)
            {
                    Point3d pt1 = c.PointAtStart;
                    Point3d pt2 = c.PointAtEnd;
                    Point3 p1 = new Point3(pt1.X, pt1.Y, pt1.Z);
                    Point3 p2 = new Point3(pt2.X, pt2.Y, pt2.Z);
                    Line3 l3 = new Line3(p1, p2);
                    lines3.Add(l3);
                }
            }
            
            return lines3;
        }

        List<List<Curve>> Curvelist(GH_Structure<GH_Curve> GHcurves)
        {
            var crvlistlist = new List<List<Curve>>();

            foreach (var path in GHcurves.Paths)
            {
                var crv = new List<Curve>();
                var branch = GHcurves.get_Branch(path);
                foreach (GH_Curve c in branch)
                    crv.Add(c.Value);
                crvlistlist.Add(crv);
            }

            return crvlistlist;
        }

        List<List<Point3d>> Pointlist(GH_Structure<GH_Point> GHpoints)
        {
            var pointlistlist = new List<List<Point3d>>();

            foreach (var path in GHpoints.Paths)
            {
                var pt = new List<Point3d>();
                var branch = GHpoints.get_Branch(path);
                foreach (GH_Point p in branch)
                    pt.Add(p.Value);
                pointlistlist.Add(pt);
            }

            return pointlistlist;
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

        string GetString(string text, string pattern)
        {
            var s = Regex.Match(text, pattern);
            return s.Groups[1].Value;
        }

        double GetDouble(string text, string pattern)
        {
            var s = Regex.Match(text, pattern);
            return double.Parse(s.Groups[1].Value, CultureInfo.InvariantCulture) * 100 * 100;
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

            for (int i = 0; i < Ilist.Count; i = i + 3)
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
            get { return new Guid("AE30F1BE-37BE-4FC7-91D4-4F062197F69A"); }
        }
    }
}