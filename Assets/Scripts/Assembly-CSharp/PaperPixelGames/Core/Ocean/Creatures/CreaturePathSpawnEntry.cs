using System;
using PaperPixelGames.Core.Data;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	[Serializable]
	public sealed class CreaturePathSpawnEntry
	{
		public CreatureSO CreatureData;

		[Min(0f)]
		public int SpawnCount = 1;

		public SeaArea PatrolArea;

		public bool Loop = true;

		public bool StartFromClosest;

		[Min(0.01f)]
		public float ReachDistance = 0.35f;
	}
}
