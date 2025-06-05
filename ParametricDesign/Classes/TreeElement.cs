using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel.Types;
namespace Masterv2
{
    public class TreeElement
    {
        public double L { get; set; }
        public string Mat { get; set; }
        public double h { get; set; }
        public double b { get; set; }
        public double A { get; set; }
        
        public TreeElement(double l, string mat, double he, double br, double Ar)
        {
            L = l;
            Mat = mat;
            h = he;
            b = br;
            A = Ar;
        }
    }
}
