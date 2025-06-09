using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using Rhino.Display;
using Rhino.Geometry;

namespace MeshFromPointCloud
{
    public static class GetCrossSection
    {
        public static Polyline CrossSectionEnd(List<Point3d> ptsWorld, double inp_dist, double inp_segLength, double inp_distBtwPtsCrack)
        {
            var pts2d = new List<Point2d>();
            foreach (var pt in ptsWorld)
                pts2d.Add(new Point2d(pt.X, pt.Y)); // make 2d list for convex hull

            var convexHull = PolylineCurve.CreateConvexHull2d(pts2d.ToArray(), out int[] hullIndices); // get convex hull (CH)
            
            var ptsDistance = new List<double>(); // list to store distance between pt and the same pt on CH
            var parameters = new List<double>(); // list to store parameters of pts on CH


            // find closest pt on CH and the distance between them
            foreach (var pt in ptsWorld)
            {
                convexHull.ClosestPoint(pt, out double t); // get parameter of closest point on CH
                var ptPline = new Point3d(convexHull.PointAt(t)); // make point on CH                
                ptsDistance.Add(pt.DistanceTo(ptPline)); // add distance between the two points to list
                parameters.Add(t);
            }

            var dataList = new List<(Point3d, double)>(); // list to sort points by parameter on CH
                        
            var middlePts = new List<Point3d>();

            // find part of CH that matches the original pts
            for (int i = 0; i < ptsDistance.Count; ++i)
            {
                var dist = ptsDistance[i];
                if (dist < inp_dist)                                    
                    dataList.Add((ptsWorld[i], parameters[i])); // add the original ptsWorld and the parameters from the closest pt on CH to list
                
                else
                    middlePts.Add(ptsWorld[i]);

            }
            var sortedDataList = dataList.OrderBy(item => item.Item2).ToList(); // sort list based on keys (parameters on CH)

            var ptsSorted = new List<Point3d>();

            foreach (var item in sortedDataList)
                ptsSorted.Add(item.Item1);

            ptsSorted.Add(sortedDataList[0].Item1); // add first item to end of list for closed polyLine

            var crossSectionCurve = new Polyline(ptsSorted);
            var segments = crossSectionCurve.GetSegments();
            

            // get all parts of polyline longer than a certain length, because it is a "crack"
            var cracks = new List<Line>();            
            foreach (var seg in segments)
                if (seg.Length > inp_segLength)
                    cracks.Add(seg);            

            if (cracks.Count != 0)
            {
                var extraCrossSectionPts = new List<Point3d>();

                // find the closest orthogonal points to the curve
                for (int c = 0; c < cracks.Count; c++)
                {
                    var crack = cracks[c];

                    var ptsCrack = new List<Point3d>();
                    var ptsMiddle = new List<Point3d>();

                    var crackStart = crack.PointAt(0);
                    var crackEnd = crack.PointAt(1);

                    for (int i = 0; i < middlePts.Count; ++i)
                    {
                        var pt = middlePts[i];
                        var ptCrack = crack.ClosestPoint(pt, true);

                        if (ptCrack != crackStart && ptCrack != crackEnd) // condition to only look at the orthogonal points
                        {
                            ptsCrack.Add(ptCrack);
                            ptsMiddle.Add(pt);
                        }
                    }

                    var crackMiddleLine = new List<Line>();

                    // create lines between the orthogonal pts and the closest pts on crackLine
                    for (int i = 0; i < ptsCrack.Count; ++i)
                        crackMiddleLine.Add(new Line(ptsCrack[i], ptsMiddle[i]));


                    // find the shortest line in each interval along the crack
                    var ptsShortestLines = new List<Line>();                    
                    var spans = crack.Length/ inp_distBtwPtsCrack;
                    for (int i = 0; i < Convert.ToInt32(spans); i++)
                    {
                        var intStart = 1 / Convert.ToDouble(spans) * Convert.ToDouble(i); // interval start
                        var intEnd = 1 / Convert.ToDouble(spans) * Convert.ToDouble(i + 1); // interval end

                        var crackMiddleLineInterval = new List<Line>();
                        for (int j = 0; j < ptsCrack.Count; j++)
                        {
                            var ptCrack = ptsCrack[j];
                            var p = crack.ClosestParameter(ptCrack);

                            if (p > intStart && p < intEnd)
                                crackMiddleLineInterval.Add(crackMiddleLine[j]); // add all lines in interval to list
                        }

                        var crackMiddleLineIntervalSorted = crackMiddleLineInterval.OrderBy(line => line.Length).ToList(); // sort list                   


                        var shortLine = crackMiddleLineIntervalSorted[0]; // get shortest line from interval                    
                        extraCrossSectionPts.Add(shortLine.PointAt(1.0)); // get endpoint from shortest line and add to the rest of pts for CS
                    }


                }
                foreach (var pt in extraCrossSectionPts) // need to sort points again, so find new parameters along CH
                {
                    convexHull.ClosestPoint(pt, out double t);
                    dataList.Add((pt, t));
                }

                var newDataListSorted = dataList.OrderBy(item => item.Item2).ToList(); // sort along CH
                var newPtsSorted = new List<Point3d>();

                foreach (var item in newDataListSorted)
                    newPtsSorted.Add(item.Item1);
                newPtsSorted.Add(newDataListSorted[0].Item1); // add first point to end of list for closed polyline               
                
                crossSectionCurve = new Polyline(newPtsSorted);

            }            

            return crossSectionCurve;
        }

        public static Polyline CrossSection(List<Point3d> ptsPlane, List<Point3d> ptsWorld)
        {
            var ptsProjectedOnFrame2d = new List<Point2d>(); // prepare list to hold 2dPoints for convex hull

            foreach (var pt in ptsWorld)
            {
                ptsProjectedOnFrame2d.Add(new Point2d(pt));                
            }

            // make convex hull to use as curve to sort along, to deal with a lot of edge cases
            var sortCurve = PolylineCurve.CreateConvexHull2d(ptsProjectedOnFrame2d.ToArray(), out int[] hullIndices);
            
            var tParams = new List<double>();

            // get relative parameters from points rotated to same plane as convex hull
            foreach (var pt in ptsWorld)
            {
                sortCurve.ClosestPoint(pt, out double t);
                tParams.Add(t);
            }

            var dataList = new List<(Point3d, double)>();

            // make list where pts on frame can be sorted according to parameter along sorting curve
            for (int j = 0; j < ptsPlane.Count; ++j)
                dataList.Add((ptsPlane[j], tParams[j]));


            var dataListSorted = dataList.OrderBy(item => item.Item2).ToList(); // sort using pts as values and params as keys
            var ptsProjectSorted = new List<Point3d>();
            foreach (var item in dataListSorted)                            
                ptsProjectSorted.Add(item.Item1); // add sorted points to list
            
            ptsProjectSorted.Add(dataListSorted[0].Item1); // add first point to the end of the list to make a closed curve            

            var crossSectionCurve = new Polyline(ptsProjectSorted); // fit a polyline through the sorted points to get perimeter curve
            //var crossSectionCurve = sortCurve.ToPolyline(); // In case I want the CH instead

            return crossSectionCurve;
        }

        public static void QuickHull(List<Point3d> pts, Point3d p1, Point3d p2, int side,
            double maxLength, List<Point3d> hullPts)
        {
            int ind = -1;
            double maxDist = 0;

            var n = pts.Count;            

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
                hullPts.Add(p1);
                hullPts.Add(p2);
                return;
            }

            QuickHull(pts, pts[ind], p1, -SideOfLine(pts[ind], p1, p2), maxLength, hullPts);
            QuickHull(pts, pts[ind], p2, -SideOfLine(pts[ind], p2, p1), maxLength, hullPts);
        }

        public static void PtBetween(List<Point3d> pts, Point3d p1, Point3d p2, double maxLength,
            List<Point3d> ptsContainer)
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

                    if (t > tol && t < 1 - tol) // only use the orthogonal points
                    {
                        ptsOnLongLine.Add(longLine.PointAt(t));
                        ptsReduced.Add(pt);
                        orthoLines.Add(new Line(longLine.PointAt(t), pt)); // make lines from ptOnLongLine (pointAt(0)) and pt (PointAt(1))
                    }

                }
                var orthoLinesSorted = orthoLines.OrderBy(l => l.Length).ToList(); // sort lines based on length
                                
                var closestPoint = orthoLinesSorted[0].PointAt(1.0); // get pt from shortest line
                newPoint = orthoLinesSorted[0].PointAt(0.0); // get ptOnLongLine from shortest line
                ptsContainer.Add(closestPoint);
            }

            else
                return;

            PtBetween(ptsReduced, newPoint, p1, maxLength, ptsContainer); // check if part 1 of new line is too long
            PtBetween(ptsReduced, newPoint, p2, maxLength, ptsContainer); // check if part 2 of new line is too long          

        }

        public static Polyline CrossSectionEndQH(List<Point3d> pts, double maxLength)
        {

            var ptsSortedX = pts.OrderBy(p => p.X).ToList();

            var ptMin = ptsSortedX.First();
            var ptMax = ptsSortedX.Last();
            var line = new Line(ptMin, ptMax);

            var hullPts = new List<Point3d>();

            QuickHull(pts, ptMin, ptMax, 1, maxLength, hullPts);
            QuickHull(pts, ptMin, ptMax, -1, maxLength, hullPts);

            var sortCurve = new Circle(line.PointAt(0.5), line.Length / 2).ToNurbsCurve(); // make sortcurve for sorting the basic CH pts
            
            var hullPtsSorted = Methods.SortPointsAlongCurve(sortCurve, hullPts, true);

            var convexHull = new Polyline(hullPts);

            var segments = convexHull.GetSegments();

            var ptsNewConvexHull = new List<Point3d>();
            ptsNewConvexHull.Add(hullPtsSorted[0]);


            for (int i = 0; i < hullPtsSorted.Count - 1; i++)
            {
                var ptsBetween = new List<Point3d>();
                var p1 = hullPtsSorted[i];
                var p2 = hullPtsSorted[i + 1];

                PtBetween(pts, p1, p2, maxLength, ptsBetween);
                var ptsBetweenSorted = Methods.SortPointsAlongCurve(new Line(p1, p2).ToNurbsCurve(), ptsBetween, false);

                ptsNewConvexHull.AddRange(ptsBetweenSorted);
                ptsNewConvexHull.Add(p2);
            }

            var ptsNewConvexHullNoDuplicates = new List<Point3d>();
            ptsNewConvexHullNoDuplicates.Add(ptsNewConvexHull[0]);

            for (int i = 1; i < ptsNewConvexHull.Count - 1; i++)
            {
                bool duplicate = false;
                for (int j = 1; j < ptsNewConvexHull.Count - 1; j++)
                {
                    if (i != j)
                        if (ptsNewConvexHull[i] == ptsNewConvexHull[j])
                            duplicate = true;
                }

                if (duplicate == false)
                    ptsNewConvexHullNoDuplicates.Add(ptsNewConvexHull[i]);
            }

            ptsNewConvexHullNoDuplicates.Add(ptsNewConvexHull[0]);

            //var hullPtsSorted = Methods.SortPointsAlongCurve(convexHull.ToNurbsCurve(), hullPts, true);
            var crossSection = new Polyline(ptsNewConvexHull);

            return crossSection;
        }

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
    }
}

    
