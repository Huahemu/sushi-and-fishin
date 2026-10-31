using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.Extensions;

namespace IncrementalAddons.Casino
{
    public sealed class NativeCasinoButtonObserver:MonoBehaviour
    {
        readonly Dictionary<string,NativeFloorRequest> requests=new Dictionary<string,NativeFloorRequest>();
        public event Action<NativeFloorRequest> Completed;
        public NativeFloorRequest[] Requests=>requests.Values.ToArray();
        public NativeFloorRequest Request(int floor,string actorId,string operationId,PlayerInteract originalCaller=null)
        {
            string key=(actorId??"")+"/"+(operationId??"");
            if(requests.TryGetValue(key,out var old))return old;
            var receipt=NativeCasinoButtonAccess.Request(floor,actorId,operationId,originalCaller);
            if(receipt.eventIssued)requests[key]=receipt;return receipt;
        }
        void Update()
        {
            var casino=ParkCasinoIntegration.Instance;if(!casino||!casino.Ready||!NetworkSingleton<ElevatorManager>.TryGetInstance(out var elevator)||!elevator)return;
            foreach(var r in requests.Values.Where(r=>r.phase=="WaitingForOriginalFloor").ToArray())
            {
                r.observedFloor=casino.CurrentFloor;
                if(elevator.isTeleporting||r.observedFloor!=r.nativeFloor||NativeCasinoButtonAccess.ReadNativeFloor(elevator)!=r.nativeFloor)continue;
                r.phase="OriginalFloorObserved";r.completedUtc=DateTime.UtcNow.ToString("O");Completed?.Invoke(r);
            }
        }
    }
}
