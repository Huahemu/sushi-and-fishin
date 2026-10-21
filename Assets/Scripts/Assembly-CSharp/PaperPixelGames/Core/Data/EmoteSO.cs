using PaperPixelGames.Core.Localization;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "NewEmote", menuName = "Data/EmoteSO")]
	public class EmoteSO : ScriptableObject, IWheelSlice
	{
		public string Name;

		public Sprite Sprite;

		public int AnimationKey;

		public string WheelName => LocalizationDatabase.GetString("UiTable", "wheel.emote." + LocalizationDatabase.ToSnakeCase(Name), Name);

		public Sprite WheelSprite => Sprite;
	}
}
