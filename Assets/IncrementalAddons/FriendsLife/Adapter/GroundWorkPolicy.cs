using System;

namespace SushiAndFish.FriendsLife
{
    public static class GroundWorkPolicy
    {
        public static bool OriginalGroundReady(bool casinoReady,int integrationFloor,int? nativeFloor,bool transferring,bool locked)
            =>casinoReady&&integrationFloor==0&&nativeFloor==0&&!transferring&&!locked;
        public static bool PhysicalArrival(bool originalMovementArrived,double actualDistance,double destinationError)
            =>originalMovementArrived&&!double.IsNaN(actualDistance)&&!double.IsInfinity(actualDistance)&&actualDistance>=0&&actualDistance<=.9&&
                !double.IsNaN(destinationError)&&!double.IsInfinity(destinationError)&&destinationError>=0&&destinationError<=.15;
        public static bool OriginalStationDuty(string jobPhase,bool stationOnDuty,string stationOwner,string personId)
            =>jobPhase=="OnDuty"&&stationOnDuty&&!string.IsNullOrEmpty(personId)&&stationOwner==personId;
    }
}
