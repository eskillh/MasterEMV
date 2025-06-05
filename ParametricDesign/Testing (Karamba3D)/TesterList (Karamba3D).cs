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

namespace Masterv2
{
    public class TesterList__Karamba3D_ : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the TesterList__Karamba3D_ class.
        /// </summary>
        public TesterList__Karamba3D_()
          : base("TesterList (Karamba3D)", "Nickname",
              "Description",
              "Master", "Karamba3D")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("TopCurves", "TopCrvs", "Curves that represents the top beams", GH_ParamAccess.list);
            pManager.AddCurveParameter("TrussCurves", "TrussCrvs", "Curves that represents the truss beams", GH_ParamAccess.list);
            pManager.AddCurveParameter("BottomCurves", "BtmCrvs", "Curves that represents the bottom beams", GH_ParamAccess.list);
            pManager.AddPointParameter("SupportPoints", "sPts", "Points of the location of the supports", GH_ParamAccess.list);
            pManager.AddTextParameter("Material", "mat", "Material of the beams", GH_ParamAccess.item);
            pManager.AddNumberParameter("CrossSections","CSdim","Cross section dimensions of the top beam, truss and bottom beam" +
                "[h_top, b_top, h_truss, b_truss, h_bottom, b_bottom]", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("MaxDisplacement", "dMax", "Maximum displacement of the truss", GH_ParamAccess.item);
            pManager.AddGenericParameter("test1", "t1", "", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Utilization", "util", "Utilization of the beams", GH_ParamAccess.item);
            pManager.AddGenericParameter("JointForces", "jF", "N, V and M forces where the joints are", GH_ParamAccess.list);
            pManager.AddGenericParameter("Breps", "brep", "The truss represented as Breps", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> topcrvs = new List<Curve>();
            List<Curve> trusscrvs = new List<Curve>();
            List<Curve> bottomcrvs = new List<Curve>();
            List<Point3d> supports3d = new List<Point3d>();
            string material = "";
            List<double> dimlist = new List<double>();

            DA.GetDataList(0, topcrvs);
            DA.GetDataList(1, trusscrvs);
            DA.GetDataList(2, bottomcrvs);
            DA.GetDataList(3, supports3d);
            DA.GetData(4, ref material);
            DA.GetDataList(5, dimlist);

            //Getting points on top beams where point load is acting
            var toppointsstart = new List<Point3d>();
            var toppoints = new List<Point3d>();
            for (int i = 0; i < topcrvs.Count; i++)
            {
                if (i < topcrvs.Count - 1)
                    toppointsstart.Add(topcrvs[i].PointAtStart);
                if (i == topcrvs.Count - 1)
                {
                    toppointsstart.Add(topcrvs[i].PointAtStart);
                    toppointsstart.Add(topcrvs[i].PointAtEnd);
                }
            }
            if (topcrvs.Count > 29)
            {
                for (int i = 0; i < toppointsstart.Count; i = i + 8)
                    toppoints.Add(toppointsstart[i]);
            }
            if (topcrvs.Count < 29)
                toppoints = toppointsstart;

            //Converting data to Karamba Geometry
            var supports3 = ConvertToPoint3(supports3d);
            var top3 = ConvertToLine3(topcrvs);
            var truss3 = ConvertToLine3(trusscrvs);
            var bottom3 = ConvertToLine3(bottomcrvs);

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

            //For the rest of the operations, I need a for loop to iterate trought the different cross sections
            List<double> results = new List<double>(); //list to store the max displacements for the different cross sections
            List<double> util = new List<double>();
            List<string> info = new List<string>();
            List<CroSec> allbeams = new List<CroSec>();
            var utilfull = new List<double>();
            List<Curve> critcurve = new List<Curve>();

            //For displacement check
            var span = (new Line(supports3d[0], supports3d[1])).Length;
            var check = span * 100 / 250;

            //Creating cross sections
            double htop = dimlist[0];
            double btop = dimlist[1];
            double htruss = dimlist[2];
            double btruss = dimlist[3];
            double hbottom = dimlist[4];
            double bbottom = dimlist[5];

            CroSec recttop = k3d.CroSec.Trapezoid(htop, btop, btop, mat, name, $"{name}:{htop}x{btop}");
            CroSec recttruss = k3d.CroSec.Trapezoid(htruss, btruss, btruss, mat, name, $"{name}:{htruss}x{btruss}");
            CroSec rectbottom = k3d.CroSec.Trapezoid(hbottom, bbottom, bbottom, mat, name, $"{name}:{hbottom}x{bbottom}");

            //Creating beams
            var logger = new MessageLogger();
            List<BuilderBeam> beams = new List<BuilderBeam>();

            List<BuilderBeam> topbeams = new List<BuilderBeam>();
            foreach (Line3 l in top3) //Creating beams for each line and adding to list
            {
                List<BuilderBeam> bm = k3d.Part.LineToBeam(l, "Top Beam", recttop, logger, out _);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklY, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklZ, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklLT, l.Length);
                topbeams.Add(bm[0]);
                beams.Add(bm[0]);

            }

            List<BuilderBeam> trussbeams = new List<BuilderBeam>();
            foreach (Line3 l in truss3)
            {
                List<BuilderBeam> bm = k3d.Part.LineToBeam(l, "Truss Beam", recttruss, logger, out _, false);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklY, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklZ, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklLT, l.Length);
                trussbeams.Add(bm[0]);
                beams.Add(bm[0]);
            }

            List<BuilderBeam> bottombeams = new List<BuilderBeam>();
            foreach (Line3 l in bottom3)
            {
                List<BuilderBeam> bm = k3d.Part.LineToBeam(l, "Bottom Beam", rectbottom, logger, out _);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklY, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklZ, l.Length);
                bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklLT, l.Length);
                bottombeams.Add(bm[0]);
                beams.Add(bm[0]);
            }

            foreach (BuilderBeam beam in beams)
                allbeams.Add(beam.crosec);

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

            List<string> beamnames = new List<string>();
            for (int i = 0; i < topbeams.Count; i++)
                beamnames.Add("Top Beam");

            //Distributed load
            var list01 = ListFrom0To1(2);
            List<double> loadlist = new List<double>();
            for (int i = 0; i < list01.Count; i++)
                loadlist.Add(10);
            loads.Add(k3d.Load.DistributedForceLoad(
                new Vector3(0, 0, 1), loadlist,
                list01, LoadOrientation.global, "LC0", beamnames));
            /*
            //Point load
            foreach (Point3 pt in ConvertToPoint3(toppoints))
                loads.Add(k3d.Load.PointLoad(pt, new Vector3(0, 0, 4)));
            */
            
            //Assembling model
            model = k3d.Model.AssembleModel(
                beams, supports, loads,
                out _, out _, out _, out _, out _);

            //Analyzing model
            IReadOnlyList<string> LCs = new List<string> { "LC0" };

            Amodel = k3d.Algorithms.Analyze(model, LCs,
                out IReadOnlyList<double> maxD,
                out _, out _, out string w);

            double maxDisp = maxD[0] * 100;
            results.Add(maxDisp);


            //Creating a list with all the beam names
            List<string> allbeamnames = beamnames;
            for (int i = 0; i < trussbeams.Count; i++)
                beamnames.Add("Truss Beam");
            for (int i = 0; i < bottombeams.Count; i++)
                beamnames.Add("Bottom Beam");

            //ElementGUIDs
            List<Guid> elementGuids = new List<Guid>();
            for (int i = 0; i < topbeams.Count; i++)
                elementGuids.Add(topbeams[i].guid);
            for (int i = 0; i < trussbeams.Count; i++)
                elementGuids.Add(trussbeams[i].guid);
            for (int i = 0; i < bottombeams.Count; i++)
                elementGuids.Add(bottombeams[i].guid);

            //Getting the internal forces for the analyzed model
            Karamba.Results.BeamForces.solve(Amodel, allbeamnames, elementGuids,
                "LC0", new List<double> { 0, 0.5, 1 },
                out List<List<List<Vector3>>> forces2,
                out List<List<List<Vector3>>> moments2, out _, out _, out _);

            //Adding the info about the length of the beams 
            List<string> beaminfo = new List<string>();

            var tbeamlength = top3[0].Length * top3.Count() / 2;
            beaminfo.Add($"Top beam:{tbeamlength} m");
            beaminfo.Add($"Top beam:{tbeamlength} m");

            foreach (Line3 truss in truss3)
            {
                var length = truss.Length;
                beaminfo.Add($"Truss:{length} m");
            }

            var bbeamlength = bottom3[0].Length * bottom3.Count();
            beaminfo.Add($"Bottom beam:{bbeamlength} m");

            //Making trees for the forces
            DataTree<Vector3> forcestree = new DataTree<Vector3>();
            for (int i = 0; i < forces2.Count; i++)
            {
                for (int j = 0; j < forces2[i].Count; j++)
                {
                    for (int k = 0; k < forces2[i][j].Count; k++)
                    {
                        forcestree.Add(forces2[i][j][k], new Grasshopper.Kernel.Data.GH_Path(i, j));
                    }
                }
            }

            DataTree<Vector3> momentstree = new DataTree<Vector3>();
            for (int i = 0; i < moments2.Count; i++)
            {
                for (int j = 0; j < moments2[i].Count; j++)
                {
                    for (int k = 0; k < moments2[i][j].Count; k++)
                    {
                        momentstree.Add(moments2[i][j][k], new Grasshopper.Kernel.Data.GH_Path(i, j));
                    }
                }
            }

            //Utilization
            List<double> utillist = new List<double>();

            List<double> Nlist = new List<double>(); //Creating a list for the N values
            for (int i = 0; i < forces2.Count / 2; i++)
                for (int j = 0; j < forces2[i].Count; j++)
                    Nlist.Add(forces2[i][j][0][0]);

            List<double> Mlist = new List<double>(); //Creating a list for the M values
            for (int i = 0; i < moments2.Count / 2; i++)
                for (int j = 0; j < moments2[i].Count; j++)
                    Mlist.Add(moments2[i][j][0][1]);

            double kmod = 0.9;
            double g_m = 1.3;
            double fmd = fm * kmod / g_m;
            double fcd = Math.Abs(fc) * kmod / (g_m * 1000);
            double ftd = Math.Abs(ft) * kmod / (g_m * 1000);

            for (int i = 0; i < Nlist.Count; i++)
            {
                if (i < topbeams.Count * 3) //top beams
                {
                    double A = htop * btop * 100; //Area in mm^2
                    double I = ((btop * Math.Pow(htop, 3)) / 12) * 10000;

                    if (Nlist[i] > 0)
                    {
                        double u = Math.Abs(Math.Pow(((Nlist[i] * 1000 / A) / fcd), 2)) +
                            Math.Abs(((Mlist[i] * 10000000 * htop / 2) / (I)) / fmd);
                        utillist.Add(u);
                    }
                    if (Nlist[i] <= 0)
                    {
                        double u = Math.Abs((Nlist[i] / A) / (ftd)) +
                            Math.Abs(((Mlist[i] * 10000000 * htop / 2) / (I)) / (fmd));
                        utillist.Add(u);
                    }
                }
                if (i >= topbeams.Count * 3 && i < (beams.Count - bottombeams.Count)*3) //truss beams
                {
                    double A = htruss * btruss * 100; //Area in mm^2
                    double I = ((btruss * Math.Pow(htruss, 3)) / 12) * 10000;

                    if (Nlist[i] > 0)
                    {
                        double u = Math.Abs(Math.Pow(((Nlist[i] * 1000 / A) / fcd), 2)) +
                            Math.Abs(((Mlist[i] * 10000000 * htruss / 2) / (I)) / fmd);
                        utillist.Add(u);
                    }
                    if (Nlist[i] <= 0)
                    {
                        double u = Math.Abs((Nlist[i] / A) / (ftd)) +
                            Math.Abs(((Mlist[i] * 10000000 * htruss / 2) / (I)) / (fmd));
                        utillist.Add(u);
                    }
                }
                if (i >= (beams.Count - bottombeams.Count) * 3) //bottom beams
                {
                    double A = hbottom * bbottom * 100; //Area in mm^2
                    double I = ((bbottom * Math.Pow(hbottom, 3)) / 12) * 10000;

                    if (Nlist[i] > 0)
                    {
                        double u = Math.Abs(Math.Pow(((Nlist[i] * 1000 / A) / fcd), 2)) +
                            Math.Abs(((Mlist[i] * 10000000 * hbottom / 2) / (I)) / fmd);
                        utillist.Add(u);
                    }
                    if (Nlist[i] <= 0)
                    {
                        double u = Math.Abs((Nlist[i] / A) / (ftd)) +
                            Math.Abs(((Mlist[i] * 10000000 * hbottom / 2) / (I)) / (fmd));
                        utillist.Add(u);
                    }
                }
            }
            
            //Finding the max utilization per beam, as there is 3 points per beam
            double maxUtil = utillist.Max();
            List<List<double>> splitutillist = utillist.Chunk(3).Select(chunk => chunk.ToList()).ToList();

            List<double> maxutillist = new List<double>();
            for (int i = 0; i < splitutillist.Count; i++)
                maxutillist.Add(splitutillist[i].Max());

            //Shear check
            List<double> Vlist = new List<double>(); //Creating a list for the N values
            for (int i = 0; i < forces2.Count / 2; i++)
                for (int j = 0; j < forces2[i].Count; j++)
                    Vlist.Add(forces2[i][j][0][2]);

            double fvd = fv * kmod / g_m;
            double k_cr = 0.67;
            var shearlist = new List<double>();

            for (int i = 0; i < Vlist.Count; i++)
            {
                if (i < topbeams.Count*3) //top beams
                {
                    var S = (btop * 10 * Math.Pow(htop * 10, 2)) / 6; 
                    double I = ((btop * Math.Pow(htop, 3)) / 12) * 10000;
                    var tau = Math.Abs((Vlist[i] * 1000 * S) / (I * btop * 10 * k_cr));
                    shearlist.Add(tau / fvd);
                }
                if (i >= topbeams.Count * 3 && i < (beams.Count - bottombeams.Count) * 3) //truss beams
                {
                    var S = (btruss * 10 * Math.Pow(htruss * 10, 2)) / 6;
                    double I = ((btruss * Math.Pow(htruss, 3)) / 12) * 10000;
                    var tau = Math.Abs((Vlist[i] * 1000 * S) / (I * btruss * 10 * k_cr));
                    shearlist.Add(tau / fvd);
                }
                if (i >= (beams.Count - bottombeams.Count) * 3) //bottom beams
                {
                    var S = (bbottom * 10 * Math.Pow(hbottom * 10, 2)) / 6;
                    double I = ((bbottom * Math.Pow(hbottom, 3)) / 12) * 10000;
                    var tau = Math.Abs((Vlist[i] * 1000 * S) / (I * bbottom * 10 * k_cr));
                    shearlist.Add(tau / fvd);
                }
            }
            
            
            //Max shear per beam
            List<List<double>> splitshearlist = shearlist.Chunk(3).Select(chunk => chunk.ToList()).ToList();

            List<double> maxshearlist = new List<double>();
            for (int i = 0; i < splitshearlist.Count; i++)
                maxshearlist.Add(splitshearlist[i].Max());

            //Creating complete arch beams for visualization and further use
            List<Brep> testing = new List<Brep>();
            List<Line> toplines = ConvertToLine(top3);
            var topplines = new List<Polyline>();
            var divpoints1 = new List<Point3d>();
            var divpoints2 = new List<Point3d>();
            var alldivpoints = new List<Point3d>();

            Polyline onehalfpl = new Polyline();
            for (int i = 0; i < top3.Count / 2 + 1; i++)
            {
                onehalfpl.Add(toplines[i].From);
                divpoints1.Add(toplines[i].PointAt(0));
                alldivpoints.Add(toplines[i].PointAt(0));
            }

            Polyline sndhalfpl = new Polyline();
            for (int i = top3.Count / 2 - 1; i < top3.Count; i++)
            {
                sndhalfpl.Add(toplines[i].To);
                divpoints2.Add(toplines[i].PointAt(1));
                alldivpoints.Add(toplines[i].PointAt(1));
            }

            topplines.Add(onehalfpl);
            topplines.Add(sndhalfpl);

            //Max utilizaiton for each of the two arches
            var utilarch = new List<double>();
            utilarch.Add(maxutillist.GetRange(0, maxutillist.Count / 2).Max());

            //Iterating trough all beams and getting values and lists
            List<Line3> beams3 = new List<Line3>();
            List<Curve> allcurves = new List<Curve>();
            var bucklinglengths = new List<double>(); //Buckling lengths for further use
            var lef = new List<double>(); //Effective length for LTB check

            for (int i = 0; i < top3.Count; i++) //Buckling length: assume 1 for all (conservative)
            {
                beams3.Add(top3[i]);
                allcurves.Add(topcrvs[i]);
                bucklinglengths.Add(top3[i].Length * 0.7 * 1000);
                lef.Add(top3[i].Length * 0.9 * 1000 + 2 * htop * 10);
            }
            for (int i = 0; i < truss3.Count; i++)
            {
                beams3.Add(truss3[i]);
                allcurves.Add(trusscrvs[i]);
                bucklinglengths.Add(truss3[i].Length * 1 * 1000);
                lef.Add(0);
            }
            for (int i = 0; i < bottom3.Count; i++)
            {
                beams3.Add(bottom3[i]);
                allcurves.Add(bottomcrvs[i]);
                bucklinglengths.Add(bottom3[i].Length * 0.7 * 1000);
                lef.Add(bottom3[i].Length * 0.9 * 1000);
            }

            List<string> beamres = new List<string>();
            if (top3[0].Length < bottom3[0].Length) //Arches
            {
                var maxutilarch1 = maxutillist.GetRange(0, maxutillist.Count / 2).Max();
                var maxutilarch2 = maxutillist.GetRange(maxutillist.Count / 2, maxutillist.Count / 2).Max();
                var maxsheararch1 = maxshearlist.GetRange(0, maxshearlist.Count / 2).Max();
                var maxsheararch2 = maxshearlist.GetRange(maxshearlist.Count / 2, maxshearlist.Count / 2).Max();
                beamres.Add($"Element: {0}, {allbeamnames[0]}, " +
                        $"Length: {Math.Round(topplines[0].Length, 4)}, " +
                        $"Utilization: {Math.Round(maxutilarch1, 4)}, " +
                        $"Shear Utilization: {Math.Round(maxsheararch1, 4)}");
                beamres.Add($"Element: {1}, {allbeamnames[1]}, " +
                        $"Length: {Math.Round(topplines[1].Length, 4)}, " +
                        $"Utilization: {Math.Round(maxutilarch2, 4)}, " +
                        $"Shear Utilizaiton: {Math.Round(maxsheararch2, 4)}");
                for (int i = top3.Count; i < beams3.Count; i++) //Trussbeams
                    beamres.Add($"Element: {i}, {allbeamnames[i]}, " +
                        $"Length: {Math.Round(beams3[i].Length, 4)}, " +
                        $"Utilization: {Math.Round(maxutillist[i], 4)}, " +
                        $"Shear Utilization: {Math.Round(maxshearlist[i], 4)}");
            }
            else //Linear top beams
            {
                for (int i = 0; i < beams3.Count; i++)
                    beamres.Add($"Element: {i}, {allbeamnames[i]}, " +
                        $"Length: {Math.Round(beams3[i].Length, 4)}, " +
                        $"Utilization: {Math.Round(maxutillist[i], 4)}, " +
                        $"Shear Utilization: {Math.Round(maxshearlist[i], 4)}");
            }

            //Info about the dimensions of the beams in the truss
            var beamdim = new List<double>();

            Polyline bottombeamfull = new Polyline();
            for (int i = 0; i < bottom3.Count; i++)
            {
                if (i < bottom3.Count - 1)
                {
                    var bline = new Line(bottomcrvs[i].PointAtStart, bottomcrvs[i].PointAtEnd);
                    bottombeamfull.Add(bline.From);
                }
                else
                {
                    var bline = new Line(bottomcrvs[i].PointAtStart, bottomcrvs[i].PointAtEnd);
                    bottombeamfull.Add(bline.From);
                    bottombeamfull.Add(bline.To);
                }
            }

            beamdim.Add(topplines[0].Length);
            beamdim.Add(topplines[1].Length);
            foreach (var t in truss3)
                beamdim.Add(t.Length);
            beamdim.Add(bottombeamfull.Length);

            var beamgeom = new List<List<string>>();
            for (int i = 0; i < beamdim.Count; i++)
            {
                var geom = new List<string>();
                if (i < 2)
                {
                    geom.Add(htop.ToString());
                    geom.Add(btop.ToString());
                    geom.Add(beamdim[i].ToString());
                    beamgeom.Add(geom);
                }
                if (i >= 2 && i < beamdim.Count-1)
                {
                    geom.Add(htruss.ToString());
                    geom.Add(btruss.ToString());
                    geom.Add(beamdim[i].ToString());
                    beamgeom.Add(geom);
                }
                if (i >= beamdim.Count-1)
                {
                    geom.Add(hbottom.ToString());
                    geom.Add(bbottom.ToString());
                    geom.Add(beamdim[i].ToString());
                    beamgeom.Add(geom);
                }
            }

            var beamgeomtree = GHTreeMaker.StringTree(beamgeom); //Converting to tree

            //Finding the max N and M values for each of the elements
            List<List<double>> splitN = Nlist.Chunk(3).Select(chunk => chunk.ToList()).ToList();

            List<double> Nmax = new List<double>();
            for (int i = 0; i < splitN.Count; i++)
            {
                Nmax.Add(splitN[i].Max());
                if (Nmax[i] < 0)
                    Nmax[i] = 0;
            }

            List<List<double>> splitM = Mlist.Chunk(3).Select(chunk => chunk.ToList()).ToList();

            List<double> Mmax = new List<double>();
            for (int i = 0; i < splitM.Count; i++)
                Mmax.Add(splitM[i].Max(Math.Abs));

            //Buckling checks
            var buckly = new List<double>();
            var bucklz = new List<double>();
            var kczlist = new List<double>();
            var E005 = matr[4]; //kN/mm^2
            var beta_c = 0.2; //for solid timber (konstruksjonstre)

            var Gmean = matr[5]; //kN/mm^2 //For the LTB buckling checks
            var LTB = new List<double>();
            var test = new List<double>();

            for (int i = 0; i < beams.Count; i++)
            {
                double I_y = 0, I_z = 0, A = 0, h = 0, b = 0;
                if (i < topbeams.Count) //top beams
                {
                    I_y = ((btop * Math.Pow(htop, 3)) / 12) * 10000;
                    I_z = ((htop * Math.Pow(btop, 3)) / 12) * 10000;
                    A = htop * btop * 100;
                    h = htop;
                    b = btop;
                }
                if (i >= topbeams.Count && i < beams.Count - bottombeams.Count) //truss beams
                {
                    I_y = ((btruss * Math.Pow(htruss, 3)) / 12) * 10000;
                    I_z = ((htruss * Math.Pow(btruss, 3)) / 12) * 10000;
                    A = htruss * btruss * 100;
                    h = htruss;
                    b = btruss;
                }
                if (i >= beams.Count - bottombeams.Count) //bottom beams
                {
                    I_y = ((bbottom * Math.Pow(hbottom, 3)) / 12) * 10000;
                    I_z = ((hbottom * Math.Pow(bbottom, 3)) / 12) * 10000;
                    A = hbottom * bbottom * 100;
                    h = hbottom;
                    b = bbottom;
                }

                //Buckling checks as columns
                var lk = bucklinglengths[i];

                //Buckling in Y direction
                var gamma_y = lk / (Math.Sqrt(I_y / A));
                var gamma_rely = (gamma_y / Math.PI) * Math.Sqrt(fc / (E005 * 1000 * 1000));
                var k_y = 0.5 * (1 + beta_c * (gamma_rely - 0.3) + Math.Pow(gamma_rely, 2));
                var k_cy = 1 / (k_y + Math.Sqrt(Math.Pow(k_y, 2) - Math.Pow(gamma_rely, 2)));

                //Buckling in Z direction
                var gamma_z = lk / (Math.Sqrt(I_z / A));
                var gamma_relz = (gamma_z / Math.PI) * Math.Sqrt(fc / (E005 * 1000 * 1000));
                var k_z = 0.5 * (1 + beta_c * (gamma_relz - 0.3) + Math.Pow(gamma_relz, 2));
                var k_cz = 1 / (k_z + Math.Sqrt(Math.Pow(k_z, 2) - Math.Pow(gamma_relz, 2)));
                kczlist.Add(k_cz);

                //Buckling checks
                if (Nmax[i] != 0)
                {
                    var bchecky = (Nmax[i] * 1000 / A) / (k_cy * fcd) + ((Mmax[i] * 10000000 * h / 2) / (I_y)) / (fmd);
                    var bcheckz = (Nmax[i] * 1000 / A) / (k_cz * fcd) + 0.7 * ((Mmax[i] * 10000000 * h / 2) / (I_y)) / (fmd);
                    buckly.Add(bchecky);
                    bucklz.Add(bcheckz);
                }
                else
                {
                    var bchecky = 0;
                    var bcheckz = 0;
                    buckly.Add(bchecky);
                    bucklz.Add(bcheckz);
                }

                //Buckling check with LTB (as beams)
                var I_tor = (1.0 / 3) * (1 - 0.63 * b / h) * h * Math.Pow(b, 3) * 10000;
                var W_y = (b * Math.Pow(h, 2) * 1000) / (6);
                var sig_mcrit = (Math.PI * Math.Sqrt(E005 * I_z * Gmean * I_tor * 1000000)) / (lef[i] * W_y);
                var lambda_relm = Math.Sqrt(fm / sig_mcrit);
                double kcrit = new double();
                if (lambda_relm <= 0.75)
                {
                    kcrit = 1.0;
                }
                else if (0.75 < lambda_relm && lambda_relm <= 1.4)
                {
                    kcrit = 1.56 - 0.75 * lambda_relm;
                }
                else
                {
                    kcrit = 1.0 / Math.Pow(lambda_relm, 2);
                }

                if (kcrit == 1)
                    LTB.Add(0);
                else
                {
                    var LTBcheck = Math.Pow((((Mmax[i] * 10000000 * h / 2) / (I_y)) / (kcrit * fmd)), 2) + (Nmax[i] * 1000 / A) / (kczlist[i] * fcd);
                    LTB.Add(LTBcheck);
                }
            }
            
            //Breps of the beams
            var allBreps = new List<Brep>();

            //Top left beam
            var pltL = new Plane(divpoints1[0], new Vector3d(alldivpoints[0 + 1].X - alldivpoints[0].X, 0, alldivpoints[0 + 1].Z - alldivpoints[0].Z));
            var shapeL = new Rectangle3d(pltL, new Interval(-htop / 200, htop / 200), new Interval(-btop / 200, btop / 200));
            var onehalfplRebuilt = onehalfpl.ToNurbsCurve().Rebuild(50, 3, true);
            allBreps.Add(Brep.CreateFromSweep(onehalfplRebuilt, shapeL.ToNurbsCurve(), false, 0.1)[0].CapPlanarHoles(0.01));
            //allBreps.Add(Brep.CreateFromSweep(onehalfpl.ToNurbsCurve(), shapeL.ToNurbsCurve(), false, 1)[0].CapPlanarHoles(0.01)); OLD CODE

            //Top right beam
            var pltR = new Plane(divpoints2[0], new Vector3d(alldivpoints[divpoints1.Count + 2].X - alldivpoints[divpoints1.Count + 1].X, 0, alldivpoints[divpoints1.Count + 2].Z - alldivpoints[divpoints1.Count + 1].Z));
            var shapeR = new Rectangle3d(pltR, new Interval(-htop / 200, htop / 200), new Interval(-btop / 200, btop / 200));
            var sndhalfplRebuilt = sndhalfpl.ToNurbsCurve().Rebuild(50, 3, true);
            allBreps.Add(Brep.CreateFromSweep(sndhalfplRebuilt, shapeR.ToNurbsCurve(), false, 0.1)[0].CapPlanarHoles(0.01));

            //Truss beams
            foreach (var truss in trusscrvs)
            {
                var pl = new Plane(truss.PointAtStart, new Vector3d(truss.PointAtEnd.X - truss.PointAtStart.X, 0, truss.PointAtEnd.Z - truss.PointAtStart.Z));
                var shape = new Rectangle3d(pl, new Interval(-htruss / 200, htruss / 200), new Interval(-btruss / 200, btruss / 200));
                allBreps.Add(Brep.CreateFromSweep(truss, shape.ToNurbsCurve(), true, 0.01)[0].CapPlanarHoles(0.01));
            }

            //Bottom beam
            var plB = new Plane(supports3d[0], new Vector3d(1, 0, 0));
            var shapeB = new Rectangle3d(plB, new Interval(-bbottom / 200, bbottom / 200), new Interval(-hbottom / 200, hbottom / 200));
            allBreps.Add(Brep.CreateFromSweep(new Line(supports3d[0], supports3d[1]).ToNurbsCurve(), shapeB.ToNurbsCurve(), true, 0.01)[0].CapPlanarHoles(0.01));
            
            //Finding the total max utilization from N+M and V and Buckling Y and Z
            var bucklingY = "No Buckling";
            if (buckly.Max() > 1.0)
                bucklingY = "Buckling";
            var bucklingZ = "No Buckling";
            if (bucklz.Max() > 1.0)
                bucklingZ = "Buckling";
            var bucklingLTB = "No Buckling";
            if (LTB.Max() > 1.0)
                bucklingLTB = $"{Math.Round(LTB.Max(), 4)} -> {bucklingLTB} at {beams[LTB.IndexOf(LTB.Max())].id} nr {LTB.IndexOf(LTB.Max()) + 1}";

            var totalmaxutil = $"N+M Utilization: {Math.Round(maxutillist.Max(), 4)} at {beams[maxutillist.IndexOf(maxutillist.Max())].id} nr {maxutillist.IndexOf(maxutillist.Max()) + 1}, " +
                $"V Utilization: {Math.Round(maxshearlist.Max(), 4)} at {beams[maxshearlist.IndexOf(maxshearlist.Max())].id} nr {maxshearlist.IndexOf(maxshearlist.Max()) + 1}, " +
                $"Buckling Y: {Math.Round(buckly.Max(), 4)} -> {bucklingY} at {beams[buckly.IndexOf(buckly.Max())].id} nr {buckly.IndexOf(buckly.Max()) + 1}, " +
                $"Buckling Z: {Math.Round(bucklz.Max(), 4)} -> {bucklingZ} at {beams[bucklz.IndexOf(bucklz.Max())].id} nr {bucklz.IndexOf(bucklz.Max()) + 1}, " +
                $"Buckling LTB: {bucklingLTB}";
            
            //Forces where the connections will be
            var N_joint = new List<double>();
            var V_joint = new List<double>();
            var M_joint = new List<double>();

            N_joint.Add(Nlist[0]); //axial force at the left joint
            N_joint.Add(Nlist[3 * (top3.Count / 2)]); //axial force at the top joint
            N_joint.Add(Nlist[3 * (top3.Count - 1) + 2]); //axial force at the right joint

            V_joint.Add(Vlist[0]); //shear force at the left joint
            V_joint.Add(Vlist[3 * (top3.Count / 2)]); //shear force at the top joint
            V_joint.Add(Vlist[3 * (top3.Count - 1) + 2]); //shear force at the right joint

            M_joint.Add(Mlist[0]); //shear force at the left joint
            M_joint.Add(Mlist[3 * (top3.Count / 2)]); //shear force at the top joint
            M_joint.Add(Mlist[3 * (top3.Count - 1) + 2]); //shear force at the right joint

            var jointforces = new List<double>();
            jointforces.AddRange(N_joint);
            jointforces.AddRange(V_joint);
            jointforces.AddRange(M_joint);

            //Utilization optimization3
            var utilopt = Math.Abs(new List<double>() { maxutillist.Max(), buckly.Max(), bucklz.Max()}.Max() - 0.95);

            //Outputs
            DA.SetData(0, maxDisp); //maxDisp
            DA.SetDataTree(1, beamgeomtree);
            DA.SetData(2, totalmaxutil);
            DA.SetDataList(3, jointforces); //toppoints
            DA.SetDataList(4, allBreps); //toppoints
        }

        //Different functions used to shorten the main code
        private Karamba.Models.Model model;
        private Karamba.Models.Model Amodel;
        private Karamba.Models.Model model2;

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
            get { return new Guid("9741AE20-3699-4698-B960-4DFE691FBB16"); }
        }
    }
}