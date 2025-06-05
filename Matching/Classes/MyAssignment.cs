using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterThesis.Classes
{
    class MyAssignment
    {
        public TimberElement SupplyElement;
        public TimberElement DemandElement;
        public TimberElement ResultElement;
        public TimberElement RemainderElement;
        public string VehicleID;

        public MyAssignment(TimberElement supplyElement, TimberElement demandElement)
        {
            SupplyElement = supplyElement;
            DemandElement = demandElement;
            ResultElement = new TimberElement(supplyElement.id, supplyElement.width, supplyElement.height, demandElement.length, supplyElement.timberClass, demandElement.location);
            RemainderElement = new TimberElement(supplyElement.id, supplyElement.width, supplyElement.height, supplyElement.length - demandElement.length, supplyElement.timberClass, demandElement.location);
        }

    }
}
