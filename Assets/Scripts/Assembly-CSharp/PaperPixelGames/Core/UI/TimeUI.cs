using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;

namespace PaperPixelGames.Core.UI
{
	public class TimeUI : MonoBehaviour
	{
		[Header("References")]
		[SerializeField]
		private TextMeshProUGUI _timeText;

		[SerializeField]
		private GameObject _visualRoot;

		private string _time = string.Empty;

		public string Time
		{
			get
			{
				return _time;
			}
			set
			{
				_time = value;
				_timeText.text = value;
			}
		}

		public void SetVisibility(bool status)
		{
			UtilityMethods.Log("[TimeUI] SetVisibility:" + status);
			_visualRoot.SetActive(status);
		}
	}
}
