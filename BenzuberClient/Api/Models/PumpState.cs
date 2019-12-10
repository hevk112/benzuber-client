using System;

namespace Benzuber.Api.Models
{
    public class PumpState
    {
        public PumpState(int pumpNo, bool available, int selectedFuelCode = 0)
        {
            if(pumpNo > 64)
                throw new ArgumentOutOfRangeException(nameof(pumpNo), "Number of pump can't be more 64.");
            if(selectedFuelCode >= 0x1000)
                throw new ArgumentOutOfRangeException(nameof(selectedFuelCode), "FuelCode can't be more 4095.");

            PumpNo = (byte) pumpNo;
            State = (ushort) (selectedFuelCode + (!available ? 0x1000 : 0));

        }
        public int PumpNo { get; }
        public ushort State { get; }
    }
}
