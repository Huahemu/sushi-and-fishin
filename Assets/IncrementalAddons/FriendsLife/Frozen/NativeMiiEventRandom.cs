using System;

namespace TomodachiLife.Simulation
{
    [Serializable]
    public struct NativeMiiEventRandomState
    {
        public uint x,y,z,w;
        public NativeMiiEventRandomState(uint x,uint y,uint z,uint w){this.x=x;this.y=y;this.z=z;this.w=w;}
    }

    public enum NativeMiiEventOutcome { Unsupported, Fail, Pass }

    public sealed class NativeMiiEventDecision
    {
        public NativeMiiEventOutcome outcome;
        public string reason,sourceFunction;
        public bool IsPass=>outcome==NativeMiiEventOutcome.Pass;
        public static NativeMiiEventDecision Pass(string source)=>new NativeMiiEventDecision{outcome=NativeMiiEventOutcome.Pass,sourceFunction=source};
        public static NativeMiiEventDecision Fail(string reason,string source)=>new NativeMiiEventDecision{outcome=NativeMiiEventOutcome.Fail,reason=reason,sourceFunction=source};
        public static NativeMiiEventDecision Unsupported(string reason,string source)=>new NativeMiiEventDecision{outcome=NativeMiiEventOutcome.Unsupported,reason=reason,sourceFunction=source};
    }

    /// <summary>Unmodified arithmetic/ordering from main 01d03800.
    /// The caller supplies/persists all four words; native startup seeding is a
    /// separate boundary. Zero state is retained rather than silently reseeded.</summary>
    public static class NativeMiiEventRandom
    {
        public const string SourceFunction="main 01d03800..01d03844";
        public static uint NextUInt(ref NativeMiiEventRandomState state)
        {
            unchecked
            {
                uint t=state.x^(state.x<<11);
                uint next=(t^state.w)^((t>>8)^(state.w>>19));
                state.x=state.y;state.y=state.z;state.z=state.w;state.w=next;
                return next;
            }
        }
        public static int NextPercent(ref NativeMiiEventRandomState state)
            =>(int)(((ulong)NextUInt(ref state)*100UL)>>32);
        // Native always advances the RNG, including rates <=0 or >=100, then
        // performs the signed comparison. Do not replace with clamped odds.
        public static bool TestRate(int rate,ref NativeMiiEventRandomState state)
            =>NextPercent(ref state)<rate;
        // main 0005021c obtains GetSystemTick, truncates to W32, and expands
        // it with the standard 0x6c078965 recurrence. Tick acquisition remains
        // a caller boundary, rather than an invented fixed native startup seed.
        public static NativeMiiEventRandomState FromSystemTick(uint lowTick)
        {
            unchecked
            {
                uint x=(lowTick^(lowTick>>30))*0x6c078965U+1;
                uint y=(x^(x>>30))*0x6c078965U+2;
                uint z=(y^(y>>30))*0x6c078965U+3;
                uint w=(z^(z>>30))*0x6c078965U+4;
                return new NativeMiiEventRandomState(x,y,z,w);
            }
        }
        public static float NextUnitFloat(ref NativeMiiEventRandomState state)
            =>BitConverter.Int32BitsToSingle((int)(NextUInt(ref state)>>9|0x3f800000U))-1f;
        // main 01d4b004: one-pass weighted reservoir, not a cumulative draw.
        // Eligibility is supplied after evaluating the source branch conditions.
        public static int SelectWeightedBranch(int[] rates,bool[] eligible,ref NativeMiiEventRandomState state)
        {
            if(rates==null||eligible==null||rates.Length!=eligible.Length)throw new ArgumentException("Native branch rates and eligibility must align.");
            if(rates.Length<=1)return 0;
            float total=0;int selected=-1;
            for(int i=0;i<rates.Length;i++)
            {
                if(!eligible[i]||rates[i]==0)continue;
                float weight=rates[i];total=total+weight;
                if(float.IsInfinity(total)||float.IsNaN(total))continue;
                float sample=total<0?0:total*NextUnitFloat(ref state);
                if(sample<weight)selected=i;
            }
            return selected;
        }
    }
}
