using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using Rhino;
using Rhino.DocObjects.Tables;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Rhino.Render.DataSources;
using static Rhino.Render.TextureGraphInfo;

namespace MeshFromPointCloud
{
    public static class PackingMethods
    {

        
        //method to check if points on rectangle are inside boundary
        public static bool IsInsidePerimeter(Rectangle3d shape, Curve perimeter, bool moveShapeToCenter) 
        {
            var perimAreaProp = AreaMassProperties.Compute(perimeter);
            var centerPoint = perimAreaProp.Centroid;
            perimeter.TryGetPlane(out Plane perimPlane);
            var centerPlane = new Plane(centerPoint, perimPlane.ZAxis);
            
            if (moveShapeToCenter) // this is leftover code from earlier kept just in case, but not really used
            {
                var centerShape = new Rectangle3d(centerPlane, shape.X, shape.Y);
                shape = centerShape;
            }

            var checkPts = new List<Point3d>() { shape.Center }; // points to check including the middle
            foreach (var segment in shape.ToNurbsCurve().GetSubCurves())
            {
                checkPts.Add(segment.PointAtStart); // add corner points
                checkPts.Add(segment.PointAtMid); // add middle point for each side
            }
            
            foreach (var pt in checkPts)
            {
                var relation = perimeter.Contains(pt, perimPlane, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                if (((int)relation) == 2) // if point is outside return false
                    return false;
            }
            return true; // if no points enters if-statement it means no checked points are outside boundary
        }

        // old method for getting neighboring positions
        public static List<Rectangle3d> GetNeighbors(Rectangle3d centerShape, Rectangle3d placeShape,
            double startParam, double endParam, bool tryRotate)
        {
            double param = startParam;
            var neighbors = new List<Rectangle3d>();

            if (tryRotate)
                placeShape = new Rectangle3d(placeShape.Plane, placeShape.Y, placeShape.X);

            var side1part1 = new List<Rectangle3d>(); var side1part2 = new List<Rectangle3d>();
            var side2part1 = new List<Rectangle3d>(); var side2part2 = new List<Rectangle3d>();
            var side3part1 = new List<Rectangle3d>(); var side3part2 = new List<Rectangle3d>();
            var side4part1 = new List<Rectangle3d>(); var side4part2 = new List<Rectangle3d>();

            
            while (param < endParam)
            {
                var pt = centerShape.PointAt(param);
                var ptPlane = new Plane(pt, centerShape.Plane.ZAxis);

                //if (param <= 1.0)
                    //side1part1.Add(new Rectangle3d(ptPlane, -placeShape.Width, -placeShape.Height));

                if (param < 1.0) // started in leftmost point, so dont care about shapes to the left
                    side1part2.Add(new Rectangle3d(ptPlane, placeShape.Width, -placeShape.Height));

                if (param >= 1.0 && param < 2.0 && centerShape.Corner(1).DistanceTo(pt) < placeShape.Height)
                    side2part1.Add(new Rectangle3d(ptPlane, placeShape.Width, -placeShape.Height));

                if (param >= 1.0 && param < 2.0)
                    side2part2.Add(new Rectangle3d(ptPlane, placeShape.Width, placeShape.Height));

                if (param >= 2.0 && param < 3.0 && centerShape.Corner(2).DistanceTo(pt) < placeShape.Width)
                    side3part1.Add(new Rectangle3d(ptPlane, placeShape.Width, placeShape.Height));

                if (param >= 2.0 && param < 3.0)
                    side3part2.Add(new Rectangle3d(ptPlane, -placeShape.Width, placeShape.Height));

                if (param >= 3.0 && centerShape.Corner(3).DistanceTo(pt) < placeShape.Height)
                    side4part1.Add(new Rectangle3d(ptPlane, -placeShape.Width, placeShape.Height));

                if (param >= 3.0)
                    side4part2.Add(new Rectangle3d(ptPlane, -placeShape.Width, -placeShape.Height));

                param += 0.1;
            }
            
            side3part1.Reverse(); side3part2.Reverse(); side4part1.Reverse(); side4part2.Reverse();
            neighbors.AddRange(side1part1); neighbors.AddRange(side1part2); neighbors.AddRange(side2part1); neighbors.AddRange(side2part2);
            neighbors.AddRange(side4part2); neighbors.AddRange(side4part1); neighbors.AddRange(side3part2); neighbors.AddRange(side3part1);
            
            return neighbors;
        }

        // method for checking overlap with already placed rectangles
        public static bool NoOverlap(Rectangle3d shape, List<Rectangle3d> packedShapes)
        {
            var pts = new List<Point3d>() { shape.Center }; // include centerpoint of rectangle

            foreach (var segment in shape.ToNurbsCurve().GetSubCurves())
            {
                pts.Add(segment.PointAtStart); // include corner points
                pts.Add(segment.PointAtMid); // include middle point of sides
            }

            // check if corner points, mid point of sides or center point is inside any other shape
            foreach (var packedShape in packedShapes)
            {
                foreach (var pt in pts)
                {
                    var curve = packedShape.ToNurbsCurve(); // use curve instead of rectangle for check
                    // SO STUPID BUT CURVE.CONTAINS WORKS SOOO MUCH BETTER THAN RECTANGLE3d.CONTAINS
                    var relation = curve.Contains(pt, packedShape.Plane, 0.00001);
                    if (((int)relation) == 1) // check if pt is inside, meaning its overlapping
                        return false;
                }
            }
            return true; // no points inside any of the shapes, theres no overlap
        }

        // method for getting only the neighboring positions on the same grid as the first placed shape
        // to have clean cuts through the whole piece of wood
        public static List<Rectangle3d> GridNeighbors(Rectangle3d centerShape, Rectangle3d placeShape)
        {
            var neighbors = new List<Rectangle3d>();
            var planes = new List<Plane>();

            for (int i = 0; i < 4; i++) // get corner points and make a plane in each corner
            {
                var pt = centerShape.Corner(i);
                planes.Add(new Plane(pt, centerShape.Plane.ZAxis));
            }

            neighbors.Add(new Rectangle3d(planes[0], placeShape.Width, -placeShape.Height)); // below
            neighbors.Add(new Rectangle3d(planes[1], placeShape.Width, placeShape.Height)); // right
            neighbors.Add(new Rectangle3d(planes[2], -placeShape.Width, placeShape.Height)); // above
            neighbors.Add(new Rectangle3d(planes[3], -placeShape.Width, -placeShape.Height)); // left

            return neighbors;
        }

        // method for placing first rectangle
        public static Rectangle3d PlaceFirstRectangle(Point3d startPoint, Rectangle3d shape, Curve perimeter)
        {

            var perimCenter = AreaMassProperties.Compute(perimeter).Centroid;

            /*
            var testX = perimCenter.X - startPoint.X;
            var testY = perimCenter.Y - startPoint.Y;

            var signX = new List<int>();
            var signY = new List<int>();

            if (Math.Abs(testY) < Math.Abs(testX))
            {
                if (testX > 0)
                {
                    signX.AddRange(new[] { 1, 1 }); signY.AddRange(new[] { -1, 1 });
                }
                else
                {
                    signX.AddRange(new[] { -1, -1 }); signY.AddRange(new[] { -1, 1 });
                }
            }

            else
            {
                if (testY > 0)
                {
                    signX.AddRange(new[] { -1, 1 }); signY.AddRange(new[] { 1, 1 });
                }
                else
                {
                    signX.AddRange(new[] { -1, 1 }); signY.AddRange(new[] { -1, -1 });
                }
            }
            */ // attempt at smarter way to check for rectangles, but didnt work as intended

            var signX = new List<int>() { 1, -1, -1, 1 }; 
            var signY = new List<int>() { 1, 1, -1, -1 };
            perimeter.ClosestPoint(startPoint, out double tStartPerim);
            perimeter.ChangeClosedCurveSeam(tStartPerim);
            perimeter.TryGetPlane(out Plane perimPlane);

            double tAttempt = 0;
            var attempt = new Rectangle3d();
            var outside = true;

            while (outside) // while no rectangle is fully inside perimeter
            {
                var tryPoint = perimeter.PointAtNormalizedLength(tAttempt); // move point along perimeter
                
                var tryPlane = new Plane(tryPoint, perimPlane.ZAxis);
                for (int i = 0; i < signX.Count; i++) // create four rectangles sharing tryPoint as their center
                {
                    // using signX and signY to manipulate width and height to get the four rectangles
                    attempt = new Rectangle3d(tryPlane, signX[i] * shape.Width, signY[i] * shape.Height);
                    
                    if (PackingMethods.IsInsidePerimeter(attempt, perimeter, false))
                    {
                        outside = false; // if a rectangle is inside perimeter set outside to false
                        break;           // and break loop
                    }
                }

                tAttempt += 0.001; // using small increment to get rectangle as tight as possible
            }

            return attempt; // returns succesful attempt on success or last attempt on failure
        }

        public static List<Rectangle3d> PlaceFirstRectangleAll(Point3d startPoint, Rectangle3d shape, Curve perimeter)
        {

            var perimCenter = AreaMassProperties.Compute(perimeter).Centroid;
            

            var signX = new List<int>() { 1, -1, -1, 1 };
            var signY = new List<int>() { 1, 1, -1, -1 };
            perimeter.ClosestPoint(startPoint, out double tStartPerim);
            perimeter.ChangeClosedCurveSeam(tStartPerim);
            perimeter.TryGetPlane(out Plane perimPlane);

            double tAttempt = 0;
            var attempt = new Rectangle3d();
            var outside = true;

            var attempts = new List<Rectangle3d>();

            while (outside) // while no rectangle is fully inside perimeter
            {
                var tryPoint = perimeter.PointAtNormalizedLength(tAttempt); // move point along perimeter

                var tryPlane = new Plane(tryPoint, perimPlane.ZAxis);
                for (int i = 0; i < signX.Count; i++) // create four rectangles sharing tryPoint as their center
                {
                    // using signX and signY to manipulate width and height to get the four rectangles
                    attempt = new Rectangle3d(tryPlane, signX[i] * shape.Width, signY[i] * shape.Height);
                    attempts.Add(attempt);
                    if (PackingMethods.IsInsidePerimeter(attempt, perimeter, false))
                    {
                        outside = false; // if a rectangle is inside perimeter set outside to false
                        break;           // and break loop
                    }
                }

                tAttempt += 0.01; // using small increment to get rectangle as tight as possible
            }

            return attempts; // returns succesful attempt on success or last attempt on failure
        }

        // method used in optimizing searching for neighbor by checking the "front point" not on the placed shape
        // perimeter to determine if an area needs to be searched
        public static bool ValidPoint(Point3d checkPoint, List<Rectangle3d> packedShapes, Curve perimeter)
        {
            perimeter.TryGetPlane(out Plane perimPlane);
            var relation = perimeter.Contains(checkPoint, perimPlane, 0.0001); // check relation to perimeter
            if (((int)relation) == 2) // if outside, no need to check neighbors containing this point
                return false;         // so can skip a width/height of distance ahead

            foreach (var shape in packedShapes)
            {
                var shapeCurve = shape.ToNurbsCurve();
                relation = shapeCurve.Contains(checkPoint, perimPlane, 0.00001); // check relation to packed rectangles
                if (((int)relation) == 1) // if inside, no need to check neighbors containing this point
                    return false;         // so can skip a width/height of distance ahead

            }
            return true;
        }

        // method to get best neighbor without having to compute all possible neighbors first
        public static Rectangle3d GetBestNeighbor(Rectangle3d centerShape, Rectangle3d placeShape,
            Curve perimeter, List<Rectangle3d> packedShapes, bool clockwise)
        {
            var neighbors = new List<Rectangle3d>();
            var neighbor = new Rectangle3d();
            var goodNeighbor = new List<Rectangle3d>();
            var bestNeighbor = new Rectangle3d();

            // find distance to increment in terms of the equivilant parameter increase
            var incrementalParam = 0.01;
            var incrementWidth = centerShape.PointAt(0).DistanceTo(centerShape.PointAt(incrementalParam));
            var incrementHeight = centerShape.PointAt(1).DistanceTo(centerShape.PointAt(1 + incrementalParam));

            if (clockwise) // if we want to check above and to the right clockwise is true
            {
                // dont want to search bottom left, so start from a position directly beneath the center shape
                double startWidth = 0;
                double endWidth = placeShape.Width;

                // when searching along the bottom, the height should be kept constant and negative
                double startHeight = -placeShape.Height;
                double endHeight = 0;

                var plane = new Plane(centerShape.Corner(0), centerShape.Plane.ZAxis);

                // want to increase the start and end until the end point of the placed shape is a placeShape-width
                // outside of the width of the center shape
                
                while (endWidth < centerShape.Width + placeShape.Width)
                {
                    var intervalWidth = new Interval(startWidth, endWidth);
                    var intervalHeight = new Interval(startHeight, endHeight);
                    neighbor = new Rectangle3d(plane, intervalWidth, intervalHeight);
                    neighbors.Add(neighbor);

                    // check for each rectangle if it is a valid placement
                    if (PackingMethods.IsInsidePerimeter(neighbor, perimeter, false)
                        && PackingMethods.NoOverlap(neighbor, packedShapes))
                    {
                        bestNeighbor = neighbor;
                        return bestNeighbor;
                    }

                    // looking at all placements below the centerShape, if the bottom right corner
                    // is not a valid placement it means we can skip all attempts including this point
                    // thus allowing us to skip ahead with a placeShape-width
                    if (!PackingMethods.ValidPoint(neighbor.Corner(1), packedShapes, perimeter))
                    {
                        startWidth += placeShape.Width;
                        endWidth += placeShape.Width;
                    }
                    // can elaborate this to check halfway also, but for now we just keep this one check
                    // and only increase by the increment if the point is valid
                    else
                    {
                        startWidth += incrementWidth;
                        endWidth += incrementWidth;
                    }
                }
                // incase we added a placeShape-width when we were less than a width
                // away from the bottom right corner of the center shape, meaning there
                // is a gap between the neighbors on the right side and the right edge of
                // the center shape, we override the start and end widths to be what we want them to be
                startWidth = centerShape.Width;
                endWidth = centerShape.Width + placeShape.Width;

                // do the same iteration for the neighbors on the right side of the center shape
                // so now we increase the start and end height while doing the same checks
                while (endHeight < centerShape.Height + placeShape.Height)
                {
                    var intervalWidth = new Interval(startWidth, endWidth);
                    var intervalHeight = new Interval(startHeight, endHeight);
                    neighbor = new Rectangle3d(plane, intervalWidth, intervalHeight);
                    neighbors.Add(neighbor);

                    if (PackingMethods.IsInsidePerimeter(neighbor, perimeter, false)
                        && PackingMethods.NoOverlap(neighbor, packedShapes))
                    {
                        bestNeighbor = neighbor;
                        return bestNeighbor;
                    }

                    if (!PackingMethods.ValidPoint(neighbor.Corner(2), packedShapes, perimeter))
                    {
                        startHeight += placeShape.Height;
                        endHeight += placeShape.Height;
                    }
                    else
                    {
                        startHeight += incrementHeight;
                        endHeight += incrementHeight;
                    }
                }
            }

            // if clockwise is false we want to get the top neighboring positions from left to right
            else
            {
                // start top left
                double startWidth = -placeShape.Width;
                double endWidth = 0;

                // we assume there is no valid positions to the left, so height is kept constant the entire time
                double startHeight = 0;
                double endHeight = placeShape.Height;

                var plane = new Plane(centerShape.Corner(3), centerShape.Plane.ZAxis);

                // here we do the same as when searching along the bottom, only above and going from left to right
                while (endWidth <= centerShape.Width + placeShape.Width)
                {
                    var intervalWidth = new Interval(startWidth, endWidth);
                    var intervalHeight = new Interval(startHeight, endHeight);
                    neighbor = new Rectangle3d(plane, intervalWidth, intervalHeight);
                    neighbors.Add(neighbor);

                    if (PackingMethods.IsInsidePerimeter(neighbor, perimeter, false)
                        && PackingMethods.NoOverlap(neighbor, packedShapes))
                    {
                        bestNeighbor = neighbor;
                        return bestNeighbor;
                    }

                    if (!PackingMethods.ValidPoint(neighbor.Corner(2), packedShapes, perimeter))
                    {
                        startWidth += placeShape.Width;
                        endWidth += placeShape.Width;
                    }
                    else
                    {
                        startWidth += incrementWidth;
                        endWidth += incrementWidth;
                    }
                }
            }
            // if we get to this return, it means no neighbors was found and a zero-rectangle is returned
            // which we can check for and end the loop
            return bestNeighbor; 
        }

        public static List<Rectangle3d> GetNeighbors2(Rectangle3d centerShape, Rectangle3d placeShape)
        {
            var neighbors = new List<Rectangle3d>();
            var neighbor = new Rectangle3d();

            // find distance to increment in terms of the equivilant parameter increase
            var incrementalParam = 0.1;
            var incrementWidth = centerShape.PointAt(0).DistanceTo(centerShape.PointAt(incrementalParam));
            var incrementHeight = centerShape.PointAt(1).DistanceTo(centerShape.PointAt(1 + incrementalParam));

            
            // dont want to search bottom left, so start from a position directly beneath the center shape
            double startWidth = 0;
            double endWidth = placeShape.Width;

            // when searching along the bottom, the height should be kept constant and negative
            double startHeight = -placeShape.Height;
            double endHeight = 0;

            var plane = new Plane(centerShape.Corner(0), centerShape.Plane.ZAxis);

            // want to increase the start and end until the end point of the placed shape is a placeShape-width
            // outside of the width of the center shape

            while (endWidth < centerShape.Width + placeShape.Width)
            {
                var intervalWidth = new Interval(startWidth, endWidth);
                var intervalHeight = new Interval(startHeight, endHeight);
                neighbor = new Rectangle3d(plane, intervalWidth, intervalHeight);
                neighbors.Add(neighbor);
                
                startWidth += incrementWidth;
                endWidth += incrementWidth;
            }

            startWidth = centerShape.Width;
            endWidth = centerShape.Width + placeShape.Width;

            // do the same iteration for the neighbors on the right side of the center shape
            // so now we increase the start and end height while doing the same checks
            while (endHeight < centerShape.Height + placeShape.Height)
            {
                var intervalWidth = new Interval(startWidth, endWidth);
                var intervalHeight = new Interval(startHeight, endHeight);
                neighbor = new Rectangle3d(plane, intervalWidth, intervalHeight);
                neighbors.Add(neighbor);

                startHeight += incrementHeight;
                endHeight += incrementHeight;
            }



            // start bottom left
            plane = new Plane(centerShape.Corner(3), centerShape.Plane.ZAxis);

            startWidth = -placeShape.Width;
            endWidth = 0;

            startHeight = -placeShape.Height;
            endHeight = 0;

            // here we do the same as when searching along the bottom, only counter clockwise
            while (endHeight < centerShape.Height)
            {
                var intervalWidth = new Interval(startWidth, endWidth);
                var intervalHeight = new Interval(startHeight, endHeight);
                neighbor = new Rectangle3d(plane, intervalWidth, intervalHeight);
                neighbors.Add(neighbor);

                startHeight += incrementHeight;
                endHeight += incrementHeight;
            }

            startHeight = 0;
            endHeight = centerShape.Height;

            while (endWidth <= centerShape.Width + placeShape.Width + incrementWidth)
            {
                var intervalWidth = new Interval(startWidth, endWidth);
                var intervalHeight = new Interval(startHeight, endHeight);
                neighbor = new Rectangle3d(plane, intervalWidth, intervalHeight);
                neighbors.Add(neighbor);

                startWidth += incrementWidth;
                endWidth += incrementWidth;
            }
            
            return neighbors;
        }

        public static void CornerInsidePerimeterOld(Rectangle3d shape, Curve perimeter, List<int> statusList)
        {
            for (int i = 0; i < 4; i++)
            {
                var corner = shape.Corner(i);
                perimeter.TryGetPlane(out Plane perimPlane);
                var relation = perimeter.Contains(corner, perimPlane, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

                statusList[i] = ((int)relation);
            }
        }
        public static Curve ReducePerimeter(Rectangle3d shape, Curve perimeter)
        {
            var perimPoints = new List<Point3d>();

            foreach (var segment in perimeter.GetSubCurves())
                perimPoints.Add(segment.PointAtStart); // get vertices of perimeter

            var shapeCorners = new List<Point3d>();
            var shapeCurve = shape.ToNurbsCurve();

            double minDist = 10000;
            var shapeSeemStart = new Point3d();

            // find side closest to the perimeter (can prob do this a better way)
            foreach (var segment in shapeCurve.GetSubCurves())
            {
                var start = segment.PointAtStart;
                var end = segment.PointAtEnd;

                perimeter.ClosestPoint(start, out double tStart);
                perimeter.ClosestPoint(end, out double tEnd);

                var startOnPerim = perimeter.PointAt(tStart);
                var endOnPerim = perimeter.PointAt(tEnd);

                var dist = start.DistanceTo(startOnPerim) + end.DistanceTo(endOnPerim);

                if (dist < minDist)
                {
                    minDist = dist;
                    shapeSeemStart = start;
                }
            }

            // orient shape so corners are opposite direction of perimeter, with the first corner to be added first
            shapeCurve.ClosestPoint(shapeSeemStart, out double tShapeSeemStart);
            shapeCurve.ChangeClosedCurveSeam(tShapeSeemStart);
            shapeCurve.Reverse();

            // get corner points in correct order
            shapeCorners.Clear();
            foreach (var segment in shapeCurve.GetSubCurves())
                shapeCorners.Add(segment.PointAtStart);

            shapeCurve.ClosestPoint(shapeCorners[0], out double tCorner1);
            shapeCurve.ClosestPoint(shapeCorners[1], out double tCorner2);
            shapeCurve.ClosestPoint(shapeCorners[2], out double tCorner3);
            shapeCurve.ClosestPoint(shapeCorners[3], out double tCorner4);

            foreach (var pt in perimPoints)
            {
                shapeCurve.ClosestPoint(pt, out double tShape);
                var shapePt = shapeCurve.PointAt(tShape);
                if (shapePt.DistanceTo(pt) < 0.0001)
                {
                    if (tShape > tCorner1 && tShape < tCorner2)
                    {
                        shapeCorners.RemoveAt(0);
                        shapeCorners.Insert(0, shapePt);
                        break;
                    }

                    else if (tShape > tCorner3 && tShape < tCorner4)
                    {
                        shapeCorners.RemoveAt(3);
                        shapeCorners.Add(shapePt);
                        break;
                    }
                }
            }

            // change start point of perimeter to make it easier to manipulate
            perimeter.ClosestPoint(shapeSeemStart, out double tPerimSeemStart);
            perimeter.ChangeClosedCurveSeam(tPerimSeemStart);
            var perimStartPoint = perimeter.PointAt(tPerimSeemStart);

            var lastCorner = shapeCorners.Last(); // get last point that is to be added to perimeter
            perimeter.ClosestPoint(lastCorner, out double tlastCornerOnPerim);
            var lastCornerOnPerim = perimeter.PointAt(tlastCornerOnPerim);


            perimPoints.Clear(); // want the perim points in order of new curve parameters
            foreach (var segment in perimeter.GetSubCurves())
                perimPoints.Add(segment.PointAtStart);

            

            var reducedPerimPts = new List<Point3d>(); // store the reduced perimeter points
            if (perimStartPoint != shapeSeemStart) // avoid duplicates if corner is on perimeter
                reducedPerimPts.Add(perimStartPoint);

            
            reducedPerimPts.AddRange(shapeCorners);
            reducedPerimPts.Add(lastCornerOnPerim);

            // dont want the points between perimStart and lastCornerOnPerim
            foreach (var pt in perimPoints)
            {
                perimeter.ClosestPoint(pt, out double t);
                if (t > tlastCornerOnPerim)
                    reducedPerimPts.Add(pt);
            }

            
            reducedPerimPts.Add(reducedPerimPts.First()); // add start point to end for closed curve
            return new Polyline(reducedPerimPts).ToNurbsCurve();
        }

        public static Curve DivideAndConquer(Curve perimeter, Point3d boundaryCenter)
        {
            var perimAreaProp = AreaMassProperties.Compute(perimeter);
            var centerPoint = perimAreaProp.Centroid;

            var simplePerim = perimeter.Simplify(CurveSimplifyOptions.All, 0.0001, 0.0001) ?? perimeter;
            var simplePerimSegments = simplePerim.GetSubCurves();
            var longestSegment = simplePerimSegments.Aggregate((longest, current)
                => current.GetLength() > longest.GetLength() ? current : longest);

            var simplePerimPts = new List<Point3d>();
            foreach (var seg in simplePerimSegments)
                simplePerimPts.Add(seg.PointAtStart);

            var dividingStart = longestSegment.PointAtMid;
            simplePerim.ClosestPoint(dividingStart, out double dividingStartParam);
            simplePerim.ChangeClosedCurveSeam(dividingStartParam);

            var tryLine = new Line(dividingStart, centerPoint);

            var tryLineIntersect = Intersection.CurveLine(perimeter, tryLine, 
                Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

            var dividingEnd = new Point3d();
            var maxDist = 0.0;
            for (int i = 0; i < tryLineIntersect.Count; i++) // this code is stupid, gotta be a way to improve
            {
                var dist = dividingStart.DistanceTo(tryLineIntersect[i].PointA);
                if (dist > maxDist)
                {
                    dividingEnd = tryLineIntersect[i].PointA;
                    maxDist = dist;
                }
            }

            var dividingLine = new Line(dividingStart, dividingEnd);

            var simplePerimSide1Pts = new List<Point3d>();
            var simplePerimSide2Pts = new List<Point3d>();
            simplePerimSide1Pts.AddRange(new[] { dividingStart, dividingEnd });
            simplePerimSide2Pts.AddRange(new[] { dividingStart, dividingEnd });

            
            simplePerim.ClosestPoint(dividingEnd, out double tEnd);

            foreach (var pt in simplePerimPts)
            {
                simplePerim.ClosestPoint(pt, out double t);
                if (t < tEnd)
                    simplePerimSide1Pts.Add(pt);
                else if (t > tEnd)
                    simplePerimSide2Pts.Add(pt);
            }

            
            simplePerimSide1Pts = Methods.SortPointsAlongCurve(simplePerim, simplePerimSide1Pts, true);
            simplePerimSide2Pts = Methods.SortPointsAlongCurve(simplePerim, simplePerimSide2Pts, true);

            var simplePerimSide1 = new Polyline(simplePerimSide1Pts).ToNurbsCurve();
            var simplePerimSide2 = new Polyline(simplePerimSide2Pts).ToNurbsCurve();

            var side1MA = AreaMassProperties.Compute(simplePerimSide1);
            var side2MA = AreaMassProperties.Compute(simplePerimSide2);
            
            Curve smallestSide = simplePerimSide1;
                        if (boundaryCenter.DistanceTo(side2MA.Centroid) > boundaryCenter.DistanceTo(side1MA.Centroid))
                return simplePerimSide2;
            
            return simplePerimSide1;
            
        }

        public static Rectangle3d PlaceRectangleFurthestAway(Rectangle3d shape, Curve perimeter, Point3d boundaryCenter)
        {
            var perimAreaProp = AreaMassProperties.Compute(perimeter);
            var centerPoint = perimAreaProp.Centroid;
            var centerPlane = new Plane(centerPoint, shape.Plane.ZAxis);
            var centerShape = new Rectangle3d(centerPlane, shape.X, shape.Y);

            perimeter.DivideByCount(20, true, out Point3d[] points);
            var perimPoints = points.ToList();

            var distances = new List<double>();
            
            foreach (var pt in perimPoints)
            {
                distances.Add(centerPoint.DistanceTo(pt));
            }

            var sortedperimPoints = Methods.SortUsingKeys(perimPoints, distances);
            sortedperimPoints.Reverse();

            var furthestPoint = sortedperimPoints[0];

            var insidePerimXY = new List<bool>() { true, true };

            var signX = furthestPoint.X - boundaryCenter.X;
            var signY = furthestPoint.Y - boundaryCenter.Y;

            var moveX = new Vector3d(signX, 0, 0);
            var moveY = new Vector3d(0, signY, 0);
            moveX.Unitize();
            moveY.Unitize();

            while (insidePerimXY[0] == true || insidePerimXY[1] == true)
            {
                
                var tryMoveShapeX = centerShape;
                tryMoveShapeX.Transform(Transform.Translation(moveX));
                if (PackingMethods.IsInsidePerimeter(tryMoveShapeX, perimeter, false))
                {
                    centerShape = tryMoveShapeX;
                    insidePerimXY[0] = true;
                }
                else
                {
                    insidePerimXY[0] = false;
                }
                
                var tryMoveShapeY = centerShape;
                tryMoveShapeY.Transform(Transform.Translation(moveY));
                if (PackingMethods.IsInsidePerimeter(tryMoveShapeY, perimeter, false))
                {
                    centerShape = tryMoveShapeY;
                    insidePerimXY[1] = true;
                }
                else
                {
                    insidePerimXY[1] = false;
                }

            }
            
            return centerShape;
        }

        public static Curve ReducePerimeterOld(Rectangle3d shape, Curve perimeter)
        {
            var perimAreaProp = AreaMassProperties.Compute(perimeter);
            var centerPoint = perimAreaProp.Centroid;

            perimeter.DivideByCount(20, true, out Point3d[] points);
            var perimPoints = points.ToList();

            var segments = perimeter.GetSubCurves();
            foreach (var segment in segments)
                perimPoints.Add(segment.PointAtStart);

            perimPoints = Methods.SortPointsAlongCurve(perimeter, perimPoints, false);

            var lines = new List<Line>();

            foreach (var pt in perimPoints)
                lines.Add(new Line(centerPoint, pt));

            bool[] intersects = new bool[lines.Count]; 

            for (int i = 0; i < intersects.Length; i++)
            {
                var intEvents = Intersection.CurveLine(shape.ToNurbsCurve(), lines[i], 0.0001, 0.0001);
                if (intEvents.Count > 0)
                    foreach (var intersection in intEvents)
                        if (intersection.ParameterB >= 0.0 && intersection.ParameterB <= 1.0)
                            intersects[i] = true;
            }

            
            var reducedPerimPts = new List<Point3d>();
            for (int i = 0; i < intersects.Length; i++)
                if (!intersects[i])
                    reducedPerimPts.Add(perimPoints[i]);


            int removedIndex = 0;
            for (int i = 0; i < intersects.Length; i++)
            {
                if (intersects[i])
                {
                    removedIndex = i; 
                    break;
                }
                    
            }

            var cornersToInclude = new List<Point3d>();
            var cornersDist = new List<double>();

            for (int i = 0; i < 4; i++)
            {
                var corner = shape.Corner(i);
                var dist = corner.DistanceTo(centerPoint);
                cornersToInclude.Add(corner);
                cornersDist.Add(dist);
            }

            cornersToInclude = Methods.SortUsingKeys(cornersToInclude, cornersDist);
            var seemPoint = cornersToInclude[cornersToInclude.Count - 1];
            cornersToInclude.RemoveAt(cornersToInclude.Count - 1);

            var shapeCurve = shape.ToNurbsCurve();
            shapeCurve.ClosestPoint(seemPoint, out double tSeem);
            shapeCurve.ChangeClosedCurveSeam(tSeem);
            shapeCurve.Reverse();

            var cornersToIncludeSorted = Methods.SortPointsAlongCurve(shapeCurve, cornersToInclude, false);

            perimeter.ClosestPoint(cornersToIncludeSorted.First(), out double t1);
            perimeter.ClosestPoint(cornersToIncludeSorted.Last(), out double t2);
            var extraP1 = perimeter.PointAt(t1);
            var extraP2 = perimeter.PointAt(t2);

            cornersToIncludeSorted.Insert(0, extraP1);
            cornersToIncludeSorted.Add(extraP2);

            reducedPerimPts.InsertRange(removedIndex, cornersToIncludeSorted);
            reducedPerimPts.Add(reducedPerimPts[0]);

            return new Polyline(reducedPerimPts).ToNurbsCurve();

        }
        public static Rectangle3d CrossSectionToVolume(Rectangle3d crossSection, Brep brep,
            Point3d lineStart, Point3d lineEnd)
        {

            var line = new Line(lineStart, lineEnd);
            double dist = 0;

            var rectangle = new Rectangle3d();

            while (dist < line.Length)
            {
                var evalPlane = crossSection.Plane;
                var evalPt = line.PointAtLength(dist);
                var evalLine = new Line(lineStart, evalPt);
                evalPlane.Translate(evalLine.Direction);
                var lastRectangle = rectangle;
                rectangle = new Rectangle3d(evalPlane, crossSection.X, crossSection.Y);

                Intersection.BrepPlane(brep, evalPlane, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, out Curve[] intCrvs, out Point3d[] intPts);
                var perim = intCrvs[0];

                
                var perimSubCurves = perim.GetSubCurves();
                var perimLines = new List<Line>();
                foreach (var subCurve in perimSubCurves)
                    perimLines.Add(new Line(subCurve.PointAtStart, subCurve.PointAtEnd));

                var rectEdges = rectangle.ToPolyline().GetSegments().ToList();
                
                
                
                foreach (var rectEdge in rectEdges)
                {
                    var intEvents = Intersection.CurveLine(perim, rectEdge, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                        Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                    if (intEvents.Count > 0)
                    {
                        foreach (var intersect in intEvents)
                        {
                            var a = intersect.ParameterB;

                            if (a >= 0.0 && a <= 1.0)
                            {
                                return lastRectangle;
                            }
                        }
                    }
                }
                
                dist += Math.Min(10, line.Length - dist);
            }
            return rectangle;
        }

        public static Rectangle3d BiggestCrossSection(Curve perim)
        {

            var angle = Math.PI;

            perim.TryGetPlane(out Plane plane);
            var perimAreaMP = AreaMassProperties.Compute(perim);
            var perimSubCurves = perim.GetSubCurves();
            var perimLines = new List<Line>();
            foreach (var subCurve in perimSubCurves)
                perimLines.Add(new Line(subCurve.PointAtStart, subCurve.PointAtEnd));

            var centerPlane = new Plane(perimAreaMP.Centroid, plane.Normal);
            var rectangles = new List<Rectangle3d>();

            var rect = new Rectangle3d();
            double deg = 0;
            while (deg < angle)
            {
                var rotatingPlane = centerPlane;
                rotatingPlane.Rotate(deg, centerPlane.ZAxis);

                //var xNegStart = 100; var xPosStart = 100; var yNegStart = 100; var yPosStart = 100;
                double startValue = 10;
                var rectangleDimensions = new List<double>()
                { startValue, startValue, startValue, startValue };

                var intersections = new List<int>() { 1, 1, 1, 1 };


                // should only check one relevant edge at a time
                while (intersections.Sum() > 0)
                {
                    for (int i = 0; i < rectangleDimensions.Count; i++)
                    {
                        if (intersections[i] != 0)
                        {
                            rectangleDimensions[i] += 1;
                            var yNeg = rectangleDimensions[0];
                            var xPos = rectangleDimensions[2];
                            var yPos = rectangleDimensions[1];
                            var xNeg = rectangleDimensions[3];

                            var xInterval = new Interval(-xNeg, xPos);
                            var yInterval = new Interval(-yNeg, yPos);

                            rect = new Rectangle3d(rotatingPlane, xInterval, yInterval);

                            var rectEdges = rect.ToPolyline().GetSegments().ToList();

                            //var rectEdge = rectEdges[i];
                            var rectEdgeParams = new List<double>();
                            var perimParams = new List<double>();

                            foreach (var rectEdge in rectEdges)
                            {
                                var intEvents = Intersection.CurveLine(perim, rectEdge, 0.0001, 0.0001);
                                if (intEvents.Count > 0)
                                {
                                    foreach (var intersect in intEvents)
                                    {
                                        var a = intersect.ParameterB;

                                        if (a >= 0.0 && a <= 1.0)
                                        {
                                            rectEdgeParams.Add(a);
                                            break;
                                        }
                                    }

                                }

                                if (rectEdgeParams.Count > 0)
                                {
                                    intersections[i] = 0;
                                    rectangleDimensions[i] -= 1.1;
                                    break;
                                }
                            }


                        }
                    }
                }
                rectangles.Add(rect);
                deg += 0.01;
            }

            var biggestRectangle = new Rectangle3d();
            double biggestArea = 0;
            foreach (var rectangle in rectangles)
            {
                var rectangleMP = AreaMassProperties.Compute(rectangle.ToNurbsCurve());
                if (rectangleMP.Area > biggestArea)
                {
                    biggestArea = rectangleMP.Area;
                    biggestRectangle = rectangle;
                }
            }
            return biggestRectangle;
        }
    }
}
