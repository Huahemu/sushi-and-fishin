using System;
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

    public bool IsOccupied => _someoneUsing.Value;

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
		if (!_someoneUsing.Value && inputKind == InputKind.Interact && phase == InputActionPhase.Canceled && interactor.Player.TryPrepareForEmoteArea())
		{
			Singleton<HintsManager>.Instance?.RemoveHint("Interact");
			UtilityMethods.Log($"Interactor {interactor}");
			interactor.Player.CurrentEmoteArea = this;
			ServerChangeSomeoneUsingRpc(value: true);
			interactor.Player.StartStaticEmote(_emote);
			Singleton<DelayHelper>.Instance.DelayAction(0.1f, () =>
			{
				interactor.Player.ServerTpPlayerRpc(_targetPos, _targetRot);
			});
			_worldUIAnchor.SetVisible(value: false);
		}
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
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default;
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(3717163485u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 3717163485u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			_someoneUsing.Value = value;
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
			((EmoteArea)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((EmoteArea)target).ServerChangeSomeoneUsingRpc(value);
			((EmoteArea)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected override string __getTypeName()
	{
		return "EmoteArea";
	}
}
