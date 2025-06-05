using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterThesis.Classes
{
    internal class Route
    {
        public string startPoint;
        public string endPoint;
        public double distance;

        public Route(string startPoint, string endPoint, double distance)
        {
            this.startPoint = startPoint;
            this.endPoint = endPoint;
            this.distance = distance;
        }

        public static List<string> GetRouteTable(List<Route> routes)
        {
            string Normalize(string name) => name.Length > 9 ? name.Substring(0, 9) : name;

            // Find all unique start and end points
            var startPoints = routes.Select(r => r.startPoint).Distinct().OrderBy(x => x).ToList();
            var endPoints = routes.Select(r => r.endPoint).Distinct().OrderBy(x => x).ToList();

            // Create a lookup dictionary
            var routeLookup = routes.ToDictionary(
                r => (r.endPoint, r.startPoint),
                r => r.distance);

            // Build the table
            var table = new List<string>();

            // Header row
            var header = "D / S".PadRight(10) + string.Join("", startPoints.Select(sp => Normalize(sp).PadLeft(10)));
            table.Add(header);

            // Rows
            foreach (var endPoint in endPoints)
            {
                var row = Normalize(endPoint).PadRight(10);
                foreach (var startPoint in startPoints)
                {
                    if (routeLookup.TryGetValue((endPoint, startPoint), out double distance))
                    {
                        row += distance.ToString().PadLeft(10);
                    }
                    else
                    {
                        row += "".PadLeft(10);
                    }
                }
                table.Add(row);
            }

            return table;
        }
    }
}
