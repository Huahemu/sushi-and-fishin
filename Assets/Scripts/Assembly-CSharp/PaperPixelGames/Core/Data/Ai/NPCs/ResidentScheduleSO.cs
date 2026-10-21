using System;
using PaperPixelGames.Framework.Enums;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Ai.NPCs
{
	[CreateAssetMenu(fileName = "ResidentSchedule", menuName = "Data/NPC/ResidentScheduleSO")]
	public class ResidentScheduleSO : ScriptableObject
	{
		[Serializable]
		public struct Entry
		{
			[Range(0f, 24f)]
			public int StartTime;

			[Range(0f, 59f)]
			public int StartMinute;

			[Range(0f, 24f)]
			public float EndTime;

			[Range(0f, 59f)]
			public int EndMinute;

			public ResidentActivityType Activity;

			public WayPointIndex ActivityIndex;
		}

		public Entry[] Entries;
	}
}
