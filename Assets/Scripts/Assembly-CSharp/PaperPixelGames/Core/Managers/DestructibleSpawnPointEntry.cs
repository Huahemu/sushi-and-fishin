using System;
using UnityEngine;

namespace PaperPixelGames.Core.Managers
{
	[Serializable]
	public class DestructibleSpawnPointEntry
	{
		[Min(0f)]
		public int Id;

		public Transform Point;

		public int PrefabType;
	}
}
