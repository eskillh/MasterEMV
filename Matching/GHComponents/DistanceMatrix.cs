using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using MasterThesis.Classes;
using Rhino.Geometry;

namespace MasterThesis.GHComponents
{
    public class DistanceMatrix : GH_Component
    {
        private bool _isFetching = false;
        private bool _dataReady = false;
        private List<string> _result2;
        private Dictionary<string, double> _locDist;
        private List<Route> _routes;

        public DistanceMatrix()
          : base("DistanceMatrix", "Nickname",
              "Description",
              "Matching", "Matching tools")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("supplyElements", "ses", "List of timber elements in store", GH_ParamAccess.list);
            pManager.AddGenericParameter("demandElements", "des", "List of wanted timber elements", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("out", "o", "", GH_ParamAccess.list);
            pManager.AddGenericParameter("preprocessing", "p", "", GH_ParamAccess.item);
            pManager.AddGenericParameter("routes", "r", "", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<TimberElement> stes = new List<TimberElement>();
            List<TimberElement> dtes = new List<TimberElement>();

            if (!DA.GetDataList(0, stes)) return;
            if (!DA.GetDataList(1, dtes)) return;

            if (!_dataReady && !_isFetching)
            {
                _isFetching = true;

                Task.Run(async () =>
                {
                    try
                    {
                        var locDist = await TimberElement.GetLocationDistances(stes, dtes);
                        var routes = await TimberElement.GetRoutes(stes, dtes);
                        var result2 = Route.GetRouteTable(routes);

                        this.OnPingDocument()?.ScheduleSolution(0, doc =>
                        {
                            _locDist = locDist;
                            _routes = routes;
                            _result2 = result2;
                            _dataReady = true;
                            _isFetching = false;
                            this.ExpireSolution(true);
                        });
                    }
                    catch (Exception ex)
                    {
                        this.OnPingDocument()?.ScheduleSolution(0, doc =>
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                            _isFetching = false;
                            this.ExpireSolution(true);
                        });
                    }
                });

                return; // Important: exit now, no outputs yet
            }

            if (_dataReady)
            {
                DA.SetDataList(0, _result2);
                DA.SetData(1, _locDist);
                DA.SetData(2, _routes);
                Rhino.RhinoApp.WriteLine("data ready");
                // Reset after successful output
                _dataReady = false;
            }
            else
            {
                // Optional: output "Fetching..." or similar to show something is happening
                DA.SetDataList(0, new List<string> { "Fetching distances..." });
                Rhino.RhinoApp.WriteLine("data not ready");
                DA.SetData(1, null);
                DA.SetData(2, null);
            }
        }

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("FD977AE4-7B7E-41D5-B470-9067F57A3D41");
    }
}
