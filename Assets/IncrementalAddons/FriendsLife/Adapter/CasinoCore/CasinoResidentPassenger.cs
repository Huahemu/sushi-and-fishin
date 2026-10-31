using System;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife.CasinoCore
{
    public sealed class CasinoResidentPassenger:INativeCasinoPassenger
    {
        public readonly CasinoJourney journey;
        readonly FriendsLifeRuntime runtime;
        readonly ResidentController sourceBody;
        Vector3 localPosition,preparedBody;Quaternion localRotation,preparedRotation;string prepared;
        int preparedActorInstance,preparedWalkerInstance;bool preparedBoundaryVerified;
        public CasinoResidentPassenger(FriendsLifeRuntime runtime,CasinoJourney journey){this.runtime=runtime;this.journey=journey;sourceBody=runtime?runtime.ActorOf(journey.personId):null;}
        public string ActorId=>journey.personId;
        public string OwnerId=>journey.id;
        public int ActualFloor=>journey.actualFloor;
        public void RequestSharedFloorYield(){journey.returnRequested=true;runtime?.NotifyChanged();}
        public void AbortPreparedTransfer(string id){if(prepared!=id)return;prepared=null;preparedBoundaryVerified=false;preparedActorInstance=preparedWalkerInstance=0;journey.transferId=null;journey.transferMapped=false;}
        ResidentController Actor=>runtime?runtime.ActorOf(ActorId):null;
        public bool IsValid=>runtime&&runtime.IsAuthority&&runtime.IsWorldRunning&&Actor&&Actor==sourceBody&&Actor.Brain!=null&&FriendsBodyAuthority.IsOwned(runtime,ActorId,OwnerId);
        public Vector3 BodyPosition=>sourceBody?sourceBody.transform.position:default;
        public int SourceBodyInstance=>sourceBody?sourceBody.GetInstanceID():0;
        public bool PrepareOriginalTransfer(string transferId,Transform module,out string reason)
        {
            reason=null;
            if(!IsValid||!module||!NetworkSingleton<ElevatorManager>.TryGetInstance(out var e)||!e.IsInElevator(BodyPosition)||
                !string.IsNullOrEmpty(prepared)&&prepared!=transferId)
            {reason="Owned actual cab passenger required before original transfer";return false;}
            var walker=Actor.GetComponent<CasinoCabinWalker>();
            if(walker&&(!string.IsNullOrEmpty(walker.Owner)&&walker.Owner!=OwnerId||walker.Owner==OwnerId&&!walker.Arrived))
            {reason="Physical cab entry not complete or owned by another journey";return false;}
            if(!CasinoCabinWalker.ValidateSegment(Actor,e.playerSpawnPosition,BodyPosition,BodyPosition,out reason))return false;
            preparedBody=BodyPosition;preparedRotation=Actor.transform.rotation;preparedActorInstance=Actor.GetInstanceID();
            preparedWalkerInstance=walker&&walker.Owner==OwnerId?walker.GetInstanceID():0;preparedBoundaryVerified=true;
            // Capture only: preparation cannot change body/agent before every
            // passenger has passed the same transaction's preflight.
            localPosition=module.InverseTransformPoint(BodyPosition);
            localRotation=Quaternion.Inverse(module.rotation)*Actor.transform.rotation;
            prepared=transferId;journey.transferId=transferId;journey.transferMapped=false;journey.transferCompleted=false;
            return true;
        }
        public bool ApplyOriginalMapPose(string transferId,Transform module,out string reason)
        {
            reason=null;
            if(!IsValid||prepared!=transferId||journey.transferMapped||!module)
            {reason="Original source map callback has no matching prepared passenger";return false;}
            var tx=NativeCasinoTransportTransaction.Current;
            if(tx==null||tx.id!=transferId||!tx.passengers.Contains(this)){reason="Original transaction identity changed";return false;}
            var actor=Actor;var agent=actor.GetComponent<NavMeshAgent>();if(!agent){reason="Actual passenger agent missing";return false;}
            var walker=actor.GetComponent<CasinoCabinWalker>();
            if(!preparedBoundaryVerified||preparedActorInstance!=actor.GetInstanceID()||
                Vector3.Distance(actor.transform.position,preparedBody)>.001f||Quaternion.Angle(actor.transform.rotation,preparedRotation)>.1f||
                preparedWalkerInstance!=0&&(!walker||walker.GetInstanceID()!=preparedWalkerInstance||!walker.CanConsumePreparedBoundary(OwnerId,preparedActorInstance,preparedBody,preparedRotation,out reason)))
            {if(string.IsNullOrEmpty(reason))reason="Prepared original cab boundary changed before map callback";return false;}
            var casino=ParkCasinoIntegration.Instance;
            if(!casino||casino.ModuleRoot!=module||casino.CurrentFloor!=tx.floor||!NetworkSingleton<ElevatorManager>.TryGetInstance(out var elevator)||elevator.NativeCurrentFloor!=tx.floor)
            {reason="Actual original map/floor callback differs from prepared transaction";return false;}
            // Fresh target physics belongs to the new map. The old cab endpoint
            // physics was verified before the original map moved.
            var body=module.TransformPoint(localPosition);var rotation=module.rotation*localRotation;
            if(!elevator.IsInElevator(body)||!CasinoCabinWalker.ValidateSegment(actor,elevator.playerSpawnPosition,body,body,out reason))
            {if(string.IsNullOrEmpty(reason))reason="Original map-local target is not a strict actual cab point";return false;}
            if(agent.enabled&&agent.isOnNavMesh)actor.Brain.Movement.Stop();agent.enabled=false;
            actor.Brain.Movement.StopAtPose(body,rotation);
            if(Vector3.Distance(actor.transform.position,body)>.001f||Quaternion.Angle(actor.transform.rotation,rotation)>.1f)
            {reason="Original map-pose application body readback failed";return false;}
            if(preparedWalkerInstance!=0&&!walker.RetireMappedSourceTransfer(OwnerId,transferId,body,rotation,out reason))return false;
            journey.transferMapped=true;journey.savedBody=body;journey.savedRotation=rotation;runtime.NotifyChanged();return true;
        }
        public void CompleteOriginalTransfer(string transferId,int actualFloor,bool mapped,string reason)
        {
            if(prepared!=transferId)return;
            if(!IsValid||!mapped||!journey.transferMapped)
            {journey.phase="AwaitInspection";journey.failure=reason??"Original transaction did not map this actual passenger";runtime?.NotifyChanged();return;}
            prepared=null;preparedBoundaryVerified=false;
            journey.actualFloor=actualFloor;journey.transferCompleted=true;
            journey.phase=actualFloor==0&&journey.returnRequested?"ReturnCabExit":"FloorCabExit";
            journey.failure=null;runtime.NotifyChanged();
        }
    }
}
