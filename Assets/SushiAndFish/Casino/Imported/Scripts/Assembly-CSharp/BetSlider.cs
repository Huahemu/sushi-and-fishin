namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Runtime.InteropServices;
using SushiAndFish.Casino.Original.Extensions;
using global::JetBrains.Annotations;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;

public class BetSlider : NetworkBehaviour
{
	[Header("References")]
	[SerializeField]
	private Transform knob;

	[SerializeField]
	private Transform startPoint;

	[SerializeField]
	private Transform endPoint;

	[SerializeField]
	private TMP_Text betAmountText;

	[CanBeNull]
	private PlayerInteract _interactor;

	[CanBeNull]
	private PlayerHead _interactorHead;

	[SyncVar(hook = "OnPercentageChanged")]
	private float _currentPercentage;

	private PlayerInteract _localInteractor;

	public Action<float, float> _Mirror_SyncVarHookDelegate__currentPercentage;

	public float Network_currentPercentage
	{
		get
		{
			return _currentPercentage;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _currentPercentage, 1uL, _Mirror_SyncVarHookDelegate__currentPercentage);
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

	private void Start()
	{
		StartCoroutine(DelayedSetup());
	}

	private IEnumerator DelayedSetup()
	{
		yield return new WaitForSeconds(1f);
		_localInteractor = NetworkClient.localPlayer.GetComponent<PlayerInteract>();
		Network_currentPercentage = 0f;
		knob.transform.position = Vector3.Lerp(startPoint.position, endPoint.position, 0f);
		betAmountText.text = "$0";
	}

	private void OnPercentageChanged(float oldValue, float newValue)
	{
		knob.transform.position = Vector3.Lerp(startPoint.position, endPoint.position, newValue);
		betAmountText.text = "$" + Mathf.RoundToInt(Mathf.Lerp(0f, NetworkSingleton<MoneyManager>.Instance.balance, newValue));
	}

	private void HandlePlayerInteract(bool isPressed)
	{
		if (!isPressed)
		{
			TryStopInteracting(_localInteractor);
		}
	}

	[Command(requiresAuthority = false)]
	private void TryStopInteracting(PlayerInteract playerInteract)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInteract);
		SendCommandInternal("System.Void BetSlider::TryStopInteracting(PlayerInteract)", 774954605, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void OnPlayerInteract(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BetSlider::OnPlayerInteract(PlayerInteract)' called when server was not active");
			return;
		}
		_interactor = playerInteract;
		_interactorHead = playerInteract.GetComponent<PlayerController>().head;
	}

	private void Update()
	{
		if (base.isServer && (bool)_interactor && (bool)_interactorHead)
		{
			Slide();
		}
	}

	[Server]
	private void Slide()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BetSlider::Slide()' called when server was not active");
			return;
		}
		FathF.NearestPointToRayOnLine(startPoint.position, endPoint.position, _interactorHead.transform.position, _interactorHead.transform.forward, out var t, out var s);
		if (s > 0f)
		{
			Network_currentPercentage = t;
		}
	}

	public BetSlider()
	{
		_Mirror_SyncVarHookDelegate__currentPercentage = OnPercentageChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_TryStopInteracting__PlayerInteract(PlayerInteract playerInteract)
	{
		if (_interactor == playerInteract)
		{
			_interactor = null;
		}
	}

	protected static void InvokeUserCode_TryStopInteracting__PlayerInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command TryStopInteracting called on client.");
		}
		else
		{
			((BetSlider)obj).UserCode_TryStopInteracting__PlayerInteract(reader.ReadNetworkBehaviour<PlayerInteract>());
		}
	}

	static BetSlider()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(BetSlider), "System.Void BetSlider::TryStopInteracting(PlayerInteract)", InvokeUserCode_TryStopInteracting__PlayerInteract, requiresAuthority: false);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteFloat(_currentPercentage);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteFloat(_currentPercentage);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _currentPercentage, _Mirror_SyncVarHookDelegate__currentPercentage, reader.ReadFloat());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _currentPercentage, _Mirror_SyncVarHookDelegate__currentPercentage, reader.ReadFloat());
		}
	}
}

}
