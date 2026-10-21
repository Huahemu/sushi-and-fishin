using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using UnityEngine;

namespace PaperPixelGames.Core
{
	public class LocationTrigger : MonoBehaviour
	{
		public string LocationName = "";

		public string ExitName = "";

		public float Duration = 2f;

		[SerializeField]
		private bool _isRestaurant;

		[SerializeField]
		private bool _showFeedback = true;

		[SerializeField]
		private bool _clearOnExit;

		private void OnTriggerEnter(Collider other)
		{
			if (!other.CompareTag("Player") || !other.TryGetComponent<PPG_Player>(out var component) || !component.IsOwner)
			{
				return;
			}
			string text = (_isRestaurant ? Singleton<GameProgressManager>.Instance.RestaurantManager.RestaurantSignManager.RestaurantName : LocationName);
			if (!(component.Zone == text))
			{
				component.ZoneName.Value = text;
				if (_showFeedback)
				{
					Singleton<NotifyManager>.Instance.ShowLocationFeedback(text, Duration);
				}
			}
		}

		private void OnTriggerExit(Collider other)
		{
			if (_clearOnExit && other.CompareTag("Player") && other.TryGetComponent<PPG_Player>(out var component) && component.IsOwner)
			{
				component.ZoneName.Value = ExitName;
			}
		}
	}
}
