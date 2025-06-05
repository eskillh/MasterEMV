using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Forms;
using Grasshopper.Kernel;
using Karamba.Geometry;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using Rhino.Geometry;
using UnitsNet;

namespace Masterv2
{
    public class _3DStructureMaker : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the _3DStructureMaker class.
        /// </summary>
        public _3DStructureMaker()
          : base("3DStructureMaker", "Nickname",
              "Description",
              "Master", "Geometry")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("TopCurves", "topCrvs", "Top curves of the structure", GH_ParamAccess.list);
            pManager.AddCurveParameter("TrussCurves", "trussCrvs", "Truss curves of the structure", GH_ParamAccess.list);
            pManager.AddCurveParameter("BottomCurves", "bottomCrvs", "Bottom curves of the structure", GH_ParamAccess.list);
            pManager.AddCurveParameter("ColumnCurves", "colCrvs", "Column curves of the structure", GH_ParamAccess.list);
            pManager.AddNumberParameter("Spacing","spac","Spacing between the truss parts of the structure [m]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Modules", "mod", "Number of modules of the truss structure", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("TopBeams", "topB", "Top beams of the structure", GH_ParamAccess.list);
            pManager.AddCurveParameter("TrussBeams", "trussB", "Truss beams of the structure", GH_ParamAccess.list);
            pManager.AddCurveParameter("BottomBeams", "bottomB", "Bottom beams of the structure", GH_ParamAccess.list);
            pManager.AddCurveParameter("Columns", "col", "Columns of the structure", GH_ParamAccess.list);
            pManager.AddPointParameter("SupportPts", "sPts", "Locations where the support of the structure is", GH_ParamAccess.list);
            pManager.AddGenericParameter("Roofmeshes", "rMsh","", GH_ParamAccess.list);
            pManager.AddGenericParameter("RoofPoints", "rPts", "", GH_ParamAccess.list);
            pManager.AddCurveParameter("Purlins", "prl", "", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var topcrvs = new List<Curve>();
            var trusscrvs = new List<Curve>();
            var bottomcrvs = new List<Curve>();
            var colcrvs = new List<Curve>();
            var spacing = new double();
            var modules = new double();

            DA.GetDataList(0, topcrvs);
            DA.GetDataList(1, trusscrvs);
            DA.GetDataList(2, bottomcrvs);
            DA.GetDataList(3, colcrvs);
            DA.GetData(4, ref spacing);
            DA.GetData(5, ref modules);

            //Supportpoints
            var supportpts = new List<Point3d>();
            for (int i = 0; i < colcrvs.Count; i++)
            {
                var pt = colcrvs[i].PointAtStart;
                supportpts.Add(pt);
            }
            //Creating lists for the points where nodes for bracing are
            var toppts = new List<Point3d>();
            for (int i = 0; i < topcrvs.Count; i++)
            {
                var pt = topcrvs[i].PointAtStart;
                var pt2 = topcrvs[i].PointAtEnd;
                var l = new Line(pt, pt2);
                var pt3 = l.PointAt(0.5);
                toppts.Add(pt);
                toppts.Add(pt3);
            }
            toppts.Add(topcrvs[topcrvs.Count - 1].PointAtEnd);

            var topcrvstest = new List<Curve>();
            for (int i = 0; i < toppts.Count-1; i++)
            {
                var line = new Line(toppts[i], toppts[i + 1]);
                topcrvstest.Add(line.ToNurbsCurve());
            }

            //Moving the geometry
            List<double> numberofiterations = Enumerable.Repeat(0.0, (int)modules).ToList();

            var topcrvsout = new List<List<Curve>>() { topcrvstest};
            var trusscrvsout = new List<List<Curve>>() { trusscrvs};
            var bottomcrvsout = new List<List<Curve>>() { bottomcrvs};
            var colcrvsout = new List<List<Curve>>() { colcrvs };
            var supportptsout = new List<List<Point3d>>() { supportpts};
            var topptsout = new List<List<Point3d>>() { toppts };

            var test = new List<Curve>();
            for (int i = 0; i <numberofiterations.Count; i++)
            {
                var topcrvsit = new List<Curve>();
                foreach (var crv in topcrvstest)
                {
                    var sp = crv.PointAtStart;
                    var ep = crv.PointAtEnd;
                    var c = new Line(new Point3d(sp.X, sp.Y + spacing + spacing*i, sp.Z), new Point3d(ep.X, ep.Y + spacing + spacing*i, ep.Z)).ToNurbsCurve();
                    topcrvsit.Add(c);
                }
                topcrvsout.Add(topcrvsit);

                var trusscrvsit = new List<Curve>();
                foreach (var crv in trusscrvs)
                {
                    var sp = crv.PointAtStart;
                    var ep = crv.PointAtEnd;
                    var c = new Line(new Point3d(sp.X, sp.Y + spacing + spacing * i, sp.Z), new Point3d(ep.X, ep.Y + spacing + spacing * i, ep.Z)).ToNurbsCurve();
                    trusscrvsit.Add(c);
                }
                trusscrvsout.Add(trusscrvsit);

                var bottomcrvsit = new List<Curve>();
                foreach (var crv in bottomcrvs)
                {
                    var sp = crv.PointAtStart;
                    var ep = crv.PointAtEnd;
                    var c = new Line(new Point3d(sp.X, sp.Y + spacing + spacing * i, sp.Z), new Point3d(ep.X, ep.Y + spacing + spacing * i, ep.Z)).ToNurbsCurve();
                    bottomcrvsit.Add(c);
                }
                bottomcrvsout.Add(bottomcrvsit);

                var colcrvsit = new List<Curve>();
                foreach (var crv in colcrvs)
                {
                    var sp = crv.PointAtStart;
                    var ep = crv.PointAtEnd;
                    var c = new Line(new Point3d(sp.X, sp.Y + spacing + spacing * i, sp.Z), new Point3d(ep.X, ep.Y + spacing + spacing * i, ep.Z)).ToNurbsCurve();
                    colcrvsit.Add(c);
                }
                colcrvsout.Add(colcrvsit);

                var supportit = new List<Point3d>();
                foreach (var pt in supportpts)
                {
                    var ptmoved = new Point3d(pt.X, pt.Y + spacing + spacing*i, pt.Z);
                    supportit.Add(ptmoved);
                }
                supportptsout.Add(supportit);

                var topptsit = new List<Point3d>();
                foreach (var pt in toppts)
                {
                    var ptmoved = new Point3d(pt.X, pt.Y + spacing + spacing * i, pt.Z);
                    topptsit.Add(ptmoved);
                }
                topptsout.Add(topptsit);
            }

            //Creating mesh for roof loads
            var leftmeshpoints = new List<Point3d>();
            leftmeshpoints.Add(topcrvsout[0][0].PointAtStart);
            leftmeshpoints.Add(topcrvsout[0][topcrvsout[0].Count / 2].PointAtStart);
            leftmeshpoints.Add(topcrvsout[topcrvsout.Count-1][topcrvsout[0].Count / 2].PointAtStart);
            leftmeshpoints.Add(topcrvsout[topcrvsout.Count-1][0].PointAtStart);
            var leftmeshsurface = NurbsSurface.CreateFromCorners(leftmeshpoints[0], leftmeshpoints[1], leftmeshpoints[2], leftmeshpoints[3]);
            var meshparam = new MeshingParameters
            {
                GridMinCount = 50,
                GridMaxCount = 100,
                RefineGrid = true,
                JaggedSeams = false,
            };
            var leftmesh = Mesh.CreateFromSurface(leftmeshsurface, meshparam);

            var rightmeshpoints = new List<Point3d>();
            rightmeshpoints.Add(topcrvsout[0][topcrvsout[0].Count / 2].PointAtStart);
            rightmeshpoints.Add(topcrvsout[0][topcrvsout[0].Count-1].PointAtEnd);
            rightmeshpoints.Add(topcrvsout[topcrvsout.Count - 1][topcrvsout[0].Count - 1].PointAtEnd);
            rightmeshpoints.Add(topcrvsout[topcrvsout.Count - 1][topcrvsout[0].Count / 2].PointAtStart);
            var rightmeshsurface = NurbsSurface.CreateFromCorners(rightmeshpoints[0], rightmeshpoints[1], rightmeshpoints[2], rightmeshpoints[3]);
            var rightmesh = Mesh.CreateFromSurface(rightmeshsurface, meshparam);

            var roofmeshes = new List<Mesh>() { leftmesh, rightmesh };

            //Creating purlins
            var purlins = new List<List<Curve>>();
            for (int r = 0; r < topptsout.Count - 1; r++)
            {
                var purlin = new List<Curve>();
                for (int i = 0; i < topptsout[r].Count; i++)
                {
                    Line p = new Line(topptsout[r][i], topptsout[r + 1][i]);
                    purlin.Add(p.ToNurbsCurve());
                }
                purlins.Add(purlin);
            }
         
            //Output
            DA.SetDataTree(0, GHTreeMaker.CurveList(topcrvsout));
            DA.SetDataTree(1, GHTreeMaker.CurveList(trusscrvsout));
            DA.SetDataTree(2, GHTreeMaker.CurveList(bottomcrvsout));
            DA.SetDataTree(3, GHTreeMaker.CurveList(colcrvsout));
            DA.SetDataTree(4, GHTreeMaker.NestedList(supportptsout)); //supportpts
            DA.SetDataList(5, roofmeshes);
            DA.SetDataTree(6, GHTreeMaker.NestedList(topptsout));
            DA.SetDataTree(7, GHTreeMaker.CurveList(purlins));
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
            get { return new Guid("6DD4E672-FB73-4EE7-9C64-4536FC766661"); }
        }
    }
}