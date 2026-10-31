using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.Extensions;
using UnityEngine;
using UnityEngine.AI;
using SushiAndFish.ResidentialIsland;

namespace SushiAndFish.FriendsLife
{
    public sealed class FriendWorkProxy : MonoBehaviour { public string personId; }
    public sealed class FriendResidentDormancy : MonoBehaviour
    {
        readonly Dictionary<Collider,bool> colliders=new Dictionary<Collider,bool>();
        ResidentController actor;
        public void Suspend(ResidentController resident)
        {actor=resident;actor.Brain?.Pause();actor.Brain?.SetModelVisible(false);foreach(var c in actor.GetComponentsInChildren<Collider>(true)){colliders[c]=c.enabled;c.enabled=false;}}
        public void Restore(bool resume=true)
        {foreach(var pair in colliders)if(pair.Key)pair.Key.enabled=pair.Value;colliders.Clear();if(actor){actor.Brain?.SetModelVisible(true);if(resume){actor.Brain?.Resume();actor.Brain?.RequestScheduleChange();}}Destroy(this);}
    }

    // A station keeps its original service component alive while replacing only
    // the visible employee. Service state and safe handover belong to the source.
    public sealed partial class FriendJobStation : MonoBehaviour
    {
        public string slotId,sourceKind,payer="town";
        public Transform stationTransform,fallbackNpc;
        public Behaviour service;
        public Func<bool> IsOpen,SafeBoundary;
        readonly Dictionary<Renderer,bool> rendererState=new Dictionary<Renderer,bool>();
        string owner,ownerContractId; bool travelling; long travelActiveMicros;int serviceDepth;
        public string OwnerPersonId=>owner;
        public string OwnerContractId=>ownerContractId;
        public bool OnDuty {get;private set;}
        public int ServiceRequests {get;private set;}
        public int DeliveredServices {get;private set;}
        public bool ExecuteService(Func<bool> command)
        {
            bool bound=OnDuty&&!string.IsNullOrEmpty(owner)&&!string.IsNullOrEmpty(ownerContractId);
            if(bound)ServiceRequests++;
            serviceDepth++;try{bool result=command!=null&&command();if(result&&bound)DeliveredServices++;return result;}
            finally{serviceDepth--;}
        }
        public bool Available=>string.IsNullOrEmpty(owner)&&service&&service.isActiveAndEnabled&&(IsOpen?.Invoke()??true);
        public bool TryResolveRoute(ResidentController actor,out Vector3 destination)
        {
            destination=default;var agent=actor?actor.GetComponent<NavMeshAgent>():null;
            if(sourceKind=="CasinoElevator")return FriendCasinoGroundNavigation.TryResolve(this,actor,out destination);
            if(actor&&stationTransform)HousingNavigation.PrepareDoorRoute(actor,stationTransform.position);
            if(!actor||actor.Brain==null||!agent||!agent.enabled||!agent.isOnNavMesh||!stationTransform)return false;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            if(!NavMesh.SamplePosition(stationTransform.position,out var target,2,filter))return false;
            var path=new NavMeshPath();if(!agent.CalculatePath(target.position,path)||path.status!=NavMeshPathStatus.PathComplete||path.corners.Length==0)return false;
            if(Vector3.Distance(path.corners[0],actor.transform.position)>.25f)return false;
            var privacy=FriendHousingIsland.Instance?.GetComponent<FriendHousingPrivacy>();
            if(privacy&&privacy.ExcludesPathFor(actor,path))return false;
            if(Vector3.Distance(path.corners[path.corners.Length-1],target.position)>.2f)return false;
            destination=target.position;return true;
        }
        public bool TryAssign(FriendJobContract contract)
        {
            if(!Available||contract==null||string.IsNullOrEmpty(contract.contractId)||string.IsNullOrEmpty(contract.personId))return false;
            if(!TryResolveRoute(FriendsLifeRuntime.Instance?.ActorOf(contract.personId),out var destination))return false;
            contract.workPosition=destination;
            owner=contract.personId;ownerContractId=contract.contractId;contract.slotId=slotId;contract.payer=payer;contract.state=FriendJobPhase.Travel;travelling=false;travelActiveMicros=0;
            FriendsLifeRuntime.Instance?.ActorOf(owner)?.Brain?.SetModelVisible(true);return true;
        }
        public void TickContract(FriendJobContract job,long micros,FriendsEconomyService economy)
        {
            if(job==null||!economy||FriendsLifeRuntime.Instance?.IsAuthority!=true||job.state==FriendJobPhase.Done)return;
            if(TickCabinContract(job,micros,economy))return;
            if(owner!=job.personId||ownerContractId!=job.contractId)
            {
                // A loaded or late contract cannot clear another person's
                // current station, appearance, service or wage stream.
                job.finishReason="station-contract-ownership-changed";job.state=FriendJobPhase.Done;economy.RestoreActor(job);return;
            }
            var actor=FriendsLifeRuntime.Instance?.ActorOf(job.personId);
            if(!actor||!service||!service.isActiveAndEnabled||!(IsOpen?.Invoke()??true))job.state=FriendJobPhase.Handover;
            if(job.state==FriendJobPhase.Handover)
            {
                // Before OnDuty no fallback/service was replaced, so a lost
                // travel route can release our own reservation immediately.
                if(serviceDepth>0||OnDuty&&!(SafeBoundary?.Invoke()??true))return;
                actor?.Brain?.Movement.Stop();
                Restore();job.state=FriendJobPhase.Done;economy.RestoreActor(job);return;
            }
            if(actor.Brain==null||!stationTransform){job.state=FriendJobPhase.Handover;return;}
            actor.Brain.SetModelVisible(true);
            if(job.state==FriendJobPhase.Travel)
            {
                if(!travelling)
                {
                    travelling=true;travelActiveMicros=0;
                    if(!TryResolveRoute(actor,out var resolved)||!actor.Brain.Movement.MoveTo(resolved,actor.Brain.EffectiveMoveSpeed,.55f,true)){job.finishReason="route-unavailable";job.state=FriendJobPhase.Handover;return;}job.workPosition=resolved;
                }
                if(micros>0)travelActiveMicros=micros>long.MaxValue-travelActiveMicros?long.MaxValue:travelActiveMicros+micros;
                if(travelActiveMicros>90000000){job.finishReason="travel-timeout";job.state=FriendJobPhase.Handover;return;}
                var agent=actor.GetComponent<NavMeshAgent>();
                if(agent&&agent.enabled&&agent.isOnNavMesh&&!agent.pathPending&&agent.pathStatus!=NavMeshPathStatus.PathComplete)
                {job.finishReason="travel-path-became-incomplete";job.state=FriendJobPhase.Handover;return;}
                if(!agent||!agent.enabled||!agent.isOnNavMesh||agent.pathPending||agent.pathStatus!=NavMeshPathStatus.PathComplete||
                    !actor.Brain.Movement.HasArrived()||serviceDepth>0||!(SafeBoundary?.Invoke()??true))return;
                var distance=Vector3.Distance(actor.transform.position,job.workPosition);if(distance>.9f||Vector3.Distance(actor.Brain.Movement.Destination,job.workPosition)>.15f){job.finishReason="arrival-target-mismatch";job.state=FriendJobPhase.Handover;return;}
                actor.Brain.Movement.StopAtPose(job.workPosition,stationTransform.rotation);TakeAppearance();job.state=FriendJobPhase.OnDuty;OnDuty=true;return;
            }
            if(job.state==FriendJobPhase.OnDuty)economy.PayWork(job,micros);
        }
        void TakeAppearance()
        {
            if(!fallbackNpc)return;rendererState.Clear();
            var proxy=fallbackNpc.GetComponent<FriendWorkProxy>();if(!proxy)proxy=fallbackNpc.gameObject.AddComponent<FriendWorkProxy>();proxy.personId=owner;
            foreach(var renderer in fallbackNpc.GetComponentsInChildren<Renderer>(true)){rendererState[renderer]=renderer.enabled;renderer.enabled=false;}
        }
        void Restore(){foreach(var pair in rendererState)if(pair.Key)pair.Key.enabled=pair.Value;rendererState.Clear();if(fallbackNpc){var proxy=fallbackNpc.GetComponent<FriendWorkProxy>();if(proxy&&proxy.personId==owner)Destroy(proxy);}owner=null;ownerContractId=null;travelling=false;OnDuty=false;}
        void OnDisable(){Restore();}
        void OnDestroy(){Restore();}
    }
}
