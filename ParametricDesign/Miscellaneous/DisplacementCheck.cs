using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace TimberStructureGenerator
{
    public class DisplacementCheck : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DisplacementCheck class.
        /// </summary>
        public DisplacementCheck()
          : base("DisplacementCheck", "Nickname",
              "Description",
              "Master", "Check")
        {
        }

        //INPUT
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Displacement", "d", "Maximum displacement of the structure [cm]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Span", "s", "Span of the truss structure [m]", GH_ParamAccess.item);
        }

        //OUTPUT
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Result", "r", "If the check is ok or not", GH_ParamAccess.item);
        }

        //CODE
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var disp = new double();
            var span = new double();

            DA.GetData(0, ref disp);
            DA.GetData(1, ref span);

            var check = span * 100 / 250;
            string res = "";

            if (check < disp)
            {
                res = "Not OK, Increase dimensions";
            }
            else
            {
                res = "OK";
            }
            DA.SetData(0, res);
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
            get { return new Guid("2D86C428-7CC1-454E-A661-F1374DD13E1F"); }
        }
    }
}