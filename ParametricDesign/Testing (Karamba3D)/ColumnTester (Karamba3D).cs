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
    public class ColumnTester__Karamba3D_ : GH_Component
    {

        public ColumnTester__Karamba3D_()
          : base("ColumnTester (Karamba3D)", "Nickname",
              "Description",
              "Master", "Karamba3D")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Columns","Col","Columns of the truss structure", GH_ParamAccess.list);
            pManager.AddPointParameter("SupportPoints", "supportPts", "Bottom points of the columns where the support is", GH_ParamAccess.list);
            pManager.AddTextParameter("Material", "mat", "Material of the columns", GH_ParamAccess.item);
            pManager.AddNumberParameter("CrossSectionHeight","h","Height of the cross section of the columns", GH_ParamAccess.item);
            pManager.AddNumberParameter("CrossSectionWidth", "b", "Width of the cross section of the columns", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("MaxDisplacement", "maxDisp", "Maximum displacement of the columns", GH_ParamAccess.item);
            pManager.AddGenericParameter("test1","t1","", GH_ParamAccess.list);
            pManager.AddGenericParameter("test2", "t2", "", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var columncrvs = new List<Curve>();
            var supportpts = new List<Point3d>();
            string material = "";
            var h = new double();
            var b = new double();

            DA.GetDataList(0, columncrvs);
            DA.GetDataList(1, supportpts);
            DA.GetData(2, ref material);
            DA.GetData(3, ref h);
            DA.GetData(4, ref b);

            //Converting data to Karamba geometry
            var columns3 = ConvertToLine3(columncrvs);
            var support3 = ConvertToPoint3(supportpts);

            //KarambaCommon toolkit
            var k3d = new KarambaCommon.Toolkit();

            //Defining material
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

            //Defining cross section
            CroSec rect = k3d.CroSec.Trapezoid(h, b, b, mat, name, $"{name}:{h}x{b}");

            //Creating columns
            var logger = new MessageLogger();
            var columns = new List<BuilderBeam>();

            foreach (var c in columns3)
            {
                var bc = k3d.Part.LineToBeam(c, "Column", rect, logger, out _);
                bc[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklY, c.Length);
                bc[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklZ, c.Length);
                bc[0].BucklingLength_Set(BuilderElementStraightLine.BucklingDir.bklLT, c.Length);
                columns.Add(bc[0]);
            }

            //Creating supports
            var supports = new List<Support>();
            foreach (var pt in support3)
            {
                var support = k3d.Support.Support(pt, k3d.Support.SupportHingedConditions);
                supports.Add(support);
            }

            //Creating loads
            var loads = new List<Load>();
            loads.Add(k3d.Load.GravityLoad(new Vector3(0, 0, 1), "LC0"));

            List<string> columnnames = new List<string>();
            for (int i = 0; i < columncrvs.Count; i++)
                columnnames.Add("Column");

            var loadpts = new List<Point3>();
            foreach(var support in supportpts)
            {
                var loadpt = new Point3(support.X, support.Y, support.Z + columns3[0].Length);
                loadpts.Add(loadpt);
            }
            foreach (var loadpt in loadpts)
                loads.Add(k3d.Load.PointLoad(loadpt, new Vector3(0, 0, 5)));

            //Assembling model
            var model = k3d.Model.AssembleModel(columns, supports, loads, out _, out _, out _, out _, out _);

            //Analyzing model
            IReadOnlyList<string> LCs = new List<string> { "LC0" };

            var Amodel = k3d.Algorithms.Analyze(model, LCs, out IReadOnlyList<double> maxD,
                out _, out _, out string w);

            double maxDisp = maxD[0] * 100;

            //Output
            DA.SetData(0, maxDisp);
            DA.SetDataList(1, columns);
            DA.SetDataList(2, support3);
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
            get { return new Guid("86F5390B-090F-4C60-B461-A764F58DBD58"); }
        }
    }
}