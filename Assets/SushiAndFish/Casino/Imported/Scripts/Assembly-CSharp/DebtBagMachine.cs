namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;
using global::UnityEngine.Events;

public class DebtBagMachine : InteractableBase
{
	[Header("Settings")]
	[SerializeField]
	private float spawnRadius;

	[SerializeField]
	private int spawnAmount;

	[Header("References")]
	[SerializeField]
	private Transform spawnPoint;

	[SerializeField]
	private DebtBag debtBagPrefab;

	public UnityEvent OnRpcInteract;

	private int _spawnedBagAmount;

	private long _debtBagMoneyAmount;

	public override void OnStartServer()
	{
		base.OnStartServer();
		_debtBagMoneyAmount = (long)Math.Round((double)NetworkSingleton<MoneyManager>.Instance.balance / (double)spawnAmount);
	}

	public override void ServerOnInteract(PlayerInteract playerInteract)
	{
		RpcInvokeInteract();
		if (_spawnedBagAmount < spawnAmount)
		{
			SpawnBag();
		}
	}

	private void SpawnBag()
	{
		long num = _debtBagMoneyAmount;
		if (_spawnedBagAmount >= spawnAmount - 1)
		{
			num = NetworkSingleton<MoneyManager>.Instance.balance;
		}
		_spawnedBagAmount++;
		if (!NetworkSingleton<MoneyManager>.Instance.TryChangeBalance(-num, null, ChangeType.Misc))
		{
			NetworkSingleton<MoneyManager>.Instance.SetBalance(0L, null, ChangeType.Misc);
		}
		Vector3 position = spawnPoint.position + UnityEngine.Random.insideUnitSphere * spawnRadius;
		DebtBag debtBag = UnityEngine.Object.Instantiate(debtBagPrefab, position, UnityEngine.Random.rotation);
		NetworkServer.Spawn(debtBag.gameObject);
		debtBag.ServerSetMoney(num);
		debtBag.ServerThrow(spawnPoint.position, UnityEngine.Random.rotation, spawnPoint.forward * 5f, UnityEngine.Random.insideUnitSphere * 10f);
	}

	[ClientRpc]
	private void RpcInvokeInteract()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void DebtBagMachine::RpcInvokeInteract()", -1951586492, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcInvokeInteract()
	{
		OnRpcInteract?.Invoke();
	}

	protected static void InvokeUserCode_RpcInvokeInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcInvokeInteract called on server.");
		}
		else
		{
			((DebtBagMachine)obj).UserCode_RpcInvokeInteract();
		}
	}

	static DebtBagMachine()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(DebtBagMachine), "System.Void DebtBagMachine::RpcInvokeInteract()", InvokeUserCode_RpcInvokeInteract);
	}
}

}
