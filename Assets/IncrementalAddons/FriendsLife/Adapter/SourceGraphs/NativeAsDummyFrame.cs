using System;
using TomodachiLife.Simulation;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [Serializable]public sealed class NativeAsDummyFrameState
    {
        // Exact scalar payload of one recovered0x4c source frame record. IDs
        // occupy raw words15/16; preserving words avoids JSON float NaN loss.
        public uint[] words=new uint[19];
        public uint nodeFlags=0x4000;
        public bool Complete=>(nodeFlags&1)!=0;
        public float Frame=>NativeAsDummyFrame.Float(words[1]);
        public float Timeline=>NativeAsDummyFrame.Float(words[8]);
        public float EndTimeline=>NativeAsDummyFrame.Float(words[9]);
    }
    public static class NativeAsDummyFrame
    {
        public const string Source="main00380798/00380aac/003813d4 and00a94d18, current Date Dummy6/controllers13383,13385; default non-seek positive rate1 subset";
        internal static uint Bits(float value)=>BitConverter.ToUInt32(BitConverter.GetBytes(value),0);
        internal static float Float(uint value)=>BitConverter.ToSingle(BitConverter.GetBytes(value),0);
        internal static float Quantize(float value)
        {
            // Actual instructions are FMUL then FRINTA (round away on ties),
            // not the decompiler's apparent int truncation. Round the single
            // product before FRINTA, matching the actual instruction boundary.
            float scaled=Float(Bits((float)(value*10000f)));
            float rounded=(float)Math.Round((double)scaled,MidpointRounding.AwayFromZero);
            if(rounded==0&&(Bits(scaled)&0x80000000U)!=0)rounded=Float(0x80000000);
            return (float)(rounded/10000f);
        }
        public static NativeAsDummyFrameState Begin(int controllerNode,int baseLoops,int randomRange,uint slotSyncMarker,ref NativeMiiEventRandomState random,float initialStepFrames=1f)
        {
            if(baseLoops<=0||randomRange<0||controllerNode!=13383&&controllerNode!=13385)throw new InvalidOperationException("Expected the actual positive-loop Date frame-controller source subset");
            uint sample=slotSyncMarker!=0?unchecked(slotSyncMarker-1):NativeMiiEventRandom.NextUInt(ref random);
            int loops=unchecked(baseLoops+(int)((ulong)sample*(uint)(randomRange+1)>>32));float duration=6f,end=Quantize((float)(duration*(float)loops));
            var state=new NativeAsDummyFrameState();var w=state.words;w[0]=0x411;w[2]=Bits(Quantize(-(initialStepFrames>0?initialStepFrames:1f)));w[3]=Bits(1);w[5]=Bits(duration);
            w[6]=w[7]=w[13]=Bits(-1);w[9]=Bits(end);w[10]=w[11]=Bits(duration);w[12]=Bits((float)(1f/end));w[15]=0xffffffff;w[16]=0xffff0000U|(uint)controllerNode;return state;
        }
        // Returns the original raw context+34 frame output. Its meaning in
        // parent AS callbacks is kept separate from any host time remainder.
        public static float Tick(NativeAsDummyFrameState state,float deltaSourceFrames)
        {
            if(state==null||state.words==null||state.words.Length!=19||float.IsNaN(deltaSourceFrames)||float.IsInfinity(deltaSourceFrames)||deltaSourceFrames<0)throw new InvalidOperationException("Invalid source Dummy state/delta");
            var w=state.words;float current=Float(w[1]),rate=Float(w[3]),start=Float(w[4]),duration=Float(w[5]),end=Float(w[9]);
            if(rate!=1||start!=0||duration!=6||end<=0||(w[0]&0x2e8000)!=0)throw new InvalidOperationException("Dummy frame left the recovered current Date default subset");
            uint flags=w[0]&0xffffbcefU;w[2]=Bits(current);
            float unwrapped=Quantize(NativeFloat32.FusedMultiplyAdd(deltaSourceFrames,rate,current)),frame=unwrapped;
            if(duration<=frame)
            {
                if((w[0]&2)==0&&end<=frame)frame=end;
                else
                {
                    flags|=0x200;float cycles=(float)(duration*(float)(int)(frame/duration));if(frame<duration+duration)cycles=duration;frame=Quantize((float)(frame-cycles));
                }
            }
            w[1]=Bits(frame);float timeline=NativeFloat32.FusedMultiplyAdd(deltaSourceFrames,rate,Float(w[8]));
            if(end<=timeline){timeline=end;w[1]=w[10];}
            w[8]=Bits(timeline);w[12]=Bits((float)(rate/(end-start)));w[0]=flags&0xffffdfffU;
            float visible=Float(w[1]),clipLimit=Float(w[6]);if(clipLimit>0&&clipLimit<visible)visible=clipLimit;
            float output=(float)(visible-Float(w[2]));if(output<0)
            {
                float range=(float)(duration-start);if(range<=0)output=0;else do{output=(float)(output+range);}while(output<0);
            }
            output=rate==0?0:(float)(output/rate);
            if((w[0]&2)==0&&end<=timeline)state.nodeFlags|=1;
            return output;
        }
    }
}
