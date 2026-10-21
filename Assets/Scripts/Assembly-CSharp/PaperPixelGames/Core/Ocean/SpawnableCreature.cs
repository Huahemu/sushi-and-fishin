using System;
using PaperPixelGames.Core.Data;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean
{
	[Serializable]
	public sealed class SpawnableCreature
	{
		public CreatureSO CreatureData;

		[Min(0f)]
		public int Count = 1;

		public SeaArea PreferredArea;

		public bool DeadSpawn;
	}
}
