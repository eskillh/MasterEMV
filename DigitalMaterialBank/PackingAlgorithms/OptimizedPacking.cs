using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace MeshFromPointCloud.Packing
{
    public class OptimizedPacking : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ImPacking2 class.
        /// </summary>
        public OptimizedPacking()
          : base("ImPacking2", "Nickname",
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
            
            DA.GetData(0, ref perimeter);
            DA.GetDataList(1, shapes);
            DA.GetData(2, ref offset);
            
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

            // get leftmost point
            var leftmostPt = perimPts.Aggregate((leftmost, current) =>
                                current.X < leftmost.X ? current : leftmost);
            var plane = new Plane(leftmostPt, planePerimeter.ZAxis); // make plane with origin in this point   

            var shape0WithOffset = shapesWithOffset[0]; // get first shape to place with offset
            shapesWithOffset.RemoveAt(0); // remove from shapes needing placing

            /*
            // place first shape with bottom left corner in origo of plane
            var firstShapeWithOffset = new Rectangle3d(plane, shape0WithOffset.Width, shape0WithOffset.Height);

            
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
                PackingMethods.CornerInsidePerimeter(firstShapeWithOffset, perimeter, cornerStatus);
            }
            */ // old way of placing first shape

            var firstShapeWithOffset = PackingMethods.PlaceFirstRectangle(leftmostPt, shape0WithOffset, perimeter);

            // reduce perim by collapsing around placed shape
            //var reducedPerim = PackingMethods.ReducePerimeter(firstShapeWithOffset, perimeter);
            //perims.Add(reducedPerim);

            var packedWithOffset = new List<Rectangle3d>() { firstShapeWithOffset };
            var mightHaveNeighbor = new List<Rectangle3d>() { firstShapeWithOffset };
            var test = new List<Rectangle3d>();
            // place the rest of the shapes one by one
            /*
            foreach (var placeShapeWithOffset in shapesWithOffset)
            {
                // find all neighbors below or on the right for the last packed shape
                var neighbors = PackingMethods.GetNeighbors(packedWithOffset.Last(), placeShapeWithOffset, 0.8, 2, false);
                // WHEN GOING FROM 0 TO 2 IT DOESNT ADD A RECTANGLE IN 1, JUST KIND OF SKIPS IT IDK WHY
                // DOESNT SKIP 1 WHEN GOING FROM 0.8 TO 2???
                //neighbors.AddRange(PackingMethods.GetNeighbors(packedWithOffset.Last(), placeShapeWithOffset, 1, 2, false));
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

                    continue; // shape placed to the right, go to next iteration
                }
                
                // if no neighbors found below or right of last placed, check if a shape can be placed above any previous shape
                for (int i = 0; i < mightHaveNeighbor.Count; i++)
                {
                    neighbors = PackingMethods.GetNeighbors(mightHaveNeighbor[i], placeShapeWithOffset, 2, 3, false); // find all possible neighbors above
                    
                    goodNeighbor = new Rectangle3d();
                    
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
                        
                        //mightHaveNeighbor.RemoveRange(0, i); // remove placed shapes where an above neighbor couldnt be found
                        break; // shape placed above, exit loop
                    }
                }
            }
            */ // Old way of placing remaining rectangles

            foreach (var placeShapeWithOffset in shapesWithOffset)
            {
                // find all neighbors below or on the right for the last packed shape
                var bestNeighbor = PackingMethods.GetBestNeighbor(packedWithOffset.Last(), placeShapeWithOffset,
                    perimeter, packedWithOffset, true);

                if (bestNeighbor.Area > 0) // if a neighbor is found
                {
                    packedWithOffset.Add(bestNeighbor);
                    mightHaveNeighbor.Add(bestNeighbor);

                    continue; // shape placed to the right, go to next iteration
                }

                // if no neighbors found below or right of last placed, check if a shape can be placed above any previous shape
                for (int i = 0; i < mightHaveNeighbor.Count; i++)
                {
                    bestNeighbor = PackingMethods.GetBestNeighbor(mightHaveNeighbor[i], placeShapeWithOffset,
                        perimeter, packedWithOffset, false); // find best neighbor above

                    if (bestNeighbor.Area > 0) // if a neighbor is found
                    {
                        packedWithOffset.Add(bestNeighbor);
                        mightHaveNeighbor.Add(bestNeighbor);

                        mightHaveNeighbor.RemoveRange(0, i); // remove placed shapes where an above neighbor couldnt be found
                        break; // shape placed above, exit loop
                    }
                }
                if (bestNeighbor.Area == 0) // if no neighbors of any alrealy placed shapes could be found
                    break; // exit loop
            }
            var packed = new List<Rectangle3d>();

            // replace offset shapes with the originals, to visualize offset between shapes
            for (int i = 0; i < packedWithOffset.Count; i++)
            {
                var packedOffset = packedWithOffset[i];
                var original = shapes[i];
                var centroidPlane = new Plane(packedOffset.Center, packedOffset.Plane.ZAxis);
                var packedNoOffset = new Rectangle3d(centroidPlane, original.X, original.Y);
                packed.Add(packedNoOffset);
            }    

            DA.SetDataList(0, packed);
            DA.SetDataList(1, packedWithOffset);
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
            get { return new Guid("11643CE5-1E50-43CA-9215-356B6FA80C93"); }
        }
    }
}
