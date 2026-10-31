using System;
using System.Collections.Generic;
using System.Linq;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using UnityEngine;

namespace SushiAndFish.FriendsLife.CasinoCore
{
    public interface INativeCasinoPassenger
    {
        string ActorId{get;}
        string OwnerId{get;}
        bool IsValid{get;}
        Vector3 BodyPosition{get;}
        int ActualFloor{get;}
        void RequestSharedFloorYield();
        void AbortPreparedTransfer(string transferId);
        bool PrepareOriginalTransfer(string transferId,Transform module,out string reason);
        bool ApplyOriginalMapPose(string transferId,Transform module,out string reason);
        void CompleteOriginalTransfer(string transferId,int actualFloor,bool mapped,string reason);
    }
    public sealed class CasinoOriginalTransfer
    {
        public string id,reason;
        public int floor;
        public bool includeOriginalHumans,mapped,completed,hostLobbyRecall;
        public INativeCasinoPassenger[] passengers;
    }
    // One original shared-floor transaction at a time. Human source calls use
    // their original guards. NPC requests never fabricate registered players.
    public static class NativeCasinoTransportTransaction
    {
        static readonly Dictionary<string,INativeCasinoPassenger> registered=new Dictionary<string,INativeCasinoPassenger>(StringComparer.Ordinal);
        public static CasinoOriginalTransfer Current{get;private set;}
        public static bool IncludesOriginalHumans=>Current==null||Current.includeOriginalHumans;
        public static bool IsNpcOnly=>Current!=null&&!Current.includeOriginalHumans;
        public static bool IsHostLobbyRecall=>Current!=null&&Current.hostLobbyRecall;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){registered.Clear();Current=null;}
        public static bool Register(INativeCasinoPassenger passenger)
        {
            if(passenger==null||!passenger.IsValid)return false;
            if(registered.TryGetValue(passenger.ActorId,out var old)&&old.OwnerId!=passenger.OwnerId)return false;
            registered[passenger.ActorId]=passenger;return true;
        }
        public static bool Unregister(string actor,string owner)
        {if(!registered.TryGetValue(actor??"",out var old)||old.OwnerId!=owner||Current!=null&&Current.passengers.Contains(old))return false;return registered.Remove(actor);}
        static bool SourceGamesSafe()=>!UnityEngine.Object.FindObjectsByType<GameBase>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Any(g=>g&&g.gameObject.activeInHierarchy&&(g.isPlaying||g.NativeHasUnsettledResult));
        static bool SourcePassengersReady(ElevatorManager source,out string reason)
        {
            reason=null;bool ready=true;
            foreach(var passenger in registered.Values.Where(p=>p.IsValid&&p.ActualFloor==source.NativeCurrentFloor))
                if(!source.IsInElevator(passenger.BodyPosition)){passenger.RequestSharedFloorYield();ready=false;}
            if(!ready)reason="An owned passenger is returning physically to the original shared cab";
            return ready;
        }
        public static bool CanOriginalHumanTransfer(ElevatorManager source,out string reason)
        {
            reason=null;if(Current!=null){reason="Original shared-floor transaction already active";return false;}
            if(!SourceGamesSafe()){reason="A real original round still owns the current floor";return false;}
            if(!SourcePassengersReady(source,out reason))return false;
            return true;
        }
        public static bool BeginOriginalHumanTransfer(ElevatorManager source,int floor)
            =>Begin(source,floor,"human/"+Guid.NewGuid().ToString("N"),true,null,out _);
        public static bool TryBeginNpcTransfer(ElevatorManager source,int floor,string operation,INativeCasinoPassenger requester,out string reason)
        {
            reason=null;var casino=ParkCasinoIntegration.Instance;
            if(!Mirror.NetworkServer.active||!source||!casino||!casino.Ready||requester==null||!requester.IsValid)
            {reason="Original server and owned passenger required";return false;}
            if(Current!=null||source.isTeleporting||source.IsLocked||source.NativeCurrentFloor==floor)
            {reason="Original shared elevator/floor guard not satisfied";return false;}
            if(casino.HostUsesCurrentNativeMap)
            {reason="Human priority: wait for the human's original shared-floor transaction";return false;}
            if(!SourceGamesSafe()){reason="An original round has not reached its source safe boundary";return false;}
            if(!SourcePassengersReady(source,out reason))return false;
            if(!source.NativeFloorButtonAvailable(floor))
            {reason="Selected original floor button is not unlocked/available";return false;}
            if(!source.IsInElevator(requester.BodyPosition))
            {reason="Actual owned body is not inside the original cab volume";return false;}
            if(!Begin(source,floor,operation,false,requester,out reason))return false;
            if(source.NativeStartPassengerTransfer(floor))return true;
            AbortUnstarted("Original elevator refused the prepared transaction");reason="Original elevator refused the prepared transaction";return false;
        }
        // The host is back on the island (stuck recovery or never rode) and
        // stands in the Ground lobby while the shared map is on a native floor.
        // Same source guards as an NPC ride; no original human is moved.
        public static bool TryBeginHostLobbyRecall(ElevatorManager source,out string reason)
        {
            reason=null;var casino=ParkCasinoIntegration.Instance;
            if(!Mirror.NetworkServer.active||!source||!casino||!casino.Ready){reason="Original server required";return false;}
            if(Current!=null||source.isTeleporting||source.IsLocked||source.NativeCurrentFloor==0)
            {reason="Original shared elevator/floor guard not satisfied";return false;}
            if(!SourceGamesSafe()){reason="An original round has not reached its source safe boundary";return false;}
            if(!SourcePassengersReady(source,out reason))return false;
            if(!Begin(source,0,"host-lobby/"+Guid.NewGuid().ToString("N"),false,null,out reason))return false;
            Current.hostLobbyRecall=true;
            if(source.NativeStartPassengerTransfer(0))return true;
            AbortUnstarted("Original elevator refused the lobby recall");reason="Original elevator refused the lobby recall";return false;
        }
        static bool Begin(ElevatorManager source,int floor,string id,bool humans,INativeCasinoPassenger required,out string reason)
        {
            reason=null;if(Current!=null||!source||string.IsNullOrEmpty(id))return false;
            var map=ParkCasinoIntegration.Instance?.ModuleRoot;if(!map){reason="Original map frame missing";return false;}
            var passengers=registered.Values.Where(p=>p.IsValid&&source.IsInElevator(p.BodyPosition)).OrderBy(p=>p.ActorId,StringComparer.Ordinal).ToArray();
            if(required!=null&&!passengers.Contains(required)){reason="Prepared requester is not a registered actual cab passenger";return false;}
            var prepared=new List<INativeCasinoPassenger>();
            foreach(var passenger in passengers)
            {
                if(!passenger.PrepareOriginalTransfer(id,map,out reason))
                {foreach(var done in prepared)done.AbortPreparedTransfer(id);return false;}
                prepared.Add(passenger);
            }
            Current=new CasinoOriginalTransfer{id=id,floor=floor,includeOriginalHumans=humans,passengers=passengers};return true;
        }
        public static void OnOriginalMapPoseChanged(Transform module,int floor)
        {
            var transaction=Current;if(transaction==null||transaction.mapped||transaction.floor!=floor)return;
            bool all=true;string failure=null;
            foreach(var passenger in transaction.passengers)
            {
                string reason=null;
                if(!passenger.IsValid||!passenger.ApplyOriginalMapPose(transaction.id,module,out reason))
                {all=false;failure=reason??"Owned passenger binding changed during the original map transaction";}
            }
            transaction.mapped=all;transaction.reason=failure;
        }
        public static void CompleteOriginal(ElevatorManager source,int actualFloor)
        {
            var transaction=Current;if(transaction==null)return;
            if(actualFloor!=transaction.floor){transaction.reason="Original floor readback differs from accepted transaction";return;}
            transaction.completed=true;
            foreach(var passenger in transaction.passengers)
                passenger.CompleteOriginalTransfer(transaction.id,actualFloor,transaction.mapped,transaction.reason);
            Current=null;
        }
        static void AbortUnstarted(string reason)
        {
            var transaction=Current;if(transaction==null)return;
            foreach(var p in transaction.passengers)p.AbortPreparedTransfer(transaction.id);
            Current=null;
        }
    }
}
