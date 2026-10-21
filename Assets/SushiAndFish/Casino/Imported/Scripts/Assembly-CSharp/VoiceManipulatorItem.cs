namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;

public class VoiceManipulatorItem : Item
{
	[Header("Settings")]
	[SerializeField]
	private VoipManipulationManager.VoipFX fXType;

	[SerializeField]
	private TextMeshPro fXText;

	private PlayerVoiceFX playerVoiceFX;

	public override void OnStartClient()
	{
		if (base.isServer)
		{
			RpcUpdateText(fXType.ToString());
		}
	}

	protected override void OnUseItem(bool isPressed)
	{
		if (base.isServer && isPressed && playerVoiceFX != null)
		{
			fXType = fXType.Next();
			playerVoiceFX.RpcStartVoiceFX(fXType);
			RpcUpdateText(fXType.ToString());
		}
	}

	[ClientRpc]
	private void RpcUpdateText(string fxTypeText)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(fxTypeText);
		SendRPCInternal("System.Void VoiceManipulatorItem::RpcUpdateText(System.String)", -1004062755, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	protected override void OnPickedUp(PlayerInventory playerInventory)
	{
		base.OnPickedUp(playerInventory);
		playerInventory.TryGetComponent<PlayerVoiceFX>(out playerVoiceFX);
	}

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		playerVoiceFX.CmdResetVoiceFX();
		playerVoiceFX = null;
		base.OnDropped(playerInventory);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcUpdateText__String(string fxTypeText)
	{
		fXText.text = fxTypeText;
	}

	protected static void InvokeUserCode_RpcUpdateText__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcUpdateText called on server.");
		}
		else
		{
			((VoiceManipulatorItem)obj).UserCode_RpcUpdateText__String(reader.ReadString());
		}
	}

	static VoiceManipulatorItem()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(VoiceManipulatorItem), "System.Void VoiceManipulatorItem::RpcUpdateText(System.String)", InvokeUserCode_RpcUpdateText__String);
	}
}

}
