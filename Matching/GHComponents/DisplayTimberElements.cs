using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Linq;
using MasterThesis.Classes;

namespace MasterThesis.GHComponents
{
    public class DisplayTimberElements : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DisplayTimberBank class.
        /// </summary>
        public DisplayTimberElements()
          : base("DisplayTimberElements", "Nickname",
              "Description",
              "Matching", "TimberElement")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("timberElements", "tes", "list of timber elements", GH_ParamAccess.list);
            pManager.AddIntegerParameter("sortBy", "sb", "parameter to sort by (1: width, 2: height, 3: length, 4: cross sect area)", GH_ParamAccess.item, 0);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("breps", "brs", "list of breps", GH_ParamAccess.list);
            pManager.AddGenericParameter("timberElements", "te", "list of timberElements", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<TimberElement> tes = new List<TimberElement>();
            int sortBy = 0;

            DA.GetDataList(0, tes);
            DA.GetData(1, ref sortBy);

            List<Brep> breps = new List<Brep>();

            List<TimberElement> sortedTes = new List<TimberElement>();
            string sortByStr = "";

            if (sortBy == 1)
            {
                sortByStr = "width";
            } 
            else if (sortBy == 2)
            {
                sortByStr = "height";
            }
            else if (sortBy == 3)
            {
                sortByStr = "length";
            }
            else if (sortBy == 4)
            {
                sortByStr = "area";
            }

            sortedTes = TimberElement.SortTimberElements(tes, sortByStr);
            
            double orig = 0;
            foreach (var te in sortedTes)
            {
                Interval xi = new Interval(0, te.width);
                Interval yi = new Interval(0, te.length);
                Interval zi = new Interval(0, te.height);

                Point3d pt = new Point3d(orig, 0, 0);

                Box box = new Box(new Plane(pt, new Vector3d(Vector3d.ZAxis)), xi, yi, zi);
                Brep brep = Brep.CreateFromBox(box);

                breps.Add(brep);
                orig += te.width + 200;
            }

            DA.SetDataList(0, breps);
            DA.SetDataList(1, sortedTes);
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
            get { return new Guid("AFCBAE6A-62A4-4459-92DB-D3B972A3E883"); }
        }
    }
}