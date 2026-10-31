using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // Compatibility observation surface. Typed complete-path movement and
    // authored activity exits now belong to the original Brain/state machine.
    // This component no longer replaces an original state with another AI.
    // It is the single observer of a friend's body: it never writes a state,
    // movement or lease. It only records how long the body has stood still
    // and which places it reached, for FriendLifeDebug and logs.
    [DefaultExecutionOrder(-100)]
    public sealed class FriendOwnedAmbientPathGuardian:MonoBehaviour
    {
        ResidentController actor;
        public string Status{get;private set;}
        public string LastFailure{get;private set;}
        public int ProtectedTransitions{get;private set;}
        public int BlockedTransitions{get;private set;}
        public int SynchronousEntryChecks{get;private set;}
        public int SynchronousEntryProtections{get;private set;}
        public int LastSynchronousEntryFrame{get;private set;}
        public float StillSeconds=>stillSince<0?0:Time.unscaledTime-stillSince;
        public IEnumerable<string> PlacesReached=>places;
        readonly List<string> places=new List<string>();
        Vector3 stillAt;float stillSince=-1f,nextTrack;bool warned;
        public static void Attach(ResidentController resident)
        {
            if(!FriendResidentBindings.IsOwned(resident))return;
            var guard=resident.GetComponent<FriendOwnedAmbientPathGuardian>();
            if(!guard)guard=resident.gameObject.AddComponent<FriendOwnedAmbientPathGuardian>();guard.actor=resident;
        }
        public static void ProtectAfterScheduleChange(ResidentController resident)
        {
            if(!FriendResidentBindings.IsOwned(resident)||resident.Brain==null)return;
            Attach(resident);var guard=resident.GetComponent<FriendOwnedAmbientPathGuardian>();
            guard.SynchronousEntryChecks++;guard.LastSynchronousEntryFrame=Time.frameCount;guard.Inspect();
        }
        public void RetryAfterBindingChange(){Inspect();}
        void Awake(){actor=GetComponent<ResidentController>();}
        void LateUpdate(){Inspect();if(Time.unscaledTime>=nextTrack){nextTrack=Time.unscaledTime+.5f;TrackStill();}}
        void Inspect()
        {
            if(!actor||actor.Brain==null||actor.Runtime==null)return;
            var state=actor.Runtime;var nav=actor.GetComponent<NavMeshAgent>();
            LastFailure=null;Status=actor.Brain.StateMachine.CurrentName;
            if(state.IsSwimming||state.IsSnapshotApplying||!OriginalNpcActivityPort.CanAmbientOwn(actor))return;
            if(state.HasValidTarget&&!state.HasArrived&&(!nav||!nav.enabled||!nav.isOnNavMesh))
                LastFailure="Actual same-type navigation is unavailable";
            else if(nav&&nav.enabled&&nav.isOnNavMesh&&!nav.pathPending&&nav.hasPath&&nav.pathStatus!=NavMeshPathStatus.PathComplete)
                LastFailure="The original state has not reached its complete navigation goal";
        }
        // Resting = hidden at home, sitting/lying in an authored pose, eating,
        // working or talking with the player. Anything else that does not move
        // 0.3 m counts as standing still.
        bool Resting()
        {
            var state=actor.Runtime;var current=actor.Brain.StateMachine.Current;
            if(current is ResidentHomeState||current is ResidentSeatActivityState seat&&seat.Resting||state.IsInRestaurant||state.IsWorking||state.IsInteracting)return true;
            var runtime=FriendsLifeRuntime.Instance;var id=GetComponent<FriendWorldActor>()?.personId;
            return runtime&&runtime.Ready&&runtime.State.leases.Any(l=>l.personId==id&&l.facilityId=="WorldTalk");
        }
        void TrackStill()
        {
            if(!actor||actor.Brain==null||actor.Runtime==null)return;
            var state=actor.Runtime;
            if(state.HasArrived&&state.CurrentActivity!=ResidentActivityType.Home&&state.CurrentActivity!=ResidentActivityType.Idle)
            {string place=state.CurrentActivity.ToString();if(!places.Contains(place)){places.Add(place);if(places.Count>12)places.RemoveAt(0);}}
            var position=actor.transform.position;
            if(Resting()){stillSince=-1f;stillAt=position;warned=false;return;}
            if(stillSince<0||(position-stillAt).sqrMagnitude>.09f){stillSince=Time.unscaledTime;stillAt=position;warned=false;return;}
            if(!warned&&StillSeconds>60f)
            {
                warned=true;
                Debug.LogWarning("[FriendsLife] "+name+" stood still for 60 s: state="+Status+", failure="+LastFailure+
                    ", status="+FriendLifeStatus.Describe(GetComponent<FriendWorldActor>()?.personId),this);
            }
        }
    }
}
