using PaperPixelGames.Core.Localization;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Tools
{
	[CreateAssetMenu(fileName = "BaseToolData", menuName = "Data/PlayerTools/BaseToolData")]
	public class BaseToolData : ScriptableObject, IWheelSlice
	{
		public string Name;

		public Sprite Sprite;

		public ItemOffset OffsetFps;

		public ItemOffset OffsetTps;

		public string WheelName => LocalizationDatabase.GetString("UiTable", "wheel.tool." + LocalizationDatabase.ToSnakeCase(Name), Name);

		public Sprite WheelSprite => Sprite;
	}
}
