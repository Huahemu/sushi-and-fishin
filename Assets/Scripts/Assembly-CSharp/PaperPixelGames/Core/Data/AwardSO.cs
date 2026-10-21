using PaperPixelGames.Framework.Enums;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(menuName = "Data/Awards", fileName = "Award")]
	public sealed class AwardSO : ScriptableObject
	{
		public DailyAwardId Id;

		public string Title = "";

		public Sprite Sprite;
	}
}
