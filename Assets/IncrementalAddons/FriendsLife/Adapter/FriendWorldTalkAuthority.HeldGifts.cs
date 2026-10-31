using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendWorldTalkAuthority
    {
        sealed class PendingHeldGift
        {
            public FriendsLifeRuntime runtime;public FriendsLifeSave save;public NetworkManager network;public FriendWorldTalkRequest row;
            public BaseHoldable item;public NetworkObject source;public string world,epoch,op,kind;public ulong owner;
            public int sourceScene,activeScene;public float deadline;public bool invalidated,wasSpawned;
        }
        readonly Dictionary<string,PendingHeldGift> heldGifts=new Dictionary<string,PendingHeldGift>(StringComparer.Ordinal);
        FriendsLifeRuntime heldRuntime;FriendsLifeSave heldSave;NetworkManager heldNetwork;string heldWorld,heldEpoch;
        bool heldSessionActive,heldSceneHooks;

        bool MaintainHeldGiftSession(FriendsLifeRuntime runtime)
        {
            var network=NetworkManager.Singleton;var state=runtime?runtime.State:null;string world=state?.worldId;
            bool same=ReferenceEquals(runtime,heldRuntime)&&ReferenceEquals(state,heldSave)&&ReferenceEquals(network,heldNetwork)&&world==heldWorld;
            bool valid=runtime&&runtime.Ready&&runtime.IsAuthority&&runtime.IsWorldRunning&&network&&network.IsListening&&network.IsServer;
            if(!same||heldSessionActive&&!valid)
            {
                InvalidateHeldGiftObservers();UnhookHeldGiftEvents();heldRuntime=runtime;heldSave=state;heldNetwork=network;heldWorld=world;heldSessionActive=false;
            }
            if(!valid)return false;
            if(!heldSessionActive)
            {
                heldEpoch=Guid.NewGuid().ToString("N");heldSessionActive=true;
                SceneManager.sceneUnloaded+=HeldGiftSceneUnloaded;SceneManager.activeSceneChanged+=HeldGiftActiveSceneChanged;heldSceneHooks=true;
                heldNetwork.OnClientDisconnectCallback+=HeldGiftOwnerDisconnected;
            }
            return true;
        }
        void InvalidateHeldGiftObservers()
        {
            // Requested rows were checkpointed before the original RPC. Losing
            // this live witness context never re-arms them after load/stop.
            heldGifts.Clear();
        }
        void UnhookHeldGiftEvents()
        {
            if(heldSceneHooks){SceneManager.sceneUnloaded-=HeldGiftSceneUnloaded;SceneManager.activeSceneChanged-=HeldGiftActiveSceneChanged;heldSceneHooks=false;}
            if(heldNetwork)heldNetwork.OnClientDisconnectCallback-=HeldGiftOwnerDisconnected;
        }
        void HeldGiftSceneUnloaded(Scene scene)
        {foreach(var pending in heldGifts.Values)if(pending.sourceScene==scene.handle||pending.activeScene==scene.handle)pending.invalidated=true;}
        void HeldGiftActiveSceneChanged(Scene before,Scene after)
        {if(before.handle!=after.handle)foreach(var pending in heldGifts.Values)pending.invalidated=true;}
        void HeldGiftOwnerDisconnected(ulong client)
        {foreach(var pending in heldGifts.Values)if(pending.owner==client)pending.invalidated=true;}
        void DisposeHeldGiftObservers(){InvalidateHeldGiftObservers();UnhookHeldGiftEvents();heldSessionActive=false;}
        void OnDisable(){DisposeHeldGiftObservers();}

        static bool OtherHeldGiftUnresolved(FriendsLifeRuntime runtime,string human,string op)
            =>runtime.State.worldTalks.Any(r=>HeldGiftRules.BlocksOther(op,HeldGiftRules.Operation(r.requestId),human,r.humanId,r.phase,r.completedAction,InputRecorded(runtime,r)));
        internal string BeginHeldGift(FriendsLifeRuntime runtime,PersonBinding friend,PersonBinding human,string kind,string itemId,string op)
        {
            if(!MaintainHeldGiftSession(runtime))return "现在不能送礼，请稍后再试。";
            if(dispatch==null||!ReferenceEquals(dispatch.runtime,runtime)||dispatch.op!=op||dispatch.human!=human.personId||dispatch.person!=friend.personId)
                return "请走到朋友身边再送礼。";
            var row=runtime.State.worldTalks.FirstOrDefault(r=>HeldGiftRules.Operation(r.requestId)==op&&r.humanId==human.personId&&r.personId==friend.personId);
            if(row==null)return "这次交谈已经结束了。";
            bool other=OtherHeldGiftUnresolved(runtime,human.personId,op);
            var from=runtime.TransformOf(human.personId);var player=from?from.GetComponent<PPG_Player>():null;
            var item=player&&player.Interactor?player.Interactor.CurrentHeld:null;
            bool exact=ulong.TryParse(itemId,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var id)&&
                item&&item.NetworkObject&&item.NetworkObject.IsSpawned&&item.NetworkObjectId==id&&item.OwnerClientId==player.OwnerClientId&&
                !item.CanHold.Value&&FriendPlayerInteractions.HeldGiftKind(item)==kind&&heldNetwork.ConnectedClients.ContainsKey(player.OwnerClientId);
            if(!HeldGiftRules.CanRequest(exact,other,row.heldGiftConsumptionRequested))
                return other?"上一份礼物还在处理中，请稍等。":row.heldGiftConsumptionRequested?"这份礼物还在处理中。":"手里的物品有变化，请重新选择。";
            var sourceScene=item.gameObject.scene;var activeScene=SceneManager.GetActiveScene();
            if(!sourceScene.isLoaded||!activeScene.isLoaded)return "现在不能送礼，请稍后再试。";
            row.heldGiftSessionId=heldEpoch;row.heldGiftObjectId=id;row.heldGiftConsumptionRequested=true;row.heldGiftConsumptionObserved=false;
            row.heldGiftCompletionSaved=false;row.heldGiftRewardAttempted=false;row.heldGiftStatus="Requested";
            // No original item mutation until the existing save owner confirms
            // this exact intent. Save failure is a definite local no-send.
            if(!runtime.TrySaveInteractionCheckpoint())
            {row.heldGiftConsumptionRequested=false;row.heldGiftStatus="IntentSaveFailed";runtime.NotifyChanged();return "还没保存好，这份礼物没有送出。";}
            var pending=new PendingHeldGift{runtime=runtime,save=runtime.State,network=heldNetwork,row=row,item=item,source=item.NetworkObject,wasSpawned=item.NetworkObject.IsSpawned,
                world=runtime.State.worldId,epoch=heldEpoch,op=op,kind=kind,owner=player.OwnerClientId,sourceScene=sourceScene.handle,activeScene=activeScene.handle,deadline=Time.unscaledTime+15f};
            heldGifts.Add(op,pending);
            // Native consumption is requested once. Its void return is NOT an
            // ACK. BaseHoldable keeps the object alive for .5s after Despawn.
            player.Interactor.ClientDestroyHeldItemRpc(new NetworkObjectReference(pending.source));
            ObserveHeldGift(runtime,pending);
            return row.phase=="Done"?row.resultMessage:"正在送出礼物，请稍等。";
        }
        void AdvanceHeldGifts(FriendsLifeRuntime runtime)
        {
            if(!MaintainHeldGiftSession(runtime))return;
            foreach(var pending in heldGifts.Values.ToArray())ObserveHeldGift(runtime,pending);
        }
        void ObserveHeldGift(FriendsLifeRuntime runtime,PendingHeldGift pending)
        {
            bool exists=pending.item&&pending.source;
            bool context=ReferenceEquals(runtime,pending.runtime)&&ReferenceEquals(runtime.State,pending.save)&&runtime.State.worldId==pending.world&&
                ReferenceEquals(NetworkManager.Singleton,pending.network)&&pending.network&&pending.network.IsListening&&pending.network.IsServer&&runtime.IsAuthority&&runtime.IsWorldRunning;
            bool scene=exists&&!pending.invalidated&&pending.item.gameObject.scene.handle==pending.sourceScene&&pending.item.gameObject.scene.isLoaded&&
                SceneManager.GetActiveScene().handle==pending.activeScene;
            var proof=HeldGiftRules.Observe(new HeldGiftObservation{requested=pending.row.heldGiftConsumptionRequested,
                epochMatches=pending.epoch==heldEpoch&&pending.row.heldGiftSessionId==heldEpoch,contextValid=context,sourceExists=exists,
                sameObject=exists&&ReferenceEquals(pending.item.NetworkObject,pending.source)&&pending.source.NetworkObjectId==pending.row.heldGiftObjectId,
                wasSpawned=pending.wasSpawned,nowSpawned=exists&&pending.source.IsSpawned,ownerMatches=exists&&pending.source.OwnerClientId==pending.owner,
                heldStateUnchanged=exists&&!pending.item.CanHold.Value,sceneValid=scene,ownerConnected=pending.network&&pending.network.IsListening&&pending.network.IsServer&&pending.network.ConnectedClients.ContainsKey(pending.owner),
                timedOut=Time.unscaledTime>pending.deadline});
            if(proof==HeldGiftProof.Wait)return;
            heldGifts.Remove(pending.op);
            if(proof!=HeldGiftProof.Witness){PreserveHeldGiftUnknown(runtime,pending,"ConsumptionNotWitnessed");return;}
            // The exact still-live object positively transitioned to unspawned
            // in this session. A null/missing/reloaded reference never gets here.
            var row=pending.row;row.heldGiftConsumptionObserved=true;row.heldGiftRewardAttempted=true;row.heldGiftStatus="Observed";
            if(!runtime.TrySaveInteractionCheckpoint()){PreserveHeldGiftUnknown(runtime,pending,"ObservedSaveFailed");return;}
            var friend=runtime.GetPerson(row.personId);var human=runtime.GetPerson(row.humanId);
            if(friend==null||human==null){PreserveHeldGiftUnknown(runtime,pending,"ParticipantMissingAfterConsumption");return;}
            try
            {
                string message=FriendPlayerInteractions.CommitGift(runtime,friend,human,pending.kind,0,pending.op,out bool accepted);
                if(!accepted){PreserveHeldGiftUnknown(runtime,pending,"RewardNotCommitted");return;}
                row.heldGiftCompletionSaved=true;row.heldGiftStatus="Committed";row.phase="Done";row.actionAccepted=true;row.resultMessage=message;
                if(!runtime.TrySaveInteractionCheckpoint())
                {row.heldGiftCompletionSaved=false;PreserveHeldGiftUnknown(runtime,pending,"CompletionSaveFailed");}
            }
            catch(Exception error){Debug.LogWarning("[FriendsLife] Held gift completion unresolved: "+error);PreserveHeldGiftUnknown(runtime,pending,"CompletionException");}
        }
        void PreserveHeldGiftUnknown(FriendsLifeRuntime runtime,PendingHeldGift pending,string reason)
        {
            if(!runtime||!ReferenceEquals(runtime.State,pending.save))return;
            pending.row.phase="Unknown";pending.row.heldGiftCompletionSaved=false;pending.row.actionAccepted=false;pending.row.resultMessage=null;pending.row.heldGiftStatus=reason;runtime.NotifyChanged();
            Debug.LogWarning("[FriendsLife] Held gift retained Unknown: "+reason+" op="+pending.op);
            // Existing periodic save may preserve diagnostics. No automatic
            // consumption, reward replay, new nonce or compensation follows.
        }
    }
}
