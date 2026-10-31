using System;

namespace TomodachiLife.Simulation
{
    public readonly struct NativeLifeWalletPlan
    {
        public readonly int balance;
        public readonly uint statisticsTypeRaw, statisticsAmountRawU32;
        public NativeLifeWalletPlan(int value,uint type,uint amount) { balance=value;statisticsTypeRaw=type;statisticsAmountRawU32=amount; }
    }
    public readonly struct NativeLifeSatisfactionPlan
    {
        public readonly int level,meter;
        public readonly bool crossedThreshold;
        public NativeLifeSatisfactionPlan(int valueLevel,int valueMeter,bool crossed)
        {level=valueLevel;meter=valueMeter;crossedThreshold=crossed;}
    }
    public readonly struct NativeLifeEmotionBallPlan
    {
        public readonly int mini,small,medium,big;
        public NativeLifeEmotionBallPlan(int a,int b,int c,int d){mini=a;small=b;medium=c;big=d;}
    }
    public readonly struct NativeLifeMoodGiftPlan
    {
        public readonly long deadlineRaw;
        public readonly int effectiveAmount,recoveryPoints,gauge;
        public readonly bool recovered;
        public NativeLifeMoodGiftPlan(long deadline,int amount,int points,int value,bool complete)
        {deadlineRaw=deadline;effectiveAmount=amount;recoveryPoints=points;gauge=value;recovered=complete;}
    }
    public static class NativeLifeRulesCalculations
    {
        // Original UI binding020e72dc, specifically ADD W1,W19,#1 at020e7330.
        public static int DisplaySatisfyLevel(int storedLevel)=>unchecked(storedLevel+1);
        // Original 00212744/017176f8. Input clocks are resolved native raw
        // timestamps; no conversion from reconstructed island minutes exists.
        public static int NativeMoodGauge(long deadlineRaw,long clockRaw)
        {
            long remaining=unchecked(deadlineRaw-clockRaw);
            if(remaining<1)return 0;
            ulong gauge=((ulong)remaining+2159UL)/2160UL;
            return (int)Math.Min(gauge,100UL);
        }
        public static long SetNativeMoodGauge(int gauge,long clockRaw)
            =>unchecked(clockRaw+(long)Math.Max(0,Math.Min(100,gauge))*2160L);
        public static bool NativeMoodRecovered(long deadlineRaw,long clockRaw)
            =>deadlineRaw!=0&&unchecked(deadlineRaw-clockRaw)<1;
        // Original 01f9d7d8 recovery lane -> 017175a0/01717574. The native
        // full-recovery item predicate01782370 matches raw item1 or7 (food7).
        public static NativeLifeMoodGiftPlan PlanNativeMoodGift(long deadlineRaw,long clockRaw,int amount,int reactionClass,bool fullRecoveryItem)
        {
            int effective=fullRecoveryItem||reactionClass==7||reactionClass==8?200:amount;
            int points=unchecked(effective+(effective>=-1?1:2))>>1;
            ulong deadline=unchecked((ulong)deadlineRaw);
            if(points>0){ulong decrement=(ulong)(uint)points*2160UL;deadline=decrement<=deadline?deadline-decrement:0;}
            long after=unchecked((long)deadline);
            return new NativeLifeMoodGiftPlan(after,effective,points,NativeMoodGauge(after,clockRaw),NativeMoodRecovered(after,clockRaw));
        }
        // Original 0039b3a0 uses inclusive rank maxima; its seventh rank uses
        // MeterMax after the six authored MeterRankMeterMax entries.
        public static int RelationRank(int meter,NativeLifeRelationRules rules)
        {
            if(rules==null||rules.rankMeterMax==null||rules.rankMeterMax.Length!=6)
                throw new InvalidOperationException("Native relation rank parameters are unavailable.");
            for(int i=0;i<6;i++)if(meter<=rules.rankMeterMax[i])return i;
            return 6;
        }
        // Original 01d1b970: IsNoChangeRank narrows the clamp to the current
        // rank's inclusive interval; it does not merely preserve a rank label.
        public static int ApplyRelationReaction(int meter,int delta,bool noChangeRank,NativeLifeRelationRules rules)
        {
            if(rules==null)throw new InvalidOperationException("Native relation parameters are unavailable.");
            int minimum=0,maximum=rules.meterMax;
            if(noChangeRank)
            {
                int rank=RelationRank(meter,rules);
                minimum=rank==0?0:unchecked(rules.rankMeterMax[rank-1]+1);
                maximum=rank==6?rules.meterMax:rules.rankMeterMax[rank];
            }
            int total=unchecked(meter+delta);
            return total<minimum?minimum:Math.Min(total,maximum);
        }
        // Original 01d3b250: the doubled flag is the satisfaction core's
        // crossed-threshold result, as passed by 01ff1620 at 01ff179c..17a8.
        public static int EmotionBallValue(int satisfaction,bool crossedThreshold,int moneyPerMeter)
        {return satisfaction<1?0:unchecked((satisfaction*moneyPerMeter)<<(crossedThreshold?1:0));}
        // Original 01d3a84c, restricted to the positive-value domain accepted
        // by its spawning caller 01d3a37c. Disabled price fields are skipped.
        public static NativeLifeEmotionBallPlan SplitEmotionBalls(int value,NativeLifeEmotionBallRules prices)
        {
            if(prices==null)throw new ArgumentNullException(nameof(prices));
            if(value<1)return new NativeLifeEmotionBallPlan(0,0,0,0);
            int big=0,medium=0,small=0,mini=0;
            if(prices.priceBig>0){big=value/prices.priceBig;value=unchecked(value-big*prices.priceBig);}
            if(prices.priceMedium>0){medium=value/prices.priceMedium;value=unchecked(value-medium*prices.priceMedium);}
            if(prices.priceSmall>0){small=value/prices.priceSmall;value=unchecked(value-small*prices.priceSmall);}
            if(prices.priceMini>0)mini=value/prices.priceMini;
            return new NativeLifeEmotionBallPlan(mini,small,medium,big);
        }
        // Original main 0171749c, reached by 01f9d7d8's normal-mood gift lane.
        // Native storage is zero based: level 998 is the last incrementable
        // result. Crossing 100 still consumes the gauge at the level cap.
        public static NativeLifeSatisfactionPlan PlanSatisfaction(int level,int meter,int amount)
        {
            int total=Math.Max(0,unchecked(meter+Math.Min(amount,100)));
            int carries=total/100;
            int increments=level<=997?(int)Math.Min(carries,998L-level):0;
            return new NativeLifeSatisfactionPlan(unchecked(level+increments),total%100,carries!=0);
        }
        // Original main 01d6d404: ADD W wraps, then signed upper/lower clamp.
        public static NativeLifeWalletPlan PlanWalletDelta(int current,int delta)
        {
            int value=Math.Max(0,Math.Min(99999999,unchecked(current+delta)));
            return new NativeLifeWalletPlan(value,delta>=1?0x14u:0x15u,unchecked((uint)(delta>=1?delta:-delta)));
        }
        // Original main 01d08870 arithmetic AFTER explicit taste/context lookup.
        // The runtime labels its configured reaction-class selection separately.
        public static int CalculateResolvedFood(uint reactionClass,int baseSatisfy,float quantityFactor,int priceRaw,
            bool stomachBig,bool stomachSmall,int priceDiv,float bigFactor,float smallFactor,int maximum)
        {
            int value=Fcvtzs((float)baseSatisfy*quantityFactor);
            if(reactionClass>=4)value=unchecked(value+priceRaw/Math.Max(priceDiv,1));
            if(stomachBig)value=Fcvtzs((float)value*bigFactor);
            else if(stomachSmall)value=Fcvtzs((float)value*smallFactor);
            return value < -100 ? -100 : Math.Min(value,maximum);
        }
        private static int Fcvtzs(float value)
        {
            if(float.IsNaN(value))return 0;
            if(value>=2147483648.0f)return int.MaxValue;
            if(value<=-2147483648.0f)return int.MinValue;
            return (int)value;
        }
    }
}
