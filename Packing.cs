using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace MeshFromPointCloud.Packing
{
    public class Packing : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ImPacking2Slow class.
        /// </summary>
        public Packing()
          : base("ImPacking2Slow", "Nickname",
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
            

            var firstShapeWithOffset = PackingMethods.PlaceFirstRectangle(leftmostPt, shape0WithOffset, perimeter);


            var packedWithOffset = new List<Rectangle3d>() { firstShapeWithOffset };

            // place the rest of the shapes one by one
            foreach (var placeShapeWithOffset in shapesWithOffset)
            {
                var bestNeighbor = new Rectangle3d();
                foreach (var packedShape in packedWithOffset) // try first shape that was placed
                {
                    // find all neighbors
                    var neighbors = PackingMethods.GetNeighbors2(packedShape, placeShapeWithOffset);

                    foreach (var neighbor in neighbors)
                        if (PackingMethods.IsInsidePerimeter(neighbor, perimeter, false)
                            && PackingMethods.NoOverlap(neighbor, packedWithOffset)) // if a valid neighbor is found
                        {
                            bestNeighbor = neighbor; // update best neighbor
                            packedWithOffset.Add(bestNeighbor);
                            
                            break; // stop loop and start again for next shape
                        }
                    if (bestNeighbor.Area > 0) // if a best neighbor was found
                        break; // stop checking more already placed shapes for neighbors
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
            get { return new Guid("DC1CC526-7F99-4A2C-978B-E168AF477B94"); }
        }
    }
}