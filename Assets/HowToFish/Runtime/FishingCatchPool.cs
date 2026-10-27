using UnityEngine;
namespace HowToFish {
    // Every model-backed definition stays reachable with every normal lure.
    // Native lure entries supply preferences, rather than locking out species.
    public static class FishingCatchPool {
        public struct CastOdds {
            public bool shallow;
            public float normalWeight,commonWeight,uncommonWeight,rareWeight,legendaryWeight,rareChance,legendaryChance,uncommonChance,commonChance;
        }
        public static float CastQuality(FishingProfile profile,float depth,float distance)=>
            .65f*Mathf.InverseLerp(profile.minDepth,Mathf.Max(profile.minDepth+.1f,profile.fullQualityDepth),depth)+
            .35f*Mathf.InverseLerp(profile.distanceQualityStart,Mathf.Max(profile.distanceQualityStart+.1f,profile.distanceQualityFull),distance);
        static bool Eligible(FishDefinition fish,bool shallow,int vertices,int triangles)=>Fits(fish,vertices,triangles)&&(!shallow||fish.shallowWaterFish&&fish.rarity==CatchRarity.Common);
        static float CastBaseWeight(FishDefinition fish,OriginalBait bait,bool mist)=>Weight(fish,bait)*(mist&&(fish.length>=1.8f||fish.original!=null&&fish.original.component=="AttackingFish")?1.35f:1);
        public static CastOdds Odds(FishingProfile profile,OriginalBait bait,float depth,float distance,float rareMultiplier=1,bool mist=false,int vertexBudget=int.MaxValue,int triangleBudget=int.MaxValue){
            var odds=new CastOdds{shallow=depth<profile.minDepth};
            foreach(var fish in profile.fish){
                if(!Eligible(fish,odds.shallow,vertexBudget,triangleBudget))continue;
                float weight=CastBaseWeight(fish,bait,mist);
                if(fish.rarity==CatchRarity.Legendary)odds.legendaryWeight+=weight;
                else if(fish.rarity==CatchRarity.Rare)odds.rareWeight+=weight;
                else {odds.normalWeight+=weight;if(fish.rarity==CatchRarity.Common)odds.commonWeight+=weight;else odds.uncommonWeight+=weight;}
            }
            if(!odds.shallow&&odds.normalWeight>0){
                float quality=CastQuality(profile,depth,distance),boost=Mathf.Max(1,rareMultiplier);
                odds.rareChance=odds.rareWeight>0?Mathf.Clamp(Mathf.Lerp(profile.nearRareChance,profile.deepRareChance,quality)*boost,0,profile.maxRareChance):0;
                odds.legendaryChance=odds.legendaryWeight>0?Mathf.Clamp(Mathf.Lerp(profile.nearLegendaryChance,profile.deepLegendaryChance,quality)*boost,0,Mathf.Min(profile.maxLegendaryChance,1-odds.rareChance)):0;
            }
            float normalChance=1-odds.rareChance-odds.legendaryChance;
            odds.uncommonChance=odds.normalWeight>0?normalChance*odds.uncommonWeight/odds.normalWeight:0;
            float multiplier=Mathf.Clamp01(profile.nonCommonProbabilityMultiplier);
            odds.rareChance*=multiplier;odds.legendaryChance*=multiplier;odds.uncommonChance*=multiplier;
            odds.commonChance=odds.commonWeight>0?1-odds.rareChance-odds.legendaryChance-odds.uncommonChance:0;
            // If a custom budget/pool has no common fish, normalize the remaining
            // available tiers rather than leaving an unselectable probability gap.
            float sum=odds.commonChance+odds.uncommonChance+odds.rareChance+odds.legendaryChance;
            if(sum>0){odds.commonChance/=sum;odds.uncommonChance/=sum;odds.rareChance/=sum;odds.legendaryChance/=sum;}
            return odds;
        }
        public static float CastWeight(FishDefinition fish,OriginalBait bait,CastOdds odds,bool mist=false,int vertexBudget=int.MaxValue,int triangleBudget=int.MaxValue){
            if(!Eligible(fish,odds.shallow,vertexBudget,triangleBudget))return 0;
            float weight=CastBaseWeight(fish,bait,mist);
            if(fish.rarity==CatchRarity.Legendary)return odds.legendaryWeight>0?weight/odds.legendaryWeight*odds.legendaryChance:0;
            if(fish.rarity==CatchRarity.Rare)return odds.rareWeight>0?weight/odds.rareWeight*odds.rareChance:0;
            if(fish.rarity==CatchRarity.Uncommon)return odds.uncommonWeight>0?weight/odds.uncommonWeight*odds.uncommonChance:0;
            return odds.commonWeight>0?weight/odds.commonWeight*odds.commonChance:0;
        }
        public static FishDefinition PickForCast(FishingProfile profile,OriginalBait bait,float unit,float depth,float distance,float rareMultiplier=1,bool mist=false,int vertexBudget=int.MaxValue,int triangleBudget=int.MaxValue){
            var odds=Odds(profile,bait,depth,distance,rareMultiplier,mist,vertexBudget,triangleBudget);
            float cursor=Mathf.Clamp01(unit);FishDefinition fallback=null;
            foreach(var fish in profile.fish){float weight=CastWeight(fish,bait,odds,mist,vertexBudget,triangleBudget);if(weight<=0)continue;fallback=fish;cursor-=weight;if(cursor<=0)return fish;}
            return fallback;
        }
        public static FishDefinition SmallCatch(FishingProfile profile,OriginalBait bait,float unit,int vertices,int triangles){
            float sum=0;FishDefinition fallback=null;
            foreach(var fish in profile.fish)if(Eligible(fish,true,vertices,triangles)){float w=Weight(fish,bait);sum+=w;if(w>0)fallback=fish;}
            float cursor=Mathf.Clamp01(unit)*sum;
            foreach(var fish in profile.fish)if(Eligible(fish,true,vertices,triangles)){float w=Weight(fish,bait);if(w<=0)continue;cursor-=w;if(cursor<=0)return fish;}
            return fallback;
        }
        public static float Weight(FishDefinition fish,OriginalBait bait,float rareMultiplier=1){
            if(!fish||!fish.catchPrefab||fish.catchWeight<=0)return 0;
            float tier=fish.rarity==CatchRarity.Common?9:fish.rarity==CatchRarity.Uncommon?3.5f:fish.rarity==CatchRarity.Rare?1.1f:.25f;
            float affinity=1;
            if(bait!=null&&bait.entries!=null)foreach(var entry in bait.entries)if(entry.fish==fish){affinity+=Mathf.Clamp(entry.weight/5f,.2f,2);break;}
            return tier*fish.catchWeight*affinity*(fish.rarity>=CatchRarity.Rare?Mathf.Max(1,rareMultiplier):1);
        }
        public static FishDefinition Pick(FishingProfile profile,OriginalBait bait,float unit,float rareMultiplier=1,int vertexBudget=int.MaxValue,int triangleBudget=int.MaxValue){
            float total=0;FishDefinition fallback=null;
            foreach(var fish in profile.fish){if(!Fits(fish,vertexBudget,triangleBudget))continue;float w=Weight(fish,bait,rareMultiplier);total+=w;if(w>0)fallback=fish;}
            float cursor=Mathf.Clamp01(unit)*total;
            foreach(var fish in profile.fish){if(!Fits(fish,vertexBudget,triangleBudget))continue;float w=Weight(fish,bait,rareMultiplier);if(w<=0)continue;cursor-=w;if(cursor<=0)return fish;}
            return fallback;
        }
        public static bool Fits(FishDefinition fish,int vertices,int triangles)=>fish&&fish.renderVertexCost<=vertices&&fish.renderTriangleCost<=triangles;
    }
}
