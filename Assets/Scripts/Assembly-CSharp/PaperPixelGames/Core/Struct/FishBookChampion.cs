using System;
using Unity.Collections;
using UnityEngine;

namespace PaperPixelGames.Core.Struct
{
	public readonly struct FishBookChampion : IEquatable<FishBookChampion>
	{
		public readonly FixedString64Bytes FishId;

		public readonly float BestWeightKg;

		public readonly ulong SteamId;

		public readonly FixedString64Bytes PlayerName;

		public bool IsValid
		{
			get
			{
				if (!FishId.IsEmpty)
				{
					return BestWeightKg > 0f;
				}
				return false;
			}
		}

		public FishBookChampion(FixedString64Bytes fishId, float bestWeightKg, ulong steamId, FixedString64Bytes playerName)
		{
			FishId = fishId;
			BestWeightKg = bestWeightKg;
			SteamId = steamId;
			PlayerName = playerName;
		}

		public bool Equals(FishBookChampion other)
		{
			if (FishId.Equals(other.FishId) && Mathf.Approximately(BestWeightKg, other.BestWeightKg) && SteamId == other.SteamId)
			{
				return PlayerName.Equals(other.PlayerName);
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is FishBookChampion other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(FishId, BestWeightKg, SteamId, PlayerName);
		}
	}
}
