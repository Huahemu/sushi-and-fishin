namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;
using global::DG.Tweening;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class PlayerController : NetworkBehaviour
{
	public enum PlayerState
	{
		Free = 0,
		Ragdoll = 1,
		Locked = 2
	}

	[Header("References")]
	public PlayerHead head;

	[SerializeField]
	private SFXPhysicsObject sfxPhysicsObject;

	[SyncVar]
	private PlayerState _state;

	[SyncVar]
	public bool hasBody = true;

	[SyncVar]
	public bool isGrounded;

	private bool _canJump;

	private bool _isLocked;

	private Rigidbody _rb;

	private PlayerSettings _ps;

	private PlayerInventory _pi;

	private PlayerCarry _pc;

	private Vector3 _groundVector = Vector3.down;

	private Vector3 _horizontalMoveDirection;

	private Vector2 _moveInput;

	private float _lastJumpTime;

	private Vector3 _previousVelocity;

	[SyncVar]
	public Vector3 serverVelocity;

	private RaycastHit[] _groundCheckHits = new RaycastHit[8];

	private RaycastHit[] _jumpCheckHits = new RaycastHit[8];

	private Coroutine _ragdollRoutine;

	[ReadOnly]
	public PlayerState State
	{
		get
		{
			return _state;
		}
		set
		{
			if (_ragdollRoutine != null)
			{
				StopCoroutine(_ragdollRoutine);
			}
			if (value == PlayerState.Ragdoll)
			{
				_ragdollRoutine = StartCoroutine(DelayedDisableRagdoll());
			}
			if (value != _state)
			{
				Network_state = value;
				SetPlayerState(value);
			}
		}
	}

	public bool IsLocked
	{
		get
		{
			return _isLocked;
		}
		set
		{
			if (value != _isLocked)
			{
				_isLocked = value;
				if (_isLocked)
				{
					Crouch(isPressed: false);
					_moveInput = Vector3.zero;
					_horizontalMoveDirection = Vector3.zero;
				}
			}
		}
	}

	public PlayerState Network_state
	{
		get
		{
			return _state;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _state, 1uL, null);
		}
	}

	public bool NetworkhasBody
	{
		get
		{
			return hasBody;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref hasBody, 2uL, null);
		}
	}

	public bool NetworkisGrounded
	{
		get
		{
			return isGrounded;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref isGrounded, 4uL, null);
		}
	}

	public Vector3 NetworkserverVelocity
	{
		get
		{
			return serverVelocity;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref serverVelocity, 8uL, null);
		}
	}

	public event Action<bool> OnClientJumped;

	public event Action<float> OnClientLanded;

	public event Action<bool> OnClientCrouched;

	private void Awake()
	{
		_ps = SushiAndFish.Casino.Original.CasinoResources.Load<PlayerSettings>("PlayerSettings");
		_rb = GetComponent<Rigidbody>();
		_pc = GetComponent<PlayerCarry>();
		_pi = GetComponent<PlayerInventory>();
		if (!head)
		{
			head = GetComponentInChildren<PlayerHead>();
		}
		_rb.freezeRotation = true;
	}

	private void OnEnable()
	{
		InputEvents.OnMoveEvent = (Action<Vector2>)Delegate.Combine(InputEvents.OnMoveEvent, new Action<Vector2>(OnMove));
		InputEvents.OnJumpEvent = (Action<bool>)Delegate.Combine(InputEvents.OnJumpEvent, new Action<bool>(OnJump));
		InputEvents.OnCrouchEvent = (Action<bool>)Delegate.Combine(InputEvents.OnCrouchEvent, new Action<bool>(OnCrouch));
	}

	private void OnDisable()
	{
		InputEvents.OnMoveEvent = (Action<Vector2>)Delegate.Remove(InputEvents.OnMoveEvent, new Action<Vector2>(OnMove));
		InputEvents.OnJumpEvent = (Action<bool>)Delegate.Remove(InputEvents.OnJumpEvent, new Action<bool>(OnJump));
		InputEvents.OnCrouchEvent = (Action<bool>)Delegate.Remove(InputEvents.OnCrouchEvent, new Action<bool>(OnCrouch));
	}

	private void FixedUpdate()
	{
        if (CasinoRuntimeContext.IsHostIntegration) { _rb.isKinematic = true; _rb.detectCollisions = false; return; }
		CheckGround();
		MovePlayer();
		StepClimb();
		ApplyGravity();
		HandleVelocityChange();
		SendVelocity();
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		head.transform.DOLocalMoveY(_ps.headHeight, _ps.headMoveDuration).SetEase(Ease.InOutSine);
		if (!base.isLocalPlayer)
		{
			base.enabled = false;
		}
		else
		{
			_rb.interpolation = RigidbodyInterpolation.Interpolate;
		}
	}

	private void OnMove(Vector2 input)
	{
		_moveInput = input;
	}

	private void OnJump(bool isPressed)
	{
		if (isPressed && !(Time.time - _lastJumpTime < 0.05f))
		{
			_lastJumpTime = Time.time;
			CheckJump();
			Jump();
		}
	}

	private void OnCrouch(bool isPressed)
	{
		Crouch(isPressed);
	}

	private void CheckGround()
	{
		Vector3 vector = base.transform.position + base.transform.up * (_ps.playerRadius - _ps.playerHeadRadius);
		Vector3 origin = vector + Vector3.up * 0.04f;
		float maxDistance = _ps.groundCheckDistance + 0.05f;
		float radius = _ps.playerRadius - 0.01f;
		if (!hasBody)
		{
			vector = base.transform.position;
			origin = vector + Vector3.up * 0.04f;
			maxDistance = _ps.groundCheckDistance + 0.05f;
			radius = _ps.playerHeadRadius - 0.01f;
		}
		int num = Physics.SphereCastNonAlloc(new Ray(origin, Vector3.down), radius, _groundCheckHits, maxDistance, _ps.groundMask, QueryTriggerInteraction.Ignore);
		List<Vector3> list = new List<Vector3>();
		for (int i = 0; i < num; i++)
		{
			RaycastHit raycastHit = _groundCheckHits[i];
			if ((bool)raycastHit.collider && (!raycastHit.collider.attachedRigidbody || !(raycastHit.collider.attachedRigidbody == _rb)) && !(Vector3.Angle(raycastHit.point - vector, Vector3.down) > _ps.maxSlopeAngle))
			{
				list.Add(raycastHit.point);
			}
		}
		if (list.Count > 0)
		{
			Vector3 vector2 = list[0];
			foreach (Vector3 item in list)
			{
				if ((item - vector).sqrMagnitude < (vector2 - vector).sqrMagnitude)
				{
					vector2 = item;
				}
			}
			NetworkisGrounded = true;
			_groundVector = (vector2 - vector).normalized;
		}
		else
		{
			NetworkisGrounded = false;
			_groundVector = Vector3.down;
		}
	}

	private void CheckJump()
	{
		Vector3 origin = head.transform.position + Vector3.up * 0.04f;
		float maxDistance = head.transform.localPosition.y + _ps.groundCheckDistance + 0.05f;
		float radius = _ps.playerHeadRadius - 0.01f;
		if (hasBody && _state == PlayerState.Ragdoll)
		{
			origin = base.transform.position + base.transform.up * (_ps.playerRadius - _ps.playerHeadRadius) + Vector3.up * 0.04f;
			maxDistance = _ps.groundCheckDistance + 0.05f;
			radius = _ps.playerRadius - 0.01f;
		}
		int num = Physics.SphereCastNonAlloc(new Ray(origin, Vector3.down), radius, _jumpCheckHits, maxDistance, _ps.groundMask, QueryTriggerInteraction.Ignore);
		bool canJump = false;
		for (int i = 0; i < num; i++)
		{
			RaycastHit raycastHit = _jumpCheckHits[i];
			if ((bool)raycastHit.collider && (!raycastHit.collider.attachedRigidbody || !(raycastHit.collider.attachedRigidbody == _rb)))
			{
				canJump = true;
				break;
			}
		}
		_canJump = canJump;
	}

	private void MovePlayer()
	{
		if (!IsLocked)
		{
			if (State == PlayerState.Free && hasBody)
			{
				MoveFree();
			}
			else if (State == PlayerState.Ragdoll && !hasBody)
			{
				MoveRoll();
			}
		}
	}

	private void MoveFree()
	{
		Vector3 vector = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
		Vector3 vector2 = Vector3.ProjectOnPlane(head.transform.right, Vector3.up);
		_horizontalMoveDirection = (vector * _moveInput.y + vector2 * _moveInput.x).normalized;
		Vector3 normalized = Vector3.ProjectOnPlane(_horizontalMoveDirection, _groundVector).normalized;
		float num = _ps.maxSpeed;
		if (InputEvents.IsCrouchPressed)
		{
			num = _ps.crouchMaxSpeed;
		}
		else if (InputEvents.IsSprintPressed)
		{
			num = _ps.sprintMaxSpeed;
		}
		if ((bool)_pi.NetworkholdingItem)
		{
			num *= 1f - _pi.NetworkholdingItem.slowPercent;
		}
		Vector3 vector3 = normalized * num;
		Vector3 vector4 = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
		Vector3 force = (vector3 - vector4) * _ps.acceleration;
		_rb.AddForce(force, ForceMode.Acceleration);
	}

	private void MoveRoll()
	{
		Vector3 forward = head.transform.forward;
		Vector3 right = head.transform.right;
		Vector3 normalized = (forward * _moveInput.y + right * _moveInput.x).normalized;
		if (!(normalized.sqrMagnitude < 0.01f))
		{
			float rollMaxSpeed = _ps.rollMaxSpeed;
			Vector3 vector = -Vector3.Cross(normalized, head.transform.up).normalized;
			float num = Vector3.Dot(_rb.angularVelocity, vector);
			float num2 = rollMaxSpeed - num;
			Vector3 torque = vector * (num2 * _ps.rollAcceleration);
			_rb.AddTorque(torque, ForceMode.Acceleration);
		}
	}

	private void Jump()
	{
		if (!IsLocked && State != PlayerState.Locked && _canJump)
		{
			if (hasBody)
			{
				_rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
				_rb.AddForce(Vector3.up * _ps.jumpForce, ForceMode.VelocityChange);
			}
			else
			{
				Vector3 forward = head.transform.forward;
				Vector3 vector = Vector3.ProjectOnPlane(forward, Vector3.up);
				float num = Mathf.Clamp01(Vector3.Dot(forward, Vector3.up));
				Vector3 normalized = (vector + Vector3.up * num).normalized;
				Vector3 normalized2 = Vector3.Cross(Vector3.up, normalized).normalized;
				Vector3 normalized3 = (Quaternion.AngleAxis(Vector3.Angle(Vector3.up, normalized) / 3f, normalized2) * Vector3.up).normalized;
				_rb.linearVelocity = Vector3.zero;
				_rb.AddForce(normalized3 * _ps.jumpForce * 3f / 4f, ForceMode.VelocityChange);
			}
			OnJumpFeedback(isGrounded);
		}
	}

	private void Crouch(bool isPressed)
	{
		if (!IsLocked && State != PlayerState.Locked && hasBody)
		{
			head.transform.DOLocalMoveY(isPressed ? _ps.headHeightCrouch : _ps.headHeight, _ps.headMoveDuration).SetEase(Ease.InOutSine);
			OnClientCrouched?.Invoke(isPressed);
		}
	}

	private void StepClimb()
	{
		if (IsLocked || State != PlayerState.Free)
		{
			return;
		}
		Vector3 horizontalMoveDirection = _horizontalMoveDirection;
		Vector3 vector = base.transform.position + base.transform.up * (0f - _ps.playerRadius);
		Vector3 vector2 = vector + base.transform.up * 0.01f;
		if (Physics.Raycast(vector2, horizontalMoveDirection, out var hitInfo, _ps.stepCheckDistance, _ps.groundMask))
		{
			Vector3 vector3 = vector + base.transform.up * _ps.maxStepHeight;
			float magnitude = (hitInfo.point - vector2).magnitude;
			if (!Physics.Raycast(vector3, horizontalMoveDirection, out var _, magnitude, _ps.groundMask) && Physics.Raycast(vector3 + horizontalMoveDirection * (magnitude + 0.01f), Vector3.down, out var hitInfo3, _ps.maxStepHeight + 0.01f, _ps.groundMask) && !(Vector3.Angle(hitInfo3.normal, Vector3.up) > 5f))
			{
				Vector3 vector4 = Vector3.up * (hitInfo3.point.y - hitInfo.point.y + 0.1f);
				_rb.MovePosition(base.transform.position + vector4 * _ps.stepUpDistance);
			}
		}
	}

	private void ApplyGravity()
	{
		if (State != PlayerState.Locked && (bool)_rb && !_rb.isKinematic && _ps.gravity != 0f)
		{
			_rb.AddForce(_groundVector * _ps.gravity, ForceMode.Acceleration);
		}
	}

	private void HandleVelocityChange()
	{
		float num = _rb.linearVelocity.y - _previousVelocity.y;
		if (isGrounded && num >= _ps.landThreshold && _rb.linearVelocity.y < 1f)
		{
			OnLandFeedback(num);
		}
		_previousVelocity = _rb.linearVelocity;
	}

	private void SendVelocity()
	{
		Vector3 linearVelocity = _rb.linearVelocity;
		if ((linearVelocity - serverVelocity).sqrMagnitude > 0.005f)
		{
			NetworkserverVelocity = linearVelocity;
		}
	}

	private void SetPlayerState(PlayerState newState)
	{
		if (!_rb.isKinematic)
		{
			_rb.linearVelocity = Vector3.zero;
		}
		_rb.isKinematic = newState == PlayerState.Locked;
		_rb.constraints = ((newState != PlayerState.Ragdoll) ? RigidbodyConstraints.FreezeRotation : RigidbodyConstraints.None);
		if (newState == PlayerState.Free)
		{
			_rb.DORotate(Vector3.zero, 0.5f).SetEase(Ease.OutCubic);
		}
		_pc.LocalSetInteractable(newState == PlayerState.Ragdoll);
		if ((bool)sfxPhysicsObject)
		{
			sfxPhysicsObject.enabled = newState == PlayerState.Ragdoll;
		}
	}

	private IEnumerator DelayedDisableRagdoll()
	{
		if (hasBody)
		{
			yield return new WaitForSeconds(_ps.ragdollDuration);
			if (hasBody)
			{
				State = PlayerState.Free;
			}
		}
	}

	[Server]
	public void ServerTeleport(Vector3 position)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlayerController::ServerTeleport(UnityEngine.Vector3)' called when server was not active");
		}
		else
		{
			TargetTeleport(base.netIdentity.connectionToClient, position);
		}
	}

	[TargetRpc]
	private void TargetTeleport(NetworkConnection conn, Vector3 position)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVector3(position);
		SendTargetRPCInternal(conn, "System.Void PlayerController::TargetTeleport(Mirror.NetworkConnection,UnityEngine.Vector3)", -1471364446, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	public void LocalTeleport(Vector3 position)
	{
        if (CasinoRuntimeContext.IsHostIntegration) { transform.position = position; CasinoRuntimeContext.PlayerTeleported?.Invoke(position); return; }
		base.transform.SetParent(null);
		_rb.Teleport(position, resetVelocity: true);
		_rb.Rotate(Quaternion.identity, resetVelocity: true);
	}

	[Server]
	public void ServerRotate(Vector2 rotation)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlayerController::ServerRotate(UnityEngine.Vector2)' called when server was not active");
		}
		else
		{
			TargetRotate(base.netIdentity.connectionToClient, rotation);
		}
	}

	[TargetRpc]
	private void TargetRotate(NetworkConnection conn, Vector2 rotation)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVector2(rotation);
		SendTargetRPCInternal(conn, "System.Void PlayerController::TargetRotate(Mirror.NetworkConnection,UnityEngine.Vector2)", 889457579, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	public void LocalRotate(Vector2 rotation)
	{
        if (CasinoRuntimeContext.IsHostIntegration) { CasinoRuntimeContext.PlayerRotated?.Invoke(rotation); return; }
		base.transform.SetParent(null);
		head.SetRotation(rotation.x, rotation.y);
	}

	[Server]
	public void ServerKnockback(Vector3 force, Vector3 torque)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlayerController::ServerKnockback(UnityEngine.Vector3,UnityEngine.Vector3)' called when server was not active");
			return;
		}
		_pi.ServerThrowItemRandomly();
		TargetKnockback(base.netIdentity.connectionToClient, force, torque);
	}

	[TargetRpc]
	private void TargetKnockback(NetworkConnection conn, Vector3 force, Vector3 torque)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVector3(force);
		writer.WriteVector3(torque);
		SendTargetRPCInternal(conn, "System.Void PlayerController::TargetKnockback(Mirror.NetworkConnection,UnityEngine.Vector3,UnityEngine.Vector3)", -557766891, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	public void LocalKnockback(Vector3 force, Vector3 torque)
	{
		if (!IsLocked && _state != PlayerState.Locked)
		{
			State = PlayerState.Ragdoll;
			_rb.AddForce(force, ForceMode.VelocityChange);
			_rb.AddTorque(torque, ForceMode.VelocityChange);
		}
	}

	[Server]
	public void ServerLock(bool isLocked)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlayerController::ServerLock(System.Boolean)' called when server was not active");
		}
		else
		{
			TargetLock(base.netIdentity.connectionToClient, isLocked);
		}
	}

	[TargetRpc]
	private void TargetLock(NetworkConnection conn, bool isLocked)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isLocked);
		SendTargetRPCInternal(conn, "System.Void PlayerController::TargetLock(Mirror.NetworkConnection,System.Boolean)", -1289107260, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	public void LocalLock(bool isLocked)
	{
		base.transform.SetParent(null);
		IsLocked = isLocked;
		if (isLocked && !_rb.isKinematic)
		{
			_rb.linearVelocity = Vector3.zero;
		}
		_rb.isKinematic = isLocked;
	}

	[Server]
	public void ServerLockHead(bool isLocked)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlayerController::ServerLockHead(System.Boolean)' called when server was not active");
		}
		else
		{
			TargetLockHead(base.netIdentity.connectionToClient, isLocked);
		}
	}

	[TargetRpc]
	private void TargetLockHead(NetworkConnection conn, bool isLocked)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isLocked);
		SendTargetRPCInternal(conn, "System.Void PlayerController::TargetLockHead(Mirror.NetworkConnection,System.Boolean)", -1308872738, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	public void LocalLockHead(bool isLocked)
	{
		head.isLocked = isLocked;
	}

	[Server]
	public void ServerWakeUp()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlayerController::ServerWakeUp()' called when server was not active");
		}
		else
		{
			TargetWakeUp(base.netIdentity.connectionToClient);
		}
	}

	[TargetRpc]
	private void TargetWakeUp(NetworkConnection conn)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendTargetRPCInternal(conn, "System.Void PlayerController::TargetWakeUp(Mirror.NetworkConnection)", 1791146995, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	private void WakeUp()
	{
		InputEvents.ActiveLayer = InputLayer.Default;
		IsLocked = false;
		bool num = State == PlayerState.Ragdoll;
		if (num)
		{
			_rb.isKinematic = false;
			_rb.constraints = RigidbodyConstraints.None;
			if ((bool)sfxPhysicsObject)
			{
				sfxPhysicsObject.enabled = true;
			}
		}
		State = PlayerState.Ragdoll;
		if (num)
		{
			_rb.AddTorque(base.transform.right * UnityEngine.Random.Range(-5f, -5f), ForceMode.VelocityChange);
			Vector3 insideUnitSphere = UnityEngine.Random.insideUnitSphere;
			insideUnitSphere.y = 0f;
			insideUnitSphere.Normalize();
			Vector3 force = Vector3.up * 20f + insideUnitSphere * 10f;
			_rb.AddForce(force, ForceMode.VelocityChange);
		}
		else
		{
			_rb.AddTorque(base.transform.right * UnityEngine.Random.Range(-5f, -5f), ForceMode.VelocityChange);
			_rb.AddForce(Vector3.up * 15f + -base.transform.up * 10f, ForceMode.VelocityChange);
		}
	}

	private IEnumerator DelayedWakeUp()
	{
		yield return new WaitForSeconds(2.5f);
		IsLocked = false;
		InputEvents.ActiveLayer = InputLayer.Default;
	}

	public void TriggerRagdoll()
	{
		if (!IsLocked && State == PlayerState.Free)
		{
			State = PlayerState.Ragdoll;
			_rb.AddTorque(new Vector3(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-5f, 5f)), ForceMode.VelocityChange);
			_rb.AddForce(Vector3.up * 10f + head.transform.forward * 10f, ForceMode.VelocityChange);
		}
	}

	private void OnLandFeedback(float fallImpact)
	{
		OnClientLanded?.Invoke(fallImpact);
		CmdOnLand(fallImpact);
	}

	[Command]
	private void CmdOnLand(float fallImpact)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(fallImpact);
		SendCommandInternal("System.Void PlayerController::CmdOnLand(System.Single)", 711888585, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcOnLand(float fallImpact)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(fallImpact);
		SendRPCInternal("System.Void PlayerController::RpcOnLand(System.Single)", -204472168, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void OnJumpFeedback(bool wasGrounded)
	{
		OnClientJumped?.Invoke(wasGrounded);
		CmdOnJump(wasGrounded);
	}

	[Command]
	private void CmdOnJump(bool wasGrounded)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(wasGrounded);
		SendCommandInternal("System.Void PlayerController::CmdOnJump(System.Boolean)", 2066950688, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcOnJump(bool wasGrounded)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(wasGrounded);
		SendRPCInternal("System.Void PlayerController::RpcOnJump(System.Boolean)", -1493115925, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_TargetTeleport__NetworkConnection__Vector3(NetworkConnection conn, Vector3 position)
	{
		LocalTeleport(position);
	}

	protected static void InvokeUserCode_TargetTeleport__NetworkConnection__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("TargetRPC TargetTeleport called on server.");
		}
		else
		{
			((PlayerController)obj).UserCode_TargetTeleport__NetworkConnection__Vector3(null, reader.ReadVector3());
		}
	}

	protected void UserCode_TargetRotate__NetworkConnection__Vector2(NetworkConnection conn, Vector2 rotation)
	{
		LocalRotate(rotation);
	}

	protected static void InvokeUserCode_TargetRotate__NetworkConnection__Vector2(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("TargetRPC TargetRotate called on server.");
		}
		else
		{
			((PlayerController)obj).UserCode_TargetRotate__NetworkConnection__Vector2(null, reader.ReadVector2());
		}
	}

	protected void UserCode_TargetKnockback__NetworkConnection__Vector3__Vector3(NetworkConnection conn, Vector3 force, Vector3 torque)
	{
		LocalKnockback(force, torque);
	}

	protected static void InvokeUserCode_TargetKnockback__NetworkConnection__Vector3__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("TargetRPC TargetKnockback called on server.");
		}
		else
		{
			((PlayerController)obj).UserCode_TargetKnockback__NetworkConnection__Vector3__Vector3(null, reader.ReadVector3(), reader.ReadVector3());
		}
	}

	protected void UserCode_TargetLock__NetworkConnection__Boolean(NetworkConnection conn, bool isLocked)
	{
		LocalLock(isLocked);
	}

	protected static void InvokeUserCode_TargetLock__NetworkConnection__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("TargetRPC TargetLock called on server.");
		}
		else
		{
			((PlayerController)obj).UserCode_TargetLock__NetworkConnection__Boolean(null, reader.ReadBool());
		}
	}

	protected void UserCode_TargetLockHead__NetworkConnection__Boolean(NetworkConnection conn, bool isLocked)
	{
		LocalLockHead(isLocked);
	}

	protected static void InvokeUserCode_TargetLockHead__NetworkConnection__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("TargetRPC TargetLockHead called on server.");
		}
		else
		{
			((PlayerController)obj).UserCode_TargetLockHead__NetworkConnection__Boolean(null, reader.ReadBool());
		}
	}

	protected void UserCode_TargetWakeUp__NetworkConnection(NetworkConnection conn)
	{
		WakeUp();
		StartCoroutine(DelayedWakeUp());
	}

	protected static void InvokeUserCode_TargetWakeUp__NetworkConnection(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("TargetRPC TargetWakeUp called on server.");
		}
		else
		{
			((PlayerController)obj).UserCode_TargetWakeUp__NetworkConnection(null);
		}
	}

	protected void UserCode_CmdOnLand__Single(float fallImpact)
	{
		RpcOnLand(fallImpact);
	}

	protected static void InvokeUserCode_CmdOnLand__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdOnLand called on client.");
		}
		else
		{
			((PlayerController)obj).UserCode_CmdOnLand__Single(reader.ReadFloat());
		}
	}

	protected void UserCode_RpcOnLand__Single(float fallImpact)
	{
		if (!base.isLocalPlayer)
		{
			OnClientLanded?.Invoke(fallImpact);
		}
	}

	protected static void InvokeUserCode_RpcOnLand__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnLand called on server.");
		}
		else
		{
			((PlayerController)obj).UserCode_RpcOnLand__Single(reader.ReadFloat());
		}
	}

	protected void UserCode_CmdOnJump__Boolean(bool wasGrounded)
	{
		RpcOnJump(wasGrounded);
	}

	protected static void InvokeUserCode_CmdOnJump__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdOnJump called on client.");
		}
		else
		{
			((PlayerController)obj).UserCode_CmdOnJump__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcOnJump__Boolean(bool wasGrounded)
	{
		if (!base.isLocalPlayer)
		{
			OnClientJumped?.Invoke(wasGrounded);
		}
	}

	protected static void InvokeUserCode_RpcOnJump__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnJump called on server.");
		}
		else
		{
			((PlayerController)obj).UserCode_RpcOnJump__Boolean(reader.ReadBool());
		}
	}

	static PlayerController()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerController), "System.Void PlayerController::CmdOnLand(System.Single)", InvokeUserCode_CmdOnLand__Single, requiresAuthority: true);
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerController), "System.Void PlayerController::CmdOnJump(System.Boolean)", InvokeUserCode_CmdOnJump__Boolean, requiresAuthority: true);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerController), "System.Void PlayerController::RpcOnLand(System.Single)", InvokeUserCode_RpcOnLand__Single);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerController), "System.Void PlayerController::RpcOnJump(System.Boolean)", InvokeUserCode_RpcOnJump__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerController), "System.Void PlayerController::TargetTeleport(Mirror.NetworkConnection,UnityEngine.Vector3)", InvokeUserCode_TargetTeleport__NetworkConnection__Vector3);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerController), "System.Void PlayerController::TargetRotate(Mirror.NetworkConnection,UnityEngine.Vector2)", InvokeUserCode_TargetRotate__NetworkConnection__Vector2);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerController), "System.Void PlayerController::TargetKnockback(Mirror.NetworkConnection,UnityEngine.Vector3,UnityEngine.Vector3)", InvokeUserCode_TargetKnockback__NetworkConnection__Vector3__Vector3);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerController), "System.Void PlayerController::TargetLock(Mirror.NetworkConnection,System.Boolean)", InvokeUserCode_TargetLock__NetworkConnection__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerController), "System.Void PlayerController::TargetLockHead(Mirror.NetworkConnection,System.Boolean)", InvokeUserCode_TargetLockHead__NetworkConnection__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerController), "System.Void PlayerController::TargetWakeUp(Mirror.NetworkConnection)", InvokeUserCode_TargetWakeUp__NetworkConnection);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			GeneratedNetworkCode._Write_PlayerController_002FPlayerState(writer, _state);
			writer.WriteBool(hasBody);
			writer.WriteBool(isGrounded);
			writer.WriteVector3(serverVelocity);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			GeneratedNetworkCode._Write_PlayerController_002FPlayerState(writer, _state);
		}
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteBool(hasBody);
		}
		if ((syncVarDirtyBits & 4L) != 0L)
		{
			writer.WriteBool(isGrounded);
		}
		if ((syncVarDirtyBits & 8L) != 0L)
		{
			writer.WriteVector3(serverVelocity);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _state, null, GeneratedNetworkCode._Read_PlayerController_002FPlayerState(reader));
			GeneratedSyncVarDeserialize(ref hasBody, null, reader.ReadBool());
			GeneratedSyncVarDeserialize(ref isGrounded, null, reader.ReadBool());
			GeneratedSyncVarDeserialize(ref serverVelocity, null, reader.ReadVector3());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _state, null, GeneratedNetworkCode._Read_PlayerController_002FPlayerState(reader));
		}
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref hasBody, null, reader.ReadBool());
		}
		if ((num & 4L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref isGrounded, null, reader.ReadBool());
		}
		if ((num & 8L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref serverVelocity, null, reader.ReadVector3());
		}
	}
}

}
