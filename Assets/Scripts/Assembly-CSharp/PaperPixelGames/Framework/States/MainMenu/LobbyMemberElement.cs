using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public class LobbyMemberElement : MonoBehaviour
	{
		[SerializeField]
		private Image _avatar;

		[SerializeField]
		private Image _hostIcon;

		public Image Avatar => _avatar;

		public void SetHostIconVisible(bool visible)
		{
			_hostIcon.enabled = visible;
		}
	}
}
