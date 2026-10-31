using System;
namespace SushiAndFish.FriendsLife
{
    // Pure cent accounting: no clock, wallet or Unity side effects.
    public static class FriendStaffPay
    {
        public struct PlanResult {public long workedMicros,earnedCents;public bool budgetReached;}
        public static PlanResult Plan(long previousMicros,long deltaMicros,int centsPerMinute,long budgetCents)
        {
            if(previousMicros<0||deltaMicros<0||centsPerMinute<=0||budgetCents<=0)throw new ArgumentOutOfRangeException();
            decimal end=decimal.Ceiling((decimal)budgetCents*60000000m/centsPerMinute);
            decimal next=Math.Min(Math.Min((decimal)previousMicros+deltaMicros,end),long.MaxValue);
            long earned=(long)Math.Min(budgetCents,decimal.Floor((decimal)centsPerMinute*next/60000000m));
            return new PlanResult{workedMicros=(long)next,earnedCents=earned,budgetReached=earned>=budgetCents};
        }
    }
}
