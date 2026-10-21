namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using global::UnityEngine;

public class SpawnArea : NetworkBehaviour
{
	[SerializeField]
	private ValuableItem objectToSpawn;

	[SerializeField]
	private BoxCollider spawnAreaCollider;

	[SerializeField]
	private int maxObjectsInArea = 30;

	[SerializeField]
	private int currentObjectCount;

	private int _spawnedObjectCount;

	private void Start()
	{
		if (base.isServer)
		{
			InvokeRepeating("SpawnObject", 0f, 0.1f);
		}
	}

	private void Update()
	{
		if (base.isServer)
		{
			currentObjectCount = Object.FindObjectsByType<ValuableItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
		}
	}

	private Vector3 CalculateRandomPositionWithinBoxColliderBounds()
	{
		Vector3 vector = spawnAreaCollider.center + base.transform.position;
		Vector3 size = spawnAreaCollider.size;
		float x = Random.Range(vector.x - size.x / 2f, vector.x + size.x / 2f);
		float y = Random.Range(vector.y - size.y / 2f, vector.y + size.y / 2f);
		float z = Random.Range(vector.z - size.z / 2f, vector.z + size.z / 2f);
		return new Vector3(x, y, z);
	}

	[Server]
	public void SpawnObject()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void SpawnArea::SpawnObject()' called when server was not active");
		}
		else if (_spawnedObjectCount < maxObjectsInArea)
		{
			_spawnedObjectCount++;
			Vector3 position = CalculateRandomPositionWithinBoxColliderBounds();
			ValuableItem valuableItem = Object.Instantiate(objectToSpawn, position, Quaternion.identity);
			NetworkServer.Spawn(valuableItem.gameObject);
			RandomizeValuableItemValue(valuableItem.TryGetComponent<ValuableItem>(out var component) ? component : null);
		}
	}

	[Server]
	public void RandomizeValuableItemValue(ValuableItem item)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void SpawnArea::RandomizeValuableItemValue(ValuableItem)' called when server was not active");
		}
		else if ((bool)item)
		{
			float num = 0.15f;
			if (Random.value <= num)
			{
				item.ServerChangeValue(item.value * 20);
			}
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
