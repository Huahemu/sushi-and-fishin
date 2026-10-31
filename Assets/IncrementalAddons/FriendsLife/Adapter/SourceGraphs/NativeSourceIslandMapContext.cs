using System;
using System.Linq;
using ECM2;
using SushiAndFish.Casino;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // This is an explicit host-map-kind adapter. It does not calibrate native
    // coordinates or evaluate a Place rule. The existing original engine uses
    // its IsOnIsland branch only after this actual host observation succeeds.
    public static class NativeSourceIslandMapContext
    {
        public const string IslandScene="Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity";
        public const string Source="Declared host OriginalIsland GameState surface Terrain region; host LiveSceneTabletMap surface-height cutoff>-4; original Place003ef214 IsOnIsland flag+18 remains in NativeMiiEventEngine";
        public sealed class Observation
        {
            public string nativeResidentId,personId,reason,scenePath;
            public Transform body;
            public Terrain terrain;
            public float terrainWorldY,bodyToTerrainY,bodyEnvelope;
            public int matchingTerrains;
            public bool accepted;
            public bool? OnIsland=>accepted?(bool?)true:null;
        }
        static int frame=int.MinValue;
        static Terrain[] terrains=Array.Empty<Terrain>();
        static PhysicsVolume[] waterVolumes=Array.Empty<PhysicsVolume>();
        static HousingPrivateRoom[] privateRooms=Array.Empty<HousingPrivateRoom>();
        static Collider[] higherCasino=Array.Empty<Collider>();
        static Bounds? higherCasinoBounds;
        // Water volumes and the casino module are static scene content: they
        // are searched once a second (each search costs several ms); the
        // per-frame state filters still run on the cached candidates.
        static PhysicsVolume[] volumeCandidates=Array.Empty<PhysicsVolume>();
        static Collider[] casinoCandidates=Array.Empty<Collider>();
        static float nextSceneSearch;static Transform searchedCasinoRoot;static int searchedCasinoFloor;
        static void Refresh()
        {
            if(frame==Time.frameCount)return;frame=Time.frameCount;
            var casino=ParkCasinoIntegration.Instance;
            var casinoRoot=casino&&casino.Ready&&casino.CurrentFloor!=0?casino.ModuleRoot:null;int casinoFloor=casino?casino.CurrentFloor:0;
            if(Time.unscaledTime>=nextSceneSearch||casinoRoot!=searchedCasinoRoot||casinoFloor!=searchedCasinoFloor)
            {
                nextSceneSearch=Time.unscaledTime+1f;searchedCasinoRoot=casinoRoot;searchedCasinoFloor=casinoFloor;
                volumeCandidates=UnityEngine.Object.FindObjectsByType<PhysicsVolume>(FindObjectsSortMode.None);
                casinoCandidates=casinoRoot?casinoRoot.GetComponentsInChildren<Collider>():Array.Empty<Collider>();
            }
            terrains=Terrain.activeTerrains.Where(t=>t&&t.terrainData&&t.gameObject.activeInHierarchy&&t.gameObject.scene.path==IslandScene).ToArray();
            waterVolumes=volumeCandidates.Where(v=>v&&v.enabled&&v.waterVolume&&v.boxCollider&&v.boxCollider.enabled&&v.gameObject.activeInHierarchy).ToArray();
            privateRooms=FriendInstances<HousingPrivateRoom>.Active();
            higherCasino=casinoRoot?casinoCandidates.Where(c=>c&&c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy).ToArray():Array.Empty<Collider>();
            higherCasinoBounds=null;
            foreach(var collider in higherCasino)
            {var bounds=higherCasinoBounds??collider.bounds;bounds.Encapsulate(collider.bounds);higherCasinoBounds=bounds;}
        }
        static bool InPrivateMap(HousingPrivateRoom room,Vector3 point)
        {
            if(!room||!room.house||room.house.definition==null||!room.gameObject.activeInHierarchy)return false;
            var size=room.house.definition.footprint;if(size.x<=0||size.y<=0)return false;
            return new Bounds(new Vector3(0,2.5f,0),new Vector3(size.x,11,size.y)).Contains(room.house.transform.InverseTransformPoint(point));
        }
        public static Observation Inspect(FriendsLifeRuntime runtime,string nativeResidentId)
        {
            var result=new Observation{nativeResidentId=nativeResidentId};
            if(!runtime||!runtime.Ready){result.reason="Current host world is unavailable";return result;}
            var person=runtime.PersonFromNative(nativeResidentId);result.personId=person?.personId;
            var body=person==null?null:runtime.TransformOf(person.personId);result.body=body;
            if(!body){result.reason="The source person has no actual host body";return result;}
            Refresh();var point=body.position;var scene=SceneManager.GetSceneByPath(IslandScene);result.scenePath=scene.path;
            if(!scene.IsValid()||!scene.isLoaded){result.reason="The declared original host island scene is not loaded";return result;}
            // A room remains a distinct map even if its precise source room
            // owner is temporarily unavailable or overlapping during restore.
            if(privateRooms.Any(r=>InPrivateMap(r,point))){result.reason="Actual remote private-room volume takes precedence over island map kind";return result;}
            if(higherCasinoBounds.HasValue&&higherCasinoBounds.Value.Contains(point)){result.reason="Actual higher-floor casino module takes precedence over island map kind";return result;}
            if(waterVolumes.Any(v=>v.boxCollider.bounds.Contains(point)&&(v.boxCollider.ClosestPoint(point)-point).sqrMagnitude<.000001f))
            {result.reason="Actual host water PhysicsVolume contains the body";return result;}
            var agent=body.GetComponent<NavMeshAgent>();var capsule=body.GetComponent<CapsuleCollider>();
            if(agent&&agent.enabled)result.bodyEnvelope=agent.height+Mathf.Abs(agent.baseOffset);
            else if(capsule&&capsule.enabled)result.bodyEnvelope=capsule.bounds.size.y;
            if(result.bodyEnvelope<=0||float.IsNaN(result.bodyEnvelope)||float.IsInfinity(result.bodyEnvelope))
            {result.reason="No actual host navigation/capsule vertical envelope is available";return result;}
            float nearest=float.PositiveInfinity;
            foreach(var terrain in terrains)
            {
                // Unity Terrain heightmaps are axis-aligned here. Do not treat
                // an arbitrarily transformed renderer as this map declaration.
                if(Quaternion.Angle(terrain.transform.rotation,Quaternion.identity)>.001f||(terrain.transform.lossyScale-Vector3.one).sqrMagnitude>.000001f)continue;
                var offset=point-terrain.transform.position;var size=terrain.terrainData.size;
                if(size.x<=0||size.z<=0||offset.x<0||offset.z<0||offset.x>size.x||offset.z>size.z)continue;
                float y=terrain.transform.position.y+terrain.terrainData.GetInterpolatedHeight(offset.x/size.x,offset.z/size.z);
                // This is the installed surface map's declared land-height
                // rule, copied from LiveSceneTabletMap.SetLayer(Surface).
                if(y<=-4f)continue;
                float separation=Mathf.Abs(point.y-y);if(separation>result.bodyEnvelope)continue;
                result.matchingTerrains++;
                if(separation>=nearest)continue;nearest=separation;result.terrain=terrain;result.terrainWorldY=y;result.bodyToTerrainY=point.y-y;
            }
            if(!result.terrain){result.reason="Body is outside the declared island surface terrain region/vertical envelope";return result;}
            result.accepted=true;result.reason="Actual body is in the loaded original island surface terrain region, outside declared alternate maps/water";return result;
        }
        public static bool? ObserveOnIsland(FriendsLifeRuntime runtime,string nativeResidentId)=>Inspect(runtime,nativeResidentId).OnIsland;
    }
}
