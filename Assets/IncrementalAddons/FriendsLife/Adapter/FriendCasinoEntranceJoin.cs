using System;
using System.Linq;
using SushiAndFish.Casino;
using UnityEngine;
using UnityEngine.AI;
using PaperPixelGames.Core.Player;

namespace SushiAndFish.FriendsLife
{
    // A small added floor patch spans the measured original entrance gap.
    // Authored geometry, source elevator, agent profile and original thresholds stay authoritative.
    [DefaultExecutionOrder(-100)]
    public sealed class FriendCasinoEntranceJoin : MonoBehaviour
    {
        static FriendCasinoEntranceJoin instance;
        GameObject join;
        Transform map;
        Transform waitingLimitsFor;
        float nextLimitRetry;
        public static string LastFailure { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){instance=null;LastFailure=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(instance)return;var go=new GameObject("Friend original entrance floor join");
            DontDestroyOnLoad(go);instance=go.AddComponent<FriendCasinoEntranceJoin>();
        }
        static RaycastHit Foot(Collider source,Vector3 near)
        {
            if(!source.Raycast(new Ray(near+Vector3.up*3,Vector3.down),out var hit,6))
                throw new InvalidOperationException("Original entrance edge has no support: "+source.name);
            return hit;
        }
        // Additional height is accepted only when each end of the centred
        // patch uses at most half of the smallest live step/climb capability.
        // This does not certify the approach surface, collision clearance or walkability.
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        internal static bool SafeEndpointRise(float difference,float playerStep,float minAgentClimb)
        {
            if(float.IsNaN(difference)||float.IsInfinity(difference)||difference<0||
               float.IsNaN(playerStep)||float.IsInfinity(playerStep)||playerStep<=0||
               float.IsNaN(minAgentClimb)||float.IsInfinity(minAgentClimb)||minAgentClimb<=0)return false;
            float endpointRise=difference*.5f;
            return endpointRise<=Math.Min(playerStep,minAgentClimb)*.5f;
        }
        static bool TryCurrentStepLimits(out float playerStep,out float minAgentClimb)
        {
            playerStep=0;minAgentClimb=0;
            var owners=FindObjectsByType<PPG_Player>(FindObjectsSortMode.None)
                .Where(p=>p&&p.IsSpawned&&p.IsOwner&&p.FirstPersonCharacter&&p.FirstPersonCharacter.characterMovement).ToArray();
            if(owners.Length!=1)return false;
            playerStep=owners[0].FirstPersonCharacter.characterMovement.stepOffset;
            int count=NavMesh.GetSettingsCount();if(count<=0)return false;
            minAgentClimb=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                float climb=NavMesh.GetSettingsByIndex(i).agentClimb;
                if(float.IsNaN(climb)||float.IsInfinity(climb)||climb<=0)return false;
                minAgentClimb=Math.Min(minAgentClimb,climb);
            }
            return !float.IsNaN(playerStep)&&!float.IsInfinity(playerStep)&&playerStep>0;
        }
        void Update(){Refresh();}
        void FixedUpdate(){Refresh();}
        void Refresh()
        {
            var casino=ParkCasinoIntegration.Instance;
            bool ready=casino&&casino.Ready&&casino.CurrentFloor==0&&casino.ModuleRoot;
            if(join&&(!casino||casino.ModuleRoot!=map)){join.SetActive(false);Destroy(join);join=null;map=null;}
            if(!ready){if(join)join.SetActive(false);return;}
            if(join){join.SetActive(true);return;}
            // Extra-height candidates awaiting a player/profile do not rescan
            // every Update and FixedUpdate, including on a dedicated server.
            if(waitingLimitsFor==casino.ModuleRoot&&Time.unscaledTime<nextLimitRetry)return;
            // A rejection remains visible until a different source map is loaded.
            if(map==casino.ModuleRoot&&LastFailure!=null)return;
            map=casino.ModuleRoot;
            try
            {
                var all=map.GetComponentsInChildren<Collider>(true);
                var ramp=all.Single(c=>c.name=="SM_PillarLobby01 (8)"&&c is BoxCollider);
                var front=all.Single(c=>c.name=="polySurface239");
                var floor=all.Single(c=>c.name=="SM_PillarLobby01 (6)"&&c is MeshCollider);
                var direction=Vector3.ProjectOnPlane(ramp.bounds.center-front.bounds.center,Vector3.up).normalized;
                var a=Foot(front,front.bounds.ClosestPoint(ramp.bounds.center)-direction*.10f);
                var b=Foot(ramp,ramp.bounds.ClosestPoint(front.bounds.center)+direction*.12f);
                float length=Vector3.Distance(Vector3.ProjectOnPlane(a.point,Vector3.up),Vector3.ProjectOnPlane(b.point,Vector3.up));
                float heightDifference=Mathf.Abs(a.point.y-b.point.y);
                if(!Finite(a.point)||!Finite(b.point)||!Finite(direction)||direction.sqrMagnitude<.000001f||
                   !Finite(length)||!Finite(heightDifference)||!Finite(a.normal.y))
                    throw new InvalidOperationException("Original entrance edge geometry is not finite");
                if(length>2||a.normal.y<.9f)
                    throw new InvalidOperationException("Original entrance edge witnesses differ from the measured narrow gap");
                if(heightDifference>.35f)
                {
                    // Original <=.35m cases are unchanged. Missing live settings do
                    // not manufacture a bridge or latch a permanent map rejection.
                    if(!TryCurrentStepLimits(out float playerStep,out float agentClimb))
                    {waitingLimitsFor=casino.ModuleRoot;nextLimitRetry=Time.unscaledTime+.5f;map=null;return;}
                    waitingLimitsFor=null;
                    if(!SafeEndpointRise(heightDifference,playerStep,agentClimb))
                    {
                        // A live profile can improve on the same map; retry at most
                        // twice per second without changing hard geometry rejection.
                        LastFailure="Original entrance height exceeds conservative live step/climb allowance";
                        waitingLimitsFor=casino.ModuleRoot;nextLimitRetry=Time.unscaledTime+.5f;map=null;return;
                    }
                }
                var source=floor.GetComponent<Renderer>();
                if(!source||!source.sharedMaterial)throw new InvalidOperationException("Original visible floor material is unavailable");
                float top=(a.point.y+b.point.y)*.5f,thickness=.18f;
                var centre=new Vector3((a.point.x+b.point.x)*.5f,top-thickness*.5f,(a.point.z+b.point.z)*.5f);
                if(!Finite(top)||!Finite(centre)||!Finite(length+.28f))
                    throw new InvalidOperationException("Original entrance join extent is not finite");
                join=GameObject.CreatePrimitive(PrimitiveType.Cube);join.name="Friend original entrance floor join";join.layer=floor.gameObject.layer;
                join.transform.SetPositionAndRotation(centre,Quaternion.LookRotation(direction));
                join.transform.localScale=new Vector3(2f,thickness,length+.28f);join.transform.SetParent(map,true);
                join.GetComponent<Renderer>().sharedMaterial=source.sharedMaterial;
                LastFailure=null;Physics.SyncTransforms();
            }
            catch(Exception e)
            {
                if(join){join.SetActive(false);Destroy(join);join=null;}
                LastFailure=e.GetBaseException().Message;Debug.LogWarning("[FriendsLife] Entrance join retained as unavailable: "+LastFailure);
            }
        }
        void OnDestroy(){if(join)Destroy(join);if(instance==this)instance=null;}
    }
}
