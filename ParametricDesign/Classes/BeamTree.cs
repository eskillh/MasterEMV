using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Masterv2
{
    public class BeamTree
    {
        public List<TreeElement> elements { get; set; }

        public BeamTree()
        {
            elements = new List<TreeElement>();
        }
        
        public void AddElement(double l, string m, double h, double b, double A)
        {
            elements.Add(new TreeElement(l, m, h, b, A));
        }
    }
}
