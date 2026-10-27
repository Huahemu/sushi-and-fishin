using System;
using UnityEngine;
namespace HowToFish {
    [Serializable] public sealed class OriginalCatchWeight {public FishDefinition fish;public int weight;}
    [Serializable] public sealed class OriginalBait {
        public string title,sourceName;public Vector3 hookPoint;public float waitMin,waitMax,loss;public bool moving;public Mesh mesh;public OriginalCatchWeight[] entries;
        public FishDefinition Pick(float unit){float total=0;foreach(var e in entries)if(e.fish)total+=e.weight;float point=Mathf.Clamp01(unit)*total;foreach(var e in entries)if(e.fish){point-=e.weight;if(point<=0)return e.fish;}return entries[entries.Length-1].fish;}
    }
    public sealed class FishingProfile : ScriptableObject {
        public FishDefinition[] fish;
        public OriginalBait[] baits;
        public float minDepth=2,minLine=.5f,maxLine=30,reelStep=.5f,reelFrequency=5,reelSmooth=.25f;
        [Header("Bounded cast distance and charge")]
        [Min(.1f)] public float castChargeSeconds=1;
        [Min(1)] public float boundedCastNearDistance=14,boundedCastFarDistance=65;
        [Min(.1f)] public float boundedCastNearSeconds=.8f,boundedCastFarSeconds=1.4f;
        [Header("Fast reel after a fish is hooked")]
        [Min(.1f)] public float hookedReelSpeed=32;
        [Min(.02f)] public float hookedReelSmooth=.12f;
        [Min(.02f)] public float hookedMinLine=.12f;
        [Min(.1f)] public float hookedNearReelSpeed=10;
        [Min(.2f)] public float hookedReelSlowdownDistance=3;
        [Min(1)] public float hookedLiftDistance=6;
        [Min(.5f)] public float hookedLandingDistance=2.8f;
        public float MinimumLine(bool caught)=>caught?Mathf.Clamp(hookedMinLine,.02f,Mathf.Max(.02f,minLine)):minLine;
        public float HookedReelSpeedAtDistance(float distance)=>Mathf.Lerp(Mathf.Min(hookedNearReelSpeed,hookedReelSpeed),hookedReelSpeed,Mathf.InverseLerp(MinimumLine(true),Mathf.Max(MinimumLine(true)+.1f,hookedReelSlowdownDistance),distance));
        [Header("Guaranteed water casts and restrained rarity")]
        [Min(.1f)] public float biteWaitMin=2,biteWaitMax=4;
        [Min(2)] public float fullQualityDepth=10;
        public float distanceQualityStart=8,distanceQualityFull=28;
        [Range(0,1)] public float nearRareChance=.20f,deepRareChance=.25f,maxRareChance=.30f;
        [Range(0,1)] public float nearLegendaryChance=.05f,deepLegendaryChance=.06f,maxLegendaryChance=.07f;
        [Range(0,1)] public float nonCommonProbabilityMultiplier=.7f;
        [HideInInspector] public float removeDepth=2,releaseAfter=5; // Retained serialized data; no longer drive escape or detachment.
        public float jointSpring=4000,jointDamper=100,underwaterDrag=10;
        [Header("Shore movement and shot response")]
        [HideInInspector] public float landedEscapeMultiplier=.68f,maxLandEscapeSpeed=3.2f;
        [Range(.01f,1)] public float shotForceScale=.12f;
        [Min(.1f)] public float maxShotVelocityChange=.85f;
        [Header("Landed fish hops and water escape")]
        [Min(9.81f)] public float landGravity=28;
        [Min(.1f)] public float shoreMoveSpeedMultiplier=1.25f;
        [Min(.1f)] public float landHopHeight=2.8f;
        [HideInInspector] public float landHopTowardSpeed=2.4f; // Legacy: per-species shore speeds now drive movement.
        [Min(.05f)] public float landHopRestMin=.12f,landHopRestMax=.22f;
        [Min(.01f)] public float escapeWaterDepth=.15f;
        [Header("Loose catch geometry budget")]
        [Min(10000)] public int catchVertexBudget=40000,catchTriangleBudget=50000;
        public AnimationCurve bendResponse=new AnimationCurve(new Keyframe(0,0,.0203566f,.0203566f),new Keyframe(16.87644f,.224423f,.00491848f,.00491848f),new Keyframe(216.5769f,.7330809f,.00103421f,.00103421f),new Keyframe(750,1,.000106024f,.000106024f));
        public AnimationCurve bendDistribution=new AnimationCurve(new Keyframe(0,0,.0205208f,.0205208f),new Keyframe(1,1,.018439f,.018439f));
        public AudioClip[] castClips,reelClips,releaseClips;
    }
}
