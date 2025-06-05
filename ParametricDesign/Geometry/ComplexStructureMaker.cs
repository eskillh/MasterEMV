using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;


namespace Masterv2
{
    public class ComplexStructureMaker : GH_Component
    {

        public ComplexStructureMaker()
          : base("ComplexStructureMaker", "Nickname",
              "Description",
              "Master", "Geometry")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("X-spacing","Xsp","Spacing between the columns in the x direction [m]", GH_ParamAccess.item, 3);
            pManager.AddNumberParameter("Y-spacing", "Ysp", "Spacing between the columns in the y direction [m]", GH_ParamAccess.item, 3);
            pManager.AddNumberParameter("X-modules", "Xm", "Number of modules in the x direction", GH_ParamAccess.item, 2);
            pManager.AddNumberParameter("Y-modules", "Ym", "Number of modules in the y direction", GH_ParamAccess.item, 2);
            pManager.AddNumberParameter("Floor height","fH","Height of the floors in the building [m]", GH_ParamAccess.item, 3);
            pManager.AddNumberParameter("Floor count","fC","Number of floors in the building", GH_ParamAccess.item, 2);
            pManager.AddNumberParameter("Bracing", "br", "Option to have bracing in the structure", GH_ParamAccess.item, 0);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Supports","t1","", GH_ParamAccess.list);
            pManager.AddGenericParameter("Floorpoints", "t2", "", GH_ParamAccess.list);
            pManager.AddGenericParameter("Columns", "t3", "", GH_ParamAccess.list);
            pManager.AddGenericParameter("Beams", "t4", "", GH_ParamAccess.list);
            pManager.AddGenericParameter("Meshes", "t5", "", GH_ParamAccess.list);
            pManager.AddGenericParameter("Bracing", "br", "", GH_ParamAccess.list);
            pManager.AddGenericParameter("ConcreteMesh", "cMsh", "", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Input
            var xspac = new double();
            var yspac = new double();
            var xn = new double();
            var yn = new double();
            var floorH = new double();
            var floorC = new double();
            var bracingoption = new double();

            DA.GetData(0, ref xspac);
            DA.GetData(1, ref yspac);
            DA.GetData(2, ref xn);
            DA.GetData(3, ref yn);
            DA.GetData(4, ref floorH);
            DA.GetData(5, ref floorC);
            DA.GetData(6, ref bracingoption);

            //Creating base points based on the inputs
            var points = new List<List<List<Point3d>>>();

            for (int f = 0; f < floorC + 1; f++)
            {
                var basepoints = new List<List<Point3d>>();
                for (int i = 0; i < yn + 1; i++)
                {
                    var pointrow = new List<Point3d>();
                    for (int j = 0; j < xn + 1; j++)
                    {
                        pointrow.Add(new Point3d(j * xspac, i * yspac, f*floorH));
                    }
                    basepoints.Add(pointrow);
                }
                points.Add(basepoints);
            }

            var points2 = new List<List<Point3d>>();
            foreach (var floor in points)
            {
                var flattenedpoints = new List<Point3d>();
                foreach (var row in floor)
                    flattenedpoints.AddRange(row);
                points2.Add(flattenedpoints);
            }
            //Making the columns
            var columns = new List<Curve>();
         
            for (int f = 0; f < floorC; f++)
            {
                for (int i = 0; i < yn + 1; i++)
                {
                    for (int j = 0; j < xn + 1; j++)
                    {
                        var col = new Line(points[f][i][j], points[f + 1][i][j]);
                        columns.Add(col.ToNurbsCurve());
                    }
                }
            }

            //Making the beams
            var beams = new List<List<Curve>>();

            for (int f = 1; f < floorC + 1; f++)
            {
                var floorbeams = new List<Curve>();
                for (int i = 0; i < yn + 1; i++)
                {
                    for (int j = 0; j < xn; j++)
                    {
                        var beam = new Line(points[f][i][j], points[f][i][j+1]);
                        floorbeams.Add(beam.ToNurbsCurve());
                    }
                }
                for (int i = 0; i < yn; i++)
                {
                    for (int j = 0; j < xn + 1; j++)
                    {
                        var beam = new Line(points[f][i][j], points[f][i+1][j]);
                        floorbeams.Add(beam.ToNurbsCurve());
                    }
                }
                beams.Add(floorbeams);
            }

            //Making the floors as meshes
            var floors = new List<Mesh>();
            var floors2 = new List<Surface>();
            for (int f = 1; f < floorC + 1; f++)
            {
                var floor2 = NurbsSurface.CreateFromCorners(points[f][0][0], points[f][0][(int)xn], points[f][(int)yn][(int)xn], points[f][(int)yn][0]);
                floors2.Add(floor2);
                var meshparam = new MeshingParameters
                {
                    GridMinCount = 50,
                    GridMaxCount = 100,
                    RefineGrid = true,
                    JaggedSeams = false,
                };
                var floor = Mesh.CreateFromSurface(floor2, meshparam);
                floors.Add(floor);
            }

            //Making meshes for the concrete floors
            var concretes = new List<Mesh>();
            var concretesurfs = new List<Surface>();
            int uCount = (int)xn + 1; // your custom u divisions
            int vCount = (int)yn + 1; // your custom v divisions

            for (int f = 1; f < floorC + 1; f++)
            {
                var surface = NurbsSurface.CreateFromCorners(points[f][0][0], points[f][0][(int)xn], points[f][(int)yn][(int)xn], points[f][(int)yn][0]);
                concretesurfs.Add(surface);
                var vertices = new List<Point3d>();
                var faces = new List<MeshFace>();
                double uStep = surface.Domain(0).Length / (uCount - 1);
                double vStep = surface.Domain(1).Length / (vCount - 1);

                for (int i = 0; i < uCount; i++)
                {
                    double u = surface.Domain(0).T0 + i * uStep;
                    for (int j = 0; j < vCount; j++)
                    {
                        double v = surface.Domain(1).T0 + j * vStep;
                        vertices.Add(surface.PointAt(u, v));
                    }
                }
                for (int i = 0; i < uCount - 1; i++)
                {
                    for (int j = 0; j < vCount - 1; j++)
                    {
                        int a = i * vCount + j;
                        int b = a + 1;
                        int c = a + vCount;
                        int d = c + 1;

                        faces.Add(new MeshFace(a, b, d, c));
                    }
                }
                var mesh = new Mesh();
                mesh.Vertices.AddVertices(vertices);
                mesh.Faces.AddFaces(faces);
                mesh.Normals.ComputeNormals();
                mesh.Compact();
                concretes.Add(mesh);
            }

            //Making floor bracing to simulate a concrete floor
            var floorbracing = new List<Curve>();
            for (int f = 1; f < floorC + 1; f++)
            {
                for (int i = 0; i < yn; i++)
                {
                    for (int j = 0; j < xn; j++)
                    {
                        var beam = new Line(points[f][i][j], points[f][i + 1][j + 1]);
                        var beam2 = new Line(points[f][i + 1][j], points[f][i][j + 1]);
                        floorbracing.Add(beam.ToNurbsCurve());
                        floorbracing.Add(beam2.ToNurbsCurve());
                    }
                }
            }

            //Making the bracing if it is an option
            var bracing = new List<Curve>();

            if (bracingoption == 1.0)
            {
                for (int f = 0; f < floorC; f++)
                {
                    for (int i = 0; i < yn + 1; i++)
                    {
                        for (int j = 0; j < xn; j++)
                        {
                            var brace = new Line(points[f][i][j], points[f + 1][i][j + 1]);
                            var brace2 = new Line(points[f][i][j+1], points[f + 1][i][j]);
                            bracing.Add(brace.ToNurbsCurve());
                            bracing.Add(brace2.ToNurbsCurve());
                        }
                    }
                }
            }

            if (bracingoption == 2.0)
            {
                for (int f = 0; f < floorC; f++)
                {
                    for (int i = 0; i < yn; i++)
                    {
                        for (int j = 0; j < xn + 1; j++)
                        {
                            var brace = new Line(points[f][i][j], points[f + 1][i+1][j]);
                            var brace2 = new Line(points[f][i+1][j], points[f + 1][i][j]);
                            bracing.Add(brace.ToNurbsCurve());
                            bracing.Add(brace2.ToNurbsCurve());
                        }
                    }
                }
            }
            if (bracingoption == 3.0)
            {
                for (int f = 0; f < floorC; f++)
                {
                    for (int i = 0; i < yn + 1; i++)
                    {
                        for (int j = 0; j < xn; j++)
                        {
                            var brace = new Line(points[f][i][j], points[f + 1][i][j + 1]);
                            var brace2 = new Line(points[f][i][j + 1], points[f + 1][i][j]);
                            bracing.Add(brace.ToNurbsCurve());
                            bracing.Add(brace2.ToNurbsCurve());
                        }
                    }
                }

                for (int f = 0; f < floorC; f++)
                {
                    for (int i = 0; i < yn; i++)
                    {
                        for (int j = 0; j < xn + 1; j++)
                        {
                            var brace = new Line(points[f][i][j], points[f + 1][i + 1][j]);
                            var brace2 = new Line(points[f][i + 1][j], points[f + 1][i][j]);
                            bracing.Add(brace.ToNurbsCurve());
                            bracing.Add(brace2.ToNurbsCurve());
                        }
                    }
                }
            }
            if (bracingoption == 4.0)
            {
                for (int f = 0; f < floorC; f++)
                {
                    /*
                    for (int i = 0; i < yn + 1; i = i + (int)yn) //i++
                    {
                        for (int j = 0; j < xn; j = j + 2)
                        {
                            var brace = new Line(points[f][i][j], points[f + 1][i][j + 1]);
                            bracing.Add(brace.ToNurbsCurve());
                        }
                    }
                    for (int i = 0; i < yn + 1; i = i + (int)yn) //i++
                    {
                        for (int j = 1; j < xn; j = j + 2)
                        {
                            var brace = new Line(points[f][i][j + 1], points[f + 1][i][j]);
                            bracing.Add(brace.ToNurbsCurve());
                        }
                    }
                    
                    for (int i = 0; i < yn; i = i + 2)
                    {
                        for (int j = 0; j < xn + 1; j = j + (int)xn) //j++
                        {
                            var brace = new Line(points[f][i][j], points[f + 1][i + 1][j]);
                            bracing.Add(brace.ToNurbsCurve());
                        }
                    }
                    for (int i = 1; i < yn; i = i + 2)
                    {
                        for (int j = 0; j < xn + 1; j = j + (int)xn) //j++
                        {
                            var brace = new Line(points[f][i + 1][j], points[f + 1][i][j]);
                            bracing.Add(brace.ToNurbsCurve());
                        }
                    }
                    */

                    if (xn > 3.0 && xn % 2.0 == 0)
                    {
                        for (int i = 0; i < yn; i = i + 2)
                        {
                            for (int j = 0; j < xn + 1; j = j + 2) //j++
                            {
                                var brace = new Line(points[f][i][j], points[f + 1][i + 1][j]);
                                bracing.Add(brace.ToNurbsCurve());
                            }
                        }
                        for (int i = 1; i < yn; i = i + 2)
                        {
                            for (int j = 0; j < xn + 1; j = j + 2) //j++
                            {
                                var brace = new Line(points[f][i + 1][j], points[f + 1][i][j]);
                                bracing.Add(brace.ToNurbsCurve());
                            }
                        }
                    }
                    if (yn > 3.0 && yn % 2.0 == 0)
                    {
                        for (int i = 0; i < yn + 1; i = i + 2) //i++
                        {
                            for (int j = 0; j < xn; j = j + 2)
                            {
                                var brace = new Line(points[f][i][j], points[f + 1][i][j + 1]);
                                bracing.Add(brace.ToNurbsCurve());
                            }
                        }
                        for (int i = 0; i < yn + 1; i = i + 2) //i++
                        {
                            for (int j = 1; j < xn; j = j + 2)
                            {
                                var brace = new Line(points[f][i][j + 1], points[f + 1][i][j]);
                                bracing.Add(brace.ToNurbsCurve());
                            }
                        }
                    }
                    if (yn % 2.0 != 0 || yn < 4.0)
                    {
                        for (int i = 0; i < yn + 1; i = i + (int)yn) //i++
                        {
                            for (int j = 0; j < xn; j = j + 2)
                            {
                                var brace = new Line(points[f][i][j], points[f + 1][i][j + 1]);
                                bracing.Add(brace.ToNurbsCurve());
                            }
                        }
                        for (int i = 0; i < yn + 1; i = i + (int)yn) //i++
                        {
                            for (int j = 1; j < xn; j = j + 2)
                            {
                                var brace = new Line(points[f][i][j + 1], points[f + 1][i][j]);
                                bracing.Add(brace.ToNurbsCurve());
                            }
                        }
                    }
                    if (xn % 2.0 != 0 || xn < 4.0)
                    {
                        for (int i = 0; i < yn; i = i + 2)
                        {
                            for (int j = 0; j < xn + 1; j = j + (int)xn) //j++
                            {
                                var brace = new Line(points[f][i][j], points[f + 1][i + 1][j]);
                                bracing.Add(brace.ToNurbsCurve());
                            }
                        }
                        for (int i = 1; i < yn; i = i + 2)
                        {
                            for (int j = 0; j < xn + 1; j = j + (int)xn) //j++
                            {
                                var brace = new Line(points[f][i + 1][j], points[f + 1][i][j]);
                                bracing.Add(brace.ToNurbsCurve());
                            }
                        }
                    }
                }

            }

            if (bracingoption == 5.0)
            {
                for (int f = 0; f < floorC; f++)
                {

                    if (xn > 3.0 && xn % 2.0 == 0)
                    {
                        for (int i = 0; i < yn; i++)
                        {
                            for (int j = 0; j < xn + 1; j = j + 2)
                            {
                                var brace = new Line(points[f][i][j], points[f + 1][i + 1][j]);
                                var brace2 = new Line(points[f][i + 1][j], points[f + 1][i][j]);
                                bracing.Add(brace.ToNurbsCurve());
                                bracing.Add(brace2.ToNurbsCurve());
                            }
                        }
                        
                    }
                    if (yn > 3.0 && yn % 2.0 == 0)
                    {
                        for (int i = 0; i < yn + 1; i = i + 2)
                        {
                            for (int j = 0; j < xn; j++)
                            {
                                var brace = new Line(points[f][i][j], points[f + 1][i][j + 1]);
                                var brace2 = new Line(points[f][i][j + 1], points[f + 1][i][j]);
                                bracing.Add(brace.ToNurbsCurve());
                                bracing.Add(brace2.ToNurbsCurve());
                            }
                        }
                    }
                    if (yn % 2.0 != 0 || yn < 4.0)
                    {
                        for (int i = 0; i < yn + 1; i = i + (int)yn)
                        {
                            for (int j = 0; j < xn; j++)
                            {
                                var brace = new Line(points[f][i][j], points[f + 1][i][j + 1]);
                                var brace2 = new Line(points[f][i][j + 1], points[f + 1][i][j]);
                                bracing.Add(brace.ToNurbsCurve());
                                bracing.Add(brace2.ToNurbsCurve());
                            }
                        }
                    }
                    if (xn % 2.0 != 0 || xn < 4.0)
                    {
                        for (int i = 0; i < yn; i++)
                        {
                            for (int j = 0; j < xn + 1; j = j + (int)xn)
                            {
                                var brace = new Line(points[f][i][j], points[f + 1][i + 1][j]);
                                var brace2 = new Line(points[f][i + 1][j], points[f + 1][i][j]);
                                bracing.Add(brace.ToNurbsCurve());
                                bracing.Add(brace2.ToNurbsCurve());
                            }
                        }
                    }
                }
            }

            if (bracingoption == 6.0)
            {
                for (int f = 0; f < floorC; f++)
                {

                    for (int i = 0; i < yn + 1; i = i + (int)yn) //i++
                    {
                        for (int j = 0; j < xn; j = j + 2)
                        {
                            var brace = new Line(points[f][i][j], points[f + 1][i][j + 1]);
                            bracing.Add(brace.ToNurbsCurve());
                        }
                    }
                    for (int i = 0; i < yn + 1; i = i + (int)yn) //i++
                    {
                        for (int j = 1; j < xn; j = j + 2)
                        {
                            var brace = new Line(points[f][i][j + 1], points[f + 1][i][j]);
                            bracing.Add(brace.ToNurbsCurve());
                        }
                    }

                    for (int i = 0; i < yn; i = i + 2)
                    {
                        for (int j = 0; j < xn + 1; j = j + (int)xn) //j++
                        {
                            var brace = new Line(points[f][i][j], points[f + 1][i + 1][j]);
                            bracing.Add(brace.ToNurbsCurve());
                        }
                    }
                    for (int i = 1; i < yn; i = i + 2)
                    {
                        for (int j = 0; j < xn + 1; j = j + (int)xn) //j++
                        {
                            var brace = new Line(points[f][i + 1][j], points[f + 1][i][j]);
                            bracing.Add(brace.ToNurbsCurve());
                        }
                    }
                }
            }

            if (bracingoption == 7.0)
            {
                for (int f = 0; f < floorC; f++)
                {
                    for (int i = 0; i < yn + 1; i = i + (int)yn)
                    {
                        for (int j = 0; j < xn; j++)
                        {
                            var brace = new Line(points[f][i][j], points[f + 1][i][j + 1]);
                            var brace2 = new Line(points[f][i][j + 1], points[f + 1][i][j]);
                            bracing.Add(brace.ToNurbsCurve());
                            bracing.Add(brace2.ToNurbsCurve());
                        }
                    }
                    for (int i = 0; i < yn; i++)
                    {
                        for (int j = 0; j < xn + 1; j = j + (int)xn)
                        {
                            var brace = new Line(points[f][i][j], points[f + 1][i + 1][j]);
                            var brace2 = new Line(points[f][i + 1][j], points[f + 1][i][j]);
                            bracing.Add(brace.ToNurbsCurve());
                            bracing.Add(brace2.ToNurbsCurve());
                        }
                    }
                }
                
            }


            var pointtree = GHTreeMaker.ThreeDPointList(points);
            //Output
            DA.SetDataTree(0, GHTreeMaker.NestedList(points[0]));
            DA.SetDataTree(1, GHTreeMaker.NestedList(points2));
            DA.SetDataList(2, columns);
            DA.SetDataTree(3, GHTreeMaker.CurveList(beams));
            DA.SetDataList(4, floors);
            DA.SetDataList(5, bracing);
            DA.SetDataList(6, floorbracing);

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
            get { return new Guid("F7F8311E-F129-4840-9204-9C41A80F1E40"); }
        }
    }
}