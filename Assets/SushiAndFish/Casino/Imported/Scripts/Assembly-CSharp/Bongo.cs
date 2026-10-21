namespace SushiAndFish.Casino.Original
{
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class Bongo : Item
{
	[SerializeField]
	private EventReference bongoHi;

	[SerializeField]
	private EventReference bongoLo;

	private PlayerController playerController;

	protected override void OnUseItem(bool isPressed)
	{
		if (base.isServer && isPressed && playerController != null)
		{
			float x = playerController.head.transform.rotation.x;
			SFXParams[] sFXParams = new SFXParams[1]
			{
				new SFXParams("Force", Mathf.Clamp01(Mathf.Abs(x * 1.5f)))
			};
			if (x > 0f)
			{
				SFXManager.SFXOneShotWithParameters(bongoLo, sFXParams, base.transform.position);
			}
			else
			{
				SFXManager.SFXOneShotWithParameters(bongoHi, sFXParams, base.transform.position);
			}
		}
	}

	[ClientRpc]
	private void PlayBongLo(SFXParams[] sFXParams)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_SFXParams_005B_005D(writer, sFXParams);
		SendRPCInternal("System.Void Bongo::PlayBongLo(SFXParams[])", 985448762, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void PlayBongHi(SFXParams[] sFXParams)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_SFXParams_005B_005D(writer, sFXParams);
		SendRPCInternal("System.Void Bongo::PlayBongHi(SFXParams[])", -2025937380, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	protected override void OnPickedUp(PlayerInventory playerInventory)
	{
		base.OnPickedUp(playerInventory);
		playerInventory.TryGetComponent<PlayerController>(out playerController);
	}

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		playerController = null;
		base.OnDropped(playerInventory);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_PlayBongLo__SFXParams_005B_005D(SFXParams[] sFXParams)
	{
		SFXManager.SFXOneShotWithParameters(bongoLo, sFXParams, base.transform.position);
	}

	protected static void InvokeUserCode_PlayBongLo__SFXParams_005B_005D(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC PlayBongLo called on server.");
		}
		else
		{
			((Bongo)obj).UserCode_PlayBongLo__SFXParams_005B_005D(GeneratedNetworkCode._Read_SFXParams_005B_005D(reader));
		}
	}

	protected void UserCode_PlayBongHi__SFXParams_005B_005D(SFXParams[] sFXParams)
	{
		SFXManager.SFXOneShotWithParameters(bongoHi, sFXParams, base.transform.position);
	}

	protected static void InvokeUserCode_PlayBongHi__SFXParams_005B_005D(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC PlayBongHi called on server.");
		}
		else
		{
			((Bongo)obj).UserCode_PlayBongHi__SFXParams_005B_005D(GeneratedNetworkCode._Read_SFXParams_005B_005D(reader));
		}
	}

	static Bongo()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(Bongo), "System.Void Bongo::PlayBongLo(SFXParams[])", InvokeUserCode_PlayBongLo__SFXParams_005B_005D);
		RemoteProcedureCalls.RegisterRpc(typeof(Bongo), "System.Void Bongo::PlayBongHi(SFXParams[])", InvokeUserCode_PlayBongHi__SFXParams_005B_005D);
	}
}

}
