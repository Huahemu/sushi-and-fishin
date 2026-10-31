using System;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    public static class NativeAsExpressionNames
    {
        public const string Source="Original029eed64/sentinel and029eed68/75 presets;006928ac native enum codec table-index minus1,75 original ARM64 observations";
        static readonly string[] names={"Normal","Blink","OpenMouth","BlinkOpenMouth","Smile","SmileOpenMouth","Anger","AngerOpenMouth","Sorrow","SorrowOpenMouth","Surprise","SurpriseOpenMouth","Frustrated","FrustratedOM","ShoutCloseMouth","Shout","DisappointCloseMouth","Disappoint","EyesShutTightly","Sing","Jizoface","RaiseEyebrow","Enthusiastic","Blush","BlushOpenMouth","BlushSmile","BlushBlink","BlushAngerCloseEyes","BlushFrustrated","NappingBlush","NappingBlushOpenMouth","BlushSurprise","BlushFeelsGood","BlushSurpriseOpenMouth","NappingBlushSmileMouth","WinkLeft","WinkRight","WinkLeftOpenMouth","WinkRightOpenMouth","LikeWinkLeft","LikeWinkRight","WinkRightPucherMouth","WinkLeftPucherMouth","SmileAnger","AngerCloseEyes","CryCM","Cry","UnspeakableCM","Unspeakable","Lousy","LousyOM","Stupefaction","StupefactionOpenMouth","ConfusionCM","Confusion","ImpressionCM","Impression","Smug","SmugOM","Napping","NappingOpenMouth","Great","PoutingLips","Sour","Faceless","BlinkAngerMouth","Tickles","Freezing","BadSurprise","Enlightenment","Iffy","Unpalatable","FeelsGood","OpenMouthBig","BlushSmileOpenMouth"};
        public static string RowName(int code)=>code>=0&&code<names.Length?names[code]:throw new InvalidOperationException("Original Mii expression code is unavailable: "+code);
    }
}
