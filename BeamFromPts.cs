using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace MeshFromPointCloud
{
    public class BeamFromPts : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamFromPts class.
        /// </summary>
        public BeamFromPts()
          : base("BeamFromPts", "Nickname",
              "Description",
              "Master", "PointCloud")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points from Scan", "pfc", "List of points", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Divisions", "", "How many divisions along beam", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("tol", "", "how far away are points projected on each plane", GH_ParamAccess.item, 5);
            pManager.AddNumberParameter("crackLength", "", "opening of crack length for something to be considered a crack", GH_ParamAccess.item, 100);
            pManager.AddIntegerParameter("rebuildPts", "", "how many pts are used to rebuild the cross section (more are needed if many cracks)", GH_ParamAccess.item, 20);
            //pManager.AddNumberParameter("deleteShortSeg", "", "deletes segments of cross section shorter than this number", GH_ParamAccess.item, 2);
            pManager.AddIntegerParameter("Degree", "", "", GH_ParamAccess.item, 4);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Beam", "", "", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Planes", "", "", GH_ParamAccess.list);
            pManager.AddPointParameter("Points close to plane", "", "", GH_ParamAccess.tree);            
            pManager.AddCurveParameter("crossSections", "", "", GH_ParamAccess.list);            
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Import points
            var pts = new List<Point3d>();
            int div = 0;
            double tol = 0;
            double maxLength = 0;
            int rebuildPts = 0;
            //double deleteShortSeg = 0;
            int degree = 0;

            DA.GetDataList(0, pts);
            DA.GetData(1, ref div);
            DA.GetData(2, ref tol);
            DA.GetData(3, ref maxLength);
            DA.GetData(4, ref rebuildPts);
            //DA.GetData(5, ref deleteShortSeg);
            DA.GetData(5, ref degree);

            // Get centerline            
            Line.TryFitLineToPoints(pts, out Line axis);

            Curve axisCurve = axis.ToNurbsCurve();
            Interval acDomain = axisCurve.Domain;

            // Get Perp Frames along centerline
            List<double> parameters = new List<double>();
            //int n = Convert.ToInt32(div);

            for (int i = 0; i < div; i++)
            {
                var par = Convert.ToDouble(i) / (Convert.ToDouble(div) - 1);
                parameters.Add(acDomain.ParameterAt(par));
            }

            
            var perpFrames = axisCurve.GetPerpendicularFrames(parameters).ToList();

            int ptsCount = pts.Count;
            int frameCount = perpFrames.Count;


            // Get point coordinates related to perpframes[0] (all frames are along the same z-axis)
            Point3d[] ptsPlaneSpace = new Point3d[ptsCount];


            for (int i = 0; i < ptsCount; ++i)
            {
                var ptInPlaneSpace = new Point3d();
                perpFrames[0].RemapToPlaneSpace(pts[i], out ptInPlaneSpace);
                ptsPlaneSpace[i] = ptInPlaneSpace;
            }

            var planeDist = new List<double>();

            for (int i = 0; i < frameCount; ++i) // Get distance to other planes so we dont have to change coordinates for each plane
            {
                var pl_0 = perpFrames[0].Origin;
                var pl_i = perpFrames[i].Origin;
                var dist = pl_0.DistanceTo(pl_i);

                planeDist.Add(dist);
            }


            // Find the points that are within a tolerance of the plane
            var ghPtsFrame = new GH_Structure<GH_Point>();

            for (int i = 0; i < frameCount; ++i)
            {
                var path = new GH_Path(i);

                var tol_b = planeDist[i] - tol; // adjust tolerance to fit z-coord of other planes so we only have to remap the pts ones
                var tol_t = planeDist[i] + tol;

                for (int j = 0; j < ptsCount; ++j)
                {
                    var pt = ptsPlaneSpace[j];

                    if (pt.Z > tol_b && pt.Z < tol_t) // cheack if point is within tolerance of plane
                    {
                        ghPtsFrame.Append(new GH_Point(pts[j]), path); // add points within tolerance                       
                    }
                }
            }

            // Project the points onto the plane and sort them in the same direction to make polylines            
            var crossSections = new List<Curve>();
            Curve mainCurve = null;
            //var crossSectionsPline = new List<Polyline>();            

            var worldPlane = new Plane(new Point3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));            

            for (int i = 0; i < frameCount; ++i)
            {
                var path = new GH_Path(i);
                var ptsProjectedOnFrame = new List<Point3d>();
                var ptsProjectedOnFrameWorld = new List<Point3d>();
                var rToWorld = Transform.PlaneToPlane(perpFrames[i], worldPlane); // make transformation matrix from local plane to world plane
                var rToLocal = Transform.PlaneToPlane(worldPlane, perpFrames[i]); // make transformation matrix from world plane to local plane

                for (int j = 0; j < ghPtsFrame[path].Count; ++j)
                {
                    var pt = ghPtsFrame[path][j];
                    perpFrames[i].ClosestParameter(pt.Value, out double s, out double t);  // get params of closest point on plane

                    var ptPlane = perpFrames[i].PointAt(s, t);
                    ptsProjectedOnFrame.Add(ptPlane); // point projected on local plane

                    ptPlane.Transform(rToWorld); // transform to world plane
                    ptsProjectedOnFrameWorld.Add(ptPlane); // point projected on world plane                    
                }
                
                
                if (i == 0 || i == frameCount-1)
                {                    
                    var crossSection = GetCrossSection.CrossSectionEndQH(ptsProjectedOnFrameWorld, maxLength).ToNurbsCurve();                    
                    crossSection.Transform(rToLocal);
                    mainCurve = crossSection;
                    var crossSectionRebuilt = crossSection.Rebuild(rebuildPts, degree, false);
                    //crossSectionRebuilt.RemoveShortSegments(deleteShortSeg);
                    crossSections.Add(crossSectionRebuilt);                    
                }               

                else
                {                    
                    var crossSection = GetCrossSection.CrossSection(ptsProjectedOnFrame, ptsProjectedOnFrameWorld).ToNurbsCurve();
                    var crossSectionRebuilt = crossSection.Rebuild(rebuildPts, degree, false);
                    //crossSectionRebuilt.RemoveShortSegments(deleteShortSeg);
                    crossSections.Add(crossSectionRebuilt);
                }
                    
                
            }

            var mainSubCurves = mainCurve.GetSubCurves();
            var longestSubCurve = mainSubCurves.Aggregate((longest, current) =>
                                current.GetLength() < longest.GetLength() ? current : longest);
            var mainPoint = longestSubCurve.PointAtMid;

            //var mainCurve = crossSections[0];
            //var mainPoint = mainCurve.PointAtStart;
            
            //for (int i = 0; i < crossSections.Count-1; i++)
            foreach(var cs in crossSections)
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
            DA.SetDataTree(2, ghPtsFrame);            
            DA.SetDataList(3, crossSections);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("2D1A67CB-3395-4046-90E2-0C407AB74FA8"); }
        }
    }
}