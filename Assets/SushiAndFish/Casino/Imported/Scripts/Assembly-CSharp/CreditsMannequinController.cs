namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class CreditsMannequinController : NetworkBehaviour
{
	[Header("Components")]
	[SerializeField]
	private SteamIdComponent steamIdComponent;

	[Header("Cosmetic Mesh Targets")]
	[SerializeField]
	private MeshFilter hat;

	[SerializeField]
	private MeshFilter hair;

	[SerializeField]
	private MeshFilter mustache;

	[SerializeField]
	private MeshFilter beard;

	[SerializeField]
	private MeshFilter neckwear;

	[SerializeField]
	private MeshFilter clothing;

	[SerializeField]
	private MeshFilter facewear;

	[ClientRpc]
	public void RpcApplySnapshot(PlayerCreditsSnapshot snapshot)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WritePlayerCreditsSnapshot(snapshot);
		SendRPCInternal("System.Void CreditsMannequinController::RpcApplySnapshot(PlayerCreditsSnapshot)", 156465597, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public void ApplySnapshot(PlayerCreditsSnapshot snapshot)
	{
		if (snapshot != null)
		{
			if (steamIdComponent != null && snapshot.steamId != 0L)
			{
				steamIdComponent.SetSteamID(snapshot.steamId);
			}
			if (snapshot.cosmetics != null && snapshot.cosmetics.Count != 0)
			{
				ApplyCosmetics(snapshot.cosmetics);
			}
		}
	}

	private void ApplyCosmetics(List<PlayerCreditsSnapshot.CosmeticEntry> cosmetics)
	{
		foreach (PlayerCreditsSnapshot.CosmeticEntry cosmetic in cosmetics)
		{
			if (!CosmeticDataManager.HasCosmetic(cosmetic.cosmeticId))
			{
				continue;
			}
			CosmeticData cosmeticById = CosmeticDataManager.GetCosmeticById(cosmetic.cosmeticId);
			if (!(cosmeticById == null) && !(cosmeticById.cosmeticModel == null) && cosmeticById.cosmeticModel.TryGetComponent<MeshFilter>(out var component))
			{
				MeshFilter meshFilterForType = GetMeshFilterForType(cosmeticById.cosmeticType);
				if (!(meshFilterForType == null))
				{
					meshFilterForType.mesh = component.sharedMesh;
				}
			}
		}
	}

	private MeshFilter GetMeshFilterForType(CosmeticType type)
	{
		return type switch
		{
			CosmeticType.Hat => hat, 
			CosmeticType.Hair => hair, 
			CosmeticType.Mustache => mustache, 
			CosmeticType.Beard => beard, 
			CosmeticType.Neckwear => neckwear, 
			CosmeticType.Clothing => clothing, 
			CosmeticType.Facewear => facewear, 
			_ => null, 
		};
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcApplySnapshot__PlayerCreditsSnapshot(PlayerCreditsSnapshot snapshot)
	{
		ApplySnapshot(snapshot);
	}

	protected static void InvokeUserCode_RpcApplySnapshot__PlayerCreditsSnapshot(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcApplySnapshot called on server.");
		}
		else
		{
			((CreditsMannequinController)obj).UserCode_RpcApplySnapshot__PlayerCreditsSnapshot(reader.ReadPlayerCreditsSnapshot());
		}
	}

	static CreditsMannequinController()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(CreditsMannequinController), "System.Void CreditsMannequinController::RpcApplySnapshot(PlayerCreditsSnapshot)", InvokeUserCode_RpcApplySnapshot__PlayerCreditsSnapshot);
	}
}

}
