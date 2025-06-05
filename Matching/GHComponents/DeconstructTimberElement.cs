using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using MasterThesis.Classes;
using Rhino.Geometry;

namespace MasterThesis.GHComponents
{
    public class DeconstructTimberElement : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DeconstructTimberElement class.
        /// </summary>
        public DeconstructTimberElement()
          : base("DeconstructTimberElement", "Nickname",
              "Description",
              "Matching", "TimberElement")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("timberElement", "te", "", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("id", "id", "", GH_ParamAccess.item);
            pManager.AddNumberParameter("width", "w", "", GH_ParamAccess.item);
            pManager.AddNumberParameter("height", "h", "", GH_ParamAccess.item);
            pManager.AddNumberParameter("length", "l", "", GH_ParamAccess.item);
            pManager.AddTextParameter("class", "c", "", GH_ParamAccess.item);
            pManager.AddTextParameter("location", "loc", "", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            TimberElement te = new TimberElement();

            DA.GetData(0, ref te);

            string id = te.id;
            double w = te.width;
            double h = te.height;
            double l = te.length;
            string c = te.timberClass;
            string loc = te.location;

            DA.SetData(0, id);
            DA.SetData(1, w);
            DA.SetData(2, h);
            DA.SetData(3, l);
            DA.SetData(4, c);
            DA.SetData(5, loc);
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
            get { return new Guid("DB87F17F-171E-4AC2-8A8B-09DC87A51DCE"); }
        }
    }
}