namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;
using global::UnityEngine.Events;

public class ElevatorGuy : InteractableBase
{
	[SerializeField]
	private Transform itemSpawnTransform;

	[SerializeField]
	private float itemSpawnInterval;

	[SerializeField]
	private float throwForce;

	[SerializeField]
	private float throwTorque;

	[SerializeField]
	private UnityEvent serverOnSpawnItem;

	[SerializeField]
	private UnityEvent serverOnInteractElevatorGuy;

	private bool _isSpawning;

	private bool _hasSpawned;

	public override void ServerOnInteract(PlayerInteract playerInteract)
	{
		base.ServerOnInteract(playerInteract);
		if (!_isSpawning && !NetworkSingleton<ElevatorManager>.Instance.isTeleporting)
		{
			_isSpawning = true;
			serverOnInteractElevatorGuy?.Invoke();
			if (!_hasSpawned)
			{
				StartCoroutine(ItemInitializeRoutine());
			}
			else
			{
				StartCoroutine(ItemSpawnRoutine());
			}
		}
	}

	private IEnumerator ItemInitializeRoutine()
	{
		List<SpawnableSO> currentItems = NetworkSingleton<ItemManager>.Instance.currentItems;
		foreach (SpawnableSO item in currentItems)
		{
			GameObject gameObject = Object.Instantiate(item.prefab, itemSpawnTransform.position, itemSpawnTransform.rotation);
			ConsumableItem it = gameObject.GetComponent<ConsumableItem>();
			NetworkSingleton<ItemManager>.Instance.spawnedItemInstances.Add(it);
			NetworkServer.Spawn(gameObject);
			yield return null;
			it.ServerThrow(itemSpawnTransform.position, itemSpawnTransform.rotation, itemSpawnTransform.forward * throwForce, Random.insideUnitSphere * throwTorque);
			serverOnSpawnItem?.Invoke();
			yield return new WaitForSeconds(itemSpawnInterval);
		}
		yield return new WaitForSeconds(0.5f);
		_hasSpawned = true;
		_isSpawning = false;
	}

	private IEnumerator ItemSpawnRoutine()
	{
		List<ConsumableItem> spawnedItemInstances = NetworkSingleton<ItemManager>.Instance.spawnedItemInstances;
		foreach (ConsumableItem item in spawnedItemInstances)
		{
			if (!item.GetIsBeingHeld() && !NetworkSingleton<ElevatorManager>.Instance.IsInElevator(item.transform.position))
			{
				item.ServerSetEnabled(isEnabled: true);
				yield return new WaitForFixedUpdate();
				item.ServerThrow(itemSpawnTransform.position, itemSpawnTransform.rotation, itemSpawnTransform.forward * throwForce, Random.insideUnitSphere * throwTorque);
				serverOnSpawnItem?.Invoke();
				yield return new WaitForSeconds(itemSpawnInterval);
			}
		}
		yield return new WaitForSeconds(0.5f);
		_isSpawning = false;
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
