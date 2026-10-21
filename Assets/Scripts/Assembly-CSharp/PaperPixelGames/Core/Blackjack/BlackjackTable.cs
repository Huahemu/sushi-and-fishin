using System;
using System.Collections;
using System.Collections.Generic;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Blackjack
{
	[RequireComponent(typeof(NetworkObject))]
	public class BlackjackTable : NetworkBehaviour
	{
		public const int MaxPlayers = 1;

		public const int DealerSeatIndex = -1;

		[Header("References")]
		[SerializeField]
		private BlackjackSeat[] _seats = new BlackjackSeat[1];

		[SerializeField]
		private BlackjackTableCanvas _tableUI;

		[SerializeField]
		private BlackjackCardDisplay _cardDisplay;

		[Header("Betting")]
		[SerializeField]
		[Min(1f)]
		private int _minimumBet = 10;

		[SerializeField]
		[Min(1f)]
		private int _maximumBet = 1000;

		[SerializeField]
		[Min(1f)]
		private int _betStep = 10;

		[Header("Timing")]
		[SerializeField]
		[Min(0f)]
		private float _dealDelay = 0.35f;

		[SerializeField]
		[Min(0f)]
		private float _dealerDrawDelay = 0.55f;

		[SerializeField]
		[Min(0f)]
		private float _resultDuration = 4f;

		private readonly NetworkVariable<BlackjackTablePhase> _phase = new NetworkVariable<BlackjackTablePhase>(BlackjackTablePhase.Betting);

		private readonly NetworkVariable<int> _activeSeatIndex = new NetworkVariable<int>(-1);

		private readonly NetworkList<BlackjackPlayerState> _players = new NetworkList<BlackjackPlayerState>();

		private readonly NetworkList<BlackjackCardState> _cards = new NetworkList<BlackjackCardState>();

		private readonly List<byte> _deck = new List<byte>(52);

		private readonly Dictionary<int, List<byte>> _serverHands = new Dictionary<int, List<byte>>();

		private Coroutine _roundRoutine;

		public BlackjackTablePhase Phase => _phase.Value;

		public int ActiveSeatIndex => _activeSeatIndex.Value;

		public NetworkList<BlackjackPlayerState> Players => _players;

		public NetworkList<BlackjackCardState> Cards => _cards;

		public int MinimumBet => _minimumBet;

		public int MaximumBet => _maximumBet;

		public int BetStep => _betStep;

		private void Awake()
		{
			if (_seats == null || _seats.Length != 1)
			{
				Array.Resize(ref _seats, 1);
			}
			_minimumBet = Mathf.Max(1, _minimumBet);
			_maximumBet = Mathf.Max(_minimumBet, _maximumBet);
			_betStep = Mathf.Max(1, _betStep);
			if (_tableUI == null)
			{
				_tableUI = GetComponent<BlackjackTableCanvas>();
			}
			if (_tableUI != null)
			{
				_tableUI.Bind(this);
			}
			else
			{
				UtilityMethods.Log("[BlackjackTable] BlackjackTableCanvas is not assigned.", LogType.Warning);
			}
			if (_cardDisplay == null)
			{
				_cardDisplay = GetComponent<BlackjackCardDisplay>();
			}
			if (_cardDisplay == null)
			{
				_cardDisplay = base.gameObject.AddComponent<BlackjackCardDisplay>();
			}
			_cardDisplay.Bind(this);
			for (int i = 0; i < _seats.Length && i < 1; i++)
			{
				if (_seats[i] != null)
				{
					_seats[i].Configure(this, i);
				}
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<BlackjackTablePhase> phase = _phase;
			phase.OnValueChanged = (NetworkVariable<BlackjackTablePhase>.OnValueChangedDelegate)Delegate.Combine(phase.OnValueChanged, new NetworkVariable<BlackjackTablePhase>.OnValueChangedDelegate(OnTableStateChanged));
			NetworkVariable<int> activeSeatIndex = _activeSeatIndex;
			activeSeatIndex.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(activeSeatIndex.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnActiveSeatChanged));
			_players.OnListChanged += OnPlayersChanged;
			_cards.OnListChanged += OnCardsChanged;
			if (base.IsServer && base.NetworkManager != null)
			{
				base.NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
			}
			RefreshLocalPresentation();
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<BlackjackTablePhase> phase = _phase;
			phase.OnValueChanged = (NetworkVariable<BlackjackTablePhase>.OnValueChangedDelegate)Delegate.Remove(phase.OnValueChanged, new NetworkVariable<BlackjackTablePhase>.OnValueChangedDelegate(OnTableStateChanged));
			NetworkVariable<int> activeSeatIndex = _activeSeatIndex;
			activeSeatIndex.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(activeSeatIndex.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnActiveSeatChanged));
			_players.OnListChanged -= OnPlayersChanged;
			_cards.OnListChanged -= OnCardsChanged;
			if (base.IsServer && base.NetworkManager != null)
			{
				base.NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
			}
			if (_roundRoutine != null)
			{
				StopCoroutine(_roundRoutine);
				_roundRoutine = null;
			}
			base.OnNetworkDespawn();
		}

		public bool CanJoinSeat(int seatIndex)
		{
			if (_phase.Value == BlackjackTablePhase.Betting && seatIndex >= 0 && seatIndex < 1)
			{
				return FindPlayerIndexBySeat(seatIndex) < 0;
			}
			return false;
		}

		public bool ServerTrySeatPlayer(int seatIndex, Interactor interactor)
		{
			if (!base.IsServer || interactor == null || !CanJoinSeat(seatIndex))
			{
				return false;
			}
			if (FindPlayerIndexByClient(interactor.OwnerClientId) >= 0)
			{
				return false;
			}
			_players.Add(new BlackjackPlayerState
			{
				SeatIndex = seatIndex,
				ClientId = interactor.OwnerClientId,
				BetStored = _minimumBet * 100,
				HandValue = 0,
				HandState = BlackjackHandState.Waiting
			});
			return true;
		}

		public void ServerRemoveSeatedPlayer(int seatIndex, ulong clientId)
		{
			if (!base.IsServer)
			{
				return;
			}
			int num = FindPlayerIndexByClient(clientId);
			if (num < 0 || _players[num].SeatIndex != seatIndex)
			{
				return;
			}
			if (_phase.Value == BlackjackTablePhase.Betting)
			{
				_players.RemoveAt(num);
				return;
			}
			BlackjackPlayerState value = _players[num];
			value.HandState = BlackjackHandState.Forfeited;
			_players[num] = value;
			if (_activeSeatIndex.Value == seatIndex)
			{
				AdvancePlayerTurnServer();
			}
		}

		public void OpenLocalTable(int seatIndex)
		{
			_tableUI?.Open(seatIndex);
			RefreshLocalPresentation();
		}

		public void CloseLocalTable(int seatIndex)
		{
			_tableUI?.Close(seatIndex);
		}

		public void RequestLocalLeave(int seatIndex)
		{
			if (seatIndex >= 0 && seatIndex < _seats.Length)
			{
				_seats[seatIndex]?.RequestLocalExit();
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerSetBetRpc(int betDollars, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(966934434u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, betDollars);
				__endSendRpc(ref bufferWriter, 966934434u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_phase.Value == BlackjackTablePhase.Betting)
			{
				int num = FindPlayerIndexByClient(rpcParams.Receive.SenderClientId);
				if (num >= 0)
				{
					int value = Mathf.RoundToInt((float)betDollars / (float)_betStep) * _betStep;
					value = Mathf.Clamp(value, _minimumBet, _maximumBet);
					BlackjackPlayerState value2 = _players[num];
					value2.BetStored = value * 100;
					_players[num] = value2;
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerStartRoundRpc(RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2085832134u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2085832134u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_phase.Value == BlackjackTablePhase.Betting && _roundRoutine == null && FindPlayerIndexByClient(rpcParams.Receive.SenderClientId) >= 0 && _players.Count != 0)
			{
				int num = 0;
				for (int i = 0; i < _players.Count; i++)
				{
					num += _players[i].BetStored;
				}
				EconomyHandler economyHandler = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.EconomyHandler : null);
				if (economyHandler == null || !economyHandler.ServerTrySpendStoredMoney(num, rpcParams.Receive.SenderClientId))
				{
					ClientRoundRejectedRpc("There is not enough money in the shared balance to cover all bets.", base.RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
					return;
				}
				_phase.Value = BlackjackTablePhase.PlayerTurns;
				_roundRoutine = StartCoroutine(ServerRoundRoutine());
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerHitRpc(RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2921029869u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2921029869u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_phase.Value != BlackjackTablePhase.PlayerTurns)
			{
				return;
			}
			int num = FindPlayerIndexByClient(rpcParams.Receive.SenderClientId);
			if (num < 0)
			{
				return;
			}
			BlackjackPlayerState blackjackPlayerState = _players[num];
			if (blackjackPlayerState.SeatIndex != _activeSeatIndex.Value || blackjackPlayerState.HandState != BlackjackHandState.Playing)
			{
				return;
			}
			DealCardServer(blackjackPlayerState.SeatIndex, hidden: false);
			UpdatePlayerHandServer(num);
			blackjackPlayerState = _players[num];
			if (blackjackPlayerState.HandValue >= 21)
			{
				if (blackjackPlayerState.HandValue > 21)
				{
					blackjackPlayerState.HandState = BlackjackHandState.Bust;
					_players[num] = blackjackPlayerState;
				}
				AdvancePlayerTurnServer();
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerStandRpc(RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2241082187u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2241082187u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_phase.Value != BlackjackTablePhase.PlayerTurns)
			{
				return;
			}
			int num = FindPlayerIndexByClient(rpcParams.Receive.SenderClientId);
			if (num >= 0)
			{
				BlackjackPlayerState value = _players[num];
				if (value.SeatIndex == _activeSeatIndex.Value && value.HandState == BlackjackHandState.Playing)
				{
					value.HandState = BlackjackHandState.Standing;
					_players[num] = value;
					ClientFeedbackRpc($"You stand at {value.HandValue}.", 0, base.RpcTarget.Single(value.ClientId, RpcTargetUse.Temp));
					AdvancePlayerTurnServer();
				}
			}
		}

		private IEnumerator ServerRoundRoutine()
		{
			_cards.Clear();
			_serverHands.Clear();
			BuildAndShuffleDeckServer();
			for (int i = 0; i < _players.Count; i++)
			{
				BlackjackPlayerState value = _players[i];
				value.HandValue = 0;
				value.HandState = BlackjackHandState.Playing;
				_players[i] = value;
				_serverHands[value.SeatIndex] = new List<byte>();
			}
			_serverHands[-1] = new List<byte>();
			for (int cardRound = 0; cardRound < 2; cardRound++)
			{
				for (int j = 0; j < _players.Count; j++)
				{
					DealCardServer(_players[j].SeatIndex, hidden: false);
					yield return Wait(_dealDelay);
				}
				DealCardServer(-1, cardRound == 1);
				yield return Wait(_dealDelay);
			}
			for (int k = 0; k < _players.Count; k++)
			{
				UpdatePlayerHandServer(k);
				BlackjackPlayerState value2 = _players[k];
				if (value2.HandValue == 21 && _serverHands[value2.SeatIndex].Count == 2)
				{
					value2.HandState = BlackjackHandState.Blackjack;
					_players[k] = value2;
				}
			}
			AdvancePlayerTurnServer();
			_roundRoutine = null;
			if (_activeSeatIndex.Value < 0)
			{
				_roundRoutine = StartCoroutine(ServerDealerAndResultsRoutine());
			}
		}

		private void AdvancePlayerTurnServer()
		{
			if (!base.IsServer)
			{
				return;
			}
			int num = 0;
			if (_activeSeatIndex.Value >= 0)
			{
				int num2 = FindPlayerIndexBySeat(_activeSeatIndex.Value);
				num = ((num2 >= 0) ? (num2 + 1) : 0);
			}
			for (int i = num; i < _players.Count; i++)
			{
				BlackjackPlayerState blackjackPlayerState = _players[i];
				if (blackjackPlayerState.HandState == BlackjackHandState.Playing)
				{
					_activeSeatIndex.Value = blackjackPlayerState.SeatIndex;
					return;
				}
			}
			_activeSeatIndex.Value = -1;
			if (_roundRoutine == null)
			{
				_roundRoutine = StartCoroutine(ServerDealerAndResultsRoutine());
			}
		}

		private IEnumerator ServerDealerAndResultsRoutine()
		{
			_phase.Value = BlackjackTablePhase.DealerTurn;
			RevealDealerHoleCardServer();
			yield return Wait(_dealerDrawDelay);
			bool soft;
			while (GetHandValue(_serverHands[-1], out soft) < 17)
			{
				DealCardServer(-1, hidden: false);
				yield return Wait(_dealerDrawDelay);
			}
			ResolveRoundServer();
			_phase.Value = BlackjackTablePhase.Results;
			yield return Wait(_resultDuration);
			ResetForBettingServer();
			_roundRoutine = null;
		}

		private void ResolveRoundServer()
		{
			int handValue = GetHandValue(_serverHands[-1], out var _);
			bool flag = handValue == 21 && _serverHands[-1].Count == 2;
			bool flag2 = handValue > 21;
			EconomyHandler economyHandler = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.EconomyHandler : null);
			for (int i = 0; i < _players.Count; i++)
			{
				BlackjackPlayerState blackjackPlayerState = _players[i];
				int num = 0;
				if (blackjackPlayerState.HandState == BlackjackHandState.Forfeited)
				{
					_players[i] = blackjackPlayerState;
					continue;
				}
				bool flag3 = blackjackPlayerState.HandState == BlackjackHandState.Blackjack;
				if (blackjackPlayerState.HandState == BlackjackHandState.Bust)
				{
					blackjackPlayerState.HandState = BlackjackHandState.Lost;
				}
				else if (flag3 & flag)
				{
					blackjackPlayerState.HandState = BlackjackHandState.Push;
					num = blackjackPlayerState.BetStored;
				}
				else if (flag3)
				{
					blackjackPlayerState.HandState = BlackjackHandState.Won;
					num = blackjackPlayerState.BetStored * 5 / 2;
				}
				else if (flag)
				{
					blackjackPlayerState.HandState = BlackjackHandState.Lost;
				}
				else if (flag2 || blackjackPlayerState.HandValue > handValue)
				{
					blackjackPlayerState.HandState = BlackjackHandState.Won;
					num = blackjackPlayerState.BetStored * 2;
				}
				else if (blackjackPlayerState.HandValue == handValue)
				{
					blackjackPlayerState.HandState = BlackjackHandState.Push;
					num = blackjackPlayerState.BetStored;
				}
				else
				{
					blackjackPlayerState.HandState = BlackjackHandState.Lost;
				}
				_players[i] = blackjackPlayerState;
				if (num > 0)
				{
					economyHandler?.ServerTryEarnStoredMoney(num, blackjackPlayerState.ClientId);
				}
				SendResultFeedback(blackjackPlayerState);
			}
		}

		private void ResetForBettingServer()
		{
			for (int i = 0; i < _players.Count; i++)
			{
				ClientFeedbackRpc("Round finished.", 0, base.RpcTarget.Single(_players[i].ClientId, RpcTargetUse.Temp));
			}
			_cards.Clear();
			_serverHands.Clear();
			_activeSeatIndex.Value = -1;
			for (int num = _players.Count - 1; num >= 0; num--)
			{
				BlackjackPlayerState value = _players[num];
				if (value.HandState == BlackjackHandState.Forfeited)
				{
					_players.RemoveAt(num);
				}
				else
				{
					value.HandValue = 0;
					value.HandState = BlackjackHandState.Waiting;
					_players[num] = value;
				}
			}
			_phase.Value = BlackjackTablePhase.Betting;
		}

		private void DealCardServer(int seatIndex, bool hidden)
		{
			if (_deck.Count == 0)
			{
				BuildAndShuffleDeckServer();
			}
			byte b = _deck[_deck.Count - 1];
			_deck.RemoveAt(_deck.Count - 1);
			if (!_serverHands.TryGetValue(seatIndex, out var value))
			{
				value = new List<byte>();
				_serverHands[seatIndex] = value;
			}
			value.Add(b);
			_cards.Add(new BlackjackCardState
			{
				SeatIndex = seatIndex,
				Order = (byte)(value.Count - 1),
				Rank = (byte)(b % 13 + 1),
				Suit = (BlackjackSuit)(b / 13),
				Hidden = hidden
			});
		}

		private void RevealDealerHoleCardServer()
		{
			for (int i = 0; i < _cards.Count; i++)
			{
				BlackjackCardState value = _cards[i];
				if (value.SeatIndex == -1 && value.Hidden)
				{
					value.Hidden = false;
					_cards[i] = value;
				}
			}
		}

		private void UpdatePlayerHandServer(int playerIndex)
		{
			BlackjackPlayerState value = _players[playerIndex];
			value.HandValue = GetHandValue(_serverHands[value.SeatIndex], out var _);
			_players[playerIndex] = value;
		}

		private static int GetHandValue(List<byte> cards, out bool soft)
		{
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < cards.Count; i++)
			{
				int num3 = cards[i] % 13 + 1;
				if (num3 == 1)
				{
					num += 11;
					num2++;
				}
				else
				{
					num += Mathf.Min(num3, 10);
				}
			}
			while (num > 21 && num2 > 0)
			{
				num -= 10;
				num2--;
			}
			soft = num2 > 0;
			return num;
		}

		private void BuildAndShuffleDeckServer()
		{
			_deck.Clear();
			for (byte b = 0; b < 52; b++)
			{
				_deck.Add(b);
			}
			for (int num = _deck.Count - 1; num > 0; num--)
			{
				int num2 = UnityEngine.Random.Range(0, num + 1);
				List<byte> deck = _deck;
				int index = num;
				List<byte> deck2 = _deck;
				int index2 = num2;
				byte b2 = _deck[num2];
				byte b3 = _deck[num];
				byte b4 = (deck[index] = b2);
				b4 = (deck2[index2] = b3);
			}
		}

		private int FindPlayerIndexByClient(ulong clientId)
		{
			for (int i = 0; i < _players.Count; i++)
			{
				if (_players[i].ClientId == clientId)
				{
					return i;
				}
			}
			return -1;
		}

		private int FindPlayerIndexBySeat(int seatIndex)
		{
			for (int i = 0; i < _players.Count; i++)
			{
				if (_players[i].SeatIndex == seatIndex)
				{
					return i;
				}
			}
			return -1;
		}

		private void OnClientDisconnected(ulong clientId)
		{
			int num = FindPlayerIndexByClient(clientId);
			if (num >= 0)
			{
				BlackjackPlayerState blackjackPlayerState = _players[num];
				ServerRemoveSeatedPlayer(blackjackPlayerState.SeatIndex, clientId);
				if (blackjackPlayerState.SeatIndex >= 0 && blackjackPlayerState.SeatIndex < _seats.Length && _seats[blackjackPlayerState.SeatIndex] != null)
				{
					_seats[blackjackPlayerState.SeatIndex].InUse.Value = false;
				}
			}
		}

		private static IEnumerator Wait(float duration)
		{
			if (duration > 0f)
			{
				yield return new WaitForSeconds(duration);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientRoundRejectedRpc(string reason, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1328644984u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bool value = reason != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(reason);
				}
				__endSendRpc(ref bufferWriter, 1328644984u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_tableUI?.ShowMessage(reason);
			}
		}

		private void OnTableStateChanged(BlackjackTablePhase previous, BlackjackTablePhase current)
		{
			RefreshLocalPresentation();
		}

		private void OnActiveSeatChanged(int previous, int current)
		{
			RefreshLocalPresentation();
		}

		private void OnPlayersChanged(NetworkListEvent<BlackjackPlayerState> change)
		{
			RefreshLocalPresentation();
		}

		private void OnCardsChanged(NetworkListEvent<BlackjackCardState> change)
		{
			RefreshLocalPresentation();
		}

		private void SendResultFeedback(BlackjackPlayerState player)
		{
			string message;
			NotificationState state;
			switch (player.HandState)
			{
			case BlackjackHandState.Won:
				message = "You win!";
				state = NotificationState.Success;
				break;
			case BlackjackHandState.Push:
				message = "Push. Your bet was returned.";
				state = NotificationState.Info;
				break;
			default:
				message = "You lose.";
				state = NotificationState.Error;
				break;
			}
			ClientFeedbackRpc(message, (byte)state, base.RpcTarget.Single(player.ClientId, RpcTargetUse.Temp));
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientFeedbackRpc(string message, byte state, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3634199860u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bool value = message != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(message);
				}
				bufferWriter.WriteValueSafe(in state, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3634199860u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_tableUI?.ShowMessage(message, (NotificationState)state);
			}
		}

		private void RefreshLocalPresentation()
		{
			_tableUI?.Refresh();
			_cardDisplay?.Refresh();
		}

		protected override void __initializeVariables()
		{
			if (_phase == null)
			{
				throw new Exception("BlackjackTable._phase cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_phase.Initialize(this);
			__nameNetworkVariable(_phase, "_phase");
			NetworkVariableFields.Add(_phase);
			if (_activeSeatIndex == null)
			{
				throw new Exception("BlackjackTable._activeSeatIndex cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_activeSeatIndex.Initialize(this);
			__nameNetworkVariable(_activeSeatIndex, "_activeSeatIndex");
			NetworkVariableFields.Add(_activeSeatIndex);
			if (_players == null)
			{
				throw new Exception("BlackjackTable._players cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_players.Initialize(this);
			__nameNetworkVariable(_players, "_players");
			NetworkVariableFields.Add(_players);
			if (_cards == null)
			{
				throw new Exception("BlackjackTable._cards cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_cards.Initialize(this);
			__nameNetworkVariable(_cards, "_cards");
			NetworkVariableFields.Add(_cards);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(966934434u, __rpc_handler_966934434, "ServerSetBetRpc", RpcInvokePermission.Everyone);
			__registerRpc(2085832134u, __rpc_handler_2085832134, "ServerStartRoundRpc", RpcInvokePermission.Everyone);
			__registerRpc(2921029869u, __rpc_handler_2921029869, "ServerHitRpc", RpcInvokePermission.Everyone);
			__registerRpc(2241082187u, __rpc_handler_2241082187, "ServerStandRpc", RpcInvokePermission.Everyone);
			__registerRpc(1328644984u, __rpc_handler_1328644984, "ClientRoundRejectedRpc", RpcInvokePermission.Everyone);
			__registerRpc(3634199860u, __rpc_handler_3634199860, "ClientFeedbackRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_966934434(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				RpcParams ext = rpcParams.Ext;
				((BlackjackTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BlackjackTable)target).ServerSetBetRpc(value, ext);
				((BlackjackTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2085832134(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((BlackjackTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BlackjackTable)target).ServerStartRoundRpc(ext);
				((BlackjackTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2921029869(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((BlackjackTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BlackjackTable)target).ServerHitRpc(ext);
				((BlackjackTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2241082187(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((BlackjackTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BlackjackTable)target).ServerStandRpc(ext);
				((BlackjackTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1328644984(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				string s = null;
				if (value)
				{
					reader.ReadValueSafe(out s, false);
				}
				RpcParams ext = rpcParams.Ext;
				((BlackjackTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BlackjackTable)target).ClientRoundRejectedRpc(s, ext);
				((BlackjackTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3634199860(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				string s = null;
				if (value)
				{
					reader.ReadValueSafe(out s, false);
				}
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((BlackjackTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BlackjackTable)target).ClientFeedbackRpc(s, value2, ext);
				((BlackjackTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "BlackjackTable";
		}
	}
}
