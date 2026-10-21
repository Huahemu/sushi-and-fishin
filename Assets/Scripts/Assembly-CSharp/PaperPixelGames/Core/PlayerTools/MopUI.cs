using TMPro;
using UnityEngine;

namespace PaperPixelGames.Core.PlayerTools
{
	public class MopUI : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _debugText;

		public void UpdateDebugUI(int level)
		{
			_debugText.text = "<b><color=#FFD700>[Mop DEBUG]</color></b>\n" + $"<b><color=#FFD700>Level:</color></b>              <color=#FFFFFF>{level}</color>\n";
		}

		public void ClearDebug()
		{
			if (_debugText != null)
			{
				_debugText.text = "";
			}
		}
	}
}
