using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Kitchen.ServedDishes;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    [Serializable]
    public sealed class FriendSushiDelivery
    {
        public string deliveryId,billId,personId,sourcePath,targetType,phase="AwaitFunds",worldId,sourceFoodId="Sushi",nativeWitness,lastFailure,runtimeToken,sourceFoodResult;
        public int tableId,seatIndex,originalOrderId,preparedFrame,nativeSeed,sourceInstance;
        public bool sourceFoodFinal;
        public ulong foodNetworkId;
        public long priceCents;
        public Vector3 preparedFoodPosition;
    }
    public sealed partial class FriendsEconomySave
    {public List<FriendSushiDelivery> sushiDeliveries=new List<FriendSushiDelivery>();}

    // This provider cannot intercept a source method. It only receives the
    // explicit proposed BeforeDelivery port; until that human-approved source
    // port is installed it creates no deliveries and charges nobody.
    [DefaultExecutionOrder(-10)]
    public sealed class FriendsSushiBillingAuthority : MonoBehaviour
    {
        public static FriendsSushiBillingAuthority Instance{get;private set;}
        public int PreparedCalls{get;private set;}
        public string LastFailure{get;private set;}
        const BindingFlags ReadFlags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        float next;
        readonly string runtimeToken=Guid.NewGuid().ToString("N");
        sealed class NativeFacts
        {
            public bool sourceFound,sameConsumer,foodFound,placed,sameOrder,eating,atNativeAnchor;
            public string nativeState;
            public int seed;
            public Vector3 foodPosition;
        }
        void Awake()
        {
            if(Instance&&Instance!=this){Destroy(this);return;}
            Instance=this;FriendsSushiDeliveryGate.Register(BeforeDelivery);
        }
        static bool Terminal(FriendSushiDelivery row)=>row.phase=="Committed"||row.phase=="Cancelled"||row.phase=="SourceRejected";
        static object Read(object target,string field)=>target?.GetType().GetField(field,ReadFlags)?.GetValue(target);
        static string PathOf(MonoBehaviour source)
        {
            string path=source.name;for(var parent=source.transform.parent;parent;parent=parent.parent)path=parent.name+"/"+path;
            return source.gameObject.scene.name+"/"+path;
        }
        static NetworkObject Food(ulong id)
        {return NetworkManager.Singleton&&NetworkManager.Singleton.SpawnManager!=null&&NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id,out var value)?value:null;}
        static MonoBehaviour Source(FriendSushiDelivery row)
        {
            if(row.targetType==ResidentRestaurantTargetType.DiningTable.ToString())
                return UnityEngine.Object.FindObjectsByType<RestaurantTable>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>PathOf(t)==row.sourcePath);
            return UnityEngine.Object.FindObjectsByType<ConveyorBeltTable>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>PathOf(t)==row.sourcePath);
        }
        static int NativeSeed(MonoBehaviour source)
        {
            if(source is RestaurantTable)return Read(source,"_lateJoinTakingOrderSeed") is int seed?seed:0;
            return Read(source,"_stateVersion") is int version?version:0;
        }
        bool BeforeDelivery(MonoBehaviour source,string personId,int tableId,ResidentRestaurantTargetType targetType,int seatIndex,int originalOrderId,ulong foodNetworkId,int exactMenuGold)
        {
            PreparedCalls++;var runtime=FriendsLifeRuntime.Instance;var economy=FriendsEconomyService.Instance;var food=Food(foodNetworkId);
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning||!economy||!source||!food||exactMenuGold<0||runtime.GetPerson(personId)?.controllerKind!=PersonControllerKind.Npc)return false;
            if(source is RestaurantTable classic)
            {if(targetType!=ResidentRestaurantTargetType.DiningTable||classic.Id!=tableId||classic.CurrentState!=RestaurantTableState.DeliveringOrder||!classic.TryGetSeatOrder(seatIndex,out _))return false;}
            else if(source is ConveyorBeltTable conveyor)
            {if(targetType!=ResidentRestaurantTargetType.ConveyorTable||conveyor.SeatId!=tableId||conveyor.CurrentState!=ConveyorBeltTableState.TakingOrder)return false;}
            else return false;
            economy.State.sushiDeliveries=economy.State.sushiDeliveries??new List<FriendSushiDelivery>();
            string path=PathOf(source);long price=(long)exactMenuGold*100;
            var matches=economy.State.sushiDeliveries.Where(r=>!Terminal(r)&&r.worldId==runtime.State.worldId&&r.personId==personId&&r.sourcePath==path&&
                r.targetType==targetType.ToString()&&r.tableId==tableId&&r.seatIndex==seatIndex&&r.originalOrderId==originalOrderId&&r.foodNetworkId==foodNetworkId).ToArray();
            if(matches.Length>1){LastFailure="Multiple unresolved delivery identities require source reconciliation";return false;}
            var row=matches.FirstOrDefault();
            if(row!=null&&(row.phase=="Unknown"||row.priceCents!=price))
            {row.lastFailure=LastFailure="An unresolved or differently quoted delivery is retained under its original ID";return false;}
            if(row==null)
            {
                string id="sushi-delivery/"+Guid.NewGuid().ToString("N");
                row=new FriendSushiDelivery{deliveryId=id,billId=id,personId=personId,sourcePath=path,targetType=targetType.ToString(),worldId=runtime.State.worldId,
                    tableId=tableId,seatIndex=seatIndex,originalOrderId=originalOrderId,foodNetworkId=foodNetworkId,priceCents=price,
                    preparedFoodPosition=food.transform.position,preparedFrame=Time.frameCount,nativeSeed=NativeSeed(source),runtimeToken=runtimeToken,sourceInstance=source.GetInstanceID()};
                economy.State.sushiDeliveries.Add(row);
            }
            // Both identity and the locked actual quote enter the same existing
            // economy snapshot before the native method can place/eat the food.
            using(economy.DeferExternalCommit())
            {
                if(!economy.QuoteAndReserve(personId,row.billId,"SushiRestaurant",price,row.sourceFoodId))
                {row.phase="AwaitFunds";row.lastFailure="The person's actual menu quote could not be reserved";LastFailure=row.lastFailure;runtime.NotifyChanged();return false;}
                row.phase="Reserved";row.preparedFrame=Time.frameCount;row.lastFailure=null;LastFailure=null;runtime.NotifyChanged();return true;
            }
        }
        static NativeFacts Facts(FriendSushiDelivery row)
        {
            var result=new NativeFacts();var source=Source(row);if(!source)return result;result.sourceFound=true;result.seed=NativeSeed(source);
            var kind=row.targetType==ResidentRestaurantTargetType.DiningTable.ToString()?ResidentRestaurantTargetType.DiningTable:ResidentRestaurantTargetType.ConveyorTable;
            var manager=Singleton<ResidentManager>.Instance;
            result.sameConsumer=manager&&manager.TryGetRestaurantResident(row.tableId,kind,row.seatIndex,out var actor)&&actor&&actor.GetComponent<FriendWorldActor>()?.personId==row.personId;
            var food=Food(row.foodNetworkId);result.foodFound=food;if(food)result.foodPosition=food.transform.position;
            if(source is RestaurantTable table)
            {
                result.nativeState=table.CurrentState.ToString();result.eating=table.CurrentState==RestaurantTableState.Eating||table.CurrentState==RestaurantTableState.AwaitingPayment||table.CurrentState==RestaurantTableState.Cleaning;
                if(Read(table,"_seatOrders") is IList orders&&row.seatIndex>=0&&row.seatIndex<orders.Count)
                {
                    var order=orders[row.seatIndex];
                    result.sameOrder=order!=null&&Read(order,"Order")!=null&&Read(order,"OrderId") is int id&&id==row.originalOrderId;
                }
                var tray=food?food.GetComponent<Tray>():null;
                result.placed=tray&&Read(table,"_trays") is IList trays&&trays.Contains(tray);
                var points=Read(table,"_trayPointsParent") as Transform;
                result.atNativeAnchor=food&&points&&row.seatIndex>=0&&row.seatIndex<points.childCount&&Vector3.Distance(food.transform.position,points.GetChild(row.seatIndex).position)<=.08f;
            }
            else if(source is ConveyorBeltTable conveyor)
            {
                result.nativeState=conveyor.CurrentState.ToString();result.eating=conveyor.CurrentState==ConveyorBeltTableState.Eating;
                result.placed=food&&food.transform.IsChildOf(conveyor.transform);
                result.sameOrder=conveyor.CurrentState==ConveyorBeltTableState.TakingOrder;
                var anchor=Read(conveyor,"_servedDishAnchor") as Transform;
                result.atNativeAnchor=food&&anchor&&Vector3.Distance(food.transform.position,anchor.position)<=.08f;
            }
            return result;
        }
        void Update()
        {
            var runtime=FriendsLifeRuntime.Instance;var economy=FriendsEconomyService.Instance;
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!runtime.IsWorldRunning||!economy||Time.unscaledTime<next)return;next=Time.unscaledTime+.15f;
            economy.State.sushiDeliveries=economy.State.sushiDeliveries??new List<FriendSushiDelivery>();
            foreach(var row in economy.State.sushiDeliveries.Where(r=>r.worldId==runtime.State.worldId&&r.phase!="Cancelled"&&(r.phase!="Committed"||!r.sourceFoodFinal)).ToArray())
            {
                try{Advance(runtime,economy,row);}
                catch(Exception error){row.lastFailure=LastFailure=error.GetBaseException().Message;runtime.NotifyChanged();}
            }
        }
        void Advance(FriendsLifeRuntime runtime,FriendsEconomyService economy,FriendSushiDelivery row)
        {
            var bill=economy.State.bills.FirstOrDefault(b=>b.billId==row.billId);
            if(bill?.committed==true)
            {
                economy.ReconcileDeliveredSourceFood(row.billId);
                var result=runtime.State.receipts.FirstOrDefault(r=>r.opId=="food-delivery/"+row.billId);
                bool rejected=economy.State.receipts.Any(r=>r.opId=="food-rejected/"+row.billId);
                bool changed=ApplySourceFoodAck(row,result!=null||rejected||runtime.GetPerson(row.personId)==null,
                    result?.success==true?"Accepted":rejected?"RejectedBySource":runtime.GetPerson(row.personId)==null?"TargetRemoved":"Pending");
                if(row.phase!="Committed"){row.phase="Committed";row.lastFailure=null;changed=true;}
                changed|=CloseCommittedIncomeRequest(economy.State.incomeIntents,row,bill)>0;
                if(changed)runtime.NotifyChanged();
                return;
            }
            var facts=Facts(row);
            var source=Source(row);
            bool sameLive=row.runtimeToken==runtimeToken&&source&&source.GetInstanceID()==row.sourceInstance;
            if(sameLive&&source is RestaurantTable&&facts.seed!=row.nativeSeed)sameLive=false;
            if(sameLive&&source is ConveyorBeltTable&&facts.eating&&facts.seed!=row.nativeSeed+1)sameLive=false;
            var observation=SushiDeliveryPolicy.Observe(row.phase,bill?.committed==true,!string.IsNullOrEmpty(row.nativeWitness),sameLive,
                bill!=null&&!bill.quotePending&&!bill.cancelled,facts.sourceFound,facts.sameConsumer,facts.foodFound,facts.placed,facts.sameOrder,facts.eating,facts.atNativeAnchor,Time.frameCount>row.preparedFrame);
            if(observation==SushiDeliveryObservation.Delivered&&string.IsNullOrEmpty(row.nativeWitness))
            {
                row.nativeWitness="ActualSourceEating/"+facts.nativeState+"/food="+row.foodNetworkId+"/seat="+row.seatIndex+"/source="+row.sourcePath;
                row.phase="Delivering";
            }
            if(observation==SushiDeliveryObservation.Delivered&&!string.IsNullOrEmpty(row.nativeWitness))
            {
                using(economy.DeferExternalCommit())
                {
                    if(!economy.DeliverBill(row.billId,false)){row.lastFailure="The witnessed source delivery awaits its existing reserved bill";runtime.NotifyChanged();return;}
                    row.phase="Committed";row.lastFailure=null;
                    CloseCommittedIncomeRequest(economy.State.incomeIntents,row,bill);
                    runtime.NotifyChanged();return;
                }
            }
            if(observation==SushiDeliveryObservation.Placed)
            {if(row.phase!="Placed"){row.phase="Placed";runtime.NotifyChanged();}return;}
            // A native restored order plus a real tray away from its native
            // anchor is positive evidence of an unserved/returned delivery.
            if(observation==SushiDeliveryObservation.Returned)
            {Cancel(economy,runtime,row,"The original order/tray confirms an unserved or returned delivery");return;}
            if(observation==SushiDeliveryObservation.Rejected)
            {Cancel(economy,runtime,row,"The unaccepted source order ended before any delivery permission");return;}
            if(observation==SushiDeliveryObservation.Unknown&&row.phase!="Unknown")
            {
                row.phase="Unknown";row.lastFailure="Source delivery outcome requires authoritative reconciliation; identity and reservation retained";runtime.NotifyChanged();
            }
        }
        // Pure state updates: repeated observation is a no-op. No source method,
        // wallet, reservation, job, authorization or receipt is changed here.
        static bool ApplySourceFoodAck(FriendSushiDelivery row,bool final,string result)
        {
            if(row.sourceFoodFinal==final&&String.Equals(row.sourceFoodResult,result,StringComparison.Ordinal))return false;
            row.sourceFoodFinal=final;row.sourceFoodResult=result;return true;
        }
        static int CloseCommittedIncomeRequest(List<EarnForIntent> requests,FriendSushiDelivery row,FriendBill bill)
        {
            if(requests==null||row==null||bill==null||row.phase!="Committed"||!bill.committed||!bill.delivered||bill.cancelled||
                bill.facility!="SushiRestaurant"||String.IsNullOrEmpty(row.personId)||String.IsNullOrEmpty(row.billId)||
                bill.personId!=row.personId||bill.billId!=row.billId)return 0;
            return requests.RemoveAll(i=>i.personId==row.personId&&i.intentId==row.billId);
        }
        static void Cancel(FriendsEconomyService economy,FriendsLifeRuntime runtime,FriendSushiDelivery row,string reason)
        {
            using(economy.DeferExternalCommit())
            {economy.CancelBill(row.billId);economy.State.incomeIntents.RemoveAll(i=>i.personId==row.personId&&i.intentId==row.billId);row.phase="Cancelled";row.lastFailure=reason;runtime.NotifyChanged();}
        }
        // Snapshot capture may record an already-existing native service fact.
        // It never calls GiveFood, mutates a wallet, advances a timer or saves;
        // Core.ExportJson has already been captured by the normal coordinator.
        public void CaptureNativeWitnesses()
        {
            var runtime=FriendsLifeRuntime.Instance;var economy=FriendsEconomyService.Instance;
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!economy)return;
            foreach(var row in economy.State.sushiDeliveries??new List<FriendSushiDelivery>())
            {
                if(row.worldId!=runtime.State.worldId||Terminal(row)||!string.IsNullOrEmpty(row.nativeWitness))continue;
                var source=Source(row);var facts=Facts(row);
                if(row.runtimeToken!=runtimeToken||!source||source.GetInstanceID()!=row.sourceInstance||source is RestaurantTable&&facts.seed!=row.nativeSeed||
                    source is ConveyorBeltTable&&facts.seed!=row.nativeSeed+1||!facts.sameConsumer||!facts.placed||!facts.eating)continue;
                row.nativeWitness="ActualSourceEating/"+facts.nativeState+"/food="+row.foodNetworkId+"/seat="+row.seatIndex+"/source="+row.sourcePath;row.phase="Delivering";
            }
        }
        public FriendSushiDelivery[] Observe()
        {var state=FriendsEconomyService.Instance?.State;return (state?.sushiDeliveries??new List<FriendSushiDelivery>()).Select(row=>JsonUtility.FromJson<FriendSushiDelivery>(JsonUtility.ToJson(row))).ToArray();}
        void OnDestroy(){FriendsSushiDeliveryGate.Unregister(BeforeDelivery);if(Instance==this)Instance=null;}
    }
    public sealed partial class FriendsEconomyService
    {
        public void ReconcileDeliveredSourceFood(string billId)
        {
            var bill=State.bills.FirstOrDefault(b=>b.billId==billId);
            if(!Authority||bill==null||!bill.committed||!bill.delivered||string.IsNullOrEmpty(bill.sourceFoodId)||
                runtime.State.receipts.Any(r=>r.opId=="food-delivery/"+bill.billId)||State.receipts.Any(r=>r.opId=="food-rejected/"+bill.billId))return;
            ApplyDeliveredFood(bill);
        }
    }
}
