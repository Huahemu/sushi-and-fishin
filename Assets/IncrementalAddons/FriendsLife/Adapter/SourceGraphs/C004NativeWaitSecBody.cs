using System;
using System.Runtime.InteropServices;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [Serializable] public sealed class C004NativeWaitSecState
    {
        public int schema=1,remainingSteps;
        public uint rawSecBits;
        public bool completed;
        public string executionKey;
    }
    // Exact arithmetic/state portion of original WaitSecAction. This body does
    // not synthesize its external raw frame producer or node-active flags.
    public static class C004NativeWaitSecBody
    {
        public const string Source="Original main WaitSecAction name0189a420/vtable031445a8; Enter0189a15c Sec*f32(60) then ARM FCVTZS W; Update0189a180 flag bit1 and raw0396d1c0+10 signed subtraction/<=0 completion";
        [StructLayout(LayoutKind.Explicit)] struct FloatWord
        {
            [FieldOffset(0)]public uint bits;
            [FieldOffset(0)]public float value;
        }
        public static C004NativeWaitSecState Enter(uint rawSecBits,string executionKey)
        {
            if(string.IsNullOrEmpty(executionKey))throw new OriginalBindingException("WaitSec source activation owner required");
            float seconds=new FloatWord{bits=rawSecBits}.value;
            float scaled=seconds*60f;
            // ARM FCVTZS uses signed saturation and NaN->0. A CLR unchecked
            // float->int overflow is not the native instruction's semantics.
            int steps=float.IsNaN(scaled)?0:scaled>=2147483648f?int.MaxValue:scaled<=-2147483648f?int.MinValue:(int)scaled;
            return new C004NativeWaitSecState{executionKey=executionKey,rawSecBits=rawSecBits,remainingSteps=steps};
        }
        public static bool Step(C004NativeWaitSecState state,string executionKey,bool nativeFlagBit1,int rawIntegerStep)
        {
            Validate(state,executionKey);
            if(state.completed)return true;
            if(nativeFlagBit1)state.remainingSteps=unchecked(state.remainingSteps-rawIntegerStep);
            if(state.remainingSteps<=0)state.completed=true;
            return state.completed;
        }
        public static void Validate(C004NativeWaitSecState state,string executionKey)
        {
            if(state==null||state.schema!=1||state.executionKey!=executionKey||string.IsNullOrEmpty(executionKey)||state.completed&&state.remainingSteps>0)
                throw new OriginalBindingException("Invalid/foreign raw native WaitSec continuation");
        }
    }
}
