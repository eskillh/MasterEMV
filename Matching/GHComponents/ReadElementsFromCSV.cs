using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using MasterThesis.Classes;
using Rhino.Geometry;

namespace MasterThesis.GHComponents
{
    public class ReadElementsFromCSV : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public ReadElementsFromCSV()
          : base("ReadElementsFromCSV", "Nickname",
              "Description",
              "Matching", "TimberElement")
        { 
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("CSV", "csv", "CSV-file with information about timber elements", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("timberElements", "", "", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<string> data = new List<string>();

            DA.GetDataList(0, data);

            bool isHeader = true;

            List<TimberElement> tes = new List<TimberElement>();
            foreach (var line in data)
            {
                if (isHeader)
                {
                    isHeader = false;
                    continue;
                }

                string[] values = line.Split(',');
                string id = values[0];
                double w = Convert.ToDouble(values[1]);
                double h = Convert.ToDouble(values[2]);
                double l = Convert.ToDouble(values[3]);
                string c = values[4];
                string loc = values[5];
                tes.Add(new TimberElement(id, w, h, l, c, loc));
            }

            DA.SetDataList(0, tes);
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
            get { return new Guid("6E240344-3882-4DF0-9CAF-3B96B9A0A635"); }
        }
    }
}