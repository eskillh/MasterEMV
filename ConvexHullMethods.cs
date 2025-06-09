using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using Rhino.Geometry;

namespace MeshFromPointCloud
{
    public static class ConvexHullMethods
    {        
        public static int SideOfLine(Point3d p1, Point3d p2, Point3d pt)
        {
            var val = (pt.Y - p1.Y) * (p2.X - p1.X)
                    - (pt.X - p1.X) * (p2.Y - p1.Y);

            if (Convert.ToInt32(val) > 0)
                return 1;

            if (Convert.ToInt32(val) < 0)
                return -1;

            return 0;
        }

        public static double DistLine(Point3d p1, Point3d p2, Point3d pt)
        {
            var val = (pt.Y - p1.Y) * (p2.X - p1.X)
                    - (pt.X - p1.X) * (p2.Y - p1.Y);

            return Math.Abs(val);
        }
        
        public static void PtBetween(List<Point3d> pts, Point3d p1, Point3d p2, double maxLength, 
            List<Point3d> ptsContainer, List<Line> shortestLines)
        {            
            var longLine = new Line(p1, p2); // make line            

            var ptsOnLongLine = new List<Point3d>(); // hold the projected points on line
            var ptsReduced = new List<Point3d>(); // only keep the relevant points to reduce computation time
            var orthoLines = new List<Line>(); // hold orthogonal lines

            Point3d newPoint; // the new point on the line between p1 and p2

            if (longLine.Length > maxLength)
            {
                for (int i = 0; i < pts.Count; i++)
                {
                    var pt = pts[i];
                    var t = longLine.ClosestParameter(pt); // find the closest pt on line for all pts                    
                    var tol = 1e-9;

                    if (t > tol && t < 1-tol) // only use the orthogonal points
                    {
                        ptsOnLongLine.Add(longLine.PointAt(t));
                        ptsReduced.Add(pt);
                        orthoLines.Add(new Line(longLine.PointAt(t), pt)); // make lines from ptOnLongLine (pointAt(0)) and pt (PointAt(1))
                    }

                }
                var orthoLinesSorted = orthoLines.OrderBy(l => l.Length).ToList(); // sort lines based on length

                shortestLines.Add(orthoLinesSorted[0]);
                var closestPoint = orthoLinesSorted[0].PointAt(1.0); // get pt from shortest line
                newPoint = orthoLinesSorted[0].PointAt(0.0); // get ptOnLongLine from shortest line
                ptsContainer.Add(closestPoint);
            }            

            else
                return;

            PtBetween(ptsReduced, newPoint, p1, maxLength, ptsContainer, shortestLines); // check if part 1 of new line is too long
            PtBetween(ptsReduced, newPoint, p2, maxLength, ptsContainer, shortestLines); // check if part 2 of new line is too long          
                       
        }
        public static void QuickHull(List<Point3d> pts, int n, Point3d p1, Point3d p2, int side, 
            double maxLength, List<Point3d> hullPts, List<Point3d> sortHullPts, List<Line> shortestLines)
        {
            int ind = -1;
            double maxDist = 0;

            for (int i = 0; i < n; i++)
            {
                double dist = DistLine(p1, p2, pts[i]);
                if (SideOfLine(p1, p2, pts[i]) == side && dist > maxDist)
                {
                    ind = i;
                    maxDist = dist;
                }
            }
            if (ind == -1)
            {
                
                //PtBetween_1(pts, p1, p2, maxLength, hullPts, shortestLines);

                if (hullPts.Contains(p1) == false)
                {
                    hullPts.Add(p1);
                    sortHullPts.Add(p1);
                }

                if (hullPts.Contains(p2) == false)
                {
                    hullPts.Add(p2);
                    sortHullPts.Add(p2);
                }

                return;
            }

            QuickHull(pts, n, pts[ind], p1, -SideOfLine(pts[ind], p1, p2), maxLength, hullPts, sortHullPts, shortestLines);
            QuickHull(pts, n, pts[ind], p2, -SideOfLine(pts[ind], p2, p1), maxLength, hullPts, sortHullPts, shortestLines);
        }

        

        

        
    }    
}
