namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Runtime.InteropServices;
using global::DG.Tweening;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::UnityEngine;

public class Dice : Item
{
	[Header("Dice Settings")]
	[SerializeField]
	private float stopThreshold = 0.1f;

	[SerializeField]
	private float checkDelay = 0.5f;

	[SerializeField]
	private AnimationCurve jumpCurve;

	[Header("References")]
	[SerializeField]
	private Transform[] faces;

	[SerializeField]
	private Transform diceModel;

	[SerializeField]
	private Animator anim;

	[SerializeField]
	private MMF_Player onHitFb;

	[SerializeField]
	private TrailRenderer trail;

	[SyncVar]
	private int _randomIndex;

	[Header("SFX")]
	[SerializeField]
	private SFXComponent shakeSfx;

	private static readonly Quaternion[] DiceRotations;

	private bool _isRolling;

	private bool _isShaking;

	private Quaternion _currentRotation = Quaternion.identity;

	public int Network_randomIndex
	{
		get
		{
			return _randomIndex;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _randomIndex, 2uL, null);
		}
	}

	public event Action<Dice, int> OnDiceStopped;

	public override void OnStartServer()
	{
		base.OnStartServer();
		Network_randomIndex = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
	}

	private global::System.Random GetRandom()
	{
		int randomIndex = _randomIndex;
		Network_randomIndex = _randomIndex + 1;
		long num = (randomIndex * 2654435761u + randomIndex) * 2654435761u + randomIndex;
		long num2 = (num ^ (num >> 32)) * 2246822507u;
		long num3 = (num2 ^ (num2 >> 16)) * 3266489917u;
		return new global::System.Random((int)(num3 ^ (num3 >> 13)));
	}

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		base.OnDropped(playerInventory);
		diceModel.DOKill();
		diceModel.localPosition = Vector3.zero;
		diceModel.localRotation = _currentRotation;
		_isRolling = true;
		IsInteractable = false;
		CursorType = CursorManager.CursorType.Default;
		trail.emitting = true;
		RpcSetInteractable(isInteractable: false);
		StartCoroutine(CheckIfStopped());
	}

	protected override void OnPickedUp(PlayerInventory playerInventory)
	{
		base.OnPickedUp(playerInventory);
		RpcSetDiceRotation();
	}

	private void OnCollisionEnter(Collision other)
	{
		if (base.isServer && !(other.impulse.sqrMagnitude <= 0.01f))
		{
			RpcOnHitVFX();
		}
	}

	[ClientRpc]
	private void RpcOnHitVFX()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Dice::RpcOnHitVFX()", -267509617, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	protected override void OnUseItem(bool isPressed)
	{
		base.OnUseItem(isPressed);
		if (!_isShaking)
		{
			_isShaking = true;
			int rotationIndex = GetRandom().Next(0, DiceRotations.Length);
			StartCoroutine(ShakeDice(rotationIndex));
			shakeSfx.PlayOneShotAttached();
		}
	}

	private IEnumerator ShakeDice(int rotationIndex)
	{
		anim.SetTrigger("Shake");
		diceModel.DOLocalJump(Vector3.zero, 0.5f, 1, 0.5f).SetEase(jumpCurve);
		_currentRotation = DiceRotations[rotationIndex];
		diceModel.DOLocalRotate(_currentRotation.eulerAngles, 0.5f).SetEase(Ease.OutQuad);
		yield return new WaitForSeconds(0.5f);
		_isShaking = false;
	}

	[ClientRpc]
	private void RpcSetDiceRotation()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Dice::RpcSetDiceRotation()", 518578692, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override void ServerThrow(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
	{
		if ((bool)base.NetworkHolder)
		{
			ServerDrop();
		}
		Rb.Teleport(position);
		Rb.Rotate(rotation);
		Rb.linearVelocity = velocity;
		Rb.angularVelocity = angularVelocity * 10f;
	}

	private IEnumerator CheckIfStopped()
	{
		float timer = 0f;
		while (_isRolling)
		{
			timer += Time.deltaTime;
			if (timer >= 10f)
			{
				StopDice();
				break;
			}
			if (Rb.linearVelocity.magnitude < stopThreshold && Rb.angularVelocity.magnitude < stopThreshold)
			{
				yield return new WaitForSeconds(checkDelay);
				if (Rb.linearVelocity.magnitude < stopThreshold && Rb.angularVelocity.magnitude < stopThreshold)
				{
					StopDice();
					break;
				}
			}
			yield return null;
		}
	}

	private void StopDice()
	{
		_isRolling = false;
		IsInteractable = true;
		CursorType = CursorManager.CursorType.Interact;
		trail.emitting = false;
		RpcSetInteractable(isInteractable: true);
		Rb.linearVelocity = Vector3.zero;
		Rb.angularVelocity = Vector3.zero;
		OnDiceStopped?.Invoke(this, GetTopFaceIndex());
	}

	private int GetTopFaceIndex()
	{
		int result = 0;
		float num = float.NegativeInfinity;
		for (int i = 0; i < faces.Length; i++)
		{
			if (faces[i].position.y > num)
			{
				num = faces[i].position.y;
				result = i + 1;
			}
		}
		return result;
	}

	[Server]
	public void LockDice(bool isLocked)
	{
		// Scene reset can destroy the rigidbody before Craps.OnDisable resets its dice.
		if (!this || !Rb) return;
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Dice::LockDice(System.Boolean)' called when server was not active");
			return;
		}
		IsInteractable = !isLocked;
		CursorType = ((!isLocked) ? CursorManager.CursorType.Interact : CursorManager.CursorType.Default);
		RpcSetInteractable(!isLocked);
		Rb.isKinematic = isLocked;
	}

	[ClientRpc]
	private void RpcSetInteractable(bool isInteractable)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isInteractable);
		SendRPCInternal("System.Void Dice::RpcSetInteractable(System.Boolean)", -1328582806, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ServerResetDice(Vector3 pos)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Dice::ServerResetDice(UnityEngine.Vector3)' called when server was not active");
			return;
		}
		Rb.Teleport(pos);
		Rb.Rotate(Quaternion.identity);
	}

	static Dice()
	{
		DiceRotations = new Quaternion[6]
		{
			Quaternion.Euler(0f, 0f, 0f),
			Quaternion.Euler(0f, 0f, 90f),
			Quaternion.Euler(90f, 0f, 0f),
			Quaternion.Euler(-90f, 0f, 0f),
			Quaternion.Euler(0f, 0f, -90f),
			Quaternion.Euler(180f, 0f, 0f)
		};
		RemoteProcedureCalls.RegisterRpc(typeof(Dice), "System.Void Dice::RpcOnHitVFX()", InvokeUserCode_RpcOnHitVFX);
		RemoteProcedureCalls.RegisterRpc(typeof(Dice), "System.Void Dice::RpcSetDiceRotation()", InvokeUserCode_RpcSetDiceRotation);
		RemoteProcedureCalls.RegisterRpc(typeof(Dice), "System.Void Dice::RpcSetInteractable(System.Boolean)", InvokeUserCode_RpcSetInteractable__Boolean);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcOnHitVFX()
	{
		onHitFb.PlayFeedbacks();
	}

	protected static void InvokeUserCode_RpcOnHitVFX(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnHitVFX called on server.");
		}
		else
		{
			((Dice)obj).UserCode_RpcOnHitVFX();
		}
	}

	protected void UserCode_RpcSetDiceRotation()
	{
		int num = GetRandom().Next(0, DiceRotations.Length);
		_currentRotation = DiceRotations[num];
		diceModel.localRotation = _currentRotation;
	}

	protected static void InvokeUserCode_RpcSetDiceRotation(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetDiceRotation called on server.");
		}
		else
		{
			((Dice)obj).UserCode_RpcSetDiceRotation();
		}
	}

	protected void UserCode_RpcSetInteractable__Boolean(bool isInteractable)
	{
		if (!base.isServer)
		{
			IsInteractable = isInteractable;
			CursorType = (isInteractable ? CursorManager.CursorType.Interact : CursorManager.CursorType.Default);
			trail.emitting = !isInteractable;
			if (!isInteractable)
			{
				diceModel.DOKill();
				diceModel.localPosition = Vector3.zero;
				diceModel.localRotation = _currentRotation;
			}
		}
	}

	protected static void InvokeUserCode_RpcSetInteractable__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetInteractable called on server.");
		}
		else
		{
			((Dice)obj).UserCode_RpcSetInteractable__Boolean(reader.ReadBool());
		}
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteVarInt(_randomIndex);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteVarInt(_randomIndex);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _randomIndex, null, reader.ReadVarInt());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _randomIndex, null, reader.ReadVarInt());
		}
	}
}

}
