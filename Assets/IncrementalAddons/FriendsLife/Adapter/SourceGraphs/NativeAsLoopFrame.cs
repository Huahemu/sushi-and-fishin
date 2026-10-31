using System;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    public static class NativeAsLoopFrame
    {
        public const string Source="main00380798 default loop constructor and00250d78 default nonseek frame update; source AS looping skeletal frame scalar";
        public static NativeAsDummyFrameState Begin(float duration,int initialNode=-1,float initialFrame=0,float initialStep=1)
        {
            if(float.IsNaN(duration)||float.IsInfinity(duration)||duration<=0||initialFrame<0||initialFrame>duration)throw new InvalidOperationException("Invalid actual source looping clip range");
            var state=new NativeAsDummyFrameState();var w=state.words;w[0]=0x413;w[1]=w[8]=NativeAsDummyFrame.Bits(NativeAsDummyFrame.Quantize(initialFrame));w[2]=NativeAsDummyFrame.Bits(NativeAsDummyFrame.Quantize(initialFrame-(initialStep>0?initialStep:1)));
            w[3]=NativeAsDummyFrame.Bits(1);w[5]=w[11]=NativeAsDummyFrame.Bits(duration);w[6]=w[7]=w[9]=w[10]=w[13]=NativeAsDummyFrame.Bits(-1);w[12]=NativeAsDummyFrame.Bits(1f/duration);w[15]=0xffffffff;w[16]=initialNode<0?0xffffffffU:unchecked((uint)(initialNode<<16|0xffff));return state;
        }
        public static void Tick(NativeAsDummyFrameState state,float deltaSourceFrames)
        {
            if(state==null||state.words==null||state.words.Length!=19||deltaSourceFrames<0||float.IsNaN(deltaSourceFrames)||float.IsInfinity(deltaSourceFrames))throw new InvalidOperationException("Invalid actual source looping frame/delta");
            var w=state.words;float duration=NativeAsDummyFrame.Float(w[5]);if(duration<=0||NativeAsDummyFrame.Float(w[9])!=-1||(w[0]&2)==0)throw new InvalidOperationException("Frame left the recovered infinite loop subset");
            float current=NativeAsDummyFrame.Float(w[1]);w[2]=w[1];uint flags=w[0]&0xffffbcefU;
            float frame=NativeAsDummyFrame.Quantize((float)(current+deltaSourceFrames));
            if(duration<=frame)
            {
                flags|=0x200;float cycles=(float)(int)(frame/duration);float wrapped=NativeFloat32.FusedMultiplyAdd(-cycles,duration,frame);
                if(frame<duration+duration)wrapped=(float)(frame-duration);frame=NativeAsDummyFrame.Quantize(wrapped);
            }
            w[1]=w[8]=NativeAsDummyFrame.Bits(frame);w[12]=NativeAsDummyFrame.Bits(1f/duration);w[0]=flags&0xffffdfffU;
        }
    }
}
