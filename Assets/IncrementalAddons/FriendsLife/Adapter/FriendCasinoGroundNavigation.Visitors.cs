using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendCasinoGroundNavigation
    {
        readonly HashSet<string> visitorValidated=new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> visitorRequests=new HashSet<string>(StringComparer.Ordinal);
        public string VisitorRequestOwner{get;private set;}
        // Uses the visitor's existing sole persisted body lease. The existing
        // actual elevator station supplies only its original geometry anchor;
        // no job, work authorization, wage, station occupancy or new lease exists.
        public static bool TryPrepareVisitor(ResidentController actor,string owner,bool physicalCabExit,out string reason)
        {
            reason=null;var self=Instance;var runtime=FriendsLifeRuntime.Instance;var casino=ParkCasinoIntegration.Instance;
            var person=actor?actor.GetComponent<FriendWorldActor>()?.personId:null;
            if(!self||!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning||!actor||actor.Brain==null||
                !FriendResidentBindings.IsOwned(actor)||runtime.ActorOf(person)!=actor||!FriendsBodyAuthority.IsOwned(runtime,person,owner))
            {reason="Existing exact visitor body owner required for original navigation preparation";return false;}
            if(!self.OriginalReady(casino)||!casino.ModuleRoot||!NetworkSingleton<ElevatorManager>.TryGetInstance(out var elevator))
            {reason="Original Ground floor/doors/lock state is not ready";return false;}
            bool sameMap=self.module==casino.ModuleRoot&&self.module&&self.module.position==self.mapPosition&&
                Quaternion.Angle(self.module.rotation,self.mapRotation)<=.01f&&Vector3.Distance(self.module.lossyScale,self.mapScale)<=.001f;
            if(self.attempted&&!sameMap)
            {self.Release();self.attempted=false;self.Phase="Waiting";}
            var station=UnityEngine.Object.FindObjectsByType<FriendJobStation>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .FirstOrDefault(s=>s.sourceKind=="CasinoElevator"&&s.stationTransform&&s.stationTransform.GetComponentInParent<ElevatorManager>()==elevator);
            if(!station){reason="Actual original elevator geometry anchor has not been discovered";return false;}
            self.VisitorRequestOwner=owner;
            self.visitorRequests.Add(owner);
            if(self.Phase=="Ready"&&self.ownedMesh.valid&&self.ownedLink.valid)
                return self.ValidateVisitor(actor,owner,physicalCabExit,station,out reason);
            if(self.attempted)
            {reason=self.LastFailure??"Original source projection is retained as rejected; retry only after a concrete source change";return false;}
            var agent=actor.GetComponent<NavMeshAgent>();
            if(!agent||!physicalCabExit&&(!agent.enabled||!agent.isOnNavMesh))
            {reason="Actual street visitor agent is not continuously bound";return false;}
            self.attempted=true;self.module=casino.ModuleRoot;self.mapPosition=self.module.position;self.mapRotation=self.module.rotation;self.mapScale=self.module.lossyScale;
            try
            {
                self.Build(actor,station,physicalCabExit);
                return self.ValidateVisitor(actor,owner,physicalCabExit,station,out reason);
            }
            catch(Exception e)
            {
                self.LastFailure=e.GetBaseException().Message;self.Phase="Rejected";self.Release();reason=self.LastFailure;return false;
            }
        }
        bool ValidateVisitor(ResidentController actor,string owner,bool physicalCabExit,FriendJobStation station,out string reason)
        {
            reason=null;var agent=actor.GetComponent<NavMeshAgent>();
            if(!agent||agent.agentTypeID!=type||station!=boundStation)
            {reason="Existing projection source/profile differs; it is not replaced under another user";return false;}
            string key=actor.GetInstanceID()+"/"+owner+"/"+physicalCabExit;
            if(visitorValidated.Contains(key))return true;
            if(!TryPlanExplicitApproach(actor,station.stationTransform,SeamStart,SeamEnd,Approach,physicalCabExit,out _,out _,out _,out reason))return false;
            visitorValidated.Add(key);return true;
        }
        public static bool RequestVisitorRetry(ResidentController actor,string owner,out string reason)
        {
            reason=null;var self=Instance;var runtime=FriendsLifeRuntime.Instance;var person=actor?actor.GetComponent<FriendWorldActor>()?.personId:null;
            if(!self||!actor||!FriendsBodyAuthority.IsOwned(runtime,person,owner)||!self.visitorRequests.Contains(owner))
            {reason="Same existing visitor owner required to retry a known rejected projection";return false;}
            if(self.Phase=="Ready")return true;
            self.Release();self.attempted=false;self.LastFailure=null;self.Phase="Waiting";return true;
        }
    }
}
