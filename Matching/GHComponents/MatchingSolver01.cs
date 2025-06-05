using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using MasterThesis.Classes;
using MasterThesis.Solvers;
using Rhino.Commands;
using Rhino.Geometry;

namespace MasterThesis.GHComponents
{
    public class MatchingSolver01 : GH_Component
    {

        private bool _isFetching = false;
        private bool _dataReady = false;
        private List<string> _distanceMatrix;
        private List<string> _matchings;
        private List<string> _comments;

        /// <summary>
        /// Initializes a new instance of the MatchingSolver01 class.
        /// </summary>
        public MatchingSolver01()
          : base("MatchingSolver01", "Nickname",
              "Description",
              "Matching", "Matching tools")
        {

        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("supplyElements", "ses", "List of timber elements in store", GH_ParamAccess.list);
            pManager.AddGenericParameter("demandElements", "des", "List of wanted timber elements", GH_ParamAccess.list);
            pManager.AddIntegerParameter("algorithm", "alg", "Algorithm to use for matching. 1: Greedy; 2: MIP;", GH_ParamAccess.item);
        }
        
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("distanceMatrix", "dm", "", GH_ParamAccess.list);
            pManager.AddTextParameter("matching", "o", "", GH_ParamAccess.list);
            pManager.AddTextParameter("procedure", "p", "", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<TimberElement> stes = new List<TimberElement>();
            List<TimberElement> dtes = new List<TimberElement>();
            int algorithmNumber = 0;

            if (!DA.GetDataList(0, stes)) return;
            if (!DA.GetDataList(1, dtes)) return;
            if (!DA.GetData(2, ref algorithmNumber)) return;

            if (!_dataReady && !_isFetching)
            {
                _isFetching = true;
                Task.Run(async () =>
                {
                    try
                    {
                        var routes = await TimberElement.GetRoutes(stes, dtes);
                        var distanceMatrix = Route.GetRouteTable(routes);

                        string path = @"C:\Users\Markus A Nilssen\.nuget\packages\google.ortools.runtime.win-x64\9.12.4544\runtimes\win-x64\native";
                        Environment.SetEnvironmentVariable("PATH", Environment.GetEnvironmentVariable("PATH") + ";" + path);

                        GlobalSolver solver = new GlobalSolver(stes, dtes, routes);

                        if (algorithmNumber == 1)
                            solver.SolveFullSolution(new GreedyAlgorithm());
                        else if (algorithmNumber == 2)
                            solver.SolveFullSolution(new MIPAlgorithm());
                        else
                            solver.Matchings = new List<SimpleMatching>();

                        List<string> results = SimpleMatching.PrintResults(solver.Matchings);
                        List<string> comments = solver.Comments;

                        this.OnPingDocument()?.ScheduleSolution(0, doc =>
                        {
                            _distanceMatrix = distanceMatrix;
                            _matchings = results;
                            _comments = comments;
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
                DA.SetDataList(0, _distanceMatrix);
                DA.SetDataList(1, _matchings);
                DA.SetDataList(2, _comments);
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
            get { return new Guid("DB3DC05E-0C07-4167-A2F1-4DD85384EEA6"); }
        }
    }
}