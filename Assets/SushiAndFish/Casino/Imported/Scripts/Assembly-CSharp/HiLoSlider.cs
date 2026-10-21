namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Runtime.InteropServices;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::JetBrains.Annotations;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;

public class HiLoSlider : NetworkBehaviour
{
	[Header("References")]
	[SerializeField]
	private Transform knob;

	[SerializeField]
	private Transform startPoint;

	[SerializeField]
	private Transform endPoint;

	[SerializeField]
	private Transform underIndicator;

	[SerializeField]
	private Transform overIndicator;

	[SerializeField]
	private TextMeshPro dicePercentageText;

	[Header("Settings")]
	[SerializeField]
	private float minTargetValue = 1f;

	[SerializeField]
	private float maxTargetValue = 99f;

	[SyncVar(hook = "OnValueChanged")]
	public float currentValue;

	[CanBeNull]
	private NetworkIdentity _currentInteract;

	public Action<float> OnValueChangedAction;

	private bool _isLocked;

	private bool _localIsInteracting;

	public Action<float, float> _Mirror_SyncVarHookDelegate_currentValue;

	public float NetworkcurrentValue
	{
		get
		{
			return currentValue;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref currentValue, 1uL, _Mirror_SyncVarHookDelegate_currentValue);
		}
	}

	private void OnEnable()
	{
		InputEvents.OnInteractEvent = (Action<bool>)Delegate.Combine(InputEvents.OnInteractEvent, new Action<bool>(HandlePlayerInteract));
	}

	private void OnDisable()
	{
		InputEvents.OnInteractEvent = (Action<bool>)Delegate.Remove(InputEvents.OnInteractEvent, new Action<bool>(HandlePlayerInteract));
	}

	public override void OnStartServer()
	{
		base.OnStartServer();
		NetworkcurrentValue = 0.5f;
	}

	[Server]
	public void LockSlider(bool isLocked)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void HiLoSlider::LockSlider(System.Boolean)' called when server was not active");
		}
		else if (_isLocked != isLocked)
		{
			_isLocked = isLocked;
			if (_isLocked)
			{
				_currentInteract = null;
			}
		}
	}

	private void OnValueChanged(float oldValue, float newValue)
	{
		Vector3 endValue = Vector3.Lerp(startPoint.position, endPoint.position, newValue);
		knob.transform.DOMove(endValue, 0.1f);
		dicePercentageText.text = (newValue * 100f).ToString("0.#") + "%";
		underIndicator.localScale = new Vector3(newValue, underIndicator.localScale.y, underIndicator.localScale.z);
		overIndicator.localScale = new Vector3(1f - newValue, overIndicator.localScale.y, overIndicator.localScale.z);
		OnValueChangedAction?.Invoke(newValue);
	}

	public void OnPlayerInteract(PlayerInteract playerInteract)
	{
		_localIsInteracting = true;
		CmdTrySetInteract(playerInteract.netIdentity);
	}

	private void HandlePlayerInteract(bool isPressed)
	{
		if (!isPressed)
		{
			_localIsInteracting = false;
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdTrySetInteract(NetworkIdentity player)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkIdentity(player);
		SendCommandInternal("System.Void HiLoSlider::CmdTrySetInteract(Mirror.NetworkIdentity)", 1086004524, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	private void Update()
	{
		if (_localIsInteracting)
		{
			FathF.NearestPointToRayOnLine(startPoint.position, endPoint.position, MonoSingleton<LocalManager>.Instance.mainCamera.transform.position, MonoSingleton<LocalManager>.Instance.mainCamera.transform.forward, out var t, out var s);
			if (s > 0f)
			{
				CmdTrySlide(NetworkClient.localPlayer, t);
			}
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdTrySlide(NetworkIdentity player, float t)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkIdentity(player);
		writer.WriteFloat(t);
		SendCommandInternal("System.Void HiLoSlider::CmdTrySlide(Mirror.NetworkIdentity,System.Single)", 6839220, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	public HiLoSlider()
	{
		_Mirror_SyncVarHookDelegate_currentValue = OnValueChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdTrySetInteract__NetworkIdentity(NetworkIdentity player)
	{
		if (!_isLocked)
		{
			_currentInteract = player;
		}
	}

	protected static void InvokeUserCode_CmdTrySetInteract__NetworkIdentity(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdTrySetInteract called on client.");
		}
		else
		{
			((HiLoSlider)obj).UserCode_CmdTrySetInteract__NetworkIdentity(reader.ReadNetworkIdentity());
		}
	}

	protected void UserCode_CmdTrySlide__NetworkIdentity__Single(NetworkIdentity player, float t)
	{
		if (!_isLocked && !(_currentInteract != player))
		{
			NetworkcurrentValue = Mathf.Clamp(t, minTargetValue / 100f, maxTargetValue / 100f);
		}
	}

	protected static void InvokeUserCode_CmdTrySlide__NetworkIdentity__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdTrySlide called on client.");
		}
		else
		{
			((HiLoSlider)obj).UserCode_CmdTrySlide__NetworkIdentity__Single(reader.ReadNetworkIdentity(), reader.ReadFloat());
		}
	}

	static HiLoSlider()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(HiLoSlider), "System.Void HiLoSlider::CmdTrySetInteract(Mirror.NetworkIdentity)", InvokeUserCode_CmdTrySetInteract__NetworkIdentity, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(HiLoSlider), "System.Void HiLoSlider::CmdTrySlide(Mirror.NetworkIdentity,System.Single)", InvokeUserCode_CmdTrySlide__NetworkIdentity__Single, requiresAuthority: false);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteFloat(currentValue);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteFloat(currentValue);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref currentValue, _Mirror_SyncVarHookDelegate_currentValue, reader.ReadFloat());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref currentValue, _Mirror_SyncVarHookDelegate_currentValue, reader.ReadFloat());
		}
	}
}

}
