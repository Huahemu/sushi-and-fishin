using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.PlayerTools
{
	public class CompassTargetStepTrigger : MonoBehaviour
	{
		[SerializeField]
		private byte _requiredCompassLevel = 1;

		[SerializeField]
		private byte _requiredStepIndex;

		[SerializeField]
		private bool _advanceOnce = true;

		private bool _serverCompleted;

		private void OnTriggerEnter(Collider other)
		{
			if (other.CompareTag("Player") && !(NetworkManager.Singleton == null) && NetworkManager.Singleton.IsServer && (!_advanceOnce || !_serverCompleted))
			{
				RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
				if (!(restaurantManager == null) && restaurantManager.CurrentCompassTargetLevel == _requiredCompassLevel && restaurantManager.CurrentCompassTargetStepIndex == _requiredStepIndex && restaurantManager.ServerAdvanceCompassTargetStep())
				{
					_serverCompleted = true;
					UtilityMethods.Log($"[CompassTargetStepTrigger] Advanced. level={_requiredCompassLevel}, step={_requiredStepIndex}");
				}
			}
		}
	}
}
