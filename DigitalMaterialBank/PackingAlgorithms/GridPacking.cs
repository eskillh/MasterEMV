using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace MeshFromPointCloud.Packing
{
    public class GridPacking : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ImPacking3 class.
        /// </summary>
        public GridPacking()
          : base("ImPacking3", "Nickname",
              "Description",
              "Master", "Packing")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Perimeter", "", "", GH_ParamAccess.item);
            pManager.AddRectangleParameter("Rectangles", "", "", GH_ParamAccess.list);
            pManager.AddNumberParameter("Offset", "", "", GH_ParamAccess.item, 0);
            //pManager.AddIntegerParameter("Order", "", "choose which side to start packing from", GH_ParamAccess.item, 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddRectangleParameter("Packed", "", "", GH_ParamAccess.list);
            pManager.AddRectangleParameter("testing", "", "", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Curve perimeter = null;
            var shapes = new List<Rectangle3d>();
            double offset = 0;
            //int chooseSide = 0;

            DA.GetData(0, ref perimeter);
            DA.GetDataList(1, shapes);
            DA.GetData(2, ref offset);
            //DA.GetData(3, ref chooseSide);

            var perimPts = new List<Point3d>();
            var perims = new List<Curve>() { perimeter };

            shapes = shapes.OrderBy(x => x.Height).ToList(); // sort shapes after height
            shapes.Reverse(); // tallest shape first

            var shapesWithOffset = new List<Rectangle3d>();

            foreach (var shape in shapes)
            {
                var width = Math.Round(shape.Width + 2 * offset, 3);
                var height = Math.Round(shape.Height + 2 * offset, 3);
                shapesWithOffset.Add(new Rectangle3d(shape.Plane, width, height));
            }


            perimeter.TryGetPlane(out Plane planePerimeter);

            foreach (var sub in perimeter.GetSubCurves()) // find all defining points of perim
                perimPts.Add(sub.PointAtStart);

            var shape0WithOffset = shapesWithOffset[0]; // get first shape to place with offset
            shapesWithOffset.RemoveAt(0); // remove from shapes needing placing

            /*
            var perimCenter = AreaMassProperties.Compute(perimeter).Centroid;

            var startPoint = new Point3d();
            var signX = -1;
            var signY = -1;            

            // get start point
            if (chooseSide == 0) // leftmost point
            {
                startPoint = perimPts.Aggregate((leftmost, current) =>
                                current.X < leftmost.X ? current : leftmost);
            }
            else if (chooseSide == 1) // bottom point
            {
                startPoint = perimPts.Aggregate((leftmost, current) =>
                                current.Y < leftmost.Y ? current : leftmost);
            }
            else if (chooseSide == 2) // top point
            {
                startPoint = perimPts.Aggregate((leftmost, current) =>
                                current.Y > leftmost.Y ? current : leftmost);
            }
            else if (chooseSide == 3) // righmost point
            {
                startPoint = perimPts.Aggregate((leftmost, current) =>
                                current.X > leftmost.X ? current : leftmost);
            }

            if (startPoint.X > perimCenter.X)
                signX = 1;
            if (startPoint.Y > perimCenter.Y)
                signY = 1;

            var plane = new Plane(startPoint, shape0WithOffset.Plane.ZAxis);
            var firstShapeWithOffset = new Rectangle3d(
                plane, signX * shape0WithOffset.Width, signY * shape0WithOffset.Height);

            var test = new List<Rectangle3d>() { firstShapeWithOffset };
            var cornerStatus = new List<int>() { 0, 0, 0, 0 };

            // checks which corners are inside and outside perimeter
            PackingMethods.CornerInsidePerimeter(firstShapeWithOffset, perimeter, cornerStatus);

            // iteratively snap corner outside of perim to closest point on perim until all corners are inside
            while (cornerStatus.Contains(2))
            {
                var moveToClosestPt = new Vector3d();
                double shortestDist = 10000;

                for (int j = 0; j < cornerStatus.Count; j++)
                    if (cornerStatus[j] == 2)
                    {
                        var corner = firstShapeWithOffset.Corner(j);
                        perimeter.ClosestPoint(corner, out double t);
                        var perimPt = perimeter.PointAt(t);
                        var cornerToPerimLine = new Line(corner, perimPt);

                        if (cornerToPerimLine.Length < shortestDist)
                        {
                            moveToClosestPt = cornerToPerimLine.Direction;
                            shortestDist = cornerToPerimLine.Length;
                        }
                    }

                firstShapeWithOffset.Transform(Transform.Translation(moveToClosestPt));
                test.Add(firstShapeWithOffset);
                PackingMethods.CornerInsidePerimeter(firstShapeWithOffset, perimeter, cornerStatus);
            }
            */ // old way of placing first shape, now it is generalized in a method

            var startPoints = new List<Point3d>();

            var startPoint = perimPts.Aggregate((leftmost, current) =>
                                current.X < leftmost.X ? current : leftmost);
            startPoints.Add(startPoint);

            startPoint = perimPts.Aggregate((leftmost, current) =>
                                current.Y < leftmost.Y ? current : leftmost);
            startPoints.Add(startPoint);

            startPoint = perimPts.Aggregate((leftmost, current) =>
                                current.Y > leftmost.Y ? current : leftmost);
            startPoints.Add(startPoint);

            startPoint = perimPts.Aggregate((leftmost, current) =>
                                current.X > leftmost.X ? current : leftmost);
            startPoints.Add(startPoint);
            /*
            // get start point
            if (chooseSide == 0) // leftmost point
            {
                startPoint = perimPts.Aggregate((leftmost, current) =>
                                current.X < leftmost.X ? current : leftmost);
            }
            else if (chooseSide == 1) // bottom point
            {
                startPoint = perimPts.Aggregate((leftmost, current) =>
                                current.Y < leftmost.Y ? current : leftmost);
            }
            else if (chooseSide == 2) // top point
            {
                startPoint = perimPts.Aggregate((leftmost, current) =>
                                current.Y > leftmost.Y ? current : leftmost);
            }
            else if (chooseSide == 3) // righmost point
            {
                startPoint = perimPts.Aggregate((leftmost, current) =>
                                current.X > leftmost.X ? current : leftmost);
            }
            */
            var test = new List<Rectangle3d>();

            var bestPacking = new List<Rectangle3d>();
            foreach (var pt in startPoints)
            {
                var firstShapeWithOffset = PackingMethods.PlaceFirstRectangle(pt, shape0WithOffset, perimeter);

                var packedWithOffset = new List<Rectangle3d>() { firstShapeWithOffset };

                foreach (var placeShapeWithOffset in shapesWithOffset)
                {
                    var goodNeighbor = new Rectangle3d();
                    foreach (var shape in packedWithOffset)
                    {
                        // find all neighbors along gridlines
                        var neighbors = PackingMethods.GridNeighbors(shape, placeShapeWithOffset);

                        foreach (var neighbor in neighbors) // check for the best one according to a given criteria
                        {
                            // is shape inside perimeter and not overlapping any other shapes
                            if (PackingMethods.IsInsidePerimeter(neighbor, perimeter, false)
                                && PackingMethods.NoOverlap(neighbor, packedWithOffset))
                            {
                                goodNeighbor = neighbor;
                                break;
                            }
                        }

                        if (goodNeighbor.Area > 0) // if a neighbor is found
                        {
                            packedWithOffset.Add(goodNeighbor);

                            break; // shape placed, go next iteration
                        }
                    }
                    if (goodNeighbor.Area == 0)
                        break; // if no neighbor is found for any placed shape, stop looking
                }
                if (packedWithOffset.Count > bestPacking.Count)
                    bestPacking = packedWithOffset;
            }
            /*
            var firstShapeWithOffset = PackingMethods.PlaceFirstRectangle(startPoint, shape0WithOffset, perimeter);

            test.Add(firstShapeWithOffset);

            var packedWithOffset = new List<Rectangle3d>() { firstShapeWithOffset };
            var mightHaveNeighbor = new List<Rectangle3d>() { firstShapeWithOffset };
            
            
            foreach (var placeShapeWithOffset in shapesWithOffset)
            {
                foreach (var shape in mightHaveNeighbor)
                {
                    // find all neighbors along gridlines
                    var neighbors = PackingMethods.GridNeighbors(shape, placeShapeWithOffset);
                    
                    var goodNeighbor = new Rectangle3d();

                    foreach (var neighbor in neighbors) // check for the best one according to a given criteria
                    {
                        // is shape inside perimeter and not overlapping any other shapes
                        if (PackingMethods.IsInsidePerimeter(neighbor, perimeter, false)
                            && PackingMethods.NoOverlap(neighbor, mightHaveNeighbor))
                        {
                            goodNeighbor = neighbor;
                            break;
                        }
                    }

                    if (goodNeighbor.Area > 0) // if a neighbor is found
                    {
                        packedWithOffset.Add(goodNeighbor);
                        mightHaveNeighbor.Add(goodNeighbor);

                        break; // shape placed, go next iteration
                    }
                }
            }
            */ // code if best start point should be the input and not automatically finding the best one
            var packed = new List<Rectangle3d>();

            for (int i = 0; i < bestPacking.Count; i++)
            {
                var packedOffset = bestPacking[i];
                var original = shapes[i];
                var centroidPlane = new Plane(packedOffset.Center, packedOffset.Plane.ZAxis);
                var packedNoOffset = new Rectangle3d(centroidPlane, original.X, original.Y);
                packed.Add(packedNoOffset);
            }

            DA.SetDataList(0, packed);
            DA.SetDataList(1, test);
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
            get { return new Guid("AA558FD9-C429-4367-8F4F-57813B4A3B9D"); }
        }
    }
}
