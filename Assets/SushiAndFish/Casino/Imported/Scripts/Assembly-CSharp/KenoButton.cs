namespace SushiAndFish.Casino.Original
{
using global::DG.Tweening;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::UnityEngine;

public class KenoButton : InteractableBase
{
	[SerializeField]
	private Keno keno;

	[SerializeField]
	private MeshRenderer rend;

	[SerializeField]
	private Transform diamond;

	[SerializeField]
	private MeshRenderer diamondRenderer;

	[SerializeField]
	private ParticleSystem matchParticles;

	[SerializeField]
	private MMF_Player matchFb;

	[SerializeField]
	private MMF_Player pressFb;

	[SerializeField]
	private Material selectedMaterial;

	[SerializeField]
	private Material notSelectedMaterial;

	private bool _isSelected;

	private Material _diamondMaterial;

	private Vector3 _scale;

	protected override void OnAwake()
	{
		base.OnAwake();
		_diamondMaterial = diamondRenderer.material;
		_scale = rend.transform.localScale;
	}

	public override void ServerOnInteract(PlayerInteract playerInteract)
	{
		if (!keno.isPlaying)
		{
			base.ServerOnInteract(playerInteract);
			_isSelected = keno.SelectButton(this);
			RpcChangeMaterial(_isSelected);
		}
	}

	[ClientRpc]
	private void RpcChangeMaterial(bool isSelected)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isSelected);
		SendRPCInternal("System.Void KenoButton::RpcChangeMaterial(System.Boolean)", -822695651, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ServerRevealDiamond(bool isTrue)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void KenoButton::ServerRevealDiamond(System.Boolean)' called when server was not active");
		}
		else
		{
			RpcRevealDiamond(isTrue, _isSelected);
		}
	}

	[ClientRpc]
	private void RpcRevealDiamond(bool isTrue, bool isSelected)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isTrue);
		writer.WriteBool(isSelected);
		SendRPCInternal("System.Void KenoButton::RpcRevealDiamond(System.Boolean,System.Boolean)", -326007306, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ServerWarningFeedback()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void KenoButton::ServerWarningFeedback()' called when server was not active");
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
		SendRPCInternal("System.Void KenoButton::RpcWarningFeedback()", -1152120922, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcChangeMaterial__Boolean(bool isSelected)
	{
		rend.material = (isSelected ? selectedMaterial : notSelectedMaterial);
		TooltipMessage = (isSelected ? "[E] Unselect" : "[E] Select");
		pressFb.PlayFeedbacks();
	}

	protected static void InvokeUserCode_RpcChangeMaterial__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcChangeMaterial called on server.");
		}
		else
		{
			((KenoButton)obj).UserCode_RpcChangeMaterial__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcRevealDiamond__Boolean__Boolean(bool isTrue, bool isSelected)
	{
		if (!isTrue)
		{
			diamond.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() =>
			{
				Color grey = Color.grey;
				Color grey2 = Color.grey;
				_diamondMaterial.color = grey;
				_diamondMaterial.SetColor("_EmissionColor", grey2);
			});
			matchParticles.Stop();
		}
		else if (isSelected)
		{
			diamond.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
			matchFb.PlayFeedbacks();
			Color antiqueWhite = Color.antiqueWhite;
			Color white = Color.white;
			_diamondMaterial.color = antiqueWhite;
			_diamondMaterial.SetColor("_EmissionColor", white);
		}
		else
		{
			diamond.DOScale(Vector3.one * 0.8f, 0.2f).SetEase(Ease.OutBack);
		}
	}

	protected static void InvokeUserCode_RpcRevealDiamond__Boolean__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcRevealDiamond called on server.");
		}
		else
		{
			((KenoButton)obj).UserCode_RpcRevealDiamond__Boolean__Boolean(reader.ReadBool(), reader.ReadBool());
		}
	}

	protected void UserCode_RpcWarningFeedback()
	{
		Transform target = rend.transform;
		target.DOKill();
		Vector3 endValue = _scale * 1.2f;
		Sequence s = DOTween.Sequence();
		s.Append(target.DOScale(endValue, 0.15f).SetEase(Ease.OutQuad));
		s.Append(target.DOScale(_scale, 0.15f).SetEase(Ease.InQuad));
	}

	protected static void InvokeUserCode_RpcWarningFeedback(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcWarningFeedback called on server.");
		}
		else
		{
			((KenoButton)obj).UserCode_RpcWarningFeedback();
		}
	}

	static KenoButton()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(KenoButton), "System.Void KenoButton::RpcChangeMaterial(System.Boolean)", InvokeUserCode_RpcChangeMaterial__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(KenoButton), "System.Void KenoButton::RpcRevealDiamond(System.Boolean,System.Boolean)", InvokeUserCode_RpcRevealDiamond__Boolean__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(KenoButton), "System.Void KenoButton::RpcWarningFeedback()", InvokeUserCode_RpcWarningFeedback);
	}
}

}
