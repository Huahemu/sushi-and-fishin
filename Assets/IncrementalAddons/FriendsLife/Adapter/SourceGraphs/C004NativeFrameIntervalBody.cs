using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [Serializable] public sealed class C004NativeFrameIntervalState
    {
        public int schema=1;
        public int minInterval=1,maxInterval=2,increaseDelay=2,decreaseDelay=6;
        public uint rawBiasF32=0x3d4ccccd;
        public int minFps=30,maxFps=60,fpsBoost=10;
        public int currentInterval,preferredInterval,increaseCounter,decreaseCounter,currentFps=60,presentationFps=60;
        public bool adaptiveFrozen;
        public int[] supportedPresentationFps=Array.Empty<int>();
    }
    // Original adaptive producer of039687f0+40, later copied to0396d1c0+10
    // and+18 by005e1be4. The external platform supplies the ACTUAL measured
    // prior frame work ticks and its frequency; this body never guesses them.
    public static class C004NativeFrameIntervalBody
    {
        public const string Source="Original frame controller constructor00903bc4/config00903c3c/init00903a74/game-mode-reset01d2af84; adaptive interval update0028b2b8..0028b540; source transfer005e1be4+5e1c38";
        [StructLayout(LayoutKind.Explicit)] struct FloatWord{[FieldOffset(0)]public uint bits;[FieldOffset(0)]public float value;}
        [MethodImpl(MethodImplOptions.NoInlining)] static float NativeMul60(float value)=>value*60f;
        [MethodImpl(MethodImplOptions.NoInlining)] static float NativeAdd(float a,float b)=>a+b;
        static int Fcvtzs(float value)=>float.IsNaN(value)?0:value>=2147483648f?int.MaxValue:value<=-2147483648f?int.MinValue:(int)value;
        static int Sdiv(int a,int b)=>b==0?0:a==int.MinValue&&b==-1?int.MinValue:a/b;
        public static C004NativeFrameIntervalState Constructor()=>new C004NativeFrameIntervalState();
        public static void InitializeOriginalDefaultConfiguration(C004NativeFrameIntervalState state)
        {
            Validate(state);state.minInterval=1;state.maxInterval=2;state.rawBiasF32=0x3d4ccccd;state.increaseDelay=2;state.decreaseDelay=6;
            state.minFps=30;state.maxFps=60;state.fpsBoost=10;state.currentInterval=1;state.preferredInterval=1;state.currentFps=60;state.presentationFps=60;
        }
        public static void OriginalGameModeReset(C004NativeFrameIntervalState state)
        {Validate(state);state.minInterval=2;state.maxInterval=3;state.preferredInterval=2;}
        public static int Update(C004NativeFrameIntervalState state,long actualPriorFrameWorkTicks,long actualTickFrequency,bool hasOriginalIntervalListeners=false)
        {
            Validate(state);
            if(hasOriginalIntervalListeners)throw new OriginalBindingException("Original frame-interval change listener owner requires its exact callback port");
            if(!state.adaptiveFrozen&&actualPriorFrameWorkTicks>0)
            {
                if(actualTickFrequency<=0)throw new OriginalBindingException("Actual positive native platform tick frequency required");
                float ratio=(float)actualPriorFrameWorkTicks/(float)actualTickFrequency;
                int raw=Fcvtzs(NativeAdd(NativeMul60(ratio),new FloatWord{bits=state.rawBiasF32}.value));
                int plus=unchecked(raw+1);int desired=plus>=state.maxInterval?state.maxInterval:plus;
                if(plus<state.minInterval)desired=state.minInterval;
                int interval=state.currentInterval;
                if(desired>interval)
                {
                    if(state.increaseCounter<state.increaseDelay)state.increaseCounter=unchecked(state.increaseCounter+1);
                    else
                    {
                        state.decreaseCounter=state.decreaseDelay;state.preferredInterval=Math.Max(unchecked(desired-1),state.minInterval);
                        interval=desired;state.increaseCounter=0;
                    }
                }
                else state.increaseCounter=0;
                if(state.decreaseCounter!=0)
                {
                    if(desired<interval&&state.decreaseCounter>=1)state.decreaseCounter=unchecked(state.decreaseCounter-1);
                }
                else if(state.preferredInterval<interval)
                {
                    int preferred=state.preferredInterval;
                    if(preferred>state.minInterval)
                    {
                        state.preferredInterval=Math.Max(unchecked(preferred-1),state.minInterval);state.decreaseCounter=state.decreaseDelay;
                    }
                    interval=preferred;
                }
                state.currentInterval=interval;state.currentFps=Sdiv(60,interval);
                if(state.currentFps>=state.maxFps)state.presentationFps=state.currentFps;
                else if(state.supportedPresentationFps.Length==0)
                    state.presentationFps=Math.Min(unchecked(state.currentFps+state.fpsBoost),state.maxFps);
                else
                {
                    // Exact original upper-bound selection, including its base
                    // pointer fallback when no strictly greater entry exists.
                    int first=0,last=state.supportedPresentationFps.Length-1;
                    while(first<last)
                    {
                        int mid=(first+last)/2;
                        if(state.supportedPresentationFps[mid]>state.currentFps)last=mid-1;else first=mid+1;
                    }
                    int index=first;if(state.supportedPresentationFps[index]<=state.currentFps)index++;
                    state.presentationFps=state.supportedPresentationFps[index<state.supportedPresentationFps.Length?index:0];
                }
            }
            return state.currentInterval;
        }
        public static void Validate(C004NativeFrameIntervalState state)
        {
            if(state==null||state.schema!=1||state.supportedPresentationFps==null)throw new OriginalBindingException("Malformed native frame interval state");
        }
    }
}
