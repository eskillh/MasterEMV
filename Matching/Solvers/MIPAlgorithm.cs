using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.OrTools.LinearSolver;
using MasterThesis.Classes;

namespace MasterThesis.Solvers
{
    internal class MIPAlgorithm : IAlgorithm
    {

        public MIPAlgorithm()
        {

        }

        public List<(int, int)> GetValidAssignments(List<TimberElement> supplyElements, List<TimberElement> demandElements)
        {
            List<(int, int)> validAssignments = new List<(int, int)>();
            int n = supplyElements.Count;
            int m = demandElements.Count;

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    if (TimberElement.FitInside(demandElements[j], supplyElements[i]) && TimberElement.ClassVerification(demandElements[j], supplyElements[i]))
                    {
                        validAssignments.Add((i, j));
                    }
                }
            }
            return validAssignments;
        }

        public SimpleMatching Solve(List<TimberElement> supplyElements, List<TimberElement> demandElements, Route route)
        {
            List<MyAssignment> finalAssignments = new List<MyAssignment>();

            Solver solver = Solver.CreateSolver("CBC_MIXED_INTEGER_PROGRAMMING");

            int n = supplyElements.Count;
            int m = demandElements.Count;

            Dictionary<(int, int), Variable> x = new Dictionary<(int, int), Variable>();

            List<(int, int)> validAssignments = GetValidAssignments(supplyElements, demandElements);

            // Make the desision variables, x_ij = 1 if demand j is assigned to supply i, 0 otherwise
            foreach (var (i, j) in validAssignments)
            {
                x[(i, j)] = solver.MakeIntVar(0, 1, $"x_{i}_{j}");
            }


            // Add constraints to the solver. These constraints ensure that the totalt length of demand
            // elements assigned to a supply element does not exceed the supply element length.
            for (int i = 0; i < n; i++)
            {
                LinearExpr sum = new LinearExpr();
                foreach (var (supplyIdx, demandIdx) in validAssignments)
                {
                    if (supplyIdx == i)
                    {
                        sum += demandElements[demandIdx].length * x[(supplyIdx, demandIdx)];
                    }
                }
                solver.Add(sum <= supplyElements[i].length);
            }

            // Add constraints to the solver. These constraints ensure that each demand element is assigned
            // to at most one supply element.
            for (int j = 0; j < m; j++)
            {
                LinearExpr sum = new LinearExpr();
                foreach (var (supplyIdx, demandIdx) in validAssignments)
                {
                    if (demandIdx == j)
                    {
                        sum += x[(supplyIdx, demandIdx)];
                    }
                }
                solver.Add(sum <= 1);
            }

            LinearExpr objective = new LinearExpr();

            objective += demandElements.Sum(se => TimberElement.GetGWPNew(se));

            foreach (var (i, j) in validAssignments)
            {
                
                MyAssignment assignment = new MyAssignment(supplyElements[i], demandElements[j]);
                TimberElement R = assignment.ResultElement;
                double distance = route.distance;
                double GWP_reduction = TimberElement.GetGWPNew(demandElements[j]) - TimberElement.GetGWPTotal(R, distance, 2.25);

                objective -= x[(i, j)] * GWP_reduction;
                

                //objective += demandElements[j].getVolume() * x[(i, j)];
            }

            solver.Minimize(objective);

            Solver.ResultStatus resultStatus = solver.Solve();

            List<TimberElement> demandElementsMatched = new List<TimberElement>();
            List<TimberElement> demandElementsUnmatched = new List<TimberElement>(demandElements);
            if (resultStatus == Solver.ResultStatus.OPTIMAL)
            {
                // might be something wrong here calculating the GWP with only the valid assignments
                foreach (var (i, j) in validAssignments)
                {
                    if (x[(i, j)].SolutionValue() > 0.5)
                    {
                        MyAssignment assignment = new MyAssignment(supplyElements[i], demandElements[j]);
                        finalAssignments.Add(assignment);
                        demandElementsMatched.Add(assignment.ResultElement);
                        demandElementsUnmatched.Remove(demandElements[j]);
                    }
                }
            }

            double GWPNew = demandElements.Sum(de => TimberElement.GetGWPNew(de));
            double GWPReuse = demandElementsMatched.Sum(de => TimberElement.GetGWPTotal(de, route.distance, 2.25)) + demandElementsUnmatched.Sum(de => TimberElement.GetGWPNew(de));

            /*
             NEED SOMETHING HERE ABOUT THE VEHICLES!
             */

            SimpleMatching matching = new SimpleMatching(finalAssignments, route, GWPNew, GWPReuse);

            return matching;


        }
    }
}
