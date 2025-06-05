using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Masterv2
{
    public class ColumnMaker : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ColumnMaker class.
        /// </summary>
        public ColumnMaker()
          : base("ColumnMaker", "Nickname",
              "Description",
              "Master", "Geometry")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Height","h","Height of the columns", GH_ParamAccess.item);
            pManager.AddPointParameter("TopPoints", "topPts", "Point at the top where the columns connect to the truss", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Columns","col","Output columns as curves for further analysis", GH_ParamAccess.list);
            pManager.AddPointParameter("SupportPoints","supportPts","Bottom points of the columns where the support is", GH_ParamAccess.list);  
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var h = new double();
            var toppts = new List<Point3d>();

            DA.GetData(0, ref h);
            DA.GetDataList(1, toppts);

            var span = toppts[1].X - toppts[0].X;
            if (span > 10)
                toppts.Add(new Point3d(toppts[1].X / 2, toppts[1].Y, toppts[1].Z));
            var supportpts = new List<Point3d>();
            foreach (var pt in toppts)
            {
                var support = new Point3d(pt.X, pt.Y, pt.Z - h);
                supportpts.Add(support);
            }

            var columns = new List<Line>();
            for (int i = 0; i < toppts.Count; i++)
            {
                var col = new Line(supportpts[i], toppts[i]);
                columns.Add(col);
            }

            DA.SetDataList(0, columns);
            DA.SetDataList(1, supportpts);

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
            get { return new Guid("7FAE6679-FD06-4CC8-A5E5-3984B6BCD0AE"); }
        }
    }
}