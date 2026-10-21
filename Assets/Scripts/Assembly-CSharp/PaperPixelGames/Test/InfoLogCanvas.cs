using TMPro;
using UnityEngine;

namespace PaperPixelGames.Test
{
	public class InfoLogCanvas : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _localInfoText;

		[SerializeField]
		private TextMeshProUGUI _globalInfoText;

		public void LogInfoGlobal(string message)
		{
			_globalInfoText.text = message;
		}
	}
}
