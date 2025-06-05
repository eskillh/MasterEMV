using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ed.Eto;
using MasterThesis.Classes;

namespace MasterThesis.Solvers
{
    internal class GlobalSolver2
    {
        public List<TimberElement> SupplyElements;
        public List<TimberElement> DemandElements;
        public Dictionary<string, List<TimberElement>> SupplyElementsGrouped;
        public Dictionary<string, List<TimberElement>> DemandElementsGrouped;
        public List<Route> Routes;
        public List<SimpleMatching> Matchings;
        public List<string> Comments;

        public List<TimberElement> DemandElementsMatched;
        public List<TimberElement> SupplyElementsUsed;

        public double gwpCS5;

        public GlobalSolver2(List<TimberElement> supplyElements, List<TimberElement> demandElements, List<Route> routes)
        {
            SupplyElements = supplyElements;
            DemandElements = demandElements;
            Routes = routes;

            SupplyElementsGrouped = TimberElement.GroupByLocation(SupplyElements);
            DemandElementsGrouped = TimberElement.GroupByLocation(DemandElements);

            Comments = new List<string>();
            DemandElementsMatched = new List<TimberElement>();
            SupplyElementsUsed = new List<TimberElement>();
        }

        public void SolveFullSolution(IAlgorithm algorithm)
        {
            List<SimpleMatching> finalMatchings = new List<SimpleMatching>();

            // Create a list of all possible matching candidates described by the supply and demand locations
            List<(string, string)> matchingCandidates = new List<(string, string)>();
            foreach (var supplyLoc in SupplyElementsGrouped.Keys)
            {
                foreach (var demandLoc in DemandElementsGrouped.Keys)
                {
                    matchingCandidates.Add((supplyLoc, demandLoc)); // (supply location, demand location)
                }
            }

            Comments.Add($"Starting with {matchingCandidates.Count} possible routes for matching:");
            foreach (var item in matchingCandidates)
            {
                Comments.Add($"{item.Item1} - {item.Item2}");
            }

            while (matchingCandidates.Any()) // While there exists candidates to evaluate
            {
                // For each candidate, solve the matching problem
                // and check if the GWP reduction is positive
                // If it is, add it to the list of considered matchings
                // If it is not, remove it from the list of matching candidates

                List<SimpleMatching> consideredMatchings = new List<SimpleMatching>();
                List<(string, string)> goodCandidates = new List<(string, string)>();
                List<(string, string)> badCandidates = new List<(string, string)>();

                foreach (var candidate in matchingCandidates)
                {
                    var supplyLoc = candidate.Item1;
                    var demandLoc = candidate.Item2;
                    var supplyElements = SupplyElementsGrouped[supplyLoc];
                    var demandElements = DemandElementsGrouped[demandLoc];
                    Route route = Routes.FirstOrDefault(r => r.startPoint == supplyLoc && r.endPoint == demandLoc);

                    // Solve the matching problem for the given supplies, demands and corresponding route
                    SimpleMatching matching = algorithm.Solve(supplyElements, demandElements, route);

                    // Evaluate if the matching is viable
                    if (matching.GWPReduction > 0)
                    {
                        consideredMatchings.Add(matching);
                        goodCandidates.Add(candidate);
                    }
                    else
                    {
                        badCandidates.Add(candidate);
                    }
                }

                if (consideredMatchings.Count != 0)
                {
                    // Get the best matching from the list of considered matchings and add it to the final matchings
                    SimpleMatching bestOfConsidered = consideredMatchings
                        .OrderByDescending(m => m.GWPReduction)
                        .First();

                    Comments.Add($"\n{bestOfConsidered.Route.startPoint} - {bestOfConsidered.Route.endPoint} gives the best of the currently available matching.");

                    /*
                    Here comes the part about filling actual vehicles
                    ------------------------------------------------------
                     */

                    var resultingElements = bestOfConsidered.Assignments.Select(a => a.ResultElement).Where(e => e != null).ToList();

                    double resultingMass = resultingElements.Sum(e => e.getMass());

                    double maxLoadMass = 4000;
                    double vehicleMass = 7000;

                    double numberOfVehicles = Math.Ceiling(resultingMass / maxLoadMass);

                    bool viableSolution = numberOfVehicles * vehicleMass * 1e-4 * bestOfConsidered.Route.distance < bestOfConsidered.GWPReduction;

                    double GWPFromVehiclesOnly = numberOfVehicles * vehicleMass * 1e-4 * bestOfConsidered.Route.distance;

                    Rhino.RhinoApp.WriteLine($"{GWPFromVehiclesOnly}");

                    bestOfConsidered.GWPReduction -= GWPFromVehiclesOnly;
                    bestOfConsidered.GWPReuse += GWPFromVehiclesOnly;

                    Rhino.RhinoApp.WriteLine($"{bestOfConsidered.Route.startPoint} - {bestOfConsidered.Route.endPoint} - {viableSolution}");

                    /*
                     -----------------------------------------------------
                     */

                    if (viableSolution)
                    {
                        Comments.Add($"{bestOfConsidered.Route.startPoint} - {bestOfConsidered.Route.endPoint} is viable when considering transporting vehicles.");
                        Comments.Add($"--> Matching added to final solution.");

                        gwpCS5 += GWPFromVehiclesOnly;

                        finalMatchings.Add(bestOfConsidered);

                        // Cut the supplies that were assigned, and remove demands that were met (W stands for winner)
                        var supplyLocW = bestOfConsidered.Route.startPoint;
                        var demandLocW = bestOfConsidered.Route.endPoint;
                        var supplyElementsW = SupplyElementsGrouped[supplyLocW];
                        var demandElementsW = DemandElementsGrouped[demandLocW];
                        var assignmentsW = bestOfConsidered.Assignments;

                        // Track demands that are assigned and should be removed
                        HashSet<TimberElement> assignedDemands = new HashSet<TimberElement>();

                        // Update the supply lengths based on the assignments
                        foreach (var supply in supplyElementsW)
                        {
                            double remainingLength = supply.length;

                            // Only consider assignments for this supply
                            var assignmentsForSupply = assignmentsW.Where(a => a.SupplyElement == supply).ToList();

                            foreach (var assignment in assignmentsForSupply)
                            {
                                remainingLength -= assignment.DemandElement.length;
                                assignedDemands.Add(assignment.DemandElement);
                            }

                            supply.setLength(remainingLength);
                        }

                        //foreach (var demand in assignedDemands) 
                        //    DemandElementsMatched.Add(demand);

                        // Remove assigned demands
                        DemandElementsGrouped[demandLocW] = demandElementsW
                            .Where(d => !assignedDemands.Contains(d))
                            .ToList();

                        // Update the supply list (supply lengths already updated)
                        SupplyElementsGrouped[supplyLocW] = supplyElementsW;

                        // Remove the best candidate from the list of matching candidates
                        matchingCandidates.Remove((supplyLocW, demandLocW));

                        // Remove the bad candidates
                        foreach (var candidate in badCandidates)
                        {
                            Comments.Add($"{candidate.Item1} - {candidate.Item2} is a terrible solution, removed!");
                            matchingCandidates.Remove(candidate);
                        }
                    }
                    else
                    {
                        Comments.Add($"Transport made {bestOfConsidered.Route.startPoint} - {bestOfConsidered.Route.endPoint} not viable (GWPreuse = {bestOfConsidered.GWPReuse}).");
                        Rhino.RhinoApp.WriteLine($"Transport made {bestOfConsidered.Route.startPoint} - {bestOfConsidered.Route.endPoint} not viable.");
                        matchingCandidates.Remove((bestOfConsidered.Route.startPoint, bestOfConsidered.Route.endPoint));
                    }
                }
                else
                {
                    matchingCandidates.Clear();
                    Rhino.RhinoApp.WriteLine("No viable candidates left, clearing list.");
                    Comments.Add($"No viable candidates left, clearing list.");

                }
            }
            Matchings = finalMatchings;

            foreach (var matching in finalMatchings)
            {
                foreach (var assingment in matching.Assignments)    
                {
                    gwpCS5 += TimberElement.GetGWPTotal(assingment.ResultElement, matching.Route.distance, 2.25);
                    DemandElementsMatched.Add(assingment.DemandElement);
                    SupplyElementsUsed.Add(assingment.SupplyElement);
                }
            }

            List<string> ids = DemandElementsMatched.Select(te => te.id).ToList();
            foreach (var demand in DemandElements)
            {
                if (!ids.Contains(demand.id))
                {
                    gwpCS5 += TimberElement.GetGWPNew(demand);
                }
            }

            Rhino.RhinoApp.WriteLine($"{gwpCS5}");


        }


    }
}