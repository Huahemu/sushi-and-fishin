namespace SushiAndFish.Casino.Original
{
using global::DG.Tweening;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class BossAnnouncement : NetworkBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float slideDownDistance = 5f;

	[SerializeField]
	private float slideDuration = 1f;

	[SerializeField]
	private Ease slideEase = Ease.OutQuad;

	private Vector3 _startPosition;

	private Vector3 _targetPosition;

	private bool _isAnimating;

	private void Awake()
	{
		_startPosition = base.transform.localPosition;
		_targetPosition = _startPosition - Vector3.up * slideDownDistance;
	}

	public void StartSlideIn()
	{
		if (base.isServer)
		{
			RpcStartSlideIn();
		}
	}

	[ClientRpc]
	private void RpcStartSlideIn()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void BossAnnouncement::RpcStartSlideIn()", 106223176, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void SlideIn()
	{
		if (_isAnimating)
		{
			return;
		}
		_isAnimating = true;
		base.transform.localPosition = _startPosition;
		base.transform.DOLocalMove(_targetPosition, slideDuration).SetEase(slideEase).OnComplete(() =>
		{
			_isAnimating = false;
			if (base.isServer)
			{
				RpcStartSpeech();
			}
		});
	}

	public void StartSlideOut()
	{
		if (base.isServer)
		{
			RpcStartSlideOut();
		}
	}

	[ClientRpc]
	private void RpcStartSlideOut()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void BossAnnouncement::RpcStartSlideOut()", 1113800275, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void SlideOut()
	{
		if (!_isAnimating)
		{
			_isAnimating = true;
			base.transform.DOLocalMove(_startPosition, slideDuration).SetEase(slideEase).OnComplete(() =>
			{
				_isAnimating = false;
			});
		}
	}

	[ClientRpc]
	private void RpcStartSpeech()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void BossAnnouncement::RpcStartSpeech()", -1381309830, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void InitializeSpeech()
	{
		Debug.Log("BossAnnouncement: Starting speech");
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcStartSlideIn()
	{
		SlideIn();
	}

	protected static void InvokeUserCode_RpcStartSlideIn(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcStartSlideIn called on server.");
		}
		else
		{
			((BossAnnouncement)obj).UserCode_RpcStartSlideIn();
		}
	}

	protected void UserCode_RpcStartSlideOut()
	{
		SlideOut();
	}

	protected static void InvokeUserCode_RpcStartSlideOut(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcStartSlideOut called on server.");
		}
		else
		{
			((BossAnnouncement)obj).UserCode_RpcStartSlideOut();
		}
	}

	protected void UserCode_RpcStartSpeech()
	{
		InitializeSpeech();
	}

	protected static void InvokeUserCode_RpcStartSpeech(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcStartSpeech called on server.");
		}
		else
		{
			((BossAnnouncement)obj).UserCode_RpcStartSpeech();
		}
	}

	static BossAnnouncement()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(BossAnnouncement), "System.Void BossAnnouncement::RpcStartSlideIn()", InvokeUserCode_RpcStartSlideIn);
		RemoteProcedureCalls.RegisterRpc(typeof(BossAnnouncement), "System.Void BossAnnouncement::RpcStartSlideOut()", InvokeUserCode_RpcStartSlideOut);
		RemoteProcedureCalls.RegisterRpc(typeof(BossAnnouncement), "System.Void BossAnnouncement::RpcStartSpeech()", InvokeUserCode_RpcStartSpeech);
	}
}

}
