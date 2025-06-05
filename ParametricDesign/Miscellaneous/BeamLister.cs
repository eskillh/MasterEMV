using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Masterv2
{
    public class BeamLister : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamLister class.
        /// </summary>
        public BeamLister()
          : base("BeamLister", "Nickname",
              "Description",
              "Master", "List")
        {
        }

        //INPUT
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves","crvs","Curves of the beams with the defined cross section and material", GH_ParamAccess.list);
            pManager.AddTextParameter("Cross Section", "croSec", "Cross sections with the material of the beams", GH_ParamAccess.item);
        }

        //OUTPUT
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Beams w/ properties", "b", "The beams with the cross section, length and strength class", GH_ParamAccess.tree);
            pManager.AddTextParameter("test", "t", "", GH_ParamAccess.item);
            pManager.AddNumberParameter("test2", "t2", "", GH_ParamAccess.list);
        }

        //CODE
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> crvs = new List<Curve>();
            DA.GetDataList(0, crvs);
            String cs = "";
            DA.GetData(1, ref cs);

            //Defining the material properties
            string mat = ExtractValue(cs, @"material:'([^']*)'");
            string h1 = ExtractValue(cs, @"height:(\d+)\[cm\]");
            double h = Convert.ToDouble(h1);
            string b1 = ExtractValue(cs, @"Flange-width:(\d+)\[cm\]");
            double b = Convert.ToDouble(b1);
            string A1 = ExtractValue(cs, @"A:(\d+)\[cm²\]");
            double A = Convert.ToDouble(A1);

            //Lengths of the beams
            List<double> ls = new List<double>();
            foreach (Curve c in crvs)
            {
                double length = c.GetLength();
                ls.Add(length);
            }

            //List with all the values
            BeamTree btree = new BeamTree();

            for (int i = 0; i < ls.Count; i++)
            {
                btree.AddElement(ls[i], mat, h, b, A);
            }

            GH_Tree holder = new GH_Tree();
            var beamTree = holder.ToGHTree(btree.elements);
            
            DA.SetDataTree(0, beamTree);
            DA.SetData(1, A);
            DA.SetDataList(2, ls);

            
        }

        //Getting the values from the string
        static string ExtractValue(string input, string pattern)
        {
            Match a = Regex.Match(input, pattern);
            return a.Groups[1].Value;
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
            get { return new Guid("95B97745-A355-4906-AF75-2B0083619190"); }
        }
    }
}