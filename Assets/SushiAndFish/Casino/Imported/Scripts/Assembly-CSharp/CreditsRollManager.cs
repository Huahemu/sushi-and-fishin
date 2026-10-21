namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;

public class CreditsRollManager : NetworkSingleton<CreditsRollManager>
{
	[SerializeField]
	private CreditsMannequinSpawnManager mannequinSpawnManager;

	[SerializeField]
	private List<PlayerCreditsSnapshot> debugSnapshots = new List<PlayerCreditsSnapshot>();

	private bool hasSpawnedCredits;

	public void BeginCredits(IReadOnlyList<PlayerCreditsSnapshot> snapshots)
	{
		if (!base.isServer || hasSpawnedCredits)
		{
			return;
		}
		if (snapshots == null || snapshots.Count == 0)
		{
			Debug.LogWarning("[CreditsRollManager] BeginCredits called with no snapshots.");
			return;
		}
		if (mannequinSpawnManager == null)
		{
			mannequinSpawnManager = Object.FindFirstObjectByType<CreditsMannequinSpawnManager>();
		}
		if (mannequinSpawnManager == null)
		{
			Debug.LogWarning("[CreditsRollManager] CreditsMannequinSpawnManager not found.");
			return;
		}
		Debug.Log($"[CreditsRollManager] Spawning mannequins from {snapshots.Count} snapshots.");
		mannequinSpawnManager.SpawnFromSnapshots(snapshots);
		hasSpawnedCredits = true;
	}

	[Server]
	public void BeginCreditsFromScenePlayers()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void CreditsRollManager::BeginCreditsFromScenePlayers()' called when server was not active");
		}
		else if (!hasSpawnedCredits)
		{
			StartCoroutine(BeginCreditsFromScenePlayersRoutine());
		}
	}

	private IEnumerator BeginCreditsFromScenePlayersRoutine()
	{
		yield return new WaitForSeconds(2f);
		for (int attempt = 1; attempt <= 5; attempt++)
		{
			List<PlayerCreditsSnapshot> list = BuildSnapshotsFromScenePlayers();
			int num = CountTotalCosmetics(list);
			Debug.Log($"[CreditsRollManager] Snapshot attempt {attempt}/{5}: players={list.Count}, cosmetics={num}");
			if (list.Count > 0 && (num > 0 || attempt == 5))
			{
				if (num == 0)
				{
					Debug.LogWarning("[CreditsRollManager] Proceeding after max attempts with 0 cosmetics.");
				}
				BeginCredits(list);
				break;
			}
			yield return null;
		}
	}

	private static List<PlayerCreditsSnapshot> BuildSnapshotsFromScenePlayers()
	{
		List<PlayerCreditsSnapshot> list = new List<PlayerCreditsSnapshot>();
		PlayerCustomization[] array = Object.FindObjectsByType<PlayerCustomization>(FindObjectsSortMode.None);
		Debug.Log($"[CreditsRollManager] Found {array.Length} PlayerCustomization components in scene.");
		PlayerCustomization[] array2 = array;
		foreach (PlayerCustomization playerCustomization in array2)
		{
			if (playerCustomization == null)
			{
				Debug.LogWarning("[CreditsRollManager] Skipping null PlayerCustomization.");
				continue;
			}
			PlayerProfile component = playerCustomization.GetComponent<PlayerProfile>();
			if (component == null || component.steamId == 0L)
			{
				Debug.LogWarning("[CreditsRollManager] Skipping player with missing PlayerProfile or steamId == 0.");
				continue;
			}
			PlayerCreditsSnapshot playerCreditsSnapshot = new PlayerCreditsSnapshot
			{
				steamId = component.steamId,
				displayName = component.playerName
			};
			foreach (KeyValuePair<CosmeticType, int> equippedCosmetic in playerCustomization.GetEquippedCosmetics())
			{
				playerCreditsSnapshot.cosmetics.Add(new PlayerCreditsSnapshot.CosmeticEntry
				{
					type = equippedCosmetic.Key,
					cosmeticId = equippedCosmetic.Value
				});
			}
			list.Add(playerCreditsSnapshot);
			Debug.Log($"[CreditsRollManager] Snapshot added for '{playerCreditsSnapshot.displayName}' ({playerCreditsSnapshot.steamId}) with {playerCreditsSnapshot.cosmetics.Count} cosmetics.");
		}
		return list;
	}

	private static int CountTotalCosmetics(List<PlayerCreditsSnapshot> snapshots)
	{
		int num = 0;
		foreach (PlayerCreditsSnapshot snapshot in snapshots)
		{
			num += snapshot.cosmetics.Count;
		}
		return num;
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
