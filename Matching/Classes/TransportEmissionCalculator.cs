using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Rhino.Runtime;
using Newtonsoft.Json.Linq;

namespace MasterThesis.Classes
{
    class TransportEmissionCalculator
    {
        public string PointA { get; set; }
        public string PointB { get; set; }
        public string TransportMode { get; set; }
        public List<TimberElement> TimberElements { get; set; }
        public double DistanceKm { get; set; }
        public double weightInTons = 10; // Default weight
        public double WeightInKgs { get; set; }

        public TransportEmissionCalculator(string pointA, string pointB, string transportMode, List<TimberElement> timberElements)
        {
            PointA = pointA;
            PointB = pointB;
            TransportMode = transportMode;
            TimberElements = timberElements;
            weightInTons = timberElements.Sum(e => e.getMass()) / 1000;
        }

        public TransportEmissionCalculator(double distanceInKms, double weightInKgs)
        {
            DistanceKm = distanceInKms;
            WeightInKgs = weightInKgs;
        }

        public TransportEmissionCalculator(string pointA, string pointB, string transportMode)
        {
            PointA = pointA;
            PointB = pointB;
            TransportMode = transportMode;
        }

        public static double GetInitialTruckGWP(double distance, double weight)
        {
            return distance * weight * 1e-5;
        }

        public async Task<double> getDistance()
        {
            DistanceKm = await GetDistanceFromGoogleMaps();
            return DistanceKm;
        }

        public double getEmission()
        {
            if (DistanceKm == 0)
            {
                throw new InvalidOperationException("Distance must be calculated first by calling getDistance()");
            }
            return CalculateCO2();
        }

        private async Task<double> GetDistanceFromGoogleMaps()
        {
            string apiKey = " "; // If anyone reads this, this is where you insert you API-key
            string url = $"https://maps.googleapis.com/maps/api/distancematrix/json?origins={PointA}&destinations={PointB}&mode={TransportMode}&key={apiKey}";

            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    // Log API response
                    //Rhino.RhinoApp.WriteLine("Google API Response: " + responseBody);

                    JObject json = JObject.Parse(responseBody);

                    return (double)json["rows"][0]["elements"][0]["distance"]["value"] / 1000; // Convert meters to km
                }
            }
            catch (Exception ex)
            {
                Rhino.RhinoApp.WriteLine($"API Error: {ex.Message}");
                return 0;
            }
        }

        private double CalculateCO2()
        {
            double emissionFactor;

            switch (TransportMode)
            {
                case "driving":
                    emissionFactor = 0.2; // Truck emissions per ton-km
                    break;
                case "transit":
                    emissionFactor = 0.05; // Train emissions per ton-km
                    break;
                case "walking":
                case "bicycling":
                    emissionFactor = 0;
                    break;
                default:
                    emissionFactor = 0.2; // Default to truck emissions
                    break;
            }

            return DistanceKm * weightInTons * emissionFactor;
        }

    }
}
