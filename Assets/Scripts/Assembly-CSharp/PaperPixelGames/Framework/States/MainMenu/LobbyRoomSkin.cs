using TMPro;
using UnityEngine;

namespace PaperPixelGames.Framework.States.MainMenu
{
	[CreateAssetMenu(menuName = "Paper Pixel Games/Lobby UI Skin")]
	public sealed class LobbyRoomSkin : ScriptableObject
	{
		public Sprite Window;

		public Sprite Modal;

		public Sprite Panel;

		public Sprite Button;

		public Sprite SecondaryButton;

		public Sprite DangerButton;

		public Sprite Row;

		public Sprite Input;

		public Sprite CloseIcon;

		public TMP_FontAsset BodyFont;

		public TMP_FontAsset HeadingFont;

		public Color Ink = new Color32(41, 66, 80, byte.MaxValue);

		public Color Blue = new Color32(13, 137, 171, byte.MaxValue);

		public Color Paper = new Color32(246, 235, 214, byte.MaxValue);

		public Color Muted = new Color32(97, 111, 113, byte.MaxValue);

		public Color Danger = new Color32(170, 57, 59, byte.MaxValue);

		public float PixelsPerUnitMultiplier(Sprite sprite)
		{
			if (sprite == Window || sprite == Modal)
			{
				return 3.5f;
			}
			if (sprite == Panel)
			{
				return 5f;
			}
			if (sprite == Row || sprite == Input)
			{
				return 5f;
			}
			return 10f;
		}
	}
}
