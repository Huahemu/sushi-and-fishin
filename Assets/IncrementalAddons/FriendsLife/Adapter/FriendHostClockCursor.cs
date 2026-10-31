using System;

namespace SushiAndFish.FriendsLife
{
    [Serializable]
    public struct FriendHostClockCursor
    {
        public bool initialized;
        public double consumedMinute;
        // Original host observations can move backward after a normal reload.
        // Only a new forward interval advances the original simulation calendar.
        // Active work time remains independent and is never caught up here.
        public double Observe(double rawMinute,bool firstAfterPause)
        {
            if(double.IsNaN(rawMinute)||double.IsInfinity(rawMinute)||rawMinute<0)
                throw new ArgumentOutOfRangeException(nameof(rawMinute));
            if(!initialized)
            {initialized=true;consumedMinute=rawMinute;return 0;}
            double elapsed=firstAfterPause?0:Math.Max(0,rawMinute-consumedMinute);
            consumedMinute=Math.Max(consumedMinute,rawMinute);
            return elapsed;
        }
    }
}
