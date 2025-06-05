using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

using Grasshopper.Kernel;
using Rhino.Geometry;

//Karamba3D imports
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
using System.Reflection.Metadata.Ecma335;
using Rhino.UI;
using Grasshopper.Kernel.Types;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Text;
using System.Linq;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using UnitsNet;


namespace Masterv2
{
    public class CS_Iterations__Karamba3D_ : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CS_Iterations__Karamba3D_ class.
        /// </summary>
        public CS_Iterations__Karamba3D_()
          : base("CS Iterations (Karamba3D)", "Nickname",
              "Description",
              "Master", "Karamba3D")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("TopCurves", "TopCrvs", "Curves that represents the top beams", GH_ParamAccess.list);
            pManager.AddCurveParameter("TrussCurves", "TrussCrvs", "Curves that represents the truss beams", GH_ParamAccess.list);
            pManager.AddCurveParameter("BottomCurves", "BtmCrvs", "Curves that represents the bottom beams", GH_ParamAccess.list);
            pManager.AddPointParameter("SupportPoints", "sPts", "Points of the location of the supports", GH_ParamAccess.list);
            pManager.AddTextParameter("Material", "mat", "Material of the beams", GH_ParamAccess.item);
            pManager.AddNumberParameter("CrossSectionHeights", "CroSecHs", "List of cross section heights to be iterated trough", GH_ParamAccess.list);
            pManager.AddNumberParameter("CrossSectionWidths", "CroSecBs", "List of cross section widths to be iterated trough", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("MaxDisplacement", "dMax", "Maximum displacement of the truss", GH_ParamAccess.list);
            pManager.AddNumberParameter("Utilization", "util", "Utilization of the beams", GH_ParamAccess.list);
            pManager.AddGenericParameter("CroSec Info", "Cinfo", "Info about the cross section of the beams and results", GH_ParamAccess.list);
            pManager.AddTextParameter("Beam Info", "Binfo", "Info about the lengths of the beams", GH_ParamAccess.list);
            pManager.AddGenericParameter("UtilTest", "uT", "", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> topcrvs = new List<Curve>();
            List<Curve> trusscrvs = new List<Curve>();
            List<Curve> bottomcrvs = new List<Curve>();
            List<Point3d> supports3d = new List<Point3d>();
            string material = "";
            List<double> crosecHs = new List<double>();
            List<double> crosecBs = new List<double>();

            DA.GetDataList(0, topcrvs);
            DA.GetDataList(1, trusscrvs);
            DA.GetDataList(2, bottomcrvs);
            DA.GetDataList(3, supports3d);
            DA.GetData(4, ref material);
            DA.GetDataList(5, crosecHs);
            DA.GetDataList(6, crosecBs);

            //Converting data to Karamba Geometry
            var supports3 = ConvertToPoint3(supports3d);
            var top3 = ConvertToLine3(topcrvs);
            var truss3 = ConvertToLine3(trusscrvs);
            var bottom3 = ConvertToLine3(bottomcrvs);

            //KarambaCommon Toolkit for operations
            var k3d = new KarambaCommon.Toolkit();

            //Creating cross sections
            string family = GetString(material, @"Material:\s+(\w+)");
            string name = GetString(material, @"Material:\s+\S+\s+'([^']+)'");
            double E = GetDouble(material, @"E:(-?\d+(\.\d+)?(E-?\d+)?)");
            double Gip = GetDouble(material, @"G12:(-?\d+(\.\d+)?(E-?\d+)?)");
            double Gtr = GetDouble(material, @"G3:(-?\d+(\.\d+)?(E-?\d+)?)");
            double gamma = GetDouble(material, @"gamma:(-?\d+(\.\d+)?(E-?\d+)?)") / (100 * 100);
            double ft = GetDouble(material, @"ft:(-?\d+(\.\d+)?(E-?\d+)?)");
            double fc = GetDouble(material, @"fc:(-?\d+(\.\d+)?(E-?\d+)?)");
            double alphaT = GetDouble(material, @"alphaT:(-?\d+(\.\d+)?(E-?\d+)?)") / (100 * 100);

            FemMaterial mat = k3d.Material.IsotropicMaterial(family,
                name, E, Gip, Gtr, gamma, ft, fc,
                FemMaterial.FlowHypothesis.rankine, alphaT);


            //For the rest of the operations, I need a for loop to iterate trought the different cross sections
            List<double> results = new List<double>(); //list to store the max displacements for the different cross sections
            List<double> util = new List<double>();
            List<string> info = new List<string>();
            List<CroSec> allbeams = new List<CroSec>();
            //remove after testing
            var utilfull = new List<double>(); 
            List<Curve> critcurve = new List<Curve>();

            //For displacement check
            var span = (new Line(supports3d[0], supports3d[1])).Length;
            var check = span * 100 / 250;
            foreach (double h in crosecHs)
            {
                foreach (double b in crosecBs)
                {
                    CroSec rect = k3d.CroSec.Trapezoid(h, b, b, mat, name, $"{name}:{h}x{b}");


                    //Creating beams
                    var logger = new MessageLogger();
                    List<BuilderBeam> topbeams = new List<BuilderBeam>();
                    List<BuilderBeam> beams = new List<BuilderBeam>();
                    foreach (Line3 l in top3) //Creating beams for each line and adding to list
                    {
                        List<BuilderBeam> bm = k3d.Part.LineToBeam(l, "Top Beam", rect, logger, out _);
                        topbeams.Add(bm[0]);
                        beams.Add(bm[0]);

                    }

                    List<BuilderBeam> bottombeams = new List<BuilderBeam>();
                    foreach (Line3 l in bottom3)
                    {
                        List<BuilderBeam> bm = k3d.Part.LineToBeam(l, "Bottom Beam", rect, logger, out _);
                        bottombeams.Add(bm[0]);
                        beams.Add(bm[0]);
                    }

                    List<BuilderBeam> trussbeams = new List<BuilderBeam>();
                    foreach (Line3 l in truss3)
                    {
                        List<BuilderBeam> bm = k3d.Part.LineToBeam(l, "Truss Beam", rect, logger, out _);
                        trussbeams.Add(bm[0]);
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

                    var list01 = ListFrom0To1(2);
                    List<double> loadlist = new List<double>();
                    for (int i = 0; i < list01.Count; i++)
                        loadlist.Add(10);
                    loads.Add(k3d.Load.DistributedForceLoad(
                        new Vector3(0, 0, 1), loadlist,
                        list01, LoadOrientation.global, "LC0", beamnames));

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

                    double gamma_m0 = 1.25;
                    double gamma_m1 = 1.10;
                    Karamba.Results.Utilization.solve(Amodel, allbeamnames, 0, 10,
                        true, gamma_m0, gamma_m1, false, out Karamba.Results.UtilizationResults model_util, out _);

                    List<double> utiliz = model_util.util;
                    var utilavg = utiliz.Sum() / utiliz.Count;
                    util.Add(utilavg);
                    utilfull = utiliz;
                    double maxutil = utiliz.Max();
                    //var maxindex = utiliz.IndexOf(utiliz.Max);
                    //critcurve.Add(topcrvs[maxindex]);
                    //Checking if displacement is OK
                    if (maxDisp < check & (utiliz.Sum()/utiliz.Count()) <= 1)
                        info.Add($"{rect.name}, Max Utilization:{Math.Round(maxutil, 4)}, " +
                            $"Displacement: {Math.Round(maxDisp, 4)} ");
                }
            }
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


            DA.SetDataList(0, results);
            DA.SetDataList(1, util);
            DA.SetDataList(2, info);
            DA.SetDataList(3, beaminfo);
            DA.SetDataList(4, utilfull);
        }

        //Different functions used to shorten the main code
        private Karamba.Models.Model model;
        private Karamba.Models.Model Amodel;
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
            get { return new Guid("7123082D-C83A-4EFA-93CC-331A67162D21"); }
        }
    }
}