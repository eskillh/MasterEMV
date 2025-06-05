using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MasterThesis.Classes;

namespace MasterThesis.Solvers
{
    class GreedyAlgorithmWithNewTransport : IAlgorithm
    {

        public GreedyAlgorithmWithNewTransport()
        {

        }

        public SimpleMatching Solve(List<TimberElement> supplyElements, List<TimberElement> demandElements, Route route)
        {

            // Sort the demands in descending order of volume.
            var demandComparer = Comparer<TimberElement>.Create((a, b) =>
            {
                int result = b.getVolume().CompareTo(a.getVolume());
                return result == 0 ? a.id.CompareTo(b.id) : result;
            });
            var sortedDemands = new SortedSet<TimberElement>(demandComparer);
            foreach (var demandElement in demandElements)
            {
                sortedDemands.Add(demandElement);
            }

            // Sort the tuples in ascending order of elem.getVolume(), then elem.id.
            // The reason for saving the supplies in a tuple is that we need to keep track of the length of the supply elements,
            // as the length of the supply elements will change after each assignment
            // and we still want to keep the original supply element.
            var tupleComparer = Comparer<(TimberElement elem, double length)>.Create((a, b) =>
            {
                int result = a.elem.getVolume().CompareTo(b.elem.getVolume());
                return result == 0 ? a.elem.id.CompareTo(b.elem.id) : result;
            });

            var sortedSupplies = new SortedSet<(TimberElement element, double length)>(tupleComparer);

            foreach (var supplyElement in supplyElements)
            {
                sortedSupplies.Add((supplyElement, supplyElement.length));
            }

            var assingments = new List<MyAssignment>();

            // Keep track of which demands have been matched and which have not to later calculate the GWP
            var demandsUnmatched = new List<TimberElement>();
            var demandsMatched = new List<TimberElement>();

            // Iterate through the sorted demands
            foreach (var demand in sortedDemands)
            {
                bool demandMatched = false;

                // Iterate through the sorted supplies
                foreach (var supplyTuple in sortedSupplies)
                {
                    // Distinguish between the original supply element and the new supply element
                    var originalSupply = supplyTuple.element;
                    var newSupply = new TimberElement(originalSupply.id, originalSupply.width, originalSupply.height, supplyTuple.length, originalSupply.timberClass, originalSupply.location);

                    // Check if the demand can fit inside the supply element and that the supply's timber class is larger than or equal to the demand's,
                    // if so, create a new assignment
                    if (TimberElement.FitInside(demand, newSupply) && TimberElement.ClassVerification(demand, originalSupply))
                    {
                        demandMatched = true;

                        // Create a new assignment and add it to the list of assignments
                        var assignment = new MyAssignment(originalSupply, demand);
                        assingments.Add(assignment);
                        demandsMatched.Add(assignment.ResultElement);

                        // Save the new supply element with the updated length
                        double newLength = supplyTuple.length - demand.length;
                        var (originalSupplyElement, newSupplyLength) = (originalSupply, newLength);

                        // Remove the original supply element from the sorted supplies and add the new supply element
                        sortedSupplies.Remove(supplyTuple);
                        sortedSupplies.Add((originalSupplyElement, newSupplyLength));

                        break; // Move to next demand element after finding a match
                    }
                }
                if (!demandMatched)
                    demandsUnmatched.Add(demand);
            }

            // Calculate the GWP for the reuse-solution and the new-solution
            double GWPNew = demandElements.Sum(de => TimberElement.GetGWPTotal(de, 27, 28.9));
            double resultingMass = demandElements.Sum(e => e.getMass());
            double maxLoadMass = 4000;
            double vehicleMass = 7000;
            double numberOfVehicles = Math.Ceiling(resultingMass / maxLoadMass);
            double GWPFromVehiclesOnly = numberOfVehicles * vehicleMass * 1e-4 * 27;
            GWPNew += GWPFromVehiclesOnly;

            double GWPReuse = demandsMatched.Sum(de => TimberElement.GetGWPTotal(de, route.distance, 2.25)) + demandsUnmatched.Sum(de => TimberElement.GetGWPTotal(de, 27, 28.9));
            double resultingMassReuse = demandsUnmatched.Sum(e => e.getMass());
            double numberOfVehiclesReuse = Math.Ceiling(resultingMassReuse / maxLoadMass);
            double GWPFromVehiclesOnlyReuse = numberOfVehiclesReuse * vehicleMass * 1e-4 * 27;
            GWPReuse += GWPFromVehiclesOnlyReuse;

            Rhino.RhinoApp.WriteLine($"{demandElements.Sum(e => e.getMass())}");


            // Save the results in a new SimpleMatching object
            var simpleMatching = new SimpleMatching(assingments, route, GWPNew, GWPReuse);

            return simpleMatching;
        }
    }
}