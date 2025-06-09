using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace MeshFromPointCloud
{
    public class BeamFromMesh : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamFromMesh class.
        /// </summary>
        public BeamFromMesh()
          : base("BeamFromMesh", "Nickname",
              "Description",
              "Master", "Mesh")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh from Scan", "mfs", "Mesh", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Divisions", "", "How many divisions along beam", GH_ParamAccess.item, 10);
            pManager.AddIntegerParameter("rebuildPts", "", "how many pts are used to rebuild the cross section (more are needed if many cracks)", GH_ParamAccess.item, 20);
            pManager.AddIntegerParameter("Degree", "", "", GH_ParamAccess.item, 4);
            pManager.AddNumberParameter("Offset", "", "Offset end section planes", GH_ParamAccess.item, 10);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Beam", "", "", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Planes", "", "", GH_ParamAccess.list);
            pManager.AddCurveParameter("crossSections", "", "", GH_ParamAccess.list);
            pManager.AddCurveParameter("axis", "", "", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Import points
            var mesh = new Mesh();
            int div = 0;
            int rebuildPts = 0;
            int degree = 0;
            double offset = 0;

            DA.GetData(0, ref mesh);
            DA.GetData(1, ref div);
            DA.GetData(2, ref rebuildPts);            
            DA.GetData(3, ref degree);
            DA.GetData(4, ref offset);

            var vertices = mesh.Vertices;
            var pts = new List<Point3d>();

            foreach (var vertex in vertices)
                pts.Add(new Point3d(vertex.X, vertex.Y, vertex.Z));

            
            // Get centerline            
            Line.TryFitLineToPoints(pts, out Line axis);

            axis.Extend(-offset, -offset);

            Curve axisCurve = axis.ToNurbsCurve();
            Interval acDomain = axisCurve.Domain;

            // Get Perp Frames along centerline
            List<double> parameters = new List<double>();

            for (int i = 0; i < div; i++)
            {
                var par = Convert.ToDouble(i) / (Convert.ToDouble(div) - 1);
                parameters.Add(acDomain.ParameterAt(par));
            }


            var perpFrames = axisCurve.GetPerpendicularFrames(parameters).ToList();

            int ptsCount = pts.Count;
            int frameCount = perpFrames.Count;

            var crossSections = new List<Curve>();
            
            for (int i = 0; i < frameCount; i++)
            {
                var plane = perpFrames[i];
                var crossSection = Intersection.MeshPlane(mesh, plane)[0].ToNurbsCurve();
                var crossSectionRebuilt = crossSection.Rebuild(rebuildPts, degree, false);
                crossSections.Add(crossSectionRebuilt);
            }
            
            /*
            var mainSubCurves = mainCurve.GetSubCurves();
            var longestSubCurve = mainSubCurves.Aggregate((longest, current) =>
                                current.GetLength() < longest.GetLength() ? current : longest);
            var mainPoint = longestSubCurve.PointAtMid;
            */

            var mainCurve = crossSections[0];
            var mainPoint = mainCurve.PointAtStart;

            //for (int i = 0; i < crossSections.Count-1; i++)
            foreach (var cs in crossSections)
            {
                //var cs = crossSections[i];
                cs.ClosestPoint(mainPoint, out double t);
                cs.ChangeClosedCurveSeam(t);
            }

            // refine crossSectionPline to be smooth so loft will be better

            

            // loft all crossSection curves using LoftRebuild to get a nice brep
            var loftedBeam = Brep.CreateFromLoft(crossSections, Point3d.Unset, Point3d.Unset, LoftType.Tight, false)[0];
            var beam = loftedBeam.CapPlanarHoles(0.0001);

            DA.SetData(0, beam);
            DA.SetDataList(1, perpFrames);
            DA.SetDataList(2, crossSections);
            DA.SetData(3, axisCurve);
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
            get { return new Guid("B81CA612-EB02-4AD6-B132-D08D82BD604D"); }
        }
    }
}
