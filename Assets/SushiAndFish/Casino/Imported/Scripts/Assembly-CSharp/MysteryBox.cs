namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class MysteryBox : ConsumableItem
{
	[SerializeField]
	private Animator anim;

	[SerializeField]
	private SpawnableSO lobbySpawnable;

	[SerializeField]
	private List<SpawnableEntry> spawnableList;

	[SerializeField]
	private SFXComponent sfxComponent;

	[SerializeField]
	private EventReference popOpenSfx;

	private bool _hasBeenUsed;

	protected override void OnUseItem(bool isPressed)
	{
		if (!_hasBeenUsed)
		{
			_hasBeenUsed = true;
			anim.SetTrigger("Unbox");
			if (base.isServer)
			{
				StartCoroutine(UnboxRoutine());
			}
		}
	}

	private IEnumerator UnboxRoutine()
	{
		yield return new WaitForSeconds(1.5f);
		if (NetworkSingleton<GameManager>.Instance.state == GameState.Game)
		{
			SpawnableSO randomSpawnableByWeight = GetRandomSpawnableByWeight();
			GameObject gameObject = UnityEngine.Object.Instantiate(randomSpawnableByWeight.prefab, base.transform.position, Quaternion.identity);
			NetworkServer.Spawn(gameObject);
			NetworkSingleton<ItemManager>.Instance.ServerAddItem(randomSpawnableByWeight);
			NetworkSingleton<ItemManager>.Instance.spawnedItemInstances.Add(gameObject.GetComponent<ConsumableItem>());
		}
		else
		{
			GameObject gameObject2 = UnityEngine.Object.Instantiate(lobbySpawnable.prefab, base.transform.position, Quaternion.identity);
			NetworkServer.Spawn(gameObject2);
			if ((bool)NetworkSingleton<ItemStampManager>.Instance)
			{
				ItemStamp stampFromInstance = NetworkSingleton<ItemStampManager>.Instance.GetStampFromInstance(base.gameObject);
				NetworkSingleton<ItemStampManager>.Instance.UnregisterSpawnedInstance(base.gameObject);
				NetworkSingleton<ItemStampManager>.Instance.RegisterSpawnedInstance(gameObject2, stampFromInstance);
			}
		}
		sfxComponent.RpcPlayOneShotWith3DPos();
		DestroyItem();
	}

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		base.OnDropped(playerInventory);
		RpcOnDropped();
	}

	[ClientRpc]
	private void RpcOnDropped()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void MysteryBox::RpcOnDropped()", -335798415, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private SpawnableSO GetRandomSpawnableByWeight()
	{
		if (spawnableList == null || spawnableList.Count == 0)
		{
			return null;
		}
		float num = 0f;
		for (int i = 0; i < spawnableList.Count; i++)
		{
			num += spawnableList[i].chanceWeight;
		}
		float num2 = (float)GetSeededRandom().NextDouble() * num;
		for (int j = 0; j < spawnableList.Count; j++)
		{
			num2 -= spawnableList[j].chanceWeight;
			if (num2 <= 0f)
			{
				return spawnableList[j].spawnable;
			}
		}
		List<SpawnableEntry> list = spawnableList;
		return list[list.Count - 1].spawnable;
	}

	private global::System.Random GetSeededRandom()
	{
		if (!NetworkSingleton<SeededRandomManager>.Instance || !NetworkSingleton<GameManager>.Instance)
		{
			return new global::System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
		}
		int currentSeed = NetworkSingleton<SeededRandomManager>.Instance.CurrentSeed;
		int daysPassed = NetworkSingleton<GameManager>.Instance.daysPassed;
		int mysteryBoxCounter = NetworkSingleton<SeededRandomManager>.Instance.MysteryBoxCounter;
		long num = (((currentSeed * 2654435761u + daysPassed) * 2654435761u + mysteryBoxCounter) * 2654435761u) ^ (mysteryBoxCounter << 13) ^ (mysteryBoxCounter >> 7);
		long num2 = (num ^ (num >> 32)) * 2246822507u;
		long num3 = (num2 ^ (num2 >> 16)) * 3266489917u;
		return new global::System.Random((int)(num3 ^ (num3 >> 13)));
	}

	private void PopOpenSfx()
	{
		SFXManager.SFXOneShot(popOpenSfx, base.transform.position);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcOnDropped()
	{
		anim.Play("Default", 0, 0f);
		anim.Update(0f);
	}

	protected static void InvokeUserCode_RpcOnDropped(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnDropped called on server.");
		}
		else
		{
			((MysteryBox)obj).UserCode_RpcOnDropped();
		}
	}

	static MysteryBox()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(MysteryBox), "System.Void MysteryBox::RpcOnDropped()", InvokeUserCode_RpcOnDropped);
	}
}

}
