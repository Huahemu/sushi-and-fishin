namespace SushiAndFish.Casino.Original
{
using global::DG.Tweening;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::UnityEngine;

public class MoneyWheelButton : InteractableBase
{
	[SerializeField]
	private MoneyWheel moneyWheel;

	[SerializeField]
	private Transform modelTransform;

	[SerializeField]
	private MMF_Player pressFb;

	public string betOption;

	private float _localScaleZ;

	protected override void OnAwake()
	{
		base.OnAwake();
		_localScaleZ = modelTransform.localScale.z;
	}

	public override void ServerOnInteract(PlayerInteract playerInteract)
	{
		base.ServerOnInteract(playerInteract);
		moneyWheel.SelectBettingOption(betOption);
	}

	public void SelectFeedBack(bool isSelected)
	{
		RpcSelectFeedBack(isSelected);
	}

	[ClientRpc]
	private void RpcSelectFeedBack(bool isSelected)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isSelected);
		SendRPCInternal("System.Void MoneyWheelButton::RpcSelectFeedBack(System.Boolean)", -1914425721, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override void RpcOnInteract(PlayerInteract playerInteract)
	{
		base.RpcOnInteract(playerInteract);
		pressFb.PlayFeedbacks();
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSelectFeedBack__Boolean(bool isSelected)
	{
		float num = _localScaleZ;
		if (isSelected)
		{
			num -= 0.5f;
		}
		modelTransform.DOScaleZ(num, 0.3f).SetEase(isSelected ? Ease.OutBack : Ease.InBack);
	}

	protected static void InvokeUserCode_RpcSelectFeedBack__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSelectFeedBack called on server.");
		}
		else
		{
			((MoneyWheelButton)obj).UserCode_RpcSelectFeedBack__Boolean(reader.ReadBool());
		}
	}

	static MoneyWheelButton()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(MoneyWheelButton), "System.Void MoneyWheelButton::RpcSelectFeedBack(System.Boolean)", InvokeUserCode_RpcSelectFeedBack__Boolean);
	}
}

}
