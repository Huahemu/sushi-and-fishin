using TMPro;
using UnityEngine;

namespace Assets.Scripts.Framework.States.MainMenu
{
	public sealed class LobbyPuppetNameTag : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _text;

		public void SetName(string name)
		{
			if (!(_text == null))
			{
				_text.text = name;
			}
		}
	}
}
