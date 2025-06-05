using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Threading.Tasks;
using Ed.Eto;
using MasterThesis.Classes;

namespace MasterThesis.GHComponents
{
    public class TransportGWPCalculator : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Temp class.
        /// </summary>
        public TransportGWPCalculator()
          : base("TransportGWPCalculator", "Nickname",
              "Description",
              "Matching", "Other")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Origin", "O", "Starting location", GH_ParamAccess.item, "Oslo,Norway");
            pManager.AddTextParameter("Destination", "D", "Ending location", GH_ParamAccess.item, "Trondheim,Norway");
            pManager.AddTextParameter("Mode", "M", "Transport mode (driving, transit, walking, bicycling)", GH_ParamAccess.item, "driving");
            pManager.AddGenericParameter("TimberElements", "tes", "List of timber elements used to calculate weight of goods", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Distance (km)", "Dist", "Travel distance", GH_ParamAccess.item);
            pManager.AddNumberParameter("CO2 Emission (kg)", "CO2", "CO2 emissions for transport", GH_ParamAccess.item);
            pManager.AddTextParameter("error", "e", "", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string origin = "";
            string destination = "";
            string mode = "";
            List<TimberElement> timberElements = new List<TimberElement>();

            if (!DA.GetData(0, ref origin)) return;
            if (!DA.GetData(1, ref destination)) return;
            if (!DA.GetData(2, ref mode)) return;
            if (!DA.GetDataList(3, timberElements)) return;

            TransportEmissionCalculator tec = new TransportEmissionCalculator(origin, destination, mode, timberElements);

            Task.Run(async () =>
            {
                try
                {
                    double distance = await tec.getDistance();
                    double emission = tec.getEmission();

                    Rhino.RhinoApp.WriteLine($"Distance: {distance} km, Emission: {emission} kg CO2");

                    // ✅ Schedule output update and **clear previous values**
                    this.OnPingDocument()?.ScheduleSolution(1, doc =>
                    {
                        this.Params.Output[0].ClearData(); // Clears previous values
                        this.Params.Output[1].ClearData();

                        DA.SetData(0, distance);
                        DA.SetData(1, emission);
                    });

                }
                catch (Exception ex)
                {
                    Rhino.RhinoApp.WriteLine($"Error: {ex.Message}");
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            });
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
            get { return new Guid("2DB418B4-3D71-43B6-BCDA-D67DEACDF885"); }
        }
    }
}