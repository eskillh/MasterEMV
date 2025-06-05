using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Eto;
using Grasshopper.Kernel;
using Rhino.Runtime;

namespace MasterThesis.Classes
{
    internal class TimberElement
    {
        public string id;
        public double width; // [mm]
        public double height; // [mm]
        public double length; // [mm]
        public string timberClass;
        public string location; // city, country

        public TimberElement(string id, double width, double height, double length, string timberClass)
        {
            this.id = id;
            this.width = width;
            this.height = height;
            this.length = length;
            this.timberClass = timberClass;
        }

        public TimberElement(string id, double width, double height, double length, string timberClass, string location)
        {
            this.id = id;
            this.width = width;
            this.height = height;
            this.length = length;
            this.timberClass = timberClass;
            this.location = location;
        }

        public TimberElement(double width, double height, double length)
        {
            this.width = width;
            this.height = height;
            this.length = length;
        }

        public TimberElement(string id, double width, double height, double length)
        {
            this.id = id;
            this.width = width;
            this.height = height;
            this.length = length;
        }

        public TimberElement()
        {

        }

        public TimberElement(double width, double height, double length, string timberClass)
        {
            this.width = width;
            this.height = height;
            this.length = length;
            this.timberClass = timberClass;   
        }


        public void setLength(double l)
        {
            length = l;
        }

        public double GetCrossSectArea()
        {
            if (width > 0 || height > 0)
                return width * height;
            else
                return 0;
        }
        public double getVolume()
        {
            if (width > 0 || height > 0 || length > 0)
                return width * height * length;
            else
                return 0;
        }

        public double getMass() // [kg]
        {
            return getVolume() * 350 * 1e-9;
        }

        public static bool ClassVerification(TimberElement E1, TimberElement E2) // E1 demand, E2 supply
        {
            if (int.TryParse(E1.timberClass.Substring(1), out int cNum1) && int.TryParse(E2.timberClass.Substring(1), out int cNum2))
            {
                return cNum1 <= cNum2;
            }

            return false;
        }

        public static bool FitInside(TimberElement E1, TimberElement E2)
        {
            double w1 = E1.width;
            double w2 = E2.width;
            double h1 = E1.height;
            double h2 = E2.height;
            double l1 = E1.length;
            double l2 = E2.length;

            if (l1 <= l2 && (w1 <= w2 && h1 <= h2 || w1 <= h2 && h1 <= w2))
                return true;
            else
                return false;
        }

        public static List<TimberElement> SortTimberElements(List<TimberElement> elements, string sortBy)
        {
            List<TimberElement> sortedElements = new List<TimberElement>();
            if (sortBy == "width")
            {
                sortedElements = elements.OrderBy(p => p.width).ThenBy(p => p.height).ToList();
            }
            else if (sortBy == "height")
            {
                sortedElements = elements.OrderBy(p => p.height).ThenBy(p => p.width).ToList();
            }
            else if (sortBy == "length")
            {
                sortedElements = elements.OrderBy(p => p.length).ThenBy(p => p.height).ThenBy(p => p.width).ToList();
            }
            else if (sortBy == "area")
            {
                sortedElements = elements.OrderBy(p => p.GetCrossSectArea()).ThenBy(p => p.height).ThenBy(p => p.width).ToList();
            }
            else
            {
                sortedElements = elements.OrderBy(p => p.id).ToList();
            }
            return sortedElements;
        }

        public static double getMaxCrossSectDimension(List<TimberElement> timberElements)
        {
            double maxWidth = timberElements.Max(e => e.width);
            double maxHeight = timberElements.Max(e => e.height);
            if (maxHeight > maxWidth) return maxHeight;
            else return maxWidth;
        }

        public static double getMaxLength(List<TimberElement> timberElements)
        {
            return timberElements.Max(e => e.length);
        }

        public static double GetGWPNew(TimberElement timberElement)
        {
            return timberElement.getVolume() * 1e-9 * 28.9;
        }

        public static double GetGWPreuse(TimberElement timberElement)
        {
            return timberElement.getVolume() * 1e-9 * 2.25;
        }

        public double GetTransportEmission(double distanceInKms)
        {
            return 1e-4 * this.getMass() * distanceInKms;
        }

        public static double GetGWPTotal(TimberElement timberElement, double distanceInKms, double k)
        {
            double E1 = timberElement.getVolume() * 1e-9 * k;
            double E2 = 1e-4 * timberElement.getMass() * distanceInKms;
            return E1 + E2;
        }

        public static Dictionary<string, List<TimberElement>> GroupByLocation(List<TimberElement> timberElements)
        {
            Dictionary<string, List<TimberElement>> resultDict = new Dictionary<string, List<TimberElement>>();
            foreach (TimberElement timberElement in timberElements)
            {
                string key = timberElement.location;
                if (resultDict.ContainsKey(key))
                {
                    resultDict[key].Add(timberElement);
                }
                else
                {
                    resultDict[key] = new List<TimberElement>();
                    resultDict[key].Add(timberElement);
                }
            }
            return resultDict;
        }

        public static async Task<Dictionary<string, double>> GetLocationDistances(List<TimberElement> supplyElements, List<TimberElement> demandElements)
        {
            
            string startPoint = supplyElements[0].location;
            HashSet<string> endPoints = new HashSet<string>(demandElements.Select(d => d.location));

            Dictionary<string, double> locationDistances = new Dictionary<string, double>();
            var tasks = new List<Task>();

            foreach (string endPoint in endPoints)
            {
                if (endPoint == startPoint)
                {
                    locationDistances[endPoint] = 0;
                }
                else
                {
                    TransportEmissionCalculator tec = new TransportEmissionCalculator(startPoint, endPoint, "driving");
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            double distance = await tec.getDistance();
                            locationDistances[endPoint] = distance;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    }));
                }
            }
            await Task.WhenAll(tasks);

            var sortedLocationDistances = locationDistances.OrderBy(kvp => kvp.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return sortedLocationDistances;
        }

        public static async Task<List<Route>> GetRoutes(List<TimberElement> supplyElements, List<TimberElement> demandElements)
        {

            HashSet<string> startPoints = new HashSet<string>(supplyElements.Select(s => s.location));
            HashSet<string> endPoints = new HashSet<string>(demandElements.Select(d => d.location));

            List<Route> routes = new List<Route>();
            var tasks = new List<Task>();

            foreach (string startPoint in startPoints)
            {
                foreach (string endPoint in endPoints)
                {
                    if (endPoint == startPoint)
                    {
                        routes.Add(new Route(startPoint, endPoint, 0));
                    }
                    else
                    {
                        TransportEmissionCalculator tec = new TransportEmissionCalculator(startPoint, endPoint, "driving");
                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                double distance = await tec.getDistance();
                                routes.Add(new Route(startPoint, endPoint, distance));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex);
                            }
                        }));
                    }
                }
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            var sortedRoutes = routes.OrderBy(r => r.distance).ToList();

            return sortedRoutes;
        }

        public static List<TimberElement> CloneElements(List<TimberElement> timberElements)
        {
            List<TimberElement> resultList = new List<TimberElement>();

            foreach (TimberElement e in timberElements)
            {
                resultList.Add(new TimberElement(e.id, e.width, e.height, e.length, e.timberClass, e.location));
            }
            return resultList;
        }
        public static TimberElement CloneElement(TimberElement timberElement)
        {
            return new TimberElement(timberElement.id, timberElement.width, timberElement.height, timberElement.length, timberElement.timberClass, timberElement.location);
        }
    }
}
