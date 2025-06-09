using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace MeshFromPointCloud
{
    public static class Methods
    {
        public static List<Point3d> SortPointsAlongCurve(Curve sortingCurve, List<Point3d> pts, bool closed)
        {
            var dataList = new List<(Point3d, double)>();
            foreach (var pt in pts)
            {
                sortingCurve.ClosestPoint(pt, out double t);
                dataList.Add((pt, t));
            }

            var dataListSorted = dataList.OrderBy(item => item.Item2).ToList();

            var ptsSorted = new List<Point3d>();
            foreach (var item in dataListSorted)
                ptsSorted.Add(item.Item1);
            if (closed == true)
                ptsSorted.Add(dataListSorted[0].Item1);

            return ptsSorted;
        }

        public static List<T> SortUsingKeys<T>(List<T> values, List<double> keys)
        {
            var dataList = new List<(T, double)>();
            
            for (int i = 0; i < values.Count; i++)
                dataList.Add((values[i], keys[i]));

            var dataListSorted = dataList.OrderBy(item => item.Item2);

            var sortedValues = new List<T>();
            foreach (var item in dataListSorted)
                sortedValues.Add(item.Item1);

            return sortedValues;
        }

        /*
        /// <param name="pts">List of points</param>
        /// <param name="tol">Tolerance</param>
        /// <param name="choose">Choose if you want to leave one (0)/keep average(1)/cull both(2)</param>
        public static List<Point3d> CullDuplicatePoints(List<Point3d> pts, double tol, int choose)
        {
            var ptsDuplicate = new List<Point3d>();
            var ptsLonely = new List<Point3d>();
            for (int i = 0; i < pts.Count; i++)
            {
                var ptCheck = pts[i];
                for (int j = 0; j < pts.Count; j++)
                {
                    if (i != j)
                    {
                        var ptControl = pts[j];
                        if (Math.Abs(ptCheck.X - ptControl.X) < tol
                            && Math.Abs(ptCheck.Y - ptControl.Y) < tol
                            && Math.Abs(ptCheck.Z - ptControl.Z) < tol)
                        {
                            if (ptsDuplicate.Contains(ptCheck) == false
                                && ptsDuplicate.Contains(ptControl) == false)
                            {
                                ptsDuplicate.Add(ptCheck);
                            }


                            if (choose == 1
                                && ptsDuplicate.Contains(ptCheck) == false
                                && ptsDuplicate.Contains(ptControl) == false)
                            {
                                ptsDuplicate.Add(ptControl);
                            }

                        }
                        else
                            ptsLonely.Add(ptCheck);
                    }
                    
                    
                }
            }
            if (choose == 0)
            {
                return 
            }

            if (choose == 2)
                return ptsLonely;


        }
        */
    }
}
