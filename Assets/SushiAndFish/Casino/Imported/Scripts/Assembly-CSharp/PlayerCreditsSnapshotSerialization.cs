namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::Mirror;

public static class PlayerCreditsSnapshotSerialization
{
	public static void WritePlayerCreditsSnapshot(this NetworkWriter writer, PlayerCreditsSnapshot snapshot)
	{
		writer.WriteULong(snapshot.steamId);
		writer.WriteString(snapshot.displayName ?? string.Empty);
		writer.WriteInt(snapshot.cosmetics?.Count ?? 0);
		if (snapshot.cosmetics == null)
		{
			return;
		}
		foreach (PlayerCreditsSnapshot.CosmeticEntry cosmetic in snapshot.cosmetics)
		{
			writer.WriteInt((int)cosmetic.type);
			writer.WriteInt(cosmetic.cosmeticId);
		}
	}

	public static PlayerCreditsSnapshot ReadPlayerCreditsSnapshot(this NetworkReader reader)
	{
		PlayerCreditsSnapshot playerCreditsSnapshot = new PlayerCreditsSnapshot
		{
			steamId = reader.ReadULong(),
			displayName = reader.ReadString()
		};
		int num = reader.ReadInt();
		playerCreditsSnapshot.cosmetics = new List<PlayerCreditsSnapshot.CosmeticEntry>(num);
		for (int i = 0; i < num; i++)
		{
			playerCreditsSnapshot.cosmetics.Add(new PlayerCreditsSnapshot.CosmeticEntry
			{
				type = (CosmeticType)reader.ReadInt(),
				cosmeticId = reader.ReadInt()
			});
		}
		return playerCreditsSnapshot;
	}
}

}
