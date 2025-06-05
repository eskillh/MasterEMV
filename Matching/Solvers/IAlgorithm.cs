using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MasterThesis.Classes;

namespace MasterThesis.Solvers
{
    internal interface IAlgorithm
    {
        SimpleMatching Solve(List<TimberElement> supplyElements, List<TimberElement> demandElements, Route route);
    }
}
