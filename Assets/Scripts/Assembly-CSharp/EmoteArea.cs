using System;
using System.Collections.Generic;
using System.Reflection;
using PaperPixelGames.Core;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Framework.WorldUISystem;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class EmoteArea : NetworkBehaviour, IBasicInteractable, IToolActiveInteractable
{
	[SerializeField]
	private StaticEmotes _emote;

	[SerializeField]
	private bool _allowOpenEmotesInput;

	[SerializeField]
	private Vector3 _targetPos;

	[SerializeField]
	private Vector3 _targetRot;

	[SerializeField]
	private WorldUIAnchor _worldUIAnchor;

	private NetworkVariable<bool> _someoneUsing = new NetworkVariable<bool>(value: false);
    private readonly NetworkVariable<ulong> _playerOwnerClient = new NetworkVariable<ulong>(ulong.MaxValue);
    private readonly NetworkVariable<ulong> _playerOwnerObject = new NetworkVariable<ulong>(ulong.MaxValue);
    private readonly NetworkVariable<ulong> _playerGrantEpoch = new NetworkVariable<ulong>(0);
    private readonly NetworkVariable<ulong> _playerResponseEpoch = new NetworkVariable<ulong>(0);
    // One persistent response row per real requester: client/object/epoch/value/accepted.
    // A single last-response slot would lose the winner's/loser's ACK when two
    // requests are handled before one network serialization tick.
    private readonly NetworkList<ulong> _playerResponses = new NetworkList<ulong>();
    private static readonly Dictionary<ulong,EmoteArea> localClaims = new Dictionary<ulong,EmoteArea>();
    private static readonly Dictionary<ulong,EmoteArea> serverClaims = new Dictionary<ulong,EmoteArea>();
    private static readonly FieldInfo interactorCamera = typeof(Interactor).GetField("_camera",BindingFlags.Instance|BindingFlags.NonPublic);
    private PPG_Player _localClaimPlayer;
    private ulong _localClaimClient=ulong.MaxValue;
    private bool _localPending,_localActive,_localCancelling,_localReleaseSent;
    private bool _localCustomUse,_localCustomGranted;
    private ulong _localGrantBaseline,_localResponseBaseline;
    private NetworkManager _registeredNetwork;
    // Live registry so activity code never needs a scene-wide FindObjectsByType.
    private static readonly List<EmoteArea> activeAreas = new List<EmoteArea>();
    public static IReadOnlyList<EmoteArea> ActiveAreas => activeAreas;
    // Own input colliders, resolved once (ownership never changes at runtime);
    // enabled/active state is still checked live on every query.
    private Collider[] _ownInputColliders;
    // Trusted scene configuration supplies custom interaction geometry on each
    // peer. RPC callers never supply colliders or a substitute range.
    private Collider[] _customInputColliders;

    public void ConfigureCustomPlayerInput(Collider[] colliders)
    {
        _customInputColliders=colliders==null?Array.Empty<Collider>():(Collider[])colliders.Clone();
        if(_worldUIAnchor)_worldUIAnchor.SetVisible(false);
    }
    public bool TryRequestCustomUse(Interactor interactor)
        =>_customInputColliders!=null&&TryRequestPlayerUse(interactor,true);
    public bool HasCustomUseRequest(PPG_Player player)
        =>player&&_localCustomUse&&ReferenceEquals(_localClaimPlayer,player)&&!_localCancelling&&!_localReleaseSent;
    public bool HasCustomUseGrant(PPG_Player player)
        =>HasCustomUseRequest(player)&&player.IsOwner&&isActiveAndEnabled&&IsSpawned&&base.NetworkManager&&base.NetworkManager.IsListening&&
            _localCustomGranted&&IsLocalOwner(player)&&_playerGrantEpoch.Value!=_localGrantBaseline;
    public bool HasCustomUseStarted(PPG_Player player)
        =>HasCustomUseRequest(player)&&player.IsOwner&&isActiveAndEnabled&&IsSpawned&&base.NetworkManager&&base.NetworkManager.IsListening&&
            _localActive&&IsLocalOwner(player)&&_playerGrantEpoch.Value!=_localGrantBaseline&&player.CurrentEmoteArea==this&&player.IsUsingEmoteArea;
    public bool TryStartCustomUse(PPG_Player player,StaticEmotes emote,Transform target,bool enableCameraToggle=false)
    {
        if(!HasCustomUseGrant(player))return false;
        if(!InOriginalInputRange(player)||!player.CanEnterEmoteArea||!player.TryPrepareForEmoteArea())
        {CancelLocalClaim();return false;}
        _localCustomGranted=false;_localActive=true;
        Singleton<HintsManager>.Instance?.RemoveHint("Interact");
        player.CurrentEmoteArea=this;player.StartStaticEmote(emote,target,enableCameraToggle);
        if(!player.IsUsingEmoteArea){CancelLocalClaim();return false;}
        if(_worldUIAnchor)_worldUIAnchor.SetVisible(false);
        return true;
    }
    public void CancelCustomUse(PPG_Player player)
    {
        if(player&&_localCustomUse&&ReferenceEquals(_localClaimPlayer,player))CancelLocalClaim();
        if(_customInputColliders!=null&&_worldUIAnchor)_worldUIAnchor.SetVisible(false);
    }

    [NonSerialized] string _residentReservationKey;
    public bool IsOccupied => !string.IsNullOrEmpty(_residentReservationKey)||_someoneUsing.Value;
    public Vector3 ActivityTargetPosition => _targetPos;
    public Vector3 ActivityTargetRotation => _targetRot;
    public bool ResidentReservationMatches(string ownerKey)=>!string.IsNullOrEmpty(ownerKey)&&_residentReservationKey==ownerKey;
    public bool TryReserveForResident(string ownerKey)
    {
        if(string.IsNullOrEmpty(ownerKey))return false;
        var network=NetworkManager.Singleton;if(network&&network.IsListening&&!network.IsServer)return false;
        if(_residentReservationKey==ownerKey)return true;
        if(IsOccupied)return false;_residentReservationKey=ownerKey;
        if(IsSpawned&&IsServer)_someoneUsing.Value=true;return true;
    }
    public bool ReleaseResidentReservation(string ownerKey)
    {
        if(!ResidentReservationMatches(ownerKey))return false;
        var network=NetworkManager.Singleton;if(network&&network.IsListening&&!network.IsServer)return false;
        _residentReservationKey=null;if(IsSpawned&&IsServer)_someoneUsing.Value=false;return true;
    }

	public bool CanGaze => true;

	public bool CanInteractable => true;

	public WorldUIAnchor WorldUIAnchor => _worldUIAnchor;

	public bool AllowOpenEmotesInput => _allowOpenEmotesInput;

	public void GazeEnter()
	{
		UtilityMethods.Log("[EmoteArea] Gaze Entered " + base.name);
		PPG_Player pPG_Player = Singleton<PlayersManager>.Instance?.LocalPlayer;
		if (((object)pPG_Player == null || !pPG_Player.IsUsingEmoteArea) && ((object)pPG_Player == null || pPG_Player.CanEnterEmoteArea))
		{
			Singleton<HintsManager>.Instance?.AddHint("Interact", "key_interact");
		}
	}

	public void GazeExit()
	{
		UtilityMethods.Log("[EmoteArea] Gaze Exit " + base.name);
		Singleton<HintsManager>.Instance?.RemoveHint("Interact");
	}

	public void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
	{
		TriggerInput(interactor, inputKind, phase);
	}

	public void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
	{
        if(inputKind==InputKind.Interact&&phase==InputActionPhase.Canceled)TryRequestPlayerUse(interactor,false);
    }
    private bool TryRequestPlayerUse(Interactor interactor,bool custom)
    {
        var player=interactor?interactor.Player:null;var network=base.NetworkManager;
        if(IsOccupied||!player||!player.IsOwner||!player.CanEnterEmoteArea||!IsSpawned||!network||!network.IsListening)return false;
        if(localClaims.TryGetValue(player.OwnerClientId,out var prior)&&prior)return false;
        _localClaimPlayer=player;_localClaimClient=player.OwnerClientId;_localPending=true;_localActive=_localCancelling=_localReleaseSent=false;
        _localCustomUse=custom;_localCustomGranted=false;
        _localGrantBaseline=_playerGrantEpoch.Value;_localResponseBaseline=_playerResponseEpoch.Value;localClaims[player.OwnerClientId]=this;
        // No preparation/animation/TP occurs before a real resource-owner ACK.
        ServerChangeSomeoneUsingRpc(value:true);
        return true;
	}

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();_registeredNetwork=base.NetworkManager;
        _playerResponses.OnListChanged+=OnPlayerResponseChanged;
        // The E marker follows the synced "in use" flag, so a seat taken by a
        // player or a resident shows no marker on any peer.
        _someoneUsing.OnValueChanged+=OnSomeoneUsingChanged;OnSomeoneUsingChanged(false,_someoneUsing.Value);
        if(_registeredNetwork&&IsServer)_registeredNetwork.OnClientDisconnectCallback+=OnClientDisconnected;
    }
    public override void OnNetworkDespawn()
    {
        if(_registeredNetwork)_registeredNetwork.OnClientDisconnectCallback-=OnClientDisconnected;
        _playerResponses.OnListChanged-=OnPlayerResponseChanged;
        _someoneUsing.OnValueChanged-=OnSomeoneUsingChanged;
        ulong priorOwner=_playerOwnerClient.Value;if(serverClaims.TryGetValue(priorOwner,out var priorArea)&&priorArea==this)serverClaims.Remove(priorOwner);
        ForgetLocalClaim(true);if(IsServer)ReleasePlayerOwner();base.OnNetworkDespawn();
    }
    private void OnEnable(){if(!activeAreas.Contains(this))activeAreas.Add(this);}
    private void OnDisable()
    {
        activeAreas.Remove(this);
        if(_localClaimPlayer&&IsSpawned&&base.NetworkManager&&base.NetworkManager.IsListening)CancelLocalClaim();
        else ForgetLocalClaim(true);
    }
    private void OnSomeoneUsingChanged(bool previous,bool current){if(_worldUIAnchor)_worldUIAnchor.SetVisible(_customInputColliders==null&&!current);}
    private void OnClientDisconnected(ulong clientId){if(IsServer&&_playerOwnerClient.Value==clientId)ReleasePlayerOwner();}
    private bool TryBoundPlayer(ulong sender,out PPG_Player player)
    {
        player=null;var network=base.NetworkManager;
        return network&&network.IsListening&&IsServer&&network.ConnectedClients.ContainsKey(sender)&&Singleton<PlayersManager>.Instance&&
            Singleton<PlayersManager>.Instance.TryGetPlayer(sender,out player)&&player&&player.NetworkObject&&player.NetworkObject.IsSpawned&&player.OwnerClientId==sender;
    }
    private bool InOriginalInputRange(PPG_Player player)
    {
        if(!player||!player.Interactor||interactorCamera==null)return false;
        var camera=interactorCamera.GetValue(player.Interactor)as Camera;float range=player.Interactor.InteractRange;
        if(!camera||float.IsNaN(range)||float.IsInfinity(range)||range<0)return false;
        foreach(var collider in PlayerInputColliders())
            if(IsLiveCollider(collider)&&Vector3.Distance(camera.transform.position,collider.ClosestPoint(camera.transform.position))<=range)return true;
        return false;
    }
    private bool IsOwnInputCollider(Collider collider)
        =>collider&&collider.enabled&&collider.gameObject.activeInHierarchy&&ReferenceEquals(collider.GetComponent(typeof(IBasicInteractable))??collider.GetComponentInParent(typeof(IBasicInteractable)),this);
    private static bool IsLiveCollider(Collider collider)=>collider&&collider.enabled&&collider.gameObject.activeInHierarchy;
    private Collider[] OwnInputColliders()
    {
        if(_ownInputColliders!=null)return _ownInputColliders;
        var own=new List<Collider>();
        foreach(var collider in GetComponentsInChildren<Collider>(true))
            if(collider&&ReferenceEquals(collider.GetComponent(typeof(IBasicInteractable))??collider.GetComponentInParent(typeof(IBasicInteractable)),this))own.Add(collider);
        return _ownInputColliders=own.ToArray();
    }
    private Collider[] PlayerInputColliders()=>_customInputColliders??OwnInputColliders();
    private bool IsLocalOwner(PPG_Player player)=>player&&_playerOwnerClient.Value==player.OwnerClientId&&_playerOwnerObject.Value==player.NetworkObjectId;
    private bool InBodyOrSeatRange(PPG_Player player)
    {
        if(!player||!player.Interactor)return false;float range=player.Interactor.InteractRange;
        if(float.IsNaN(range)||float.IsInfinity(range)||range<0)return false;
        if(Vector3.Distance(player.transform.position,_targetPos)<=range)return true;
        foreach(var collider in PlayerInputColliders())
            if(IsLiveCollider(collider)&&Vector3.Distance(player.transform.position,collider.ClosestPoint(player.transform.position))<=range)return true;
        return false;
    }
    private void Respond(ulong sender,ulong objectId,bool requested,bool accepted)
    {
        ulong epoch=unchecked(_playerResponseEpoch.Value+1);if(epoch==0)epoch=1;_playerResponseEpoch.Value=epoch;
        int row=-1;for(int i=0;i+4<_playerResponses.Count;i+=5)if(_playerResponses[i]==sender){row=i;break;}
        if(row<0){row=_playerResponses.Count;for(int i=0;i<5;i++)_playerResponses.Add(0);}
        _playerResponses[row]=sender;_playerResponses[row+1]=objectId;_playerResponses[row+3]=requested?1UL:0UL;_playerResponses[row+4]=accepted?1UL:0UL;
        // Commit marker last: NetworkList raises one callback per element, so
        // a new epoch must never expose the previous request's value/status.
        _playerResponses[row+2]=epoch;
    }
    private void ReceivePlayerRequest(bool value,ulong sender)
    {
        if(!IsServer||!IsSpawned)return;
        if(!TryBoundPlayer(sender,out var player)){Respond(sender,ulong.MaxValue,value,false);return;}
        ulong objectId=player.NetworkObjectId;
        if(!value)
        {
            bool owns=_playerOwnerClient.Value==sender&&_playerOwnerObject.Value==objectId;
            if(owns)ReleasePlayerOwner();Respond(sender,objectId,false,owns);return;
        }
        bool occupiedElsewhere=serverClaims.TryGetValue(sender,out var other)&&other&&other!=this;
        // CanEnterEmoteArea is owner-local (it explicitly rejects !IsOwner), so
        // do not apply it to a remote server-side player instance.
        bool allowed=!occupiedElsewhere&&!IsOccupied&&InOriginalInputRange(player)&&!player.IsTrapped&&!player.HasSurfboard&&!(player.DivingEquipment&&player.DivingEquipment.IsKnocked);
        if(allowed)
        {
            _playerOwnerClient.Value=sender;_playerOwnerObject.Value=objectId;ulong epoch=unchecked(_playerGrantEpoch.Value+1);_playerGrantEpoch.Value=epoch==0?1:epoch;
            _someoneUsing.Value=true;serverClaims[sender]=this;
        }
        Respond(sender,objectId,true,allowed);
    }
    private void ReleasePlayerOwner()
    {
        ulong owner=_playerOwnerClient.Value;if(serverClaims.TryGetValue(owner,out var area)&&area==this)serverClaims.Remove(owner);
        _playerOwnerClient.Value=ulong.MaxValue;_playerOwnerObject.Value=ulong.MaxValue;
        // An arbitrary false RPC can never release or clear an NPC token.
        _someoneUsing.Value=!string.IsNullOrEmpty(_residentReservationKey);
    }
    private bool LatestLocalResponse(out bool requested,out bool accepted)
    {
        requested=accepted=false;if(!_localClaimPlayer)return false;
        for(int i=0;i+4<_playerResponses.Count;i+=5)
            if(_playerResponses[i]==_localClaimPlayer.OwnerClientId&&_playerResponses[i+1]==_localClaimPlayer.NetworkObjectId&&_playerResponses[i+2]>_localResponseBaseline)
            {requested=_playerResponses[i+3]!=0;accepted=_playerResponses[i+4]!=0;return true;}
        return false;
    }
    private void OnPlayerResponseChanged(NetworkListEvent<ulong> change)
    {
        // Cancellation must complete even if this area was disabled while the
        // reliable request/release pair was in flight and Update cannot run.
        // While enabled, Update finishes it: on a host the release RPC runs
        // synchronously inside PPG_Player.EndStaticEmote and NetworkList raises
        // one callback per element, so acting here re-entered EndStaticEmote,
        // cleared CurrentEmoteArea mid-exit and aborted the camera/hand restore.
        if(isActiveAndEnabled)return;
        if(_localClaimPlayer&&(_localCancelling||_localReleaseSent)&&LatestLocalResponse(out bool requested,out _)&&!requested&&!IsLocalOwner(_localClaimPlayer))ForgetLocalClaim(true);
    }
    private void Update()
    {
        if(IsServer&&IsSpawned&&_playerOwnerClient.Value!=ulong.MaxValue)
        {
            if(!TryBoundPlayer(_playerOwnerClient.Value,out var owner)||owner.NetworkObjectId!=_playerOwnerObject.Value||!InBodyOrSeatRange(owner))ReleasePlayerOwner();
        }
        if(ReferenceEquals(_localClaimPlayer,null))return;if(!_localClaimPlayer){ForgetLocalClaim(false);return;}var player=_localClaimPlayer;var network=base.NetworkManager;
        if(!network||!network.IsListening||!IsSpawned||!player.IsOwner){ForgetLocalClaim(true);return;}
        bool owns=IsLocalOwner(player);bool response=LatestLocalResponse(out bool requested,out bool accepted);
        if(_localCancelling||_localReleaseSent)
        {
            if(response&&!requested&&!owns)ForgetLocalClaim(true);
            return;
        }
        if(_localPending)
        {
            if(!InOriginalInputRange(player)||!player.CanEnterEmoteArea){CancelLocalClaim();return;}
            if(owns&&_playerGrantEpoch.Value!=_localGrantBaseline)
            {
                // Custom presentation consumes the owner grant explicitly.
                // It must never run the native pose or enqueue its delayed TP.
                if(_localCustomUse){_localPending=false;_localCustomGranted=true;return;}
                if(!player.TryPrepareForEmoteArea()){CancelLocalClaim();return;}
                _localPending=false;_localActive=true;ulong grant=_playerGrantEpoch.Value;
                Singleton<HintsManager>.Instance?.RemoveHint("Interact");player.CurrentEmoteArea=this;player.StartStaticEmote(_emote);
                if(!player.IsUsingEmoteArea){CancelLocalClaim();return;}
                if(_worldUIAnchor)_worldUIAnchor.SetVisible(false);
                Singleton<DelayHelper>.Instance.DelayAction(.1f,()=>
                {
                    if(this&&isActiveAndEnabled&&_localActive&&player&&player.CurrentEmoteArea==this&&player.IsUsingEmoteArea&&IsLocalOwner(player)&&_playerGrantEpoch.Value==grant)
                        player.ServerTpPlayerRpc(_targetPos,_targetRot);
                });
            }
            else if(response&&requested&&!accepted)ForgetLocalClaim(false);
        }
        else if(_localCustomGranted)
        {
            if(!owns||!InOriginalInputRange(player)||!player.CanEnterEmoteArea)CancelLocalClaim();
        }
        else if(_localActive&&(!owns||player.CurrentEmoteArea!=this||!player.IsUsingEmoteArea))CancelLocalClaim();
    }
    private void CancelLocalClaim()
    {
        if(!_localClaimPlayer)return;_localCancelling=true;
        if(_localActive&&_localClaimPlayer.CurrentEmoteArea==this&&_localClaimPlayer.IsUsingEmoteArea)_localClaimPlayer.EndStaticEmote();
        if(!_localReleaseSent&&IsSpawned&&base.NetworkManager&&base.NetworkManager.IsListening)ServerChangeSomeoneUsingRpc(false);
    }
    private void ForgetLocalClaim(bool endEmote)
    {
        var player=_localClaimPlayer;if(player&&endEmote&&player.CurrentEmoteArea==this&&player.IsUsingEmoteArea)player.EndStaticEmote();
        if(player&&player.CurrentEmoteArea==this&&!player.IsUsingEmoteArea)player.CurrentEmoteArea=null;
        if(localClaims.TryGetValue(_localClaimClient,out var area)&&area==this)localClaims.Remove(_localClaimClient);
        _localClaimPlayer=null;_localClaimClient=ulong.MaxValue;_localPending=_localActive=_localCancelling=_localReleaseSent=false;
        _localCustomUse=_localCustomGranted=false;
        if(_worldUIAnchor&&!IsOccupied)_worldUIAnchor.SetVisible(_customInputColliders==null);
    }

	[Rpc(SendTo.Server)]
	public void ServerChangeSomeoneUsingRpc(bool value)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
        if(!IsSpawned)return;
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
            if(!value&&_localClaimPlayer)_localReleaseSent=true;
			RpcAttribute.RpcAttributeParams attributeParams = default;
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(3717163485u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 3717163485u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
            // Actual transport sender is handled by the generated ingress below.
		}
	}

	protected override void __initializeVariables()
	{
		if (_someoneUsing == null)
		{
			throw new Exception("EmoteArea._someoneUsing cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		_someoneUsing.Initialize(this);
		__nameNetworkVariable(_someoneUsing, "_someoneUsing");
		NetworkVariableFields.Add(_someoneUsing);
        _playerOwnerClient.Initialize(this);__nameNetworkVariable(_playerOwnerClient,"_playerOwnerClient");NetworkVariableFields.Add(_playerOwnerClient);
        _playerOwnerObject.Initialize(this);__nameNetworkVariable(_playerOwnerObject,"_playerOwnerObject");NetworkVariableFields.Add(_playerOwnerObject);
        _playerGrantEpoch.Initialize(this);__nameNetworkVariable(_playerGrantEpoch,"_playerGrantEpoch");NetworkVariableFields.Add(_playerGrantEpoch);
        _playerResponseEpoch.Initialize(this);__nameNetworkVariable(_playerResponseEpoch,"_playerResponseEpoch");NetworkVariableFields.Add(_playerResponseEpoch);
        _playerResponses.Initialize(this);__nameNetworkVariable(_playerResponses,"_playerResponses");NetworkVariableFields.Add(_playerResponses);
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(3717163485u, __rpc_handler_3717163485, "ServerChangeSomeoneUsingRpc", RpcInvokePermission.Everyone);
		base.__initializeRpcs();
	}

	private static void __rpc_handler_3717163485(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
            // Actual NGO RpcMessage.Handle fills Ext.Receive and SenderId from
            // the transport context before invoking this registered handler.
            if(!networkManager.IsServer)return;
            ((EmoteArea)target).ReceivePlayerRequest(value,rpcParams.Ext.Receive.SenderClientId);
		}
	}

	protected override string __getTypeName()
	{
		return "EmoteArea";
	}
}
