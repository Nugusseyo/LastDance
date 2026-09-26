using _Works.JJH._02_Scripts.Objects;

namespace _Works.JJH._02_Scripts.Items
{
    public class FuelNozzle : GrabItem
    {
        public GasStationController Station { get; private set; }

        public void SetStation(GasStationController station)
        {
            Station = station;
        }
    }
}