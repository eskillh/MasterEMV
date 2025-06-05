using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;

namespace Masterv2
{
    public class GHTreeMaker
    {
        public static DataTree<Point3d> NestedList(List<List<Point3d>> pointlistlist)
        {
            var datatree = new DataTree<Point3d>();

            for (int i = 0; i < pointlistlist.Count; i++)
                datatree.AddRange(pointlistlist[i], new GH_Path(i));

            return datatree;
        }

        public static DataTree<string> StringTree(List<List<string>> stringlistlist)
        {
            var datatree = new DataTree<string>();
            for (int i = 0; i < stringlistlist.Count; i++)
                datatree.AddRange(stringlistlist[i], new GH_Path(i));

            return datatree;
        }

        public static DataTree<Point3d> ThreeDPointList(List<List<List<Point3d>>> pointlist)
        {
            var datatree = new DataTree<Point3d>();

            for (int i = 0; i < pointlist.Count; i++)
            {
                for (int j = 0; j < pointlist[i].Count; j++)
                    datatree.AddRange(pointlist[i][j], new GH_Path(i, j));
            }
            return datatree;
        }
        public static DataTree<Curve> CurveList(List<List<Curve>> pointlistlist)
        {
            var datatree = new DataTree<Curve>();

            for (int i = 0; i < pointlistlist.Count; i++)
                datatree.AddRange(pointlistlist[i], new GH_Path(i));

            return datatree;
        }
    }
}
