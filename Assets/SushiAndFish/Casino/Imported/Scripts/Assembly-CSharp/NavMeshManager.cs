namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::Unity.AI.Navigation;
using global::UnityEngine;

public class NavMeshManager : NetworkSingleton<NavMeshManager>
{
	[Server]
	public void InitializeNavMesh()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void NavMeshManager::InitializeNavMesh()' called when server was not active");
		}
		else
		{
			RpcInitializeNavMesh();
		}
	}

	[ClientRpc]
	public void RpcInitializeNavMesh()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void NavMeshManager::RpcInitializeNavMesh()", -1113796349, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ClearNavMesh()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void NavMeshManager::ClearNavMesh()' called when server was not active");
		}
		else
		{
			RpcClearNavMesh();
		}
	}

	[ClientRpc]
	public void RpcClearNavMesh()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void NavMeshManager::RpcClearNavMesh()", -110519022, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcInitializeNavMesh()
	{
		NavMeshSurface component = GetComponent<NavMeshSurface>();
		component.RemoveData();
		component.BuildNavMesh();
	}

	protected static void InvokeUserCode_RpcInitializeNavMesh(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcInitializeNavMesh called on server.");
		}
		else
		{
			((NavMeshManager)obj).UserCode_RpcInitializeNavMesh();
		}
	}

	protected void UserCode_RpcClearNavMesh()
	{
		NavMeshSurface component = GetComponent<NavMeshSurface>();
		if ((bool)component.navMeshData)
		{
			component.RemoveData();
		}
	}

	protected static void InvokeUserCode_RpcClearNavMesh(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcClearNavMesh called on server.");
		}
		else
		{
			((NavMeshManager)obj).UserCode_RpcClearNavMesh();
		}
	}

	static NavMeshManager()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(NavMeshManager), "System.Void NavMeshManager::RpcInitializeNavMesh()", InvokeUserCode_RpcInitializeNavMesh);
		RemoteProcedureCalls.RegisterRpc(typeof(NavMeshManager), "System.Void NavMeshManager::RpcClearNavMesh()", InvokeUserCode_RpcClearNavMesh);
	}
}

}
