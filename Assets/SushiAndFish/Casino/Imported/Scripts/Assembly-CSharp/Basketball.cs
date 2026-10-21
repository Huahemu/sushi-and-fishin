namespace SushiAndFish.Casino.Original
{
using global::DG.Tweening;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class Basketball : ConsumableItem
{
	[Header("Settings")]
	[SerializeField]
	private float minBounceTime = 0.2f;

	[SerializeField]
	private float maxBounceTime = 0.3f;

	[SerializeField]
	private float bounceHorizontalDisplacement = 0.1f;

	[SerializeField]
	private float ballRadius = 0.5f;

	[SerializeField]
	private float scaleAmount = 0.5f;

	[Header("References")]
	[SerializeField]
	private Transform ballTransform;

	[SerializeField]
	private LayerMask rayMask;

	[SerializeField]
	private TrailRenderer trail;

	[SerializeField]
	private Animator anim;

	[Header("SFX")]
	[SerializeField]
	private SFXComponent bounceSfx;

	private readonly RaycastHit[] _raycastHits = new RaycastHit[8];

	private Rigidbody _holderRb;

	private bool _isBouncing;

	protected override void OnPickedUp(PlayerInventory playerInventory)
	{
		base.OnPickedUp(playerInventory);
		RpcOnPickedUp(playerInventory);
		RpcSetTrail(isEnabled: false);
	}

	[ClientRpc]
	private void RpcOnPickedUp(PlayerInventory playerInventory)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInventory);
		SendRPCInternal("System.Void Basketball::RpcOnPickedUp(PlayerInventory)", 2025474788, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		base.OnDropped(playerInventory);
		RpcResetBallPosition();
		RpcSetTrail(isEnabled: true);
	}

	[ClientRpc]
	private void RpcResetBallPosition()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Basketball::RpcResetBallPosition()", -33739656, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetTrail(bool isEnabled)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		SendRPCInternal("System.Void Basketball::RpcSetTrail(System.Boolean)", -1701249672, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	protected override void OnUseItem(bool isPressed)
	{
		if (isPressed)
		{
			Bounce();
		}
	}

	private void Bounce()
	{
		if (_isBouncing)
		{
			return;
		}
		Vector3 vector = Vector3.ProjectOnPlane(_holderRb.linearVelocity, Vector3.up);
		Vector3 origin = base.transform.position + vector * bounceHorizontalDisplacement;
		int num = Physics.RaycastNonAlloc(origin, Vector3.down, _raycastHits, float.PositiveInfinity, rayMask, QueryTriggerInteraction.Ignore);
		if (num == 0)
		{
			return;
		}
		_isBouncing = true;
		anim.SetTrigger("Bounce");
		RaycastHit raycastHit = _raycastHits[0];
		for (int i = 1; i < num; i++)
		{
			if (_raycastHits[i].distance < raycastHit.distance)
			{
				raycastHit = _raycastHits[i];
			}
		}
		float duration = Mathf.Clamp(raycastHit.distance * 0.01f + minBounceTime, minBounceTime, maxBounceTime);
		Vector3 endValue = new Vector3(origin.x, raycastHit.point.y + (ballRadius * 2f - scaleAmount) / 2f, origin.z);
		Sequence sequence = DOTween.Sequence();
		sequence.Append(ballTransform.DOMove(endValue, duration).SetEase(Ease.OutQuad));
		sequence.Join(ballTransform.DOScaleY(ballRadius * 2f - scaleAmount, duration).SetEase(Ease.OutCubic));
		sequence.AppendCallback(() =>
		{
			bounceSfx.PlayOneShotOverrideParamsWithCustomPos(ballTransform.position);
		});
		sequence.Append(ballTransform.DOLocalMove(Vector3.zero, duration).SetEase(Ease.InOutQuart));
		sequence.Join(ballTransform.DOScaleY(1f, duration).SetEase(Ease.InCubic));
		sequence.OnUpdate(() =>
		{
			ballTransform.rotation = Quaternion.identity;
		});
		sequence.OnComplete(() =>
		{
			_isBouncing = false;
		});
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcOnPickedUp__PlayerInventory(PlayerInventory playerInventory)
	{
		_holderRb = playerInventory.GetComponent<Rigidbody>();
	}

	protected static void InvokeUserCode_RpcOnPickedUp__PlayerInventory(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnPickedUp called on server.");
		}
		else
		{
			((Basketball)obj).UserCode_RpcOnPickedUp__PlayerInventory(reader.ReadNetworkBehaviour<PlayerInventory>());
		}
	}

	protected void UserCode_RpcResetBallPosition()
	{
		ballTransform.DOKill();
		ballTransform.localPosition = Vector3.zero;
		ballTransform.localRotation = Quaternion.identity;
	}

	protected static void InvokeUserCode_RpcResetBallPosition(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcResetBallPosition called on server.");
		}
		else
		{
			((Basketball)obj).UserCode_RpcResetBallPosition();
		}
	}

	protected void UserCode_RpcSetTrail__Boolean(bool isEnabled)
	{
		trail.emitting = isEnabled;
	}

	protected static void InvokeUserCode_RpcSetTrail__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetTrail called on server.");
		}
		else
		{
			((Basketball)obj).UserCode_RpcSetTrail__Boolean(reader.ReadBool());
		}
	}

	static Basketball()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(Basketball), "System.Void Basketball::RpcOnPickedUp(PlayerInventory)", InvokeUserCode_RpcOnPickedUp__PlayerInventory);
		RemoteProcedureCalls.RegisterRpc(typeof(Basketball), "System.Void Basketball::RpcResetBallPosition()", InvokeUserCode_RpcResetBallPosition);
		RemoteProcedureCalls.RegisterRpc(typeof(Basketball), "System.Void Basketball::RpcSetTrail(System.Boolean)", InvokeUserCode_RpcSetTrail__Boolean);
	}
}

}
