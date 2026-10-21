namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::Mirror;
using global::UnityEngine;

public class CreditsMannequinSpawnManager : NetworkBehaviour
{
	[SerializeField]
	private GameObject mannequinPrefab;

	[SerializeField]
	private Transform[] spawnPoints;

	private readonly List<GameObject> spawnedMannequins = new List<GameObject>();

	public void SetSpawnPoints(Transform[] points)
	{
		spawnPoints = points;
	}

	[Server]
	public void SpawnFromSnapshots(IReadOnlyList<PlayerCreditsSnapshot> snapshots)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void CreditsMannequinSpawnManager::SpawnFromSnapshots(System.Collections.Generic.IReadOnlyList`1<PlayerCreditsSnapshot>)' called when server was not active");
		}
		else
		{
			if (!base.isServer)
			{
				return;
			}
			ClearSpawned();
			if (mannequinPrefab == null || snapshots == null || spawnPoints == null || spawnPoints.Length == 0)
			{
				return;
			}
			int num = Mathf.Min(snapshots.Count, spawnPoints.Length);
			for (int i = 0; i < num; i++)
			{
				Transform transform = spawnPoints[i];
				if (!(transform == null))
				{
					GameObject gameObject = Object.Instantiate(mannequinPrefab, transform.position, transform.rotation, transform);
					spawnedMannequins.Add(gameObject);
					NetworkServer.Spawn(gameObject);
					CreditsMannequinController component = gameObject.GetComponent<CreditsMannequinController>();
					if (component != null)
					{
						component.RpcApplySnapshot(snapshots[i]);
					}
				}
			}
		}
	}

	public void ClearSpawned()
	{
		if (!base.isServer)
		{
			return;
		}
		foreach (GameObject spawnedMannequin in spawnedMannequins)
		{
			if (spawnedMannequin != null)
			{
				NetworkServer.Destroy(spawnedMannequin);
			}
		}
		spawnedMannequins.Clear();
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
