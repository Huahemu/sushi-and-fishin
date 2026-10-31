using System;
using System.Collections.Generic;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [Serializable]public sealed class NativeAsPreviousSkeletalFrame
    {public int animationId;public float normalizedFrame;public bool excluded;}
    public static class NativeAsInitialFrame
    {
        public const string Source="main003814cc mode1,shareAnimation=true,tags empty,random=false; current Date Initial13628";
        public static float SameAnimation(float startFrame,float endFrame,float originalDuration,int animationId,IReadOnlyList<NativeAsPreviousSkeletalFrame> previous,ref uint frameFlags,ref uint nodeFlags)
        {
            frameFlags&=0xfffffff7U;
            if(previous!=null)for(int index=previous.Count-1;index>=0;index--)
            {
                var candidate=previous[index];if(candidate==null||candidate.excluded||candidate.animationId!=animationId)continue;
                float frame=(float)(candidate.normalizedFrame*originalDuration);if(endFrame<=frame)frame=endFrame;
                frameFlags|=0x180;nodeFlags|=0x1000;return frame;
            }
            nodeFlags&=0xffffefffU;return startFrame;
        }
    }
}
