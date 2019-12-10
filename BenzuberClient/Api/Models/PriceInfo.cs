using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Benzuber.Api.Models
{
    public struct PriceInfo
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public string FuelName { get; }
        public int FuelCode { get; }
        public decimal Price { get; }
        public long Pumps { get; }

        public PriceInfo(string fuelName, int fuelCode, decimal price, IEnumerable<int> pumps)
        {
            FuelName = fuelName;
            FuelCode = fuelCode;
            Price = price;
            Pumps = pumps.Distinct().Sum(i => 1 << (i - 1));
        }
        
        public override string ToString()
            =>$"{FuelCode}:{Price.ToString(Culture)}:{Pumps.ToString(Culture)}";
        
    }
}