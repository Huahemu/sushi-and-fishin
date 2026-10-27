using System;
using UnityEngine;
namespace HowToFish {
    public enum CatchRarity { Common,Uncommon,Rare,Legendary }
    [Serializable] public sealed class ShoreFishMotion {
        [Min(0)] public float nativeLength;
        public bool aggressive,pursuePlayer;
        [Range(0,1)] public float aggression;
        [Min(.1f)] public float escapeSpeed=.94f,attackSpeed=2.2f;
        [Range(0,1)] public float attackChance=.5f;
        [Min(.1f)] public float attackCooldown=3,contactCooldown=1.2f;
        [Min(0)] public int attackDamage=8;
        [Range(.5f,1.5f)] public float hopHeightMultiplier=1,restMultiplier=1;
    }
    public static class ShoreAggressionRules {
        // Authored biological length, rather than enlarged shooting dimensions.
        public static float Score(float length,CatchRarity rarity){
            if(length<1.5f&&rarity<CatchRarity.Rare)return 0;
            float size=Mathf.InverseLerp(1.5f,4,length);
            float rank=rarity==CatchRarity.Legendary?1:rarity==CatchRarity.Rare?.65f:rarity==CatchRarity.Uncommon?.15f:0;
            return .25f+.75f*(1-(1-size)*(1-rank));
        }
    }
    [Serializable] public sealed class OriginalFishMotion {
        public string component;
        public int hp=40,boss,damage;
        public float restMin=.25f,restMax=.5f,motorTime=.1f,rotForce=2500,jump=5,waterForce=2,damageDelay=.5f,attackForce=5,airForce=2,waterAttackMulti=2,keepVelocity=.5f,randomDirection=.1f,runSpeed=6;
        public bool jumpOnWater,torque,attackInAir=true;
    }
    public sealed class FishDefinition:ScriptableObject {
        public const float HealthMultiplier=1.5f;
        public int originalId;
        public string displayName;
        public FishingCatch catchPrefab;
        public float weight,length;
        public OriginalFishMotion original;
        public ShoreFishMotion shore=new ShoreFishMotion();
        public bool ShoreAttackEligible=>shore!=null&&shore.aggressive&&(shore.nativeLength>=1.5f||rarity>=CatchRarity.Rare);
        public CatchRarity rarity;
        [Min(.01f)] public float catchWeight=1;
        [Min(0)] public int catchHealthOverride;
        public bool seaHorse;
        public bool shallowWaterFish;
        [HideInInspector] public int renderVertexCost,renderTriangleCost;
        public string RarityLabel=>rarity==CatchRarity.Legendary?"传说":rarity==CatchRarity.Rare?"稀有":rarity==CatchRarity.Uncommon?"少见":"普通";
        public int MaximumHealth=>catchHealthOverride>0?catchHealthOverride:Mathf.Max(1,Mathf.CeilToInt((original!=null?original.hp:40)*HealthMultiplier));
    }
}
