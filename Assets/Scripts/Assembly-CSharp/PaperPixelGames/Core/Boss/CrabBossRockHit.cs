using PaperPixelGames.Core.Player;
using UnityEngine;

namespace PaperPixelGames.Core.Boss
{
	internal readonly struct CrabBossRockHit
	{
		public CrabBossRockImpactType ImpactType { get; }

		public PPG_Player Player { get; }

		public Collider WorldCollider { get; }

		public Vector3 Position { get; }

		public Vector3 Normal { get; }

		public float Distance { get; }

		public CrabBossRockHit(CrabBossRockImpactType impactType, PPG_Player player, Collider worldCollider, Vector3 position, Vector3 normal, float distance)
		{
			ImpactType = impactType;
			Player = player;
			WorldCollider = worldCollider;
			Position = position;
			Normal = normal;
			Distance = distance;
		}
	}
}
