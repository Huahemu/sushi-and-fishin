using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.Extensions;
using SushiAndFish.Casino.Original.CasinoLab;

namespace IncrementalAddons.Casino
{
    [Serializable] public sealed class NativeFloorButton
    {
        public string path,label;public int floor,persistentCallIndex;
        public bool active,interactable,meetsRequirements,nullableCallerSupported;
        [NonSerialized] public InteractableEventTrigger trigger;
        [NonSerialized] public ElevatorManager elevator;
    }
    [Serializable] public sealed class NativeFloorRequest
    {
        public string operationId,actorId,buttonPath,phase,message,requestedUtc,completedUtc;
        public int nativeFloor,observedFloor;public bool eventIssued,sourceStarted;
    }
    // NEW additive code only. All persistent event data is read, never changed.
    // A request uses the real original ServerOnInteract and retains every native
    // player/lock/floor guard. Completion proves only the native floor callback,
    // never an NPC pose/arrival, a casino round or its settlement.
    public static class NativeCasinoButtonAccess
    {
        static object Field(object value,string name)
        {
            for(var t=value?.GetType();t!=null;t=t.BaseType)
            {var field=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);if(field!=null)return field.GetValue(value);}
            return null;
        }
        static IList Calls(UnityEventBase e)=>Field(Field(e,"m_PersistentCalls"),"m_Calls") as IList;
        static string PathOf(Transform t){if(!t)return null;var parts=new List<string>();string scene=t.gameObject.scene.name;for(;t;t=t.parent)parts.Add(t.name);parts.Reverse();return scene+"/"+string.Join("/",parts);}
        static bool OnlyStaticCallbacks(UnityEventBase e)
        {
            if(e==null)return true;var calls=Calls(e);
            var runtimeCalls=Field(Field(e,"m_Calls"),"m_RuntimeCalls") as IList;
            if(runtimeCalls==null||runtimeCalls.Count>0)return false;
            if(calls==null)return e.GetPersistentEventCount()==0;
            foreach(var call in calls)
            {
                int state=Convert.ToInt32(Field(call,"m_CallState")??0);if(state==0)continue;
                if(Convert.ToInt32(Field(call,"m_Mode")??0)==0)return false;
            }
            return true;
        }
        static bool Captures(object closure,object expected,int depth)
        {
            if(closure==null||depth<0)return false;
            foreach(var field in closure.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
            {
                var value=field.GetValue(closure);if(ReferenceEquals(value,expected))return true;
                if(depth>0&&value!=null&&value.GetType().DeclaringType==typeof(CasinoLabBootstrap)&&Captures(value,expected,depth-1))return true;
            }
            return false;
        }
        static bool RuntimeDestination(UnityEventBase e,ElevatorManager elevator,out int destination)
        {
            destination=-1;var live=Field(Field(e,"m_Calls"),"m_RuntimeCalls") as IList;var casino=ParkCasinoIntegration.Instance;
            if(live==null||live.Count!=1||!casino||!casino.Bootstrap)return false;
            var callback=Field(live[0],"Delegate") as Delegate;if(callback==null)return false;var handlers=callback.GetInvocationList();if(handlers.Length!=1)return false;
            var handler=handlers[0];var owner=handler.Method.DeclaringType;
            // This is the exact original runtime binding observed in the live
            // source world, not the disabled prefab argument (Boss has arg 0).
            if(owner?.DeclaringType!=typeof(CasinoLabBootstrap)||handler.Method.Name!="<InitializeCasino>b__0"||
                !(Field(handler.Target,"destination") is int floor)||floor<0||floor>5||
                !Captures(handler.Target,elevator,2)||!Captures(handler.Target,casino.Bootstrap,2))return false;
            destination=floor;return true;
        }
        static bool GenuineCaller(PlayerInteract caller)
        {
            if(!caller)return false;var local=UnityEngine.Object.FindFirstObjectByType<LocalManager>();var profile=caller.GetComponent<PlayerProfile>();
            return local&&profile&&local.players!=null&&local.players.Any(p=>p!=null&&p.profile==profile&&p.transform&&
                (caller.transform==p.transform||caller.transform.IsChildOf(p.transform)));
        }
        public static NativeFloorButton[] ReadButtons()
        {
            if(!NetworkSingleton<ElevatorManager>.TryGetInstance(out var elevator)||!elevator)return Array.Empty<NativeFloorButton>();
            var assigned=Field(elevator,"buttonList") as IEnumerable<Transform>;if(assigned==null)return Array.Empty<NativeFloorButton>();
            var output=new List<NativeFloorButton>();
            foreach(var root in assigned.Where(t=>t))foreach(var button in root.GetComponentsInChildren<InteractableEventTrigger>(true))
            {
                var e=button.serverOnInteractEvent;if(e==null)continue;
                if(RuntimeDestination(e,elevator,out var runtimeFloor))
                {
                    output.Add(new NativeFloorButton{path=PathOf(button.transform),label=root.name,floor=runtimeFloor,persistentCallIndex=-1,
                        active=button.isActiveAndEnabled,interactable=button.IsInteractable,meetsRequirements=button.MeetRequirements,trigger=button,elevator=elevator,
                        nullableCallerSupported=false});continue;
                }
                var calls=Calls(e);if(calls==null)continue;
                for(int i=0;i<e.GetPersistentEventCount()&&i<calls.Count;i++)
                {
                    if(e.GetPersistentTarget(i)!=elevator||e.GetPersistentMethodName(i)!="ServerTryTeleportPlayers")continue;
                    var call=calls[i];if(Convert.ToInt32(Field(call,"m_CallState")??0)==0||Convert.ToInt32(Field(call,"m_Mode")??0)!=3)continue;
                    var value=Field(Field(call,"m_Arguments"),"m_IntArgument");if(!(value is int floor))continue;
                    output.Add(new NativeFloorButton{path=PathOf(button.transform),label=root.name,floor=floor,persistentCallIndex=i,
                        active=button.isActiveAndEnabled,interactable=button.IsInteractable,meetsRequirements=button.MeetRequirements,trigger=button,elevator=elevator,
                        nullableCallerSupported=OnlyStaticCallbacks(button.rpcOnInteractEvent)&&OnlyStaticCallbacks(button.serverOnInteractEvent)});
                }
            }
            return output.ToArray();
        }
        public static int? ReadNativeFloor(ElevatorManager elevator)=>Field(elevator,"_currentFloorIndex") is int floor?(int?)floor:null;
        public static bool AllNativePlayersInside(ElevatorManager elevator)
        {
            var local=MonoSingleton<LocalManager>.Instance;if(!local||local.players==null)return false;
            // Match the original all-player condition; never edit this list or
            // reposition its entries to make a request eligible.
            return local.players.All(p=>p!=null&&p.transform&&elevator.IsInElevator(p.transform.position));
        }
        public static NativeFloorRequest Request(int destination,string actorId,string operationId,PlayerInteract originalCaller=null)
        {
            var receipt=new NativeFloorRequest{operationId=operationId,actorId=actorId,nativeFloor=destination,phase="NotIssued",requestedUtc=DateTime.UtcNow.ToString("O")};
            var casino=ParkCasinoIntegration.Instance;
            if(string.IsNullOrEmpty(operationId)||string.IsNullOrEmpty(actorId)||!Mirror.NetworkServer.active||!casino||!casino.Ready)
            {receipt.message="Original casino/server is not ready.";return receipt;}
            var button=ReadButtons().FirstOrDefault(b=>b.floor==destination&&b.active&&b.interactable&&b.meetsRequirements);
            if(button==null){receipt.message="No enabled authored native button has that actual persistent floor argument.";return receipt;}
            receipt.buttonPath=button.path;receipt.observedFloor=casino.CurrentFloor;
            if(button.persistentCallIndex<0&&(!casino.Bootstrap||!casino.Bootstrap.ready))
            {receipt.message="Original runtime button bootstrap is not ready; no event was issued.";return receipt;}
            if(button.elevator.isTeleporting||button.elevator.IsLocked||ReadNativeFloor(button.elevator)==destination||!AllNativePlayersInside(button.elevator))
            {receipt.message="Original elevator gate is not satisfied.";return receipt;}
            if(!originalCaller&&!button.nullableCallerSupported)
            {
                var existing=casino.LogicInteractor;
                if(!GenuineCaller(existing)){receipt.message="This authored button requires an existing registered original interactor; no player is fabricated.";return receipt;}
                originalCaller=existing;
            }
            if(originalCaller&&!GenuineCaller(originalCaller)){receipt.message="The supplied interactor is not an existing registered original player.";return receipt;}
            button.trigger.ServerOnInteract(originalCaller);receipt.eventIssued=true;
            receipt.sourceStarted=button.elevator.isTeleporting&&ReadNativeFloor(button.elevator)==destination;
            receipt.phase=receipt.sourceStarted?"WaitingForOriginalFloor":"NativeEventIssued";
            receipt.message="Original button event invoked; no NPC transport/arrival/round result is asserted.";return receipt;
        }
    }
}
