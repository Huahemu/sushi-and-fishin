using HowToFish;
using UnityEngine;
namespace SushiAndFish.FishingIntegration {
    [System.Serializable] public sealed class FishingIslandRegion {
        public string title;public Vector3 center,dock;public float fishingRadius;public GameObject prefab;public FishDefinition[] fish;
        public float nearRare,deepRare,maxRare,nearLegendary,deepLegendary,maxLegendary;
    }
    public sealed class ArchipelagoSettings:ScriptableObject {
        public bool enabled=true;public OriginalBoatRig boatPrefab;public GameObject accessPrefab;public Vector3 boatSpawn;public Quaternion boatRotation=Quaternion.identity;
        public FishingIslandRegion[] regions;public AudioClip motorStart,motorStop,motorIdle,motorRun,waterMoving;
        public static ArchipelagoSettings Load()=>Resources.Load<ArchipelagoSettings>("FishingArchipelago");
        public int RegionAt(Vector3 point){int best=0;float distance=float.PositiveInfinity;for(int i=1;i<regions.Length;i++){float d=Vector2.Distance(new Vector2(point.x,point.z),new Vector2(regions[i].center.x,regions[i].center.z));if(d<=regions[i].fishingRadius&&d<distance){best=i;distance=d;}}return best;}
        public int ApplyTo(FishingProfile profile,Vector3 cast){int index=RegionAt(cast);var region=regions[index];profile.fish=region.fish;profile.nearRareChance=region.nearRare;profile.deepRareChance=region.deepRare;profile.maxRareChance=region.maxRare;profile.nearLegendaryChance=region.nearLegendary;profile.deepLegendaryChance=region.deepLegendary;profile.maxLegendaryChance=region.maxLegendary;return index;}
    }
}
