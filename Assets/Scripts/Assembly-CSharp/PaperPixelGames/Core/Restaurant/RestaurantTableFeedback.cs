using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Networking.Chat;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Restaurant
{
	public sealed class RestaurantTableFeedback : MonoBehaviour
	{
		private const int SpeakerSeatIndex = 0;

		private const double ReminderIntervalSeconds = 15.0;

		private const double AmbientMinIntervalSeconds = 10.0;

		private const double AmbientMaxIntervalSeconds = 18.0;

		private const double AmbientReplyMinDelaySeconds = 1.5;

		private const double AmbientReplyMaxDelaySeconds = 2.5;

		private const double GlobalAmbientSpacingSeconds = 3.0;

		private const float AmbientReplyChance = 0.35f;

		private RestaurantTableState _reminderState;

		private double _nextReminderServerTime;

		private bool _reminderActive;

		private RestaurantTableState _ambientState;

		private double _nextAmbientServerTime;

		private double _pendingReplyServerTime;

		private int _pendingReplySeatIndex = -1;

		private int _lastAmbientSpeakerSeatIndex = -1;

		private bool _ambientActive;

		private bool _pendingReply;

		private static double s_nextGlobalAmbientServerTime;

		public void ServerCustomersAssigned(NetworkBehaviour source, int tableId, int customerCount)
		{
			Broadcast(source, (customerCount == 1) ? "chat.restaurant.customers_assigned_single" : "chat.restaurant.customers_assigned_plural", (customerCount == 1) ? "Table {0}: {1} customer assigned; waiting for arrival." : "Table {0}: {1} customers assigned; waiting for arrival.", RestaurantChatTone.Info, tableId.ToString(), customerCount.ToString());
		}

		public void ServerReadyToOrder(NetworkBehaviour source, int tableId)
		{
			Broadcast(source, "chat.restaurant.ready_to_order", "Table {0} is ready to order.", RestaurantChatTone.Warning, tableId.ToString());
		}

		public void ServerOrderTaken(NetworkBehaviour source, int tableId, string playerName)
		{
			Broadcast(source, "chat.restaurant.order_taken", "{0} took the order from Table {1}.", RestaurantChatTone.Info, ResolvePlayerName(playerName), tableId.ToString());
		}

		public void ServerOrderServed(NetworkBehaviour source, int tableId, int seatIndex, OrderIssueFlags issues)
		{
			bool flag = (issues & OrderIssueFlags.SideMissing) != 0;
			bool flag2 = (issues & OrderIssueFlags.BeverageMissing) != 0;
			var (text, text2) = GetDeliveryResult(flag, flag2);
			Broadcast(source, "chat.restaurant.order_served", "Table {0} • Seat {1}: {2}", (!(flag | flag2)) ? RestaurantChatTone.Success : RestaurantChatTone.Warning, tableId.ToString(), (seatIndex + 1).ToString(), "@" + text + "|" + text2);
		}

		public void ServerReadyForPayment(NetworkBehaviour source, int tableId)
		{
			Broadcast(source, "chat.restaurant.ready_for_payment", "Table {0} is ready for payment.", RestaurantChatTone.Warning, tableId.ToString());
		}

		public void ServerPaymentCollected(NetworkBehaviour source, int tableId, string playerName, int amount)
		{
			Broadcast(source, "chat.restaurant.payment_collected", "{0} collected ${1} from Table {2}.", RestaurantChatTone.Success, ResolvePlayerName(playerName), Mathf.Max(0, amount).ToString(), tableId.ToString());
		}

		public void ServerBeginReminder(RestaurantTableState state, double serverTime)
		{
			if (!IsReminderState(state))
			{
				ServerStopReminder();
				return;
			}
			_reminderState = state;
			_nextReminderServerTime = serverTime + 15.0;
			_reminderActive = true;
		}

		public void ServerStopReminder(RestaurantTableState expectedState)
		{
			if (_reminderActive && _reminderState == expectedState)
			{
				ServerStopReminder();
			}
		}

		public void ServerTickReminder(NetworkBehaviour source, int tableId, RestaurantTableState currentState, double serverTime)
		{
			if (!_reminderActive)
			{
				return;
			}
			if (currentState != _reminderState || !IsReminderState(currentState))
			{
				ServerStopReminder();
			}
			else if (!(serverTime < _nextReminderServerTime))
			{
				string text = currentState switch
				{
					RestaurantTableState.TakingOrder => "chat.restaurant.ready_to_order", 
					RestaurantTableState.DeliveringOrder => "chat.restaurant.waiting_for_order", 
					RestaurantTableState.AwaitingPayment => "chat.restaurant.ready_for_payment", 
					_ => string.Empty, 
				};
				string fallback = currentState switch
				{
					RestaurantTableState.TakingOrder => "Table {0} is ready to order.", 
					RestaurantTableState.DeliveringOrder => "Table {0} is waiting for its order.", 
					RestaurantTableState.AwaitingPayment => "Table {0} is ready for payment.", 
					_ => string.Empty, 
				};
				if (!string.IsNullOrEmpty(text))
				{
					Broadcast(source, text, fallback, RestaurantChatTone.Warning, tableId.ToString());
				}
				_nextReminderServerTime = serverTime + 15.0;
			}
		}

		public void PlayClientCue(int tableId, RestaurantFeedbackCue cue)
		{
			Singleton<ResidentManager>.Instance?.PlayRestaurantFeedbackCue(tableId, 0, cue);
		}

		public void PlayClientMoment(int tableId, int speakerSeatIndex, RestaurantFeedbackCue cue, RestaurantResidentGesture gesture)
		{
			Singleton<ResidentManager>.Instance?.PlayRestaurantMoment(tableId, speakerSeatIndex, cue, gesture);
		}

		public bool ServerTryGetAmbientMoment(RestaurantTableState currentState, int customerCount, double serverTime, out int speakerSeatIndex, out RestaurantFeedbackCue cue, out RestaurantResidentGesture gesture)
		{
			speakerSeatIndex = -1;
			cue = RestaurantFeedbackCue.RequestOrder;
			gesture = RestaurantResidentGesture.None;
			if (!IsAmbientState(currentState) || customerCount <= 0)
			{
				ServerStopAmbient();
				return false;
			}
			if (currentState == RestaurantTableState.DeliveringOrder && customerCount == 1)
			{
				ServerStopAmbient();
				return false;
			}
			if (!_ambientActive || _ambientState != currentState)
			{
				ServerStartAmbient(currentState, serverTime);
				return false;
			}
			if (_pendingReply && serverTime >= _pendingReplyServerTime)
			{
				if (!TryReserveGlobalAmbientSlot(serverTime))
				{
					return false;
				}
				speakerSeatIndex = Mathf.Clamp(_pendingReplySeatIndex, 0, customerCount - 1);
				cue = RestaurantFeedbackCue.WaitingChatterReply;
				gesture = ((currentState == RestaurantTableState.DeliveringOrder) ? RestaurantResidentGesture.BeforeMealChat : RestaurantResidentGesture.None);
				_pendingReply = false;
				_pendingReplySeatIndex = -1;
				_pendingReplyServerTime = 0.0;
				_lastAmbientSpeakerSeatIndex = speakerSeatIndex;
				return true;
			}
			if (serverTime < _nextAmbientServerTime || !TryReserveGlobalAmbientSlot(serverTime))
			{
				return false;
			}
			speakerSeatIndex = ChooseSpeaker(customerCount, _lastAmbientSpeakerSeatIndex);
			cue = ((currentState == RestaurantTableState.DeliveringOrder) ? RestaurantFeedbackCue.WaitingChatter : RestaurantFeedbackCue.EatingChatter);
			gesture = ((currentState == RestaurantTableState.DeliveringOrder) ? ((Random.value < 0.75f) ? RestaurantResidentGesture.BeforeMealChat : RestaurantResidentGesture.TalkPoint) : RestaurantResidentGesture.None);
			_lastAmbientSpeakerSeatIndex = speakerSeatIndex;
			_nextAmbientServerTime = serverTime + (double)Random.Range(10f, 18f);
			if (customerCount > 1 && Random.value < 0.35f)
			{
				_pendingReply = true;
				_pendingReplySeatIndex = ChooseSpeaker(customerCount, speakerSeatIndex);
				_pendingReplyServerTime = serverTime + (double)Random.Range(1.5f, 2.5f);
			}
			return true;
		}

		private void ServerStopReminder()
		{
			_reminderActive = false;
			_reminderState = RestaurantTableState.Empty;
			_nextReminderServerTime = 0.0;
		}

		private void ServerStartAmbient(RestaurantTableState state, double serverTime)
		{
			_ambientState = state;
			_ambientActive = true;
			_pendingReply = false;
			_pendingReplySeatIndex = -1;
			_pendingReplyServerTime = 0.0;
			_nextAmbientServerTime = serverTime + (double)Random.Range(10f, 18f);
		}

		private void ServerStopAmbient()
		{
			_ambientActive = false;
			_ambientState = RestaurantTableState.Empty;
			_nextAmbientServerTime = 0.0;
			_pendingReply = false;
			_pendingReplySeatIndex = -1;
			_pendingReplyServerTime = 0.0;
			_lastAmbientSpeakerSeatIndex = -1;
		}

		private static bool IsAmbientState(RestaurantTableState state)
		{
			if (state != RestaurantTableState.DeliveringOrder)
			{
				return state == RestaurantTableState.Eating;
			}
			return true;
		}

		private static int ChooseSpeaker(int customerCount, int excludedSeatIndex)
		{
			int num = Mathf.Max(1, customerCount);
			if (num == 1)
			{
				return 0;
			}
			int num2 = Random.Range(0, num - 1);
			if (num2 >= excludedSeatIndex && excludedSeatIndex >= 0 && excludedSeatIndex < num)
			{
				num2++;
			}
			return Mathf.Clamp(num2, 0, num - 1);
		}

		private static bool TryReserveGlobalAmbientSlot(double serverTime)
		{
			if (serverTime + 60.0 < s_nextGlobalAmbientServerTime)
			{
				s_nextGlobalAmbientServerTime = 0.0;
			}
			if (serverTime < s_nextGlobalAmbientServerTime)
			{
				return false;
			}
			s_nextGlobalAmbientServerTime = serverTime + 3.0;
			return true;
		}

		private static bool IsReminderState(RestaurantTableState state)
		{
			if (state != RestaurantTableState.TakingOrder && state != RestaurantTableState.DeliveringOrder)
			{
				return state == RestaurantTableState.AwaitingPayment;
			}
			return true;
		}

		private static void Broadcast(NetworkBehaviour source, string key, string fallback, RestaurantChatTone tone, params string[] arguments)
		{
			GlobalChatManager.BroadcastLocalizedRestaurantFromServer(source, key, fallback, tone, arguments);
		}

		private static string ResolvePlayerName(string playerName)
		{
			if (!string.IsNullOrWhiteSpace(playerName))
			{
				return playerName.Trim();
			}
			return "Player";
		}

		private static (string Key, string Fallback) GetDeliveryResult(bool sideMissing, bool beverageMissing)
		{
			if (sideMissing & beverageMissing)
			{
				return (Key: "chat.restaurant.delivery.missing_side_and_beverage", Fallback: "Missing side & beverage");
			}
			if (sideMissing)
			{
				return (Key: "chat.restaurant.delivery.missing_side", Fallback: "Missing side");
			}
			if (beverageMissing)
			{
				return (Key: "chat.restaurant.delivery.missing_beverage", Fallback: "Missing beverage");
			}
			return (Key: "chat.restaurant.delivery.complete", Fallback: "Complete");
		}
	}
}
