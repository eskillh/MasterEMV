using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using MasterThesis.Classes;

namespace MasterThesis.GHComponents
{
    public class WriteElementsToCSV : GH_Component
    {
        public WriteElementsToCSV()
          : base("WriteElementsToCSV", "WriteCSV",
              "Converts a list of TimberElements into CSV format.",
              "Matching", "TimberElement")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("TimberElements", "TEs", "List of timber elements", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("CSV", "csv", "CSV-format string output", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<TimberElement> elements = new List<TimberElement>();
            if (!DA.GetDataList(0, elements)) return;

            List<string> csvLines = new List<string>
            {
                "id,width,height,length,timberClass,location" // CSV header
            };

            foreach (var te in elements)
            {
                string line = $"{te.id},{te.width},{te.height},{te.length},{te.timberClass},{te.location}";
                csvLines.Add(line);
            }

            DA.SetDataList(0, csvLines);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get { return null; }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("98A4B3D0-7899-4E5C-AF08-3B9DCD9E7890"); }
        }
    }
}