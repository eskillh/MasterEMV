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
    public class SingleBeam__Karamba3D_ : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the SingleBeam__Karamba3D_ class.
        /// </summary>
        public SingleBeam__Karamba3D_()
          : base("SingleBeam (Karamba3D)", "Nickname",
              "Description",
              "Master", "Karamba3D")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("BeamCurve", "BeamCrv", "Curves that represents the beam", GH_ParamAccess.item);
            pManager.AddTextParameter("Material", "mat", "Material of the beam", GH_ParamAccess.item);
            pManager.AddNumberParameter("Height", "H", "List of cross section heights to be iterated trough", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("Width", "B", "List of cross section widths to be iterated trough", GH_ParamAccess.item, 5);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("MaxDisplacement", "dMax", "Maximum displacement of the truss", GH_ParamAccess.list);
            pManager.AddNumberParameter("Utilization", "util", "Utilization of the beams", GH_ParamAccess.list);
            pManager.AddTextParameter("test1", "t1", "", GH_ParamAccess.item);
            pManager.AddGenericParameter("test2", "t2", "", GH_ParamAccess.item);
            pManager.AddGenericParameter("test3", "t3", "", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Curve crv = null;
            string material = "";
            double H = new double();
            double B = new double();
           
            DA.GetData(0, ref crv);
            DA.GetData(1, ref material);
            DA.GetData(2, ref H);
            DA.GetData(3, ref B);

            //Converting data to Karamba Geometry
            var supports = new List<Point3d> { crv.PointAtStart, crv.PointAtEnd };
            var supports3 = ConvertToPoint3(supports);
            var crv3 = ConvertToLine3(crv);

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
                FemMaterial.FlowHypothesis.mises, alphaT);
            

            //For the rest of the operations, I need a for loop to iterate trought the different cross sections
            List<double> results = new List<double>(); //list to store the max displacements for the different cross sections
            List<string> info = new List<string>();
            List<CroSec> allbeams = new List<CroSec>();
            var utilfull = new List<double>();
            List<Curve> critcurve = new List<Curve>();

            //For displacement check
            var span = (new Line(supports[0], supports[1])).Length;
            var check = span * 100 / 250;
            double h = H;
            double b = B;
            CroSec rect = k3d.CroSec.Trapezoid(h, b, b, mat, name, $"{name}:{h}x{b}");
            

            //Creating beams
            var logger = new MessageLogger();
            List<BuilderBeam> beams = new List<BuilderBeam>();

            List<BuilderBeam> bm = k3d.Part.LineToBeam(crv3, "Beam", rect, logger, out _);
            bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklY, crv3.Length);
            bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklZ, crv3.Length);
            bm[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklLT, crv3.Length);
            beams.Add(bm[0]);
            foreach (BuilderBeam beam in beams)
                allbeams.Add(beam.crosec);

            //Creating supports
            var supportsK = new List<Support>();
            foreach (Point3 p3 in supports3)
            {
                Support s = k3d.Support.Support(p3, k3d.Support.SupportHingedConditions);
                supportsK.Add(s);
            }

            //Creating loads
            var loads = new List<Load>();
            loads.Add(k3d.Load.GravityLoad(new Vector3(0, 0, 1), "LC0"));

            List<string> beamnames = new List<string> {"Beam"};

            var list01 = ListFrom0To1(2);
            List<double> loadlist = new List<double>();
            for (int i = 0; i < list01.Count; i++)
                loadlist.Add(10);
            loads.Add(k3d.Load.DistributedForceLoad(
                new Vector3(0, 0, 1), loadlist,
                list01, LoadOrientation.global, "LC0", beamnames));

            //Assembling model
            model = k3d.Model.AssembleModel(
                beams, supportsK, loads,
                out _, out _, out _, out _, out _);

            //Analyzing model
            IReadOnlyList<string> LCs = new List<string> { "LC0" };

            Amodel = k3d.Algorithms.Analyze(model, LCs,
                out IReadOnlyList<double> maxD,
                out IReadOnlyList<Vector3> res, out IReadOnlyList<double> el, out string w);

  
            double maxDisp = maxD[0] * 100;
            results.Add(maxDisp);

            double gamma_m0 = 1.25;
            double gamma_m1 = 1.1;
            Karamba.Results.Utilization.solve(Amodel, beamnames, -1, 20,
                true, gamma_m0, gamma_m1, true,
                out Karamba.Results.UtilizationResults model_util, out string warning);

            Karamba.Results.Utilization_Beam.solve(Amodel, beamnames, "LC0", 20,
                true, gamma_m0, gamma_m1, true,
                out List<List<Karamba.Results.UtilizationResults_Item>> beamutil,
                out _, out _, out _);

            var util = model_util.util;
            var model_details = model_util.details;
            var item = beamutil[0];
            var iitem = item[0];
            var det = iitem.details;
            List<double> utilValues = beamutil
                .SelectMany(innerList => innerList)
                .Select(utilItem => utilItem.util)
                .ToList();
            var elastic = el;
            DA.SetDataList(0, results);
            DA.SetDataList(1, util);
            DA.SetDataList(2, model_details);
            DA.SetData(3, det);
            DA.SetDataList(4, utilValues);
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

        Line3 ConvertToLine3(Curve crvs)
        {
            Point3d pt1 = crvs.PointAtStart;
            Point3d pt2 = crvs.PointAtEnd;
            Point3 p1 = new Point3(pt1.X, pt1.Y, pt1.Z);
            Point3 p2 = new Point3(pt2.X, pt2.Y, pt2.Z);
            Line3 lines3 = new Line3(p1, p2);

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
            get { return new Guid("E7FF2441-3906-4C8B-B8E5-D36B47E0B01A"); }
        }
    }
}