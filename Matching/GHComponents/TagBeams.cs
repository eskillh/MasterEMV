using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Components;
using Rhino.Display;
using Rhino.Geometry;
using Rhino;
using System.Drawing;
using MasterThesis.Classes;

namespace MasterThesis.GHComponents
{
    public class TagBeams : GH_Component
    {

        private CustomDisplay _display;
        private List<Point3d> _points;
        private List<string> _texts;

        /// <summary>
        /// Initializes a new instance of the TagBeams class.
        /// </summary>
        public TagBeams()
          : base("TagBeams", "Nickname",
              "Description",
              "Matching", "TimberElement")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("breps", "brs", "", GH_ParamAccess.list);
            pManager.AddGenericParameter("timberElements", "tes", "", GH_ParamAccess.list);
            pManager.AddIntegerParameter("options", "opts", "1: id, length", GH_ParamAccess.item, 0);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
        }


        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Brep> breps = new List<Brep>();
            List<TimberElement> timberElements = new List<TimberElement>();
            int options = 0;
            List<Point3d> points = new List<Point3d>();
            List<string> texts = new List<string>();

            DA.GetDataList(0, breps);
            DA.GetDataList(1, timberElements);
            DA.GetData(2, ref options);

            List<Point3d> centroids = new List<Point3d>();
            foreach (var brep in breps)
            {
                BrepFace face = brep.Faces[0];
                AreaMassProperties areaProps = AreaMassProperties.Compute(face);
                Point3d centroid = areaProps.Centroid;
                centroids.Add(centroid);

            }
            foreach (var te in timberElements)
            {
                if (options == 1)
                {
                    texts.Add($"{te.id}, {te.length} mm");
                }
                else if (options == 2)
                {
                    texts.Add($"{te.length} mm");
                }
                else if (options == 3)
                {
                    texts.Add($"{te.width} x {te.height} x {te.length}");
                }
                else if (options == 4)
                {
                    texts.Add($"{te.id}: {te.width} x {te.height} x {te.length}");
                }
                else
                {
                    texts.Add($"{te.id}");
                }
            }



            _points = centroids;
            _texts = texts;

        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (_points == null || _texts == null) return;

            for (int i = 0; i < _points.Count; i++)
            {
                args.Display.Draw2dText(_texts[i], Color.Blue, _points[i], true, 12);
            }
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            _display?.Dispose(); // Clean up
            base.RemovedFromDocument(document);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override Bitmap Icon
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
            get { return new Guid("F628DA6F-6545-4107-921E-D69110BCFA8C"); }
        }
    }
}