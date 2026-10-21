namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class ConsumableItem : Item
{
	[SerializeField]
	private GameObject destroyVfx;

	[Server]
	public void DestroyItem()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ConsumableItem::DestroyItem()' called when server was not active");
			return;
		}
		if (base.isServer && NetworkSingleton<GameManager>.Instance != null && NetworkSingleton<GameManager>.Instance.state == GameState.Lobby && NetworkSingleton<ItemStampManager>.Instance != null)
		{
			NetworkSingleton<ItemStampManager>.Instance.OnLobbyStampItemConsumed(base.gameObject);
		}
		if ((bool)spawnableSo && NetworkSingleton<GameManager>.Instance.state == GameState.Game)
		{
			NetworkSingleton<ItemManager>.Instance.ServerRemoveItem(spawnableSo, this);
		}
		DestroyItemVFX();
		RpcDestroyItemVFX();
		ServerDrop();
		OnDestroyItem();
		RpcOnDestroyItem();
		StartCoroutine(DestroyItemCoroutine());
	}

	private IEnumerator DestroyItemCoroutine()
	{
		yield return null;
		NetworkServer.Destroy(base.gameObject);
	}

	[ClientRpc]
	private void RpcOnDestroyItem()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void ConsumableItem::RpcOnDestroyItem()", -1983958164, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void OnDestroyItem()
	{
		IsInteractable = false;
		Rb.isKinematic = true;
		SetEnableColliders(isEnabled: false);
		modelTransform.gameObject.SetActive(value: false);
	}

	[ClientRpc]
	private void RpcDestroyItemVFX()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void ConsumableItem::RpcDestroyItemVFX()", 841712571, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void DestroyItemVFX()
	{
		if ((bool)destroyVfx)
		{
			Object.Destroy(Object.Instantiate(destroyVfx, modelTransform.position, Quaternion.identity), 3f);
		}
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcOnDestroyItem()
	{
		if (!base.isServer)
		{
			OnDestroyItem();
		}
	}

	protected static void InvokeUserCode_RpcOnDestroyItem(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnDestroyItem called on server.");
		}
		else
		{
			((ConsumableItem)obj).UserCode_RpcOnDestroyItem();
		}
	}

	protected void UserCode_RpcDestroyItemVFX()
	{
		if (!base.isServer)
		{
			DestroyItemVFX();
		}
	}

	protected static void InvokeUserCode_RpcDestroyItemVFX(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcDestroyItemVFX called on server.");
		}
		else
		{
			((ConsumableItem)obj).UserCode_RpcDestroyItemVFX();
		}
	}

	static ConsumableItem()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(ConsumableItem), "System.Void ConsumableItem::RpcOnDestroyItem()", InvokeUserCode_RpcOnDestroyItem);
		RemoteProcedureCalls.RegisterRpc(typeof(ConsumableItem), "System.Void ConsumableItem::RpcDestroyItemVFX()", InvokeUserCode_RpcDestroyItemVFX);
	}
}

}
