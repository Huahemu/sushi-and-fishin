namespace SushiAndFish.Casino.Original
{
using global::DG.Tweening;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::TMPro;
using global::UnityEngine;

public class RouletteButton : InteractableBase
{
	[Header("References")]
	[SerializeField]
	private Roulette roulette;

	[SerializeField]
	private Transform chips;

	[SerializeField]
	private TextMeshPro chipText;

	[SerializeField]
	private MMF_Player pressFb;

	[SerializeField]
	private MMF_Player warningFb;

	[Header("SFX")]
	[SerializeField]
	private EventReference chipSfx;

	public override void ServerOnInteract(PlayerInteract playerInteract)
	{
		base.ServerOnInteract(playerInteract);
		roulette.SelectBettingOption(base.name, this);
	}

	public override void RpcOnInteract(PlayerInteract playerInteract)
	{
		base.RpcOnInteract(playerInteract);
		pressFb.PlayFeedbacks();
	}

	[Server]
	public void ServerWarningFeedback()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void RouletteButton::ServerWarningFeedback()' called when server was not active");
		}
		else
		{
			RpcWarningFeedback();
		}
	}

	[ClientRpc]
	private void RpcWarningFeedback()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void RouletteButton::RpcWarningFeedback()", 651345437, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ServerSetBets(long max, long bet)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void RouletteButton::ServerSetBets(System.Int64,System.Int64)' called when server was not active");
		}
		else
		{
			RpcSetBets(max, bet);
		}
	}

	[ClientRpc]
	private void RpcSetBets(long max, long bet)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarLong(max);
		writer.WriteVarLong(bet);
		SendRPCInternal("System.Void RouletteButton::RpcSetBets(System.Int64,System.Int64)", -137259232, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcWarningFeedback()
	{
		warningFb.PlayFeedbacks();
	}

	protected static void InvokeUserCode_RpcWarningFeedback(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcWarningFeedback called on server.");
		}
		else
		{
			((RouletteButton)obj).UserCode_RpcWarningFeedback();
		}
	}

	protected void UserCode_RpcSetBets__Int64__Int64(long max, long bet)
	{
		float num = Mathf.Clamp01((float)bet / (float)max);
		float endValue = Mathf.Ceil(num * 10f) / 10f;
		chips.DOKill();
		if ((double)num <= 0.0001)
		{
			chips.DOLocalMoveY(0f, 0.2f).SetEase(Ease.OutCubic).OnComplete(() =>
			{
				chips.gameObject.SetActive(value: false);
			});
		}
		else
		{
			chips.gameObject.SetActive(value: true);
			chips.DOLocalMoveY(endValue, 0.2f).SetEase(Ease.OutCubic);
		}
		chipText.text = MoneyFormatter.FormatWithDollar(bet);
		SFXManager.SFXOneShot(chipSfx, base.transform.position);
	}

	protected static void InvokeUserCode_RpcSetBets__Int64__Int64(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetBets called on server.");
		}
		else
		{
			((RouletteButton)obj).UserCode_RpcSetBets__Int64__Int64(reader.ReadVarLong(), reader.ReadVarLong());
		}
	}

	static RouletteButton()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(RouletteButton), "System.Void RouletteButton::RpcWarningFeedback()", InvokeUserCode_RpcWarningFeedback);
		RemoteProcedureCalls.RegisterRpc(typeof(RouletteButton), "System.Void RouletteButton::RpcSetBets(System.Int64,System.Int64)", InvokeUserCode_RpcSetBets__Int64__Int64);
	}
}

}
