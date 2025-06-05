using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;


namespace TimberStructureGenerator
{
    public class TrussMaker : GH_Component
    {

        public TrussMaker()
          : base("TrussMaker", "Nickname",
              "Description",
              "Master", "Geometry")
        {
        }

        //INPUT
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Span", "S", "Span of the truss", GH_ParamAccess.item, 6);
            pManager.AddNumberParameter("Height", "H", "Middle height of the truss", GH_ParamAccess.item, 2);
            pManager.AddNumberParameter("Type", "T", "Type of truss, integer", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Divisions", "div", "Choose the truss divisions", GH_ParamAccess.item, 1);
        }

        //OUTPUT
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Top Beams", "TopBs", "Top beams of the truss", GH_ParamAccess.list);
            pManager.AddCurveParameter("Truss", "Trs", "Truss part of the truss", GH_ParamAccess.list);
            pManager.AddCurveParameter("Bottom Beams", "BtmBs", "Bottom beams of the truss", GH_ParamAccess.list);
            pManager.AddPointParameter("Support Points", "sPts", "Points where the support is", GH_ParamAccess.list);
            pManager.AddCurveParameter("Top Beams Arc", "lPts", "Top points of the truss where load is applied", GH_ParamAccess.list);
        }

        //CODE
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Input
            double span = new double();
            double height = new double();
            double type = new double();
            double divs = new double();

            DA.GetData(0, ref span);
            DA.GetData(1, ref height);
            DA.GetData(2, ref type);
            DA.GetData(3, ref divs);

            //Creating the base points
            Point3d pt1 = new Point3d(0, 0, 0);
            Point3d pt2 = new Point3d(span, 0, 0);
            List<Point3d> supportpoints = new List<Point3d>() { pt1, pt2 };

            Line bot = new Line(pt1, pt2);
            Curve crv = bot.ToNurbsCurve();
            //int div = ((int)span) - 2;
            int div = ((int)span) - (span % 2 == 0 ? (int)divs + 1 : (int)divs);
            double[] pms = crv.DivideByCount((div), true);
            List<Point3d> bottompts = new List<Point3d>(); //list of the bottom points divided into
            List<Point3d> toppts = new List<Point3d>(); //list of the top points divided into
            
            foreach (var p in pms)
            {
                bottompts.Add(crv.PointAt(p));
            }

            //Defining the output variables
            Point3d toppt = new Point3d(bottompts[div / 2].X, bottompts[div / 2].Y, bottompts[div / 2].Z + height);
            List<Curve> bottomBeams = new List<Curve>();
            List<Curve> topBeams = new List<Curve>();
            List<Curve> truss = new List<Curve>();
            List<Curve> topBeamsA = new List<Curve>();

            //Verticaltruss
            if (type == 0)
            {
                var tbeam = new Polyline(new List<Point3d> { pt1, toppt, pt2 }).ToNurbsCurve(); //Making the top beam a linear beam

                //Creating the top and bottom beams
                double[] pms2 = tbeam.DivideByCount(div, true);
                foreach (var p in pms2)
                {
                    toppts.Add(tbeam.PointAt(p));
                }

                for (int i = 0; i < bottompts.Count - 1; i++)
                {
                    Line bl = new Line(bottompts[i], bottompts[i + 1]);
                    Line tl = new Line(toppts[i], toppts[i + 1]);
                    bottomBeams.Add(bl.ToNurbsCurve());
                    topBeams.Add(tl.ToNurbsCurve());
                    topBeamsA.Add(tl.ToNurbsCurve());
                }

                //Creating the truss
                for (int i = 1; i <bottompts.Count - 1; i++)
                {
                    Line v = new Line(toppts[i], bottompts[i]);
                    truss.Add(v.ToNurbsCurve());
                }
            }
            //Crosstruss
            if (type == 1)
            {
                var tbeam = new Polyline(new List<Point3d> { pt1, toppt, pt2 }).ToNurbsCurve(); //Making the top beam a linear beam

                //Creating the top and bottom beams
                double[] pms2 = tbeam.DivideByCount(div, true);
                foreach (var p in pms2)
                {
                    toppts.Add(tbeam.PointAt(p));
                }

                for (int i = 0; i < bottompts.Count - 1; i++)
                {
                    Line bl = new Line(bottompts[i], bottompts[i + 1]);
                    Line tl = new Line(toppts[i], toppts[i + 1]);
                    bottomBeams.Add(bl.ToNurbsCurve());
                    topBeams.Add(tl.ToNurbsCurve());
                    topBeamsA.Add(tl.ToNurbsCurve());
                }

                //Creating the truss
                for (int i = 1; i < bottompts.Count - 2; i = i + 2)
                {
                    Line tr = new Line(toppts[i], bottompts[i + 1]);
                    Line tr2 = new Line(bottompts[i + 1], toppts[i + 2]);
                    truss.Add(tr.ToNurbsCurve());
                    truss.Add(tr2.ToNurbsCurve());
                }

            }

            //Crosstruss with vertical elements
            if (type == 2)
            {
                var tbeam = new Polyline(new List<Point3d> { pt1, toppt, pt2 }).ToNurbsCurve(); //Making the top beam a linear beam
                
                //Creating the top and bottom beams
                double[] pms2 = tbeam.DivideByCount(div, true);
                foreach (var p in pms2) //Dividing the top beam into several divisions
                {
                    toppts.Add(tbeam.PointAt(p));
                }

                for (int i = 0; i < bottompts.Count - 1; i++)
                {
                    Line bl = new Line(bottompts[i], bottompts[i + 1]);
                    Line tl = new Line(toppts[i], toppts[i + 1]);
                    bottomBeams.Add(bl.ToNurbsCurve());
                    topBeams.Add(tl.ToNurbsCurve());
                    topBeamsA.Add(tl.ToNurbsCurve());
                }

                //Creating the truss
                for (int i = 1; i < bottompts.Count - 2; i = i + 2) //Diagonal elements
                {
                    Line tr = new Line(toppts[i], bottompts[i + 1]);
                    Line tr2 = new Line(bottompts[i + 1], toppts[i + 2]);
                    truss.Add(tr.ToNurbsCurve());
                    truss.Add(tr2.ToNurbsCurve());
                }
                for (int i = 1; i <bottompts.Count - 1; i++) //Straight elements
                {
                    Line vert = new Line(toppts[i], bottompts[i]);
                    truss.Add(vert.ToNurbsCurve());
                }
            }

            //X truss with linear top beam
            if (type == 3)
            {
                var tbeam = new Polyline(new List<Point3d> { pt1, toppt, pt2 }).ToNurbsCurve(); //Making the top beam a linear beam

                //Creating the top and bottom beams
                double[] pms2 = tbeam.DivideByCount(div, true);
                foreach (var p in pms2) //Dividing the top beam into several divisions
                {
                    toppts.Add(tbeam.PointAt(p));
                }

                for (int i = 0; i < bottompts.Count - 1; i++)
                {
                    Line bl = new Line(bottompts[i], bottompts[i + 1]);
                    Line tl = new Line(toppts[i], toppts[i + 1]);
                    bottomBeams.Add(bl.ToNurbsCurve());
                    topBeams.Add(tl.ToNurbsCurve());
                    topBeamsA.Add(tl.ToNurbsCurve());
                }

                //Creating truss
                for (int i = 1; i <bottompts.Count - 2; i++)
                {
                    Line cl = new Line(toppts[i], bottompts[i + 1]);
                    Line cl2 = new Line(bottompts[i], toppts[i + 1]);
                    truss.Add(cl.ToNurbsCurve());
                    truss.Add(cl2.ToNurbsCurve());
                }
            }

            //X truss with vetical elements with linear top beam
            if (type == 4)
            {
                var tbeam = new Polyline(new List<Point3d> { pt1, toppt, pt2 }).ToNurbsCurve(); //Making the top beam a linear beam

                //Creating the top and bottom beams
                double[] pms2 = tbeam.DivideByCount(div, true);
                foreach (var p in pms2) //Dividing the top beam into several divisions
                {
                    toppts.Add(tbeam.PointAt(p));
                }

                for (int i = 0; i < bottompts.Count - 1; i++)
                {
                    Line bl = new Line(bottompts[i], bottompts[i + 1]);
                    Line tl = new Line(toppts[i], toppts[i + 1]);
                    bottomBeams.Add(bl.ToNurbsCurve());
                    topBeams.Add(tl.ToNurbsCurve());
                    topBeamsA.Add(tl.ToNurbsCurve());
                }

                //Creating truss
                for (int i = 1; i < bottompts.Count - 2; i++)
                {
                    Line cl = new Line(toppts[i], bottompts[i + 1]);
                    Line cl2 = new Line(bottompts[i], toppts[i + 1]);
                    truss.Add(cl.ToNurbsCurve());
                    truss.Add(cl2.ToNurbsCurve());
                }
                for (int i = 1; i < bottompts.Count - 1; i++)
                {
                    Line vert = new Line(toppts[i], bottompts[i]);
                    truss.Add(vert.ToNurbsCurve());
                }
            }

            var smallerarcs = new List<Curve>();
            var topnodepts = new List<Point3d>();

            //Arch with vertical elements
            if (type == 5)
            {
                var tbeam = new Arc(pt1, toppt, pt2).ToNurbsCurve();
                double[] pnodes = tbeam.DivideByCount(div, true); //Division into where truss nodes is
                var pms2 = tbeam.DivideByCount(div * 4, true); //Division into straight elements to approximate curve
                var arcpts = new List<Point3d>(); //Full list of approximation points
                
                foreach (var p in pnodes) //Dividing the top beam into several divisions
                {
                    toppts.Add(tbeam.PointAt(p));
                }
                
                var tbeam2 = new Arc(pt1, toppt, pt2);
                var rad = tbeam2.Radius;
                var center = tbeam2.Center;

                foreach (var point in bottompts)
                {
                    double z = center.Z + Math.Sqrt(rad * rad - (point.X - center.X) * (point.X - center.X));
                    toppts.Add(new Point3d(point.X, point.Y, z));
                    topnodepts.Add(new Point3d(point.X, point.Y, z));

                }
                
                foreach (var p in pms2)
                    arcpts.Add(tbeam.PointAt(p));

                //Dividing the arc into smaller pieces and then dividing the pieces for analyze
                for (int i = 0; i < topnodepts.Count - 1; i++)
                {
                    Point3d between = new Point3d(
                        (topnodepts[i].X + topnodepts[i+1].X)/2, 
                        (topnodepts[i].Y + topnodepts[i + 1].Y) / 2,
                         center.Z + Math.Sqrt(rad * rad - ((topnodepts[i].X + topnodepts[i + 1].X) / 2 - center.X) * ((topnodepts[i].X + topnodepts[i + 1].X) / 2 - center.X)));
                    Curve arc = new Arc(topnodepts[i], between, topnodepts[i+1] ).ToNurbsCurve();
                    topBeams.Add(arc);
                }

                for (int i = 0; i < bottompts.Count - 1; i++)
                {
                    Line bl = new Line(bottompts[i], bottompts[i + 1]);
                    bottomBeams.Add(bl.ToNurbsCurve());
                }
                
                foreach (Curve arc in topBeams)
                {
                    var pmss = arc.DivideByCount(8, true);
                    var pointss = new List<Point3d>();
                    foreach (var p in pmss)
                        pointss.Add(arc.PointAt(p));
                    for (int i = 0; i < pointss.Count - 1; i++)
                        topBeamsA.Add((new Line(pointss[i], pointss[i+1]).ToNurbsCurve()));
                }
                
               
                //Creating the truss
                for (int i = 1; i < bottompts.Count - 1; i++)
                {
                    Line tv = new Line(topnodepts[i], bottompts[i]);
                    truss.Add(tv.ToNurbsCurve());
                }
            }

            //Arch with cross truss
            if (type == 6)
            {
                var tbeam = new Arc(pt1, toppt, pt2).ToNurbsCurve();
                double[] pnodes = tbeam.DivideByCount(div, true); //Division into where truss nodes is
                var pms2 = tbeam.DivideByCount(div * 4, true); //Division into straight elements to approximate curve
                var arcpts = new List<Point3d>(); //Full list of approximation points

                foreach (var p in pnodes) //Dividing the top beam into several divisions
                {
                    toppts.Add(tbeam.PointAt(p));
                }

                var tbeam2 = new Arc(pt1, toppt, pt2);
                var rad = tbeam2.Radius;
                var center = tbeam2.Center;

                foreach (var point in bottompts)
                {
                    double z = center.Z + Math.Sqrt(rad * rad - (point.X - center.X) * (point.X - center.X));
                    toppts.Add(new Point3d(point.X, point.Y, z));
                    topnodepts.Add(new Point3d(point.X, point.Y, z));

                }

                foreach (var p in pms2)
                    arcpts.Add(tbeam.PointAt(p));

                //Dividing the arc into smaller pieces and then dividing the pieces for analyze
                for (int i = 0; i < topnodepts.Count - 1; i++)
                {
                    Point3d between = new Point3d(
                        (topnodepts[i].X + topnodepts[i + 1].X) / 2,
                        (topnodepts[i].Y + topnodepts[i + 1].Y) / 2,
                         center.Z + Math.Sqrt(rad * rad - ((topnodepts[i].X + topnodepts[i + 1].X) / 2 - center.X) * ((topnodepts[i].X + topnodepts[i + 1].X) / 2 - center.X)));
                    Curve arc = new Arc(topnodepts[i], between, topnodepts[i + 1]).ToNurbsCurve();
                    topBeams.Add(arc);
                }

                for (int i = 0; i < bottompts.Count - 1; i++)
                {
                    Line bl = new Line(bottompts[i], bottompts[i + 1]);
                    bottomBeams.Add(bl.ToNurbsCurve());
                }

                foreach (Curve arc in topBeams)
                {
                    var pmss = arc.DivideByCount(8, true);
                    var pointss = new List<Point3d>();
                    foreach (var p in pmss)
                        pointss.Add(arc.PointAt(p));
                    for (int i = 0; i < pointss.Count - 1; i++)
                        topBeamsA.Add((new Line(pointss[i], pointss[i + 1]).ToNurbsCurve()));
                }

                //Creating the truss
                for (int i = 1; i < bottompts.Count - 2; i = i + 2)
                {
                    Line tr = new Line(topnodepts[i], bottompts[i + 1]);
                    Line tr2 = new Line(bottompts[i + 1], topnodepts[i + 2]);
                    truss.Add(tr.ToNurbsCurve());
                    truss.Add(tr2.ToNurbsCurve());
                }
            }

            //Arch with cross truss and vertical elements
            if (type == 7)
            {
                var tbeam = new Arc(pt1, toppt, pt2).ToNurbsCurve();
                double[] pnodes = tbeam.DivideByCount(div, true); //Division into where truss nodes is
                var pms2 = tbeam.DivideByCount(div * 4, true); //Division into straight elements to approximate curve
                var arcpts = new List<Point3d>(); //Full list of approximation points

                foreach (var p in pnodes) //Dividing the top beam into several divisions
                {
                    toppts.Add(tbeam.PointAt(p));
                }

                var tbeam2 = new Arc(pt1, toppt, pt2);
                var rad = tbeam2.Radius;
                var center = tbeam2.Center;

                foreach (var point in bottompts)
                {
                    double z = center.Z + Math.Sqrt(rad * rad - (point.X - center.X) * (point.X - center.X));
                    toppts.Add(new Point3d(point.X, point.Y, z));
                    topnodepts.Add(new Point3d(point.X, point.Y, z));

                }

                foreach (var p in pms2)
                    arcpts.Add(tbeam.PointAt(p));

                //Dividing the arc into smaller pieces and then dividing the pieces for analyze
                for (int i = 0; i < topnodepts.Count - 1; i++)
                {
                    Point3d between = new Point3d(
                        (topnodepts[i].X + topnodepts[i + 1].X) / 2,
                        (topnodepts[i].Y + topnodepts[i + 1].Y) / 2,
                         center.Z + Math.Sqrt(rad * rad - ((topnodepts[i].X + topnodepts[i + 1].X) / 2 - center.X) * ((topnodepts[i].X + topnodepts[i + 1].X) / 2 - center.X)));
                    Curve arc = new Arc(topnodepts[i], between, topnodepts[i + 1]).ToNurbsCurve();
                    topBeams.Add(arc);
                }

                for (int i = 0; i < bottompts.Count - 1; i++)
                {
                    Line bl = new Line(bottompts[i], bottompts[i + 1]);
                    bottomBeams.Add(bl.ToNurbsCurve());
                }

                foreach (Curve arc in topBeams)
                {
                    var pmss = arc.DivideByCount(8, true);
                    var pointss = new List<Point3d>();
                    foreach (var p in pmss)
                        pointss.Add(arc.PointAt(p));
                    for (int i = 0; i < pointss.Count - 1; i++)
                        topBeamsA.Add((new Line(pointss[i], pointss[i + 1]).ToNurbsCurve()));
                }

                //Creating the truss
                for (int i = 1; i < bottompts.Count - 2; i = i + 2)
                {
                    Line tr = new Line(topnodepts[i], bottompts[i + 1]);
                    Line tr2 = new Line(bottompts[i + 1], topnodepts[i + 2]);
                    truss.Add(tr.ToNurbsCurve());
                    truss.Add(tr2.ToNurbsCurve());
                }

                for (int i = 1; i <bottompts.Count - 1; i++)
                {
                    Line tv = new Line(topnodepts[i], bottompts[i]);
                    truss.Add(tv.ToNurbsCurve());
                }
            }

            //Arch with X truss
            if (type == 8)
            {
                var tbeam = new Arc(pt1, toppt, pt2).ToNurbsCurve();
                double[] pnodes = tbeam.DivideByCount(div, true); //Division into where truss nodes is
                var pms2 = tbeam.DivideByCount(div * 4, true); //Division into straight elements to approximate curve
                var arcpts = new List<Point3d>(); //Full list of approximation points

                foreach (var p in pnodes) //Dividing the top beam into several divisions
                {
                    toppts.Add(tbeam.PointAt(p));
                }

                var tbeam2 = new Arc(pt1, toppt, pt2);
                var rad = tbeam2.Radius;
                var center = tbeam2.Center;

                foreach (var point in bottompts)
                {
                    double z = center.Z + Math.Sqrt(rad * rad - (point.X - center.X) * (point.X - center.X));
                    toppts.Add(new Point3d(point.X, point.Y, z));
                    topnodepts.Add(new Point3d(point.X, point.Y, z));

                }

                foreach (var p in pms2)
                    arcpts.Add(tbeam.PointAt(p));

                //Dividing the arc into smaller pieces and then dividing the pieces for analyze
                for (int i = 0; i < topnodepts.Count - 1; i++)
                {
                    Point3d between = new Point3d(
                        (topnodepts[i].X + topnodepts[i + 1].X) / 2,
                        (topnodepts[i].Y + topnodepts[i + 1].Y) / 2,
                         center.Z + Math.Sqrt(rad * rad - ((topnodepts[i].X + topnodepts[i + 1].X) / 2 - center.X) * ((topnodepts[i].X + topnodepts[i + 1].X) / 2 - center.X)));
                    Curve arc = new Arc(topnodepts[i], between, topnodepts[i + 1]).ToNurbsCurve();
                    topBeams.Add(arc);
                }

                for (int i = 0; i < bottompts.Count - 1; i++)
                {
                    Line bl = new Line(bottompts[i], bottompts[i + 1]);
                    bottomBeams.Add(bl.ToNurbsCurve());
                }

                foreach (Curve arc in topBeams)
                {
                    var pmss = arc.DivideByCount(8, true);
                    var pointss = new List<Point3d>();
                    foreach (var p in pmss)
                        pointss.Add(arc.PointAt(p));
                    for (int i = 0; i < pointss.Count - 1; i++)
                        topBeamsA.Add((new Line(pointss[i], pointss[i + 1]).ToNurbsCurve()));
                }

                //Creating the truss
                for (int i = 1; i < bottompts.Count - 2; i++)
                {
                    Line cl = new Line(topnodepts[i], bottompts[i + 1]);
                    Line cl2 = new Line(bottompts[i], topnodepts[i + 1]);
                    truss.Add(cl.ToNurbsCurve());
                    truss.Add(cl2.ToNurbsCurve());
                }
            }

            //Arch with X truss and vertical elements
            if (type == 9)
            {
                var tbeam = new Arc(pt1, toppt, pt2).ToNurbsCurve();
                double[] pnodes = tbeam.DivideByCount(div, true); //Division into where truss nodes is
                var pms2 = tbeam.DivideByCount(div * 4, true); //Division into straight elements to approximate curve
                var arcpts = new List<Point3d>(); //Full list of approximation points

                foreach (var p in pnodes) //Dividing the top beam into several divisions
                {
                    toppts.Add(tbeam.PointAt(p));
                }

                var tbeam2 = new Arc(pt1, toppt, pt2);
                var rad = tbeam2.Radius;
                var center = tbeam2.Center;

                foreach (var point in bottompts)
                {
                    double z = center.Z + Math.Sqrt(rad * rad - (point.X - center.X) * (point.X - center.X));
                    toppts.Add(new Point3d(point.X, point.Y, z));
                    topnodepts.Add(new Point3d(point.X, point.Y, z));

                }

                foreach (var p in pms2)
                    arcpts.Add(tbeam.PointAt(p));

                //Dividing the arc into smaller pieces and then dividing the pieces for analyze
                for (int i = 0; i < topnodepts.Count - 1; i++)
                {
                    Point3d between = new Point3d(
                        (topnodepts[i].X + topnodepts[i + 1].X) / 2,
                        (topnodepts[i].Y + topnodepts[i + 1].Y) / 2,
                         center.Z + Math.Sqrt(rad * rad - ((topnodepts[i].X + topnodepts[i + 1].X) / 2 - center.X) * ((topnodepts[i].X + topnodepts[i + 1].X) / 2 - center.X)));
                    Curve arc = new Arc(topnodepts[i], between, topnodepts[i + 1]).ToNurbsCurve();
                    topBeams.Add(arc);
                }

                for (int i = 0; i < bottompts.Count - 1; i++)
                {
                    Line bl = new Line(bottompts[i], bottompts[i + 1]);
                    bottomBeams.Add(bl.ToNurbsCurve());
                }

                foreach (Curve arc in topBeams)
                {
                    var pmss = arc.DivideByCount(8, true);
                    var pointss = new List<Point3d>();
                    foreach (var p in pmss)
                        pointss.Add(arc.PointAt(p));
                    for (int i = 0; i < pointss.Count - 1; i++)
                        topBeamsA.Add((new Line(pointss[i], pointss[i + 1]).ToNurbsCurve()));
                }

                //Creating the truss
                for (int i = 1; i < bottompts.Count - 2; i++)
                {
                    Line cl = new Line(topnodepts[i], bottompts[i + 1]);
                    Line cl2 = new Line(bottompts[i], topnodepts[i + 1]);
                    truss.Add(cl.ToNurbsCurve());
                    truss.Add(cl2.ToNurbsCurve());
                }
                for (int i = 1; i < bottompts.Count - 1; i++)
                {
                    Line vert = new Line(topnodepts[i], bottompts[i]);
                    truss.Add(vert.ToNurbsCurve());
                }
            }
            /*
            //Boxtruss
            if (type == 10)
            {
                var toppt1 = new Point3d(pt1.X, pt1.Y, pt1.Z + height);
                var toppt2 = new Point3d(pt2.X, pt2.Y, pt2.Z + height);
                var tbeam = new Polyline(new List<Point3d> { toppt1, toppt, toppt2 }).ToNurbsCurve(); //Making the top beam a linear beam

                //Creating the top and bottom beams
                double[] pms2 = tbeam.DivideByCount(div, true);
                foreach (var p in pms2)
                {
                    toppts.Add(tbeam.PointAt(p));
                }

                for (int i = 0; i < bottompts.Count - 1; i++)
                {
                    Line bl = new Line(bottompts[i], bottompts[i + 1]);
                    Line tl = new Line(toppts[i], toppts[i + 1]);
                    bottomBeams.Add(bl.ToNurbsCurve());
                    topBeams.Add(tl.ToNurbsCurve());
                    topBeamsA.Add(tl.ToNurbsCurve());
                }

                //Creating the truss
                for (int i = 0; i < bottompts.Count; i++)
                {
                    Line v = new Line(toppts[i], bottompts[i]);
                    truss.Add(v.ToNurbsCurve());
                }
            }

            //Boxtruss 2
            if (type == 11)
            {
                var toppt1 = new Point3d(pt1.X, pt1.Y, pt1.Z + height);
                var toppt2 = new Point3d(pt2.X, pt2.Y, pt2.Z + height);
                var tbeam = new Polyline(new List<Point3d> { toppt1, toppt, toppt2 }).ToNurbsCurve(); //Making the top beam a linear beam

                //Creating the top and bottom beams
                double[] pms2 = tbeam.DivideByCount(div, true);
                foreach (var p in pms2)
                {
                    toppts.Add(tbeam.PointAt(p));
                }

                for (int i = 0; i < bottompts.Count - 1; i++)
                {
                    Line bl = new Line(bottompts[i], bottompts[i + 1]);
                    Line tl = new Line(toppts[i], toppts[i + 1]);
                    bottomBeams.Add(bl.ToNurbsCurve());
                    topBeams.Add(tl.ToNurbsCurve());
                    topBeamsA.Add(tl.ToNurbsCurve());
                }

                //Creating the truss
                truss.Add(new Line(bottompts[0], toppts[0]).ToNurbsCurve());
                truss.Add(new Line(bottompts[bottompts.Count-1], toppts[toppts.Count - 1]).ToNurbsCurve());
                for (int i = 0; i < bottompts.Count - 1; i = i + 2)
                {
                    Line tr = new Line(toppts[i], bottompts[i + 1]);
                    Line tr2 = new Line(bottompts[i + 1], toppts[i + 2]);
                    truss.Add(tr.ToNurbsCurve());
                    truss.Add(tr2.ToNurbsCurve());
                }
            }
            */

            //Output
            DA.SetDataList(0, topBeamsA);
            DA.SetDataList(1, truss);
            DA.SetDataList(2, bottomBeams);
            DA.SetDataList(3, supportpoints);
            DA.SetDataList(4, topBeams);
            //DA.SetDataList(5, smallerarcs);
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
            get { return new Guid("84DBA603-5647-41F9-A1C5-C7FE7DBF1F8B"); }
        }
    }
}