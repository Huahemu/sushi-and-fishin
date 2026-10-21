using System.Collections.Generic;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Indicator;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean
{
	[RequireComponent(typeof(Collider))]
	public class OpenWatersPenaltyTrigger : MonoBehaviour
	{
		[SerializeField]
		private string _playerTag = "Player";

		[SerializeField]
		private string _alertHeader = "OPEN WATERS AHEAD";

		[SerializeField]
		private string _alertDescription = "Return to safe waters.";

		[SerializeField]
		private AlertPriority _alertPriority = AlertPriority.Critical;

		[Header("Restaurant Indicator")]
		[SerializeField]
		private Transform _restaurantIndicatorTarget;

		[SerializeField]
		private Sprite _restaurantIndicatorIcon;

		[SerializeField]
		private bool _showRestaurantDistance = true;

		[SerializeField]
		private Vector3 _restaurantIndicatorWorldOffset = new Vector3(0f, 2f, 0f);

		[SerializeField]
		private bool _logStateChanges;

		private readonly HashSet<Collider> _localPlayerColliders = new HashSet<Collider>();

		private PPG_Player _localPlayer;

		private bool _isLocalPlayerInside;

		private OffscreenIndicatorHandler _restaurantIndicatorHandler;

		private OffscreenIndicatorHandler.OffscreenIndicatorHandle _restaurantIndicatorHandle;

		private void Reset()
		{
			Collider component = GetComponent<Collider>();
			if (component != null)
			{
				component.isTrigger = true;
			}
		}

		private void Awake()
		{
			Collider component = GetComponent<Collider>();
			if (component != null && !component.isTrigger)
			{
				UtilityMethods.Log("[OpenWatersPenaltyTrigger] Collider is not marked as trigger.", LogType.Warning);
			}
		}

		private void Update()
		{
			if (_isLocalPlayerInside && !(_localPlayer == null) && !(_localPlayer.DivingEquipment == null))
			{
				if (_localPlayer.DivingEquipment.IsKnocked)
				{
					ClearLocalPlayerState();
				}
				else
				{
					_localPlayer.DivingEquipment.TickOpenWaterPenalty(Time.deltaTime);
				}
			}
		}

		private void OnTriggerEnter(Collider other)
		{
			if (!TryResolveLocalPlayer(other, out var player))
			{
				return;
			}
			_localPlayerColliders.Add(other);
			if (!_isLocalPlayerInside)
			{
				_localPlayer = player;
				_isLocalPlayerInside = true;
				_localPlayer.DivingEquipment?.ResetOpenWaterPenalty();
				Singleton<NotifyManager>.Instance?.ShowAlert(_alertHeader, _alertDescription, -1f, _alertPriority, autoClose: false);
				ShowRestaurantIndicator();
				if (_logStateChanges)
				{
					UtilityMethods.Log("[OpenWatersPenaltyTrigger] Local player entered open waters.");
				}
			}
		}

		private void OnTriggerExit(Collider other)
		{
			if (_localPlayerColliders.Remove(other) && _localPlayerColliders.Count <= 0)
			{
				ClearLocalPlayerState();
			}
		}

		private void OnDisable()
		{
			if (!_isLocalPlayerInside)
			{
				HideRestaurantIndicator();
			}
			else
			{
				ClearLocalPlayerState();
			}
		}

		private bool TryResolveLocalPlayer(Collider other, out PPG_Player player)
		{
			player = null;
			if (other == null)
			{
				return false;
			}
			if (!other.CompareTag(_playerTag))
			{
				return false;
			}
			player = other.GetComponentInParent<PPG_Player>();
			if (player == null || !player.IsOwner)
			{
				return false;
			}
			if (Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.LocalPlayer != null && Singleton<PlayersManager>.Instance.LocalPlayer != player)
			{
				return false;
			}
			return true;
		}

		private void ShowRestaurantIndicator()
		{
			if (_restaurantIndicatorHandle.IsValid)
			{
				return;
			}
			if (_restaurantIndicatorTarget == null)
			{
				UtilityMethods.Log("[OpenWatersPenaltyTrigger] Restaurant indicator target is not assigned.", LogType.Error);
				return;
			}
			OffscreenIndicatorHandler instance = Singleton<OffscreenIndicatorHandler>.Instance;
			if (instance == null)
			{
				UtilityMethods.Log("[OpenWatersPenaltyTrigger] Offscreen indicator handler is unavailable.", LogType.Error);
				return;
			}
			_restaurantIndicatorHandler = instance;
			_restaurantIndicatorHandle = instance.RegisterTransform(_restaurantIndicatorTarget, 0f, refreshIfExists: true, _restaurantIndicatorIcon, _showRestaurantDistance, _restaurantIndicatorWorldOffset, _localPlayer.transform);
		}

		private void HideRestaurantIndicator()
		{
			if (_restaurantIndicatorHandle.IsValid && _restaurantIndicatorHandler != null)
			{
				_restaurantIndicatorHandler.Unregister(_restaurantIndicatorHandle);
			}
			_restaurantIndicatorHandle = default;
			_restaurantIndicatorHandler = null;
		}

		private void ClearLocalPlayerState()
		{
			_localPlayerColliders.Clear();
			if (_localPlayer != null)
			{
				_localPlayer.DivingEquipment?.ResetOpenWaterPenalty();
			}
			HideRestaurantIndicator();
			_localPlayer = null;
			_isLocalPlayerInside = false;
			Singleton<NotifyManager>.Instance?.ForceHideAlert();
			if (_logStateChanges)
			{
				UtilityMethods.Log("[OpenWatersPenaltyTrigger] Local player exited open waters.");
			}
		}
	}
}
