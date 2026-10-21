namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class CasinoBuilding : NetworkBehaviour
{
	[SerializeField]
	private VehicleDoors vehicle;

	[ClientRpc]
	public void RpcCloseTheGate()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void CasinoBuilding::RpcCloseTheGate()", -891921159, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ServerSpawnGoToHomeVehicle()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void CasinoBuilding::ServerSpawnGoToHomeVehicle()' called when server was not active");
			return;
		}
		if (!vehicle)
		{
			vehicle = Object.FindFirstObjectByType<VehicleDoors>(FindObjectsInactive.Include);
		}
		vehicle.ServerOpenDoors();
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcCloseTheGate()
	{
	}

	protected static void InvokeUserCode_RpcCloseTheGate(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcCloseTheGate called on server.");
		}
		else
		{
			((CasinoBuilding)obj).UserCode_RpcCloseTheGate();
		}
	}

	static CasinoBuilding()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(CasinoBuilding), "System.Void CasinoBuilding::RpcCloseTheGate()", InvokeUserCode_RpcCloseTheGate);
	}
}

}
