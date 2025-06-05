using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Masterv2
{
    public class GH_Tree
    {
        public GH_Structure<GH_String> ToGHTree(List<TreeElement> elements)
        {
            GH_Structure<GH_String> GHtree = new GH_Structure<GH_String> ();

            int i = 0;
            foreach (var elem in elements)
            {
                GH_Path path = new GH_Path (i);

                GHtree.Append(new GH_String($"L: {elem.L}"), path);
                GHtree.Append(new GH_String($"Mat: {elem.Mat}"), path);
                GHtree.Append(new GH_String($"h: {elem.h}"), path);
                GHtree.Append(new GH_String($"b: {elem.b}"), path);
                GHtree.Append(new GH_String($"A: {elem.A}"), path);

                i++;
            }
            return GHtree;
        }
    }
}
