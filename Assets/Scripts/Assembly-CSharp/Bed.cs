using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using PaperPixelGames.Core;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.DailyReport;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using Steamworks;
using Steamworks.Data;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityServiceLocator;

public class Bed : BaseInteractable
{
	[SerializeField]
	private CozyWeatherManager _cozyWeatherManager;

	[SerializeField]
	private SandCastleManager _sandCastleManager;

	[SerializeField]
	private GameObject _sleepCanvas;

	[SerializeField]
	private TextMeshProUGUI _sleeperCountText;

	[SerializeField]
	private UnityEngine.UI.Image _nightImage;

	[SerializeField]
	private GameObject _hint;

	[SerializeField]
	private GameObject _waitingOtherPlayersText;

	[SerializeField]
	private GameObject _sleepingText;

	[Header("Sleep Delay")]
	[SerializeField]
	[Min(0f)]
	private float _dailyReportDelaySeconds = 3f;

	[Header("Sleeping Players UI")]
	[SerializeField]
	private List<SleepingPlayerAvatarUI> _sleepingPlayerAvatarUis = new List<SleepingPlayerAvatarUI>();

	private const int RestaurantCloseHour = 21;

	private const int WakeUpHour = 8;

	private bool _isSleeping;

	private Coroutine _serverSleepRoutine;

	private readonly HashSet<ulong> _sleepers = new HashSet<ulong>();

	private readonly Dictionary<ulong, ulong> _clientIdToSteamId = new Dictionary<ulong, ulong>();

	private int _maxSleeperCount => NetworkManager.Singleton.ConnectedClientsIds.Count;

	private PPG_Player _player => base.NetworkManager.LocalClient.PlayerObject.GetComponent<PPG_Player>();

	protected override void Awake()
	{
		base.Awake();
		ServiceLocator.ForSceneOf(this).Register(typeof(Bed), this);
	}

	// The scene's single sleep authority (BedUI, sleeper count, end of day).
	// Residential beds call RequestSleep/CancelSleep instead of a scene search.
	public static Bed Active { get; private set; }

	private bool _externalSleep;

	// BEGIN LOCAL SLEEP PROTOCOL
	// One Try produces one Enter/Denied terminal. One Remove produces one
	// Denied closure fence, all on this same reliable Bed NetworkObject.
	// CancelAwaitOriginal has already sent Remove but still owes the first reply.
	private enum LocalSleepPhase { Idle, Pending, CancelAwaitOriginal, CancelFence, Active }
	private enum LocalSleepReply { Ignore, Enter, Denied, Canceled, Released }
	private sealed class LocalSleepRequests
	{
		public LocalSleepPhase Phase { get; private set; }
		public bool TryBegin()
		{
			if (Phase != LocalSleepPhase.Idle) return false;
			Phase = LocalSleepPhase.Pending; return true;
		}
		public bool TryCancel()
		{
			if (Phase == LocalSleepPhase.Pending)
			{ Phase = LocalSleepPhase.CancelAwaitOriginal; return true; }
			if (Phase == LocalSleepPhase.Active)
			{ Phase = LocalSleepPhase.CancelFence; return true; }
			return false;
		}
		public LocalSleepReply Enter()
		{
			if (Phase == LocalSleepPhase.Pending)
			{ Phase = LocalSleepPhase.Active; return LocalSleepReply.Enter; }
			if (Phase == LocalSleepPhase.CancelAwaitOriginal)
			{ Phase = LocalSleepPhase.CancelFence; return LocalSleepReply.Canceled; }
			return LocalSleepReply.Ignore;
		}
		public LocalSleepReply Denied()
		{
			if (Phase == LocalSleepPhase.Pending)
			{ Phase = LocalSleepPhase.Idle; return LocalSleepReply.Denied; }
			if (Phase == LocalSleepPhase.CancelAwaitOriginal)
			{ Phase = LocalSleepPhase.CancelFence; return LocalSleepReply.Canceled; }
			if (Phase == LocalSleepPhase.CancelFence)
			{ Phase = LocalSleepPhase.Idle; return LocalSleepReply.Released; }
			return LocalSleepReply.Ignore;
		}
		// Only object/connection lifetime termination may abandon pending replies.
		public void Reset() { Phase = LocalSleepPhase.Idle; }
	}
	// END LOCAL SLEEP PROTOCOL
	private readonly LocalSleepRequests _localSleepRequests = new LocalSleepRequests();

	public bool IsLocalSleeping => _isSleeping;

	public void RequestSleep()
	{
		TryRequestSleep(external: true);
	}

	private void TryRequestSleep(bool external)
	{
		var network = base.NetworkManager;
		if (!base.IsSpawned || !network || !network.IsListening || !network.IsClient || _isSleeping || !_localSleepRequests.TryBegin())
		{
			return;
		}
		_externalSleep = external;
		// Pending must exist before send: a host can receive Enter in this stack.
		ServerTrySleepRpc(network.LocalClientId, SteamClient.IsValid ? SteamClient.SteamId.Value : 0);
	}

	public void CancelSleep()
	{
		// This also covers a Try whose Enter/Denied has not arrived yet.
		// Duplicate cancel/ForceWake cannot enqueue a third closure fence.
		bool sendRemove = _localSleepRequests.TryCancel();
		if (_isSleeping) ExitSleepLocal();
		if (!sendRemove) return;
		var network = base.NetworkManager;
		if (!base.IsSpawned || !network || !network.IsListening)
		{
			_localSleepRequests.Reset(); _externalSleep = false; return;
		}
		// Cancel state is recorded before send for host synchronous closure.
		ServerRemoveSleeperRpc(network.LocalClientId);
	}

	public override void OnNetworkDespawn()
	{
		_localSleepRequests.Reset();
		if (_isSleeping) ExitSleepLocal();
		_externalSleep = false;
		if (Active == this)
		{
			Active = null;
		}
		base.OnNetworkDespawn();
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		_localSleepRequests.Reset(); _isSleeping = false; _externalSleep = false;
		Active = this;
		if (_sleepCanvas != null)
		{
			_sleepCanvas.SetActive(value: false);
		}
		SetBedHintVisibleLocal(visible: true);
		HideAllSleepingPlayerAvatarUis();
		ClientUpdateSleeperTextRpc(0);
	}

	protected override void ClientMouse1(Interactor interactor, InputActionPhase phase)
	{
		base.ClientMouse1(interactor, phase);
		if (phase == InputActionPhase.Started && interactor.Player.FirstPersonCharacter.IsGrounded())
		{
			if (_isSleeping || _localSleepRequests.Phase != LocalSleepPhase.Idle)
			{
				CancelSleep(); return;
			}
			TryRequestSleep(external: false);
		}
	}

	[Rpc(SendTo.Server)]
	private void ServerTrySleepRpc(ulong clientId, ulong steamIdValue)
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
			FastBufferWriter bufferWriter = __beginSendRpc(4182513259u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, clientId);
			BytePacker.WriteValueBitPacked(bufferWriter, steamIdValue);
			__endSendRpc(ref bufferWriter, 4182513259u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		RegisterClientSteamId(clientId, steamIdValue);
		int hours = _cozyWeatherManager.CurrentTime.hours;
		if (hours >= 8 && hours < 21)
		{
			ClientSleepDeniedRpc(clientId);
		}
		else
		{
			bool added = _sleepers.Add(clientId);
			// Even an idempotent Try needs exactly one terminal reply.
			ClientEnterSleepRpc(clientId);
			if (added)
			{
				ClientUpdateSleeperTextRpc(_sleepers.Count);
				ServerRefreshSleepingPlayersUi();
				if (_sleepers.Count == _maxSleeperCount)
				{
					HandleEveryoneSleeping();
				}
			}
		}
	}

	[Rpc(SendTo.Server)]
	private void ServerRemoveSleeperRpc(ulong clientId)
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
			FastBufferWriter bufferWriter = __beginSendRpc(4262633349u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, clientId);
			__endSendRpc(ref bufferWriter, 4262633349u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_sleepers.Remove(clientId))
			{
				ClientUpdateSleeperTextRpc(_sleepers.Count);
				ServerRefreshSleepingPlayersUi();
				CancelServerSleepRoutineIfAny();
			}
			// Requester-specific closure, even if Try was denied or already removed.
			// The local cancellation phase consumes this without a denial toast.
			ClientSleepDeniedRpc(clientId);
		}
	}

	private void RegisterClientSteamId(ulong clientId, ulong steamIdValue)
	{
		if (steamIdValue != 0L)
		{
			_clientIdToSteamId[clientId] = steamIdValue;
		}
	}

	private void HandleEveryoneSleeping()
	{
		CancelServerSleepRoutineIfAny();
		_serverSleepRoutine = Singleton<DelayHelper>.Instance.DelayAction(_dailyReportDelaySeconds, () =>
		{
			_serverSleepRoutine = null;
			if (base.IsServer && _sleepers.Count == _maxSleeperCount)
			{
				EndDayReset();
				ClientSetBedHintVisibleRpc(visible: false);
				_sandCastleManager.ResetAllSandCastlesRpc();
				ClientFadeNightImageRpc(0f, 2f);
				ClientResetResidentsRpc();
				Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
				ServiceLocator.ForSceneOf(this).Get<DailyReportBroadcaster>(out var service);
				service.ServerBuildPayloadAndPublish();
			}
		});
	}

	public void HandleWakeUp()
	{
		if (base.IsServer)
		{
			CancelServerSleepRoutineIfAny();
			if (_cozyWeatherManager.CurrentTime.hours >= 8)
			{
				_cozyWeatherManager.ServerRequestAddDayRpc(1);
			}
			_cozyWeatherManager.ServerRequestChangeTimeRpc(8, 0, checkFishSpawn: true);
			_sleepers.Clear();
			ClientUpdateSleeperTextRpc(0);
			ClientClearSleepingPlayersUiRpc();
			ClientSetBedHintVisibleRpc(visible: true);
			ClientForceWakeRpc();
			ClientFadeNightImageRpc(1f, 2f);
			Singleton<GameProgressManager>.Instance.RestaurantManager.ResetTrolleys();
			ServiceLocator.ForSceneOf(this).Get<DailyReportBroadcaster>(out var service);
			service.ServerResetDailyReport();
			QuestReferenceHub.Instance.QuestComplete(QuestId.Sleep);
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientFadeNightImageRpc(float targetAlpha, float duration)
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
			FastBufferWriter bufferWriter = __beginSendRpc(2111747373u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in targetAlpha, default(FastBufferWriter.ForPrimitives));
			bufferWriter.WriteValueSafe(in duration, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 2111747373u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			FadeNightImageLocal(targetAlpha, duration);
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientSetBedHintVisibleRpc(bool visible)
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
			FastBufferWriter bufferWriter = __beginSendRpc(3313591247u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in visible, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 3313591247u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			SetBedHintVisibleLocal(visible);
		}
	}

	private void FadeNightImageLocal(float targetAlpha, float duration)
	{
		if (_nightImage == null)
		{
			return;
		}
		_nightImage.gameObject.SetActive(value: true);
		_nightImage.DOKill();
		if (duration <= 0f)
		{
			SetNightImageAlphaInstantLocal(targetAlpha);
			return;
		}
		_nightImage.DOFade(targetAlpha, duration).SetEase(Ease.Linear).OnComplete(() =>
		{
			if (Mathf.Approximately(targetAlpha, 0f))
			{
				_nightImage.gameObject.SetActive(value: false);
			}
		});
	}

	private void SetNightImageAlphaInstantLocal(float alpha)
	{
		if (!(_nightImage == null))
		{
			_nightImage.DOKill();
			UnityEngine.Color color = _nightImage.color;
			color.a = alpha;
			_nightImage.color = color;
			_nightImage.gameObject.SetActive(alpha > 0.001f);
		}
	}

	private void SetBedHintVisibleLocal(bool visible)
	{
		if (_hint != null)
		{
			_hint.SetActive(visible);
		}
	}

	private void CancelServerSleepRoutineIfAny()
	{
		if (_serverSleepRoutine != null)
		{
			Singleton<DelayHelper>.Instance.StopCoroutine(_serverSleepRoutine);
			_serverSleepRoutine = null;
		}
	}

	private void ServerRefreshSleepingPlayersUi()
	{
		ulong[] orderedLobbySteamIds = GetOrderedLobbySteamIds();
		ulong[] sleepingSteamIds = GetSleepingSteamIds();
		ClientRefreshSleepingPlayersUiRpc(orderedLobbySteamIds, sleepingSteamIds);
	}

	private ulong[] GetOrderedLobbySteamIds()
	{
		Lobby lobby = ((Singleton<MultiplayerCoordinator>.Instance != null) ? Singleton<MultiplayerCoordinator>.Instance.LobbyService.CurrentLobby : default(Lobby));
		if (lobby.Id.Value != 0L)
		{
			try
			{
				return (from member in lobby.Members
					orderby member.Id.Value
					select member.Id.Value).ToArray();
			}
			catch
			{
			}
		}
		return (from pair in _clientIdToSteamId
			orderby pair.Key
			select pair.Value into steamId
			where steamId != 0
			select steamId).Distinct().ToArray();
	}

	private ulong[] GetSleepingSteamIds()
	{
		return (from clientId in _sleepers
			where _clientIdToSteamId.ContainsKey(clientId)
			select _clientIdToSteamId[clientId] into steamId
			where steamId != 0
			select steamId).Distinct().ToArray();
	}

	[Rpc(SendTo.Everyone)]
	private void ClientRefreshSleepingPlayersUiRpc(ulong[] playerSteamIds, ulong[] sleepingSteamIds)
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
			FastBufferWriter bufferWriter = __beginSendRpc(1233277364u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			bool value = playerSteamIds != null;
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			if (value)
			{
				bufferWriter.WriteValueSafe(playerSteamIds, default(FastBufferWriter.ForPrimitives));
			}
			bool value2 = sleepingSteamIds != null;
			bufferWriter.WriteValueSafe(in value2, default(FastBufferWriter.ForPrimitives));
			if (value2)
			{
				bufferWriter.WriteValueSafe(sleepingSteamIds, default(FastBufferWriter.ForPrimitives));
			}
			__endSendRpc(ref bufferWriter, 1233277364u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			RefreshSleepingPlayersUi(playerSteamIds, sleepingSteamIds);
		}
	}

	private void RefreshSleepingPlayersUi(ulong[] playerSteamIds, ulong[] sleepingSteamIds)
	{
		HideAllSleepingPlayerAvatarUis();
		if (playerSteamIds == null || playerSteamIds.Length == 0)
		{
			return;
		}
		HashSet<ulong> hashSet = ((sleepingSteamIds != null) ? new HashSet<ulong>(sleepingSteamIds) : new HashSet<ulong>());
		int num = Mathf.Min(playerSteamIds.Length, _sleepingPlayerAvatarUis.Count);
		for (int i = 0; i < num; i++)
		{
			SleepingPlayerAvatarUI sleepingPlayerAvatarUI = _sleepingPlayerAvatarUis[i];
			if (!(sleepingPlayerAvatarUI == null))
			{
				ulong num2 = playerSteamIds[i];
				sleepingPlayerAvatarUI.SetRootVisible(isVisible: true);
				sleepingPlayerAvatarUI.SetSteamId(num2);
				sleepingPlayerAvatarUI.SetZzImageVisible(hashSet.Contains(num2));
			}
		}
	}

	private void HideAllSleepingPlayerAvatarUis()
	{
		if (_sleepingPlayerAvatarUis == null)
		{
			return;
		}
		for (int i = 0; i < _sleepingPlayerAvatarUis.Count; i++)
		{
			SleepingPlayerAvatarUI sleepingPlayerAvatarUI = _sleepingPlayerAvatarUis[i];
			if (!(sleepingPlayerAvatarUI == null))
			{
				sleepingPlayerAvatarUI.SetRootVisible(isVisible: false);
			}
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientResetResidentsRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(1366391955u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 1366391955u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			Singleton<ResidentManager>.Instance.TeleportAllResidents();
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientEnterSleepRpc(ulong clientId)
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
			FastBufferWriter bufferWriter = __beginSendRpc(553720815u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, clientId);
			__endSendRpc(ref bufferWriter, 553720815u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (NetworkManager.Singleton.LocalClientId == clientId &&
				_localSleepRequests.Enter() == LocalSleepReply.Enter)
			{
				EnterSleepLocal();
			}
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientForceWakeRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(1287005648u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 1287005648u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			// Wake clears presentation and intent, but preserves reply drainage.
			// Resetting Pending here could let a late Enter match a new request.
			CancelSleep();
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientClearSleepingPlayersUiRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(2718470156u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 2718470156u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			HideAllSleepingPlayerAvatarUis();
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientSleepDeniedRpc(ulong clientId)
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
			FastBufferWriter bufferWriter = __beginSendRpc(1017094376u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, clientId);
			__endSendRpc(ref bufferWriter, 1017094376u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (NetworkManager.Singleton.LocalClientId == clientId)
			{
				var reply = _localSleepRequests.Denied();
				if (reply == LocalSleepReply.Denied)
				{
					_externalSleep = false;
					Singleton<NotifyManager>.Instance.ShowToast("You can sleep after 21:00");
				}
				else if (reply == LocalSleepReply.Released) _externalSleep = false;
			}
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientUpdateSleeperTextRpc(int sleeperCount)
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
			FastBufferWriter bufferWriter = __beginSendRpc(3875777215u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, sleeperCount);
			__endSendRpc(ref bufferWriter, 3875777215u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_sleeperCountText != null)
			{
				_sleeperCountText.text = sleeperCount + " / " + _maxSleeperCount;
			}
			if (sleeperCount == _maxSleeperCount)
			{
				_sleepingText.SetActive(value: true);
				_waitingOtherPlayersText.SetActive(value: false);
			}
			else
			{
				_sleepingText.SetActive(value: false);
				_waitingOtherPlayersText.SetActive(value: true);
			}
		}
	}

	private void EnterSleepLocal()
	{
		_isSleeping = true;
		if (_sleepCanvas != null)
		{
			_sleepCanvas.SetActive(value: true);
		}
		Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
		_player.PauseCharacter();
	}

	private void ExitSleepLocal()
	{
		_isSleeping = false;
		if (_sleepCanvas != null)
		{
			_sleepCanvas.SetActive(value: false);
		}
		var network = base.NetworkManager;
		var playerObject = network ? network.LocalClient?.PlayerObject : null;
		var player = playerObject ? playerObject.GetComponent<PPG_Player>() : null;
		if (player) player.ResumeCharacter();
		if (_externalSleep)
		{
			_externalSleep = false;
			return;
		}
		Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_use");
	}

	private void EndDayReset()
	{
		Singleton<PlayersManager>.Instance.ServerResetPlayersForEndDay();
		Singleton<GameProgressManager>.Instance.RestaurantManager.ServerResetFridgesForEndDay();
		Singleton<GameProgressManager>.Instance.RestaurantManager.ServerResetPrepStationsForEndDay();
		Singleton<GameProgressManager>.Instance.RestaurantManager.ServerResetChefDesksForEndDay();
		Singleton<GameProgressManager>.Instance.RestaurantManager.ServerResetRestaurantTablesForEndDay();
		Singleton<GameProgressManager>.Instance.RestaurantManager.ServerResetConveyorTablesForEndDay();
		Singleton<GameProgressManager>.Instance.RestaurantManager.ServerResetConveyorBeltForEndDay();
		Singleton<GameProgressManager>.Instance.RestaurantManager.ServerResetBeachBallsForEndDay();
		Singleton<GameProgressManager>.Instance.RestaurantManager.FilletMachine.ServerEndDayCleanup();
		if (DestructibleManager.HasInstance)
		{
			Singleton<DestructibleManager>.Instance.RespawnObjects();
		}
		Singleton<SeaCreatureManager>.Instance.EndDayReset();
	}

	public override void EnableGazeHints()
	{
		Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_sleep");
	}

	public override void DisableGazeHints()
	{
		Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
	}

	protected override void __initializeVariables()
	{
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(4182513259u, __rpc_handler_4182513259, "ServerTrySleepRpc", RpcInvokePermission.Everyone);
		__registerRpc(4262633349u, __rpc_handler_4262633349, "ServerRemoveSleeperRpc", RpcInvokePermission.Everyone);
		__registerRpc(2111747373u, __rpc_handler_2111747373, "ClientFadeNightImageRpc", RpcInvokePermission.Everyone);
		__registerRpc(3313591247u, __rpc_handler_3313591247, "ClientSetBedHintVisibleRpc", RpcInvokePermission.Everyone);
		__registerRpc(1233277364u, __rpc_handler_1233277364, "ClientRefreshSleepingPlayersUiRpc", RpcInvokePermission.Everyone);
		__registerRpc(1366391955u, __rpc_handler_1366391955, "ClientResetResidentsRpc", RpcInvokePermission.Everyone);
		__registerRpc(553720815u, __rpc_handler_553720815, "ClientEnterSleepRpc", RpcInvokePermission.Everyone);
		__registerRpc(1287005648u, __rpc_handler_1287005648, "ClientForceWakeRpc", RpcInvokePermission.Everyone);
		__registerRpc(2718470156u, __rpc_handler_2718470156, "ClientClearSleepingPlayersUiRpc", RpcInvokePermission.Everyone);
		__registerRpc(1017094376u, __rpc_handler_1017094376, "ClientSleepDeniedRpc", RpcInvokePermission.Everyone);
		__registerRpc(3875777215u, __rpc_handler_3875777215, "ClientUpdateSleeperTextRpc", RpcInvokePermission.Everyone);
		base.__initializeRpcs();
	}

	private static void __rpc_handler_4182513259(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((Bed)target).ServerTrySleepRpc(value, value2);
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_4262633349(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((Bed)target).ServerRemoveSleeperRpc(value);
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2111747373(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out float value, default(FastBufferWriter.ForPrimitives));
			reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((Bed)target).ClientFadeNightImageRpc(value, value2);
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3313591247(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((Bed)target).ClientSetBedHintVisibleRpc(value);
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1233277364(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			ulong[] value2 = null;
			if (value)
			{
				reader.ReadValueSafe(out value2, default(FastBufferWriter.ForPrimitives));
			}
			reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
			ulong[] value4 = null;
			if (value3)
			{
				reader.ReadValueSafe(out value4, default(FastBufferWriter.ForPrimitives));
			}
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((Bed)target).ClientRefreshSleepingPlayersUiRpc(value2, value4);
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1366391955(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((Bed)target).ClientResetResidentsRpc();
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_553720815(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((Bed)target).ClientEnterSleepRpc(value);
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1287005648(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((Bed)target).ClientForceWakeRpc();
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2718470156(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((Bed)target).ClientClearSleepingPlayersUiRpc();
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1017094376(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((Bed)target).ClientSleepDeniedRpc(value);
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3875777215(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out int value);
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((Bed)target).ClientUpdateSleeperTextRpc(value);
			((Bed)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected override string __getTypeName()
	{
		return "Bed";
	}
}
