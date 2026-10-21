namespace SushiAndFish.Casino.Original
{
using global::DG.Tweening;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class PlayerHandButtonAnimation : NetworkBehaviour
{
	[Header("References")]
	[SerializeField]
	private Transform[] handBones = new Transform[2];

	[SerializeField]
	private NetworkAnimator networkAnimator;

	[Header("Animation Settings")]
	[SerializeField]
	private float moveDuration = 0.1f;

	[SerializeField]
	private float pressDistance = 0.1f;

	[SerializeField]
	private Vector3 pressRotation;

	private Vector3[] _originalLocalPositions;

	private Vector3[] _originalLocalRotations;

	private void Awake()
	{
		_originalLocalPositions = new Vector3[handBones.Length];
		for (int i = 0; i < handBones.Length; i++)
		{
			if (handBones[i] != null)
			{
				_originalLocalPositions[i] = handBones[i].localPosition;
			}
		}
		_originalLocalRotations = new Vector3[handBones.Length];
		for (int j = 0; j < handBones.Length; j++)
		{
			if (handBones[j] != null)
			{
				_originalLocalRotations[j] = handBones[j].localEulerAngles;
			}
		}
	}

	public void PressButton(Transform buttonTransform)
	{
        if (CasinoRuntimeContext.IsHostIntegration) { if (CasinoRuntimeContext.IsCasinoButton?.Invoke(buttonTransform) == true) CasinoRuntimeContext.ButtonPressed?.Invoke(buttonTransform, Random.Range(0, 2)); return; }
		if (handBones != null && handBones.Length != 0 && (bool)buttonTransform)
		{
			int num = Random.Range(0, handBones.Length);
			bool num2 = num == 0;
			int num3 = (num2 ? 1 : (-1));
			Vector3 buttonPos = buttonTransform.position + buttonTransform.forward * pressDistance;
			Quaternion quaternion = Quaternion.Euler(pressRotation.x, pressRotation.y * (float)num3, pressRotation.z * (float)num3);
			Quaternion buttonRot = buttonTransform.rotation * quaternion;
			string trigger = (num2 ? "PressRight" : "PressLeft");
			networkAnimator.SetTrigger(trigger);
			HandBoneAnimation(num, buttonPos, buttonRot);
			CmdPressButton(num, buttonPos, buttonRot);
		}
	}

	[Command]
	private void CmdPressButton(int handIndex, Vector3 buttonPos, Quaternion buttonRot)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(handIndex);
		writer.WriteVector3(buttonPos);
		writer.WriteQuaternion(buttonRot);
		SendCommandInternal("System.Void PlayerHandButtonAnimation::CmdPressButton(System.Int32,UnityEngine.Vector3,UnityEngine.Quaternion)", 434843827, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcPressButton(int handIndex, Vector3 buttonPos, Quaternion buttonRot)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(handIndex);
		writer.WriteVector3(buttonPos);
		writer.WriteQuaternion(buttonRot);
		SendRPCInternal("System.Void PlayerHandButtonAnimation::RpcPressButton(System.Int32,UnityEngine.Vector3,UnityEngine.Quaternion)", -483286476, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void HandBoneAnimation(int handIndex, Vector3 buttonPos, Quaternion buttonRot)
	{
		Transform transform = handBones[handIndex];
		if ((bool)transform)
		{
			transform.DOKill(complete: true);
			Sequence s = DOTween.Sequence();
			s.Append(transform.DOMove(buttonPos, moveDuration).SetEase(Ease.OutCirc));
			s.Join(transform.DORotateQuaternion(buttonRot, moveDuration).SetEase(Ease.OutCirc));
			s.Append(transform.DOLocalMove(_originalLocalPositions[handIndex], moveDuration).SetEase(Ease.InOutCirc));
			s.Join(transform.DOLocalRotate(_originalLocalRotations[handIndex], moveDuration).SetEase(Ease.InOutCirc));
		}
	}

	public void LocalResetHands()
	{
		for (int i = 0; i < handBones.Length; i++)
		{
			Transform obj = handBones[i];
			obj.DOKill();
			obj.localPosition = _originalLocalPositions[i];
			obj.localEulerAngles = _originalLocalRotations[i];
		}
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdPressButton__Int32__Vector3__Quaternion(int handIndex, Vector3 buttonPos, Quaternion buttonRot)
	{
		RpcPressButton(handIndex, buttonPos, buttonRot);
	}

	protected static void InvokeUserCode_CmdPressButton__Int32__Vector3__Quaternion(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdPressButton called on client.");
		}
		else
		{
			((PlayerHandButtonAnimation)obj).UserCode_CmdPressButton__Int32__Vector3__Quaternion(reader.ReadVarInt(), reader.ReadVector3(), reader.ReadQuaternion());
		}
	}

	protected void UserCode_RpcPressButton__Int32__Vector3__Quaternion(int handIndex, Vector3 buttonPos, Quaternion buttonRot)
	{
		if (!base.isLocalPlayer)
		{
			HandBoneAnimation(handIndex, buttonPos, buttonRot);
		}
	}

	protected static void InvokeUserCode_RpcPressButton__Int32__Vector3__Quaternion(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPressButton called on server.");
		}
		else
		{
			((PlayerHandButtonAnimation)obj).UserCode_RpcPressButton__Int32__Vector3__Quaternion(reader.ReadVarInt(), reader.ReadVector3(), reader.ReadQuaternion());
		}
	}

	static PlayerHandButtonAnimation()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerHandButtonAnimation), "System.Void PlayerHandButtonAnimation::CmdPressButton(System.Int32,UnityEngine.Vector3,UnityEngine.Quaternion)", InvokeUserCode_CmdPressButton__Int32__Vector3__Quaternion, requiresAuthority: true);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerHandButtonAnimation), "System.Void PlayerHandButtonAnimation::RpcPressButton(System.Int32,UnityEngine.Vector3,UnityEngine.Quaternion)", InvokeUserCode_RpcPressButton__Int32__Vector3__Quaternion);
	}
}

}
