namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Runtime.InteropServices;
using global::FMOD.Studio;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class BankKnob : InteractableBase
{
	[Header("References")]
	[SerializeField]
	private Transform knobTransform;

	[SerializeField]
	private Transform knobParent;

	[SerializeField]
	private Vector3 pullAxis = Vector3.up;

	[SerializeField]
	private float maxPullDistance = 0.5f;

	[SerializeField]
	private float pullSensitivity = 1f;

	[Header("Settings")]
	[SerializeField]
	private long minValue;

	[SerializeField]
	private long maxValue = 1000L;

	[SerializeField]
	private int stepValue = 1;

	[Header("SFX")]
	[SerializeField]
	private EventReference sfxDragEvent;

	[SerializeField]
	private EventReference sfxSlideEvent;

	private Vector3 initialLocalPosition;

	private Vector3 currentPullDirection;

	[SyncVar(hook = "OnPullDistanceChanged")]
	private float currentPullDistance;

	private bool isBeingPulled;

	private PlayerInteract currentPullingPlayer;

	private Camera playerCamera;

	private float lastCameraYaw;

	public Action<float> OnKnobValueChanged;

	public Action<float, float> _Mirror_SyncVarHookDelegate_currentPullDistance;

	public float NetworkcurrentPullDistance
	{
		get
		{
			return currentPullDistance;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref currentPullDistance, 1uL, _Mirror_SyncVarHookDelegate_currentPullDistance);
		}
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		if (knobTransform == null)
		{
			knobTransform = base.transform;
		}
		if (knobParent == null)
		{
			knobParent = ((base.transform.parent != null) ? base.transform.parent : base.transform);
		}
		initialLocalPosition = knobTransform.localPosition;
	}

	public override void OnHold(PlayerInteract playerInteract)
	{
		base.OnHold(playerInteract);
		if (playerInteract != null && playerInteract.isLocalPlayer && playerCamera == null)
		{
			playerCamera = Camera.main;
			if (playerCamera != null)
			{
				lastCameraYaw = playerCamera.transform.eulerAngles.y;
			}
			CmdStartPulling();
		}
	}

	public override void OnHoldExit(PlayerInteract playerInteract)
	{
		base.OnHoldExit(playerInteract);
		if (playerInteract != null && playerInteract.isLocalPlayer)
		{
			playerCamera = null;
			CmdStopPulling();
		}
	}

	public override void ServerOnHold(PlayerInteract playerInteract)
	{
		base.ServerOnHold(playerInteract);
		if (!isBeingPulled && playerInteract != null)
		{
			StartPulling(playerInteract);
		}
	}

	public override void ServerOnHoldExit(PlayerInteract playerInteract)
	{
		base.ServerOnHoldExit(playerInteract);
		if (isBeingPulled && currentPullingPlayer == playerInteract)
		{
			StopPulling();
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdStartPulling()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void BankKnob::CmdStartPulling()", -2142497560, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	private void CmdStopPulling()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void BankKnob::CmdStopPulling()", -1716597248, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	private void CmdUpdatePullInput(float pullDelta)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(pullDelta);
		SendCommandInternal("System.Void BankKnob::CmdUpdatePullInput(System.Single)", 672272500, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	private void StartPulling(PlayerInteract playerInteract)
	{
		isBeingPulled = true;
		currentPullingPlayer = playerInteract;
		RpcStartPulling();
	}

	private void StopPulling()
	{
		isBeingPulled = false;
		currentPullingPlayer = null;
		RpcStopPulling();
	}

	[ClientRpc]
	private void RpcStartPulling()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void BankKnob::RpcStartPulling()", 1110737395, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcStopPulling()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void BankKnob::RpcStopPulling()", -850882243, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void Update()
	{
		if (IsBeingHold && playerCamera != null)
		{
			float y = playerCamera.transform.eulerAngles.y;
			float num = Mathf.DeltaAngle(lastCameraYaw, y);
			if (Mathf.Abs(num) > 0.01f)
			{
				float pullDelta = num * pullSensitivity * 0.01f;
				CmdUpdatePullInput(pullDelta);
				lastCameraYaw = y;
			}
		}
	}

	private void OnPullDistanceChanged(float oldValue, float newValue)
	{
		UpdateKnobVisuals();
	}

	private void FixedUpdate()
	{
		if (base.isServer && knobTransform != null)
		{
			ClampKnobPosition();
		}
	}

	private void UpdateKnobVisuals()
	{
		if (!(knobTransform == null))
		{
			Vector3 localPosition = initialLocalPosition + pullAxis.normalized * currentPullDistance;
			knobTransform.localPosition = localPosition;
		}
	}

	private void ClampKnobPosition()
	{
		if (!(knobTransform == null))
		{
			float value = Vector3.Dot(knobTransform.localPosition - initialLocalPosition, pullAxis.normalized);
			value = Mathf.Clamp(value, 0f, maxPullDistance);
			knobTransform.localPosition = initialLocalPosition + pullAxis.normalized * value;
			NetworkcurrentPullDistance = value;
		}
	}

	public void SetMaxValue(long max)
	{
		maxValue = Math.Max(minValue, max);
		if (maxValue > 0 && currentPullDistance > 0f)
		{
			_ = currentPullDistance / maxPullDistance;
		}
	}

	public long GetCurrentValue()
	{
		double num = currentPullDistance / maxPullDistance;
		long num2 = maxValue - minValue;
		long num3 = minValue + (long)Math.Round(num * (double)num2);
		num3 = (long)Math.Round((double)num3 / (double)stepValue) * stepValue;
		return Math.Max(minValue, Math.Min(num3, maxValue));
	}

	public float GetNormalizedValue()
	{
		return currentPullDistance / maxPullDistance;
	}

	[Server]
	public void SetNormalizedValue(float normalizedValue)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BankKnob::SetNormalizedValue(System.Single)' called when server was not active");
			return;
		}
		normalizedValue = Mathf.Clamp01(normalizedValue);
		NetworkcurrentPullDistance = normalizedValue * maxPullDistance;
		UpdateKnobVisuals();
		OnKnobValueChanged?.Invoke(normalizedValue);
	}

	[Command(requiresAuthority = false)]
	public void CmdSetNormalizedValue(float normalizedValue)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(normalizedValue);
		SendCommandInternal("System.Void BankKnob::CmdSetNormalizedValue(System.Single)", 1520418840, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	private IEnumerator SfxSliderRoutine()
	{
		if (sfxDragEvent.IsNull)
		{
			yield return null;
		}
		if (sfxSlideEvent.IsNull)
		{
			yield return null;
		}
		float num = 0f;
		float prev_value = num;
		bool canTick = true;
		float tickDivision = 0.1f;
		EventInstance slideInstance = RuntimeManager.CreateInstance(sfxSlideEvent);
		SFXParams[] sFXParams = new SFXParams[1]
		{
			new SFXParams("valAmount", 0f)
		};
		while (isBeingPulled)
		{
			num = GetNormalizedValue();
			sFXParams[0].value = Math.Abs(prev_value - num) * 2f;
			if ((num % tickDivision >= tickDivision - 0.01f) & canTick)
			{
				SFXManager.SFXOneShotWithParameters(sfxDragEvent, sFXParams, base.transform.position, 1f + num);
				canTick = false;
			}
			else if (num % tickDivision <= tickDivision - 0.01f)
			{
				canTick = true;
			}
			prev_value = num;
			slideInstance.getPlaybackState(out var state);
			if (state != PLAYBACK_STATE.PLAYING)
			{
				RuntimeManager.AttachInstanceToGameObject(slideInstance, base.gameObject, nonRigidbodyVelocity: true);
				slideInstance.start();
			}
			slideInstance.setParameterByName("valAmount", sFXParams[0].value * 5f);
			yield return new WaitForEndOfFrame();
		}
		slideInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
		slideInstance.release();
		yield return null;
	}

	public BankKnob()
	{
		_Mirror_SyncVarHookDelegate_currentPullDistance = OnPullDistanceChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdStartPulling()
	{
	}

	protected static void InvokeUserCode_CmdStartPulling(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdStartPulling called on client.");
		}
		else
		{
			((BankKnob)obj).UserCode_CmdStartPulling();
		}
	}

	protected void UserCode_CmdStopPulling()
	{
	}

	protected static void InvokeUserCode_CmdStopPulling(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdStopPulling called on client.");
		}
		else
		{
			((BankKnob)obj).UserCode_CmdStopPulling();
		}
	}

	protected void UserCode_CmdUpdatePullInput__Single(float pullDelta)
	{
		if (isBeingPulled)
		{
			float value = currentPullDistance + pullDelta;
			value = Mathf.Clamp(value, 0f, maxPullDistance);
			if (Mathf.Abs(value - currentPullDistance) > 0.001f)
			{
				NetworkcurrentPullDistance = value;
				UpdateKnobVisuals();
				float obj = currentPullDistance / maxPullDistance;
				OnKnobValueChanged?.Invoke(obj);
			}
		}
	}

	protected static void InvokeUserCode_CmdUpdatePullInput__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdUpdatePullInput called on client.");
		}
		else
		{
			((BankKnob)obj).UserCode_CmdUpdatePullInput__Single(reader.ReadFloat());
		}
	}

	protected void UserCode_RpcStartPulling()
	{
		StartCoroutine(SfxSliderRoutine());
	}

	protected static void InvokeUserCode_RpcStartPulling(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcStartPulling called on server.");
		}
		else
		{
			((BankKnob)obj).UserCode_RpcStartPulling();
		}
	}

	protected void UserCode_RpcStopPulling()
	{
	}

	protected static void InvokeUserCode_RpcStopPulling(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcStopPulling called on server.");
		}
		else
		{
			((BankKnob)obj).UserCode_RpcStopPulling();
		}
	}

	protected void UserCode_CmdSetNormalizedValue__Single(float normalizedValue)
	{
		SetNormalizedValue(normalizedValue);
	}

	protected static void InvokeUserCode_CmdSetNormalizedValue__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSetNormalizedValue called on client.");
		}
		else
		{
			((BankKnob)obj).UserCode_CmdSetNormalizedValue__Single(reader.ReadFloat());
		}
	}

	static BankKnob()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(BankKnob), "System.Void BankKnob::CmdStartPulling()", InvokeUserCode_CmdStartPulling, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(BankKnob), "System.Void BankKnob::CmdStopPulling()", InvokeUserCode_CmdStopPulling, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(BankKnob), "System.Void BankKnob::CmdUpdatePullInput(System.Single)", InvokeUserCode_CmdUpdatePullInput__Single, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(BankKnob), "System.Void BankKnob::CmdSetNormalizedValue(System.Single)", InvokeUserCode_CmdSetNormalizedValue__Single, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(BankKnob), "System.Void BankKnob::RpcStartPulling()", InvokeUserCode_RpcStartPulling);
		RemoteProcedureCalls.RegisterRpc(typeof(BankKnob), "System.Void BankKnob::RpcStopPulling()", InvokeUserCode_RpcStopPulling);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteFloat(currentPullDistance);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteFloat(currentPullDistance);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref currentPullDistance, _Mirror_SyncVarHookDelegate_currentPullDistance, reader.ReadFloat());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref currentPullDistance, _Mirror_SyncVarHookDelegate_currentPullDistance, reader.ReadFloat());
		}
	}
}

}
