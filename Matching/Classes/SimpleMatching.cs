using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterThesis.Classes
{
    internal class SimpleMatching
    {
        public List<MyAssignment> Assignments;
        public Route Route;
        public double GWPNew;
        public double GWPReuse;
        public double GWPReduction;

        public SimpleMatching(List<MyAssignment> assignments, Route route, double gWPNew, double gWPReuse)
        {
            Assignments = assignments;
            Route = route;
            GWPNew = gWPNew;
            GWPReuse = gWPReuse;
            GWPReduction = gWPNew-gWPReuse;
        }

        public SimpleMatching()
        {

        }

        public static List<string> PrintResults(List<SimpleMatching> matchings)
        {
            List<string> results = new List<string>();
            foreach (var matching in matchings)
            {
                string result = $"Route: {matching.Route.startPoint} -> {matching.Route.endPoint}, GWP reduction: {Math.Round(matching.GWPReduction, 3)} kgCO2eq";
                result += $"\n(GWP new: {Math.Round(matching.GWPNew, 3)} kgCO2eq, GWP reuse: {Math.Round(matching.GWPReuse, 3)} kgCO2eq)";
                foreach (var assignment in matching.Assignments)
                {
                    result += $"\n{assignment.SupplyElement.id} - {assignment.DemandElement.id}";
                }
                results.Add(result);
            }
            return results;
        }
    }
}
