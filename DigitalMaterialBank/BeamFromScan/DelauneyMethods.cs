using System;
using System.Collections.Generic;
using System.Linq;

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel.Geometry.Delaunay;
using Rhino.Geometry;

using static MeshFromPointCloud.Triangle;

namespace MeshFromPointCloud
{
    public static class DelauneyMethods
    {

        public static Mesh RemoveShortEdges(Mesh mesh, double maxLength, List<Curve> edges)
        {
            mesh.GetNakedEdgePointStatus();
            var nakedEdges = mesh.GetNakedEdges().ToList();
            var longestEdge = nakedEdges.Max(x => x.Length);
            double oldLongestEdge = 0;            

            while (longestEdge > maxLength && longestEdge != oldLongestEdge)
            {
                var faces = mesh.Faces;
                var vertices = mesh.Vertices;
                var edgeVertices = mesh.GetNakedEdgePointStatus().ToList();
                for (int i = 0; i < faces.Count; i++)               
                {
                    var face = faces[i];

                    if (edgeVertices[face.A] || edgeVertices[face.B] || edgeVertices[face.C])
                    {
                        var p1 = new Point3d(vertices[face.A]);
                        var p2 = new Point3d(vertices[face.B]);
                        var p3 = new Point3d(vertices[face.C]);

                        var edgeAB = new Polyline(new[] { p1, p2 });
                        var edgeAC = new Polyline(new[] { p2, p3 });
                        var edgeBC = new Polyline(new[] { p3, p1 });

                        edges.AddRange(new[] { edgeAB.ToNurbsCurve(), edgeBC.ToNurbsCurve(), edgeAC.ToNurbsCurve() });

                        if ((edgeAB.Length > maxLength || edgeAC.Length > maxLength || edgeBC.Length > maxLength))
                        {
                            mesh.Faces.RemoveAt(i);
                        }
                    }
                }
                
                mesh.Vertices.CullUnused();
                mesh.Compact();
                
                oldLongestEdge = longestEdge;                
                nakedEdges = mesh.GetNakedEdges().ToList();
                longestEdge = nakedEdges.Count > 0 ? nakedEdges.Max(x => x.Length) : 0;
            }    
            return mesh;    
        }

        private static bool IsEdgeInNakedEdges(Polyline edge, List<Polyline> nakedEdges)
        {
            foreach (var nakedEdge in nakedEdges)
            {
                if (((edge.PointAt(0) == nakedEdge.PointAt(0)  && edge.PointAt(1) == nakedEdge.PointAt(1)) 
                    || (edge.PointAt(0) == nakedEdge.PointAt(1) && edge.PointAt(1) == nakedEdge.PointAt(0))))
                {
                    return true;
                }
            }
            return false;
        }

        public static Mesh RemoveShortLinesFromMesh(Mesh mesh, double maxLength, List<Curve> edges)
        {
            var faces = mesh.Faces;
            var vertices = mesh.Vertices;
            var allEdges = new List<Polyline>();
            var nakedEdges = mesh.GetNakedEdges().ToList();
            var nakedEdgesSorted = nakedEdges.OrderBy(edge => edge.Length);
            var includeFaces = new List<bool>();           


            foreach (var face in faces)

            {   
                var p1 = new Point3d(vertices[face.A]);
                var p2 = new Point3d(vertices[face.B]);
                var p3 = new Point3d(vertices[face.C]);

                var edgeAB = new Polyline(new[] { p1, p2 });
                var edgeAC = new Polyline(new[] { p2, p3 });
                var edgeBC = new Polyline(new[] { p3, p1 });
                var includeFace = true;
                /*
                var edgeAB = new Line(vertices[face.A], vertices[face.B]);
                var edgeAC = new Line(vertices[face.A], vertices[face.C]);
                var edgeBC = new Line(vertices[face.B], vertices[face.C]);
                */
                allEdges.AddRange(new[] { edgeAB, edgeAC, edgeBC });

                if (edgeAB.Length > maxLength || edgeAC.Length > maxLength || edgeBC.Length > maxLength
                    && (nakedEdges.Contains(edgeAB) || nakedEdges.Contains(edgeBC) || nakedEdges.Contains(edgeAC)))
                    includeFace = false;

                includeFaces.Add(includeFace);
            }

            foreach (var edge in allEdges)
                if (edge.Length <= maxLength)
                    edges.Add(edge.ToNurbsCurve());


            var reducedMesh = Mesh.CreateFromFilteredFaceList(mesh, includeFaces);
            return reducedMesh;
        }

        public static void DelauneyTriangulation(List<Point3d> pts, double maxLength, List<Line> triangleEdges, List<Line> testing)
        {
            var ptsSortedX = pts.OrderBy(p => p.X);
            var ptsSortedY = pts.OrderBy(p => p.Y);
            var lineX = new Line(ptsSortedX.First(), ptsSortedX.Last());
            var lineY = new Line(ptsSortedY.First(), ptsSortedY.Last());


            double xSum = 0;
            double ySum = 0;
            foreach (var pt in pts)
            {
                xSum += pt.X;
                ySum += pt.Y;
            }

            var ptCenter = new Point3d(xSum / pts.Count, ySum / pts.Count, 0);
            var localPlane = new Plane(ptCenter, new Vector3d(0, 0, 1));
                        
            var triangles = new List<List<Point3d>>();
            
            var superCircle = new Circle(localPlane, 2 * (lineX.Length + lineY.Length));
            

            var p1 = superCircle.PointAt(0);
            var p2 = superCircle.PointAt(0.333 * 2 * Math.PI);
            var p3 = superCircle.PointAt(0.666 * 2 * Math.PI);
            
                        
            var superTriangle = new List<Point3d>() { p1, p2, p3 };
            triangles.Add(superTriangle);

            testing.Add(new Line(p1, p2));
            testing.Add(new Line(p2, p3));
            testing.Add(new Line(p3, p1));


            foreach (var pt in pts)
            {                
                var edges = new List<List<Point3d>>();                

                triangles = triangles.Where(triangle =>
                {
                    Circle.TryFitCircleToPoints(triangle, out Circle circumCircle);
                    if (circumCircle.Center.DistanceTo(pt) <= circumCircle.Radius)
                    {
                        edges.Add(new List<Point3d>() { triangle[0], triangle[1] });
                        edges.Add(new List<Point3d>() { triangle[1], triangle[2] });
                        edges.Add(new List<Point3d>() { triangle[2], triangle[0] });
                        testing.Add(new Line(triangle[0], triangle[1]));
                        testing.Add(new Line(triangle[1], triangle[2]));
                        testing.Add(new Line(triangle[2], triangle[0]));
                        return false; // Remove the triangle
                    }
                    return true; // Keep the triangle

                }).ToList();

                var uniqueEdges = new List<List<Point3d>>();

                for (var i = 0; i < edges.Count; ++i)
                {
                    var isUnique = true;

                    // See if edge is unique
                    for (var j = 0; j < edges.Count; ++j)
                    {
                        if (i != j 
                            && (edges[i][0] == edges[j][0] && edges[i][1] == edges[j][1]
                                || edges[i][0] == edges[j][1] && edges[i][1] == edges[j][0]))
                        {
                            isUnique = false;
                            testing.Add(new Line(edges[i][0], edges[i][1]));                            
                            break;
                        }
                    }

                    // Edge is unique, add to unique edges array
                    if (isUnique)
                        uniqueEdges.Add(edges[i]);
                }

                edges = uniqueEdges;

                foreach (var edge in edges)
                {
                    var triangle = new List<Point3d>() { edge[0], edge[1], pt};
                    testing.Add(new Line(triangle[0], triangle[1]));
                    testing.Add(new Line(triangle[1], triangle[2]));
                    testing.Add(new Line(triangle[2], triangle[0]));
                    triangles.Add(triangle); 
                }                            


            }

            // Remove triangles that share edges with the super triangle
            triangles = triangles.Where(triangle =>
                 !(triangle[0].Equals(superTriangle[0]) || triangle[0].Equals(superTriangle[1]) || triangle[0].Equals(superTriangle[2]) ||
                 triangle[1].Equals(superTriangle[0]) || triangle[1].Equals(superTriangle[1]) || triangle[1].Equals(superTriangle[2]) ||
                 triangle[2].Equals(superTriangle[0]) || triangle[2].Equals(superTriangle[1]) || triangle[2].Equals(superTriangle[2])
                 )
             ).ToList();            

            foreach (var triangle in triangles)
            {
                if (triangle[0].DistanceTo(triangle[1]) <= maxLength)
                    triangleEdges.Add(new Line(triangle[0], triangle[1]));
                if (triangle[1].DistanceTo(triangle[2]) <= maxLength)
                    triangleEdges.Add(new Line(triangle[1], triangle[2]));
                if (triangle[2].DistanceTo(triangle[0]) <= maxLength)
                    triangleEdges.Add(new Line(triangle[2], triangle[0]));


            }


        }
        
    }
}
