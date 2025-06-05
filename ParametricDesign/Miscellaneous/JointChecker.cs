using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Grasshopper.Kernel;
using System.Linq;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using Rhino.Geometry;
using Rhino.UI.Controls;

namespace Masterv2
{
    public class JointChecker : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the JointChecker class.
        /// </summary>
        public JointChecker()
          : base("JointChecker", "Nickname",
              "Description",
              "Master", "Check")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Forces", "[N,V,M]", "Forces for the joints [N,V,M]", GH_ParamAccess.list);
            pManager.AddNumberParameter("Height", "H", "Height of the cross section [cm]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Width", "B", "Width of the cross section [cm]", GH_ParamAccess.item);
            pManager.AddTextParameter("Material", "mat", "Material of the beams", GH_ParamAccess.item);
            pManager.AddNumberParameter("DowelDiameter", "dd", "Diameter of the dowels", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("test1", "", "", GH_ParamAccess.list);
            pManager.AddGenericParameter("test2", "", "", GH_ParamAccess.list);
            pManager.AddGenericParameter("test3", "", "", GH_ParamAccess.list);
            pManager.AddGenericParameter("test4","","", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Input
            var jointforces = new List<double>();
            var h = new double();
            var b = new double();
            var material = "";
            var d = new double();

            DA.GetDataList(0, jointforces);
            DA.GetData(1, ref h);
            DA.GetData(2, ref b);
            DA.GetData(3, ref material);
            DA.GetData(4, ref d);

            var Nlist = new List<double>() { jointforces[0], jointforces[1], jointforces[2] };
            var Vlist = new List<double>() { jointforces[3], jointforces[4], jointforces[5] };
            var Mlist = new List<double>() { jointforces[6], jointforces[7], jointforces[8] };

            string name = GetString(material, @"Material:\s+\S+\s+'([^']+)'");
            var matr = MaterialList.GetMaterial(GetString(name, @"(C\d+)"));

            var points = new List<Point3d>();

            //Parameters
            
            //var d = 12; //mm, INPUT
            var tsteel = 0.5 * d; //mm, thin steel plate
            var t1_1 = (b * 10 - tsteel) / 2;
            var fuk = 400; //MPA, INPUT
            var rhok = matr[6]; //[kg/m^3]
            double fc = matr[1]*10; //[N/mm^2
            double fm = matr[2];
            var gammaMconnections = 1.3;
            var kmod = 0.9;
            double g_m = 1.3;

            //One steel plate
            var MyRk = 0.3 * fuk * Math.Pow(d, 2.6);

            var fhk = 0.082 * (1 - 0.01 * d) * rhok;
            var FvRk11 = new List<double>() 
            {
                fhk*t1_1*d,
                fhk*t1_1*d*(Math.Sqrt(2+(4*MyRk)/(fhk*d*Math.Pow(t1_1,2)))-1),
                2.3*Math.Sqrt(MyRk*fhk*d)
            }.Min();
            var FvRk1 = FvRk11 * 2;
            var FvRd1 = FvRk1 * kmod / gammaMconnections; //times two because of 2 shear planes [N]

            //Geometry of dowels
            var a1 = 5 * d; //between dowels in grain direction
            var a2 = 3 * d; //between dowels perpendicular to grain
            var a3 = (new List<double>() { 7 * d, 80 }).Max(); //from dowel to loaded end (in grain direction)
            var a4 = 4 * d; //from dowel to loaded edge (perpendicular to grain)

            var dowellist = new List<double>() { 6, 8, 10, 12 }; //list with all the dowel numbers
            var result = new List<string>();

            for (int a = 0; a < 3; a++)
            {
                var N = Nlist[a]; //kN, for testing
                var V = Vlist[a]; //kN, for testing
                var M = Mlist[a]; //kNm, for testing
                result.Add($"Joint {a + 1}");
                var looppoints = new List<Point3d>();

                foreach (var ndowel in dowellist) //iterating trough all the number of dowels, adding to a list for visualization
                {
                    var Fed = N * 1000 / ndowel;
                    var util = Fed / FvRd1;
                    var Vplate = ((h * 10) * (a1 * (ndowel / 2 - 1) + a3 * 2) - ndowel * Math.PI * Math.Pow(d / 2, 2)) * tsteel; //mm^3
                    var Vdowels = ndowel * (b * 10) * (Math.PI * Math.Pow(d / 2, 2));
                    var volume = Vplate + Vdowels;
                    points.Add(new Point3d(volume / 1000000, util, FvRk1));
                    looppoints.Add(new Point3d(volume / 1000000, util, FvRk1));
                }

                //Two steel plates
                var t1_2 = b * 10 / 3 - tsteel / 2;
                var t2_2 = b * 10 / 3 - tsteel;
                var FvRk2_1 = new List<double>()
                {
                fhk*t1_2*d,
                fhk*t1_2*d*(Math.Sqrt(2+(4*MyRk)/(fhk*d*Math.Pow(t1_2,2)))-1),
                2.3*Math.Sqrt(MyRk*fhk*d)
                }.Min();
                var FvRk2_2 = new List<double>()
                {
                0.5*fhk*t2_2*d,
                2.3*Math.Sqrt(MyRk*fhk*d)
                }.Min();

                var FvRk2 = 2 * FvRk2_1 + 2 * FvRk2_2;
                var FvRd2 = FvRk2 * kmod / gammaMconnections;

                foreach (var ndowel in dowellist) //iterating trough all the number of dowels, adding to a list for visualization
                {
                    var Fed = N * 1000 / ndowel;
                    var util = Fed / FvRd2;
                    var Vplate = (((h * 10) * (a1 * (ndowel / 2 - 1) + a3 * 2) - ndowel * Math.PI * Math.Pow(d / 2, 2)) * tsteel) * 2; //mm^3
                    var Vdowels = ndowel * (b * 10) * (Math.PI * Math.Pow(d / 2, 2));
                    var volume = Vplate + Vdowels;
                    points.Add(new Point3d(volume / 1000000, util, FvRk2));
                    looppoints.Add(new Point3d(volume / 1000000, util, FvRk2));
                }

                //Three steel plates
                var t1_3 = b * 10 / 4 - tsteel / 2;
                var t2_3 = b * 10 / 4 - tsteel;
                var FvRk3_1 = new List<double>()
                {
                fhk*t1_3*d,
                fhk*t1_3*d*(Math.Sqrt(2+(4*MyRk)/(fhk*d*Math.Pow(t1_3,2)))-1),
                2.3*Math.Sqrt(MyRk*fhk*d)
                }.Min();
                var FvRk3_2 = new List<double>()
                {
                0.5*fhk*t2_3*d,
                2.3*Math.Sqrt(MyRk*fhk*d)
                }.Min();

                var FvRk3 = 2 * FvRk3_1 + 4 * FvRk3_2;
                var FvRd3 = FvRk3 * kmod / gammaMconnections;

                foreach (var ndowel in dowellist) //iterating trough all the number of dowels, adding to a list for visualization
                {
                    var Fed = N * 1000 / ndowel;
                    var util = Fed / FvRd3;
                    var Vplate = (((h * 10) * (a1 * (ndowel / 2 - 1) + a3 * 2) - ndowel * Math.PI * Math.Pow(d / 2, 2)) * tsteel) * 3; //mm^3
                    var Vdowels = ndowel * (b * 10) * (Math.PI * Math.Pow(d / 2, 2));
                    var volume = Vplate + Vdowels;
                    points.Add(new Point3d(volume / 1000000, util, FvRk3));
                    looppoints.Add(new Point3d(volume / 1000000, util, FvRk3));
                }

                //Four steel plates
                var t1_4 = b * 10 / 5 - tsteel / 2;
                var t2_4 = b * 10 / 5 - tsteel;
                var FvRk4_1 = new List<double>()
                {
                fhk*t1_4*d,
                fhk*t1_4*d*(Math.Sqrt(2+(4*MyRk)/(fhk*d*Math.Pow(t1_4,2)))-1),
                2.3*Math.Sqrt(MyRk*fhk*d)
                }.Min();
                var FvRk4_2 = new List<double>()
                {
                0.5*fhk*t2_4*d,
                2.3*Math.Sqrt(MyRk*fhk*d)
                }.Min();

                var FvRk4 = 2 * FvRk3_1 + 6 * FvRk3_2;
                var FvRd4 = FvRk4 * kmod / gammaMconnections;

                foreach (var ndowel in dowellist) //iterating trough all the number of dowels, adding to a list for visualization
                {
                    var Fed = N * 1000 / ndowel;
                    var util = Fed / FvRd4;
                    var Vplate = (((h * 10) * (a1 * (ndowel / 2 - 1) + a3 * 2) - ndowel * Math.PI * Math.Pow(d / 2, 2)) * tsteel) * 4; //mm^3
                    var Vdowels = ndowel * (b * 10) * (Math.PI * Math.Pow(d / 2, 2));
                    var volume = Vplate + Vdowels;
                    points.Add(new Point3d(volume / 1000000, util, FvRk4));
                    looppoints.Add(new Point3d(volume / 1000000, util, FvRk4));
                }

                //Result is points in the points list where the X value is the Volume of steel, and the Y value is the utilization

                //Sorting the points for the x values
                
                
                //Peforming further design checks on the combinations where the utilization is below or equal to 1
                foreach (var res in looppoints)
                {
                    var ndowels = DowelsSteelplates.Get(looppoints.IndexOf(res))[0];
                    var sp = DowelsSteelplates.Get(looppoints.IndexOf(res))[1];

                    if (res.Y <= 1 && ndowels != 0) //if the utilization is verified
                    {

                        //Splitting || grain
                        var n = ndowels / 2;
                        var nef = new List<double>() { n, Math.Pow(n, 0.9) * Math.Pow(a1 / (13 * d), 1 / 4) }.Min();
                        var FvRk = res.Z; //[N]
                        var FvefRk = nef * FvRk;
                        var FvefRd = FvefRk * kmod / gammaMconnections;
                        var Fdrow = N * 1000 / 2; //2 is the number of rows in all configs
                        var utilization = Fdrow / FvefRd;

                        if (utilization <= 1)
                        {
                            //Net section check
                            var bnet = b * 10 - sp * tsteel; //width of timber where plates are [mm]
                            var hnet = h * 10 - 2 * (d + 0.5); //height of timber where holes for dowels are [mm]
                            var sigmac0d = N * 1000 / (hnet * bnet);
                            var fcd = fc * kmod / g_m;

                            var I_rect = (bnet * Math.Pow(h * 10, 3)) / 12;
                            var I_hole = (bnet * Math.Pow(d + 0.5, 3)) / 12 + (bnet * (d + 0.5)) * Math.Pow((h * 10 - 2 * a4) / 2, 2);
                            var Inet = I_rect - 2 * I_hole;
                            var Wnet = Inet / (h * 10 / 2);
                            var sigmamd = M * 1000000 / Wnet;
                            var fmd = fm * kmod / g_m;

                            var utilnetcs = Math.Pow(sigmac0d / fcd, 2) + sigmamd / fmd;

                            if (utilnetcs <= 1)
                            {
                                //Splitting _|_ grain
                                var ev = (0.25 * ndowels - 0.5) * a1 + a3 + h * 10 / 2;
                                var Mcg = -V * 1000 * ev; //moment based on the eccentrcity of V
                                var coordinates = DowelsSteelplates.DowelCoordinates(ndowels, h, b, d);
                                var xi2 = 0.0;
                                var zi2 = 0.0;
                                for (int i = 0; i < coordinates.Count; i++)
                                {
                                    xi2 = xi2 + Math.Pow(coordinates[i].X, 2);
                                    zi2 = zi2 + Math.Pow(coordinates[i].Z, 2);
                                }
                                var ri2 = xi2 + xi2; // [mm^2]

                                //Most loaded fastener with regards to the shear force direction

                                var FM1x = -(Mcg * coordinates[0].Z) / ri2;
                                var FM1z = (Mcg * coordinates[0].X) / ri2;
                                var FM2x = -(Mcg * coordinates[1].Z) / ri2;
                                var FM2z = (Mcg * coordinates[1].X) / ri2;

                                var Vperrow = -V * 1000 * 2 / ndowels;
                                var Fz = Vperrow + FM1z + FM2z;

                                var he = h * 10 - a4;
                                var F90rk = 14 * bnet * Math.Sqrt(he / (1 - he / (h * 10)));
                                var F90rd = F90rk * kmod / gammaMconnections;

                                var utilparallell = Fz / F90rd;

                                if (utilparallell <= 1)
                                {
                                    //Steel plate check, combined stresses
                                    var hsp = h * 10; //cross section height [mm]
                                    var hsp_red = hsp - (d + 0.5) * 2;
                                    var bsp = tsteel; //cross section width [mm]
                                    var fy = 355; //assume S355 [MPa]
                                    var epsilon = 0.81; //for S355
                                    var gammaM0 = 1.05;

                                    var sigmax = N * 1000 / (hsp * bsp); //do not need to use reduced as there is dowels in the holes

                                    var I = bsp * Math.Pow(hsp, 3) / 12;
                                    var S = bsp * (hsp / 2) * (hsp / 4);
                                    var tau = (V * 1000 * S) / (I * tsteel);

                                    var plateutil = Math.Pow(sigmax / (fy / gammaM0), 2) + 3 * Math.Pow(tau / (fy / gammaM0), 2);

                                    if (plateutil <= 1)
                                    {
                                        //Steel plate check, buckling
                                        double baab = 9.0 * tsteel * Math.Sqrt(235.0 / fy);
                                        result.Add($"SP: {sp}, D: {ndowels}, GOOD");
                                    }
                                    else
                                        result.Add($"SP: {sp}, D: {ndowels}, Combined stress utilization for the plate is not good");
                                }
                                else
                                    result.Add($"SP: {sp}, D: {ndowels}, Utilization for splittin _|_ is not good");

                            }
                            else
                                result.Add($"SP: {sp}, D: {ndowels}, Utilization for net cross section is not good");

                        }
                        else
                            result.Add($"SP: {sp}, D: {ndowels}, Utilization for splitting || grain is not good");
                    }
                    else
                        result.Add($"SP: {sp}, D: {ndowels}, Utilization for FvRk is not good");
                }
                
            }


            int chunkSize = points.Count / 3; // 48 / 3 = 16

            List<Point3d> points1 = points.GetRange(0, chunkSize);
            List<Point3d> points2 = points.GetRange(chunkSize, chunkSize);
            List<Point3d> points3 = points.GetRange(2 * chunkSize, chunkSize);

            var sortedpoints = points1.OrderBy(pt => pt.X).ToList();
            var sortedpoints2 = points2.OrderBy(pt => pt.X).ToList();

            var dowelcords = DowelsSteelplates.DowelCoordinates(8, 15, 10, 12);
            //Output
            DA.SetDataList(0, result);
            DA.SetDataList(1, dowelcords);
            DA.SetDataList(2, sortedpoints);
            DA.SetData(3, Nlist[0]);
        }

        string GetString(string text, string pattern)
        {
            var s = Regex.Match(text, pattern);
            return s.Groups[1].Value;
        }

        /*
        List<Point3d> DowelCoordinates(double n, double h, double b, double d)
        {
            var coords = new List<Point3d>();
            var a1 = 5 * d; //between dowels in grain direction
            var a2 = 3 * d; //between dowels perpendicular to grain
            var a3 = (new List<double>() { 7 * d, 80 }).Max(); //from dowel to loaded end (in grain direction)
            var a4 = 4 * d; //from dowel to loaded edge (perpendicular to grain)
            var zi = (h * 10 - 2 * a4) / 2;
            if (n == 6)
            {
                coords.Add(new Point3d(a1, 0, -zi)); //1
                coords.Add(new Point3d(a1, 0, zi)); //2
                coords.Add(new Point3d(0, 0, -zi)); //3
                coords.Add(new Point3d(0, 0, zi)); //4
                coords.Add(new Point3d(-a1, 0, -zi)); //5
                coords.Add(new Point3d(-a1, 0, zi)); //6
            }
            if (n == 8)
            {
                coords.Add(new Point3d(1.5*a1, 0, -zi)); //1
                coords.Add(new Point3d(1.5*a1, 0, zi)); //2
                coords.Add(new Point3d(0.5*a1, 0, -zi)); //3
                coords.Add(new Point3d(0.5*a1, 0, zi)); //4
                coords.Add(new Point3d(-0.5*a1, 0, -zi)); //5
                coords.Add(new Point3d(-0.5*a1, 0, zi)); //6
                coords.Add(new Point3d(-1.5*a1, 0, -zi)); //7
                coords.Add(new Point3d(-1.5*a1, 0, zi)); //8
            }
            if (n == 10)
            {
                coords.Add(new Point3d(2*a1, 0, -zi)); //1
                coords.Add(new Point3d(2*a1, 0, zi)); //2
                coords.Add(new Point3d(a1, 0, -zi)); //3
                coords.Add(new Point3d(a1, 0, zi)); //4
                coords.Add(new Point3d(0, 0, -zi)); //5
                coords.Add(new Point3d(0, 0, zi)); //6
                coords.Add(new Point3d(-a1, 0, -zi)); //7
                coords.Add(new Point3d(-a1, 0, zi)); //8
                coords.Add(new Point3d(-2*a1, 0, -zi)); //9
                coords.Add(new Point3d(-2*a1, 0, zi)); //10
            }
            if (n == 12)
            {
                coords.Add(new Point3d(2.5 * a1, 0, -zi)); //1
                coords.Add(new Point3d(2.5 * a1, 0, zi)); //2
                coords.Add(new Point3d(1.5 * a1, 0, -zi)); //3
                coords.Add(new Point3d(1.5 * a1, 0, zi)); //4
                coords.Add(new Point3d(0.5 * a1, 0, -zi)); //5
                coords.Add(new Point3d(0.5 * a1, 0, zi)); //6
                coords.Add(new Point3d(-0.5 * a1, 0, -zi)); //7
                coords.Add(new Point3d(-0.5 * a1, 0, zi)); //8
                coords.Add(new Point3d(-1.5 * a1, 0, -zi)); //9
                coords.Add(new Point3d(-1.5 * a1, 0, zi)); //10
                coords.Add(new Point3d(-2.5 * a1, 0, -zi)); //11
                coords.Add(new Point3d(-2.5 * a1, 0, zi)); //12
            }

            return coords;
        }
        */
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("8CAA0AA8-CF07-4440-B243-5E0A7093A8C1"); }
        }
    }
}