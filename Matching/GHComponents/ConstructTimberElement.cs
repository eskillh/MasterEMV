using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using MasterThesis.Classes;
using Rhino.Geometry;

namespace MasterThesis.GHComponents
{
    public class ConstructTimberElement : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public ConstructTimberElement()
          : base("ConstructTimberElement", "Nickname",
              "Description",
              "Matching", "TimberElement")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("width", "w", "width of element [mm]", GH_ParamAccess.item);
            pManager.AddNumberParameter("height", "h", "height of element [mm]", GH_ParamAccess.item);
            pManager.AddNumberParameter("length", "l", "length of element [mm]", GH_ParamAccess.item);
            pManager.AddTextParameter("class", "c", "timber class", GH_ParamAccess.item, "C24");
            pManager.AddTextParameter("id", "id", "element id", GH_ParamAccess.item, "E0");
            pManager.AddTextParameter("location", "loc", "geographical location", GH_ParamAccess.item, "Trondheim");
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("timberElement", "te", "timber element object", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double width = 0.0;
            double height = 0.0;
            double length = 0.0;
            string tClass = "";
            string id = "";
            string loc = "";

            DA.GetData(0, ref width);
            DA.GetData(1, ref height);
            DA.GetData(2, ref length);
            DA.GetData(3, ref tClass);
            DA.GetData(4, ref id);
            DA.GetData(5, ref loc);

            TimberElement te = new TimberElement(id, width, height, length, tClass, loc);

            DA.SetData(0, te);




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
            get { return new Guid("970D6B32-73F2-400E-8803-9CAB245CDEA0"); }
        }
    }
}