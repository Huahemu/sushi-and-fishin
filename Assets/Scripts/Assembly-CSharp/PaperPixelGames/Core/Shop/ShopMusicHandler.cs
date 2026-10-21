using DistantLands.Cozy;
using UnityEngine;

namespace PaperPixelGames.Core.Shop
{
	public class ShopMusicHandler : MonoBehaviour
	{
		[Header("SFX")]
		[SerializeField]
		private AudioSource _loopEngineSource;

		[SerializeField]
		private int _openHour = 9;

		[SerializeField]
		private int _closeHour = 19;

		[SerializeField]
		private CozyWeatherManager _cozyWeatherManager;

		private void OnEnable()
		{
			CozyWeather.Events.onNewHour += OnNewHour;
		}

		private void OnDisable()
		{
			CozyWeather.Events.onNewHour -= OnNewHour;
		}

		private void OnNewHour()
		{
			int hours = _cozyWeatherManager.CurrentTime.hours;
			if (hours >= _openHour && hours < _closeHour)
			{
				if (!_loopEngineSource.isPlaying)
				{
					_loopEngineSource.Play();
				}
			}
			else if (_loopEngineSource.isPlaying)
			{
				_loopEngineSource.Stop();
			}
		}
	}
}
