using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace Masterv2
{
    public class DowelsSteelplates
    {
        public static List<double> Get(int i)
        {
            double sp = 0;
            double dowels = 0;

            if (i <= 3)
            {
                sp = 1.0;
                dowels = i * 2 + 6;

            }

            if (i <= 7 && i >3)
            {
                sp = 2.0;
                dowels = (i - 4) * 2 + 6;
            }

            if (i <=11 && i > 7)
            {
                sp = 3.0;
                dowels = (i - 8) * 2 + 6;
            }

            if (i <= 15 && i > 11)
            {
                sp = 4.0;
                dowels = (i - 12) * 2 + 6;
            }


            return new List<double>() { dowels, sp };
        }

        public static List<Point3d> DowelCoordinates(double n, double h, double b, double d)
        {
            var coords = new List<Point3d>();
            var a1 = 5 * d; //between dowels in grain direction
            var a2 = 3 * d; //between dowels perpendicular to grain
            var a3 = (new List<double>() { 7 * d, 80 }).Max(); //from dowel to loaded end (in grain direction)
            var a4 = 4 * d; //from dowel to loaded edge (perpendicular to grain)
            var zi = (h * 10 - 2 * a4) / 2;
            if (n == 6)
            {
                coords.Add(new Point3d(a1, 0, zi)); //1
                coords.Add(new Point3d(a1, 0, -zi)); //2
                coords.Add(new Point3d(0, 0, zi)); //3
                coords.Add(new Point3d(0, 0, -zi)); //4
                coords.Add(new Point3d(-a1, 0, zi)); //5
                coords.Add(new Point3d(-a1, 0, -zi)); //6
            }
            if (n == 8)
            {
                coords.Add(new Point3d(1.5 * a1, 0, zi)); //1
                coords.Add(new Point3d(1.5 * a1, 0, -zi)); //2
                coords.Add(new Point3d(0.5 * a1, 0, zi)); //3
                coords.Add(new Point3d(0.5 * a1, 0, -zi)); //4
                coords.Add(new Point3d(-0.5 * a1, 0, zi)); //5
                coords.Add(new Point3d(-0.5 * a1, 0, -zi)); //6
                coords.Add(new Point3d(-1.5 * a1, 0, zi)); //7
                coords.Add(new Point3d(-1.5 * a1, 0, -zi)); //8
            }
            if (n == 10)
            {
                coords.Add(new Point3d(2 * a1, 0, zi)); //1
                coords.Add(new Point3d(2 * a1, 0, -zi)); //2
                coords.Add(new Point3d(a1, 0, zi)); //3
                coords.Add(new Point3d(a1, 0, -zi)); //4
                coords.Add(new Point3d(0, 0, zi)); //5
                coords.Add(new Point3d(0, 0, -zi)); //6
                coords.Add(new Point3d(-a1, 0, zi)); //7
                coords.Add(new Point3d(-a1, 0, -zi)); //8
                coords.Add(new Point3d(-2 * a1, 0, zi)); //9
                coords.Add(new Point3d(-2 * a1, 0, -zi)); //10
            }
            if (n == 12)
            {
                coords.Add(new Point3d(2.5 * a1, 0, zi)); //1
                coords.Add(new Point3d(2.5 * a1, 0, -zi)); //2
                coords.Add(new Point3d(1.5 * a1, 0, zi)); //3
                coords.Add(new Point3d(1.5 * a1, 0, -zi)); //4
                coords.Add(new Point3d(0.5 * a1, 0, zi)); //5
                coords.Add(new Point3d(0.5 * a1, 0, -zi)); //6
                coords.Add(new Point3d(-0.5 * a1, 0, zi)); //7
                coords.Add(new Point3d(-0.5 * a1, 0, -zi)); //8
                coords.Add(new Point3d(-1.5 * a1, 0, zi)); //9
                coords.Add(new Point3d(-1.5 * a1, 0, -zi)); //10
                coords.Add(new Point3d(-2.5 * a1, 0, zi)); //11
                coords.Add(new Point3d(-2.5 * a1, 0, -zi)); //12
            }

            return coords;
        }
    }
}
