namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class CameramanMode : NetworkBehaviour
{
	[SyncVar(hook = "OnCameramanModeChanged")]
	private bool _isActive;

	[SyncVar(hook = "OnVisibilityChanged")]
	private bool _isVisible = true;

	private List<Renderer> _renderers = new List<Renderer>();

	private List<Canvas> _canvases = new List<Canvas>();

	[Header("Cameraman Settings")]
	[SerializeField]
	private GameObject cameramanCameraPrefab;

	private PlayerController _pc;

	private Rigidbody _rb;

	private PlayerHead _head;

	private Camera _mainCamera;

	private GameObject _cameramanCameraInstance;

	private Vector3 _savedBodyPosition;

	private Quaternion _savedBodyRotation;

	private bool _wasBodyKinematic;

	private RigidbodyConstraints _originalConstraints;

	private bool _wasHeadEnabled;

	private List<(Canvas canvas, bool wasEnabled)> _identifiedCanvases = new List<(Canvas, bool)>();

	private List<SelfMeshDisabler> _selfMeshDisablers = new List<SelfMeshDisabler>();

	public Action<bool, bool> _Mirror_SyncVarHookDelegate__isActive;

	public Action<bool, bool> _Mirror_SyncVarHookDelegate__isVisible;

	public bool IsActive => _isActive;

	public bool IsVisible => _isVisible;

	public bool Network_isActive
	{
		get
		{
			return _isActive;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _isActive, 1uL, _Mirror_SyncVarHookDelegate__isActive);
		}
	}

	public bool Network_isVisible
	{
		get
		{
			return _isVisible;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _isVisible, 2uL, _Mirror_SyncVarHookDelegate__isVisible);
		}
	}

	private void Awake()
	{
		_renderers.AddRange(GetComponentsInChildren<Renderer>());
		_canvases.AddRange(GetComponentsInChildren<Canvas>());
		_pc = GetComponent<PlayerController>();
		_rb = GetComponent<Rigidbody>();
		_head = GetComponentInChildren<PlayerHead>();
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		if (base.isLocalPlayer && MonoSingleton<LocalManager>.Instance != null)
		{
			_mainCamera = MonoSingleton<LocalManager>.Instance.mainCamera;
		}
		UpdateUI();
		UpdateVisibility();
	}

	private void UpdateUI()
	{
		foreach (Canvas canvase in _canvases)
		{
			if (canvase != null)
			{
				canvase.enabled = !_isActive;
			}
		}
		if (base.isLocalPlayer && MonoSingleton<LocalManager>.Instance != null && MonoSingleton<LocalManager>.Instance.playerEyesUI != null)
		{
			MonoSingleton<LocalManager>.Instance.playerEyesUI.gameObject.SetActive(!_isActive);
		}
	}

	private void UpdateVisibility()
	{
		foreach (Renderer renderer in _renderers)
		{
			if (renderer != null)
			{
				renderer.enabled = _isVisible;
			}
		}
	}

	public void ToggleCameramanMode()
	{
		if (base.isLocalPlayer)
		{
			CmdSetCameramanMode(!_isActive);
		}
	}

	public void ToggleVisibility()
	{
		if (base.isLocalPlayer)
		{
			CmdSetVisibility(!_isVisible);
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdSetCameramanMode(bool active)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(active);
		SendCommandInternal("System.Void CameramanMode::CmdSetCameramanMode(System.Boolean)", 1531626262, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	private void CmdSetVisibility(bool visible)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(visible);
		SendCommandInternal("System.Void CameramanMode::CmdSetVisibility(System.Boolean)", -1250602304, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	private void OnCameramanModeChanged(bool oldValue, bool newValue)
	{
		if (base.isLocalPlayer)
		{
			if (newValue)
			{
				EnableCameramanMode();
			}
			else
			{
				DisableCameramanMode();
			}
		}
		UpdateUI();
	}

	private void OnVisibilityChanged(bool oldValue, bool newValue)
	{
		UpdateVisibility();
	}

	private void EnableCameramanMode()
	{
		if (!base.isLocalPlayer)
		{
			return;
		}
		_savedBodyPosition = base.transform.position;
		_savedBodyRotation = base.transform.rotation;
		_wasBodyKinematic = _rb.isKinematic;
		_originalConstraints = _rb.constraints;
		ToggleAllSelfMeshDisablers(enabled: true);
		_rb.isKinematic = true;
		_rb.constraints = RigidbodyConstraints.FreezeAll;
		_rb.linearVelocity = Vector3.zero;
		_rb.angularVelocity = Vector3.zero;
		if (_pc != null)
		{
			_pc.enabled = false;
		}
		if (_head != null)
		{
			_wasHeadEnabled = _head.enabled;
			_head.enabled = false;
			_head.isLocked = true;
		}
		if (_mainCamera != null)
		{
			_mainCamera.gameObject.SetActive(value: false);
		}
		DisableIdentifiedCanvases();
		if (cameramanCameraPrefab != null)
		{
			Vector3 position = ((_head != null) ? _head.transform.position : base.transform.position);
			Quaternion rotation = ((_head != null) ? _head.transform.rotation : base.transform.rotation);
			_cameramanCameraInstance = UnityEngine.Object.Instantiate(cameramanCameraPrefab, position, rotation);
			CameramanNoclipCamera component = _cameramanCameraInstance.GetComponent<CameramanNoclipCamera>();
			if (component != null)
			{
				component.InitializeRotation(position, rotation);
			}
		}
		else
		{
			Debug.LogWarning("CameramanCameraPrefab is not assigned in CameramanMode!");
		}
	}

	private void DisableCameramanMode()
	{
		if (base.isLocalPlayer)
		{
			if (_cameramanCameraInstance != null)
			{
				UnityEngine.Object.Destroy(_cameramanCameraInstance);
				_cameramanCameraInstance = null;
			}
			if (_mainCamera != null)
			{
				_mainCamera.gameObject.SetActive(value: true);
			}
			EnableIdentifiedCanvases();
			base.transform.position = _savedBodyPosition;
			base.transform.rotation = _savedBodyRotation;
			_rb.isKinematic = _wasBodyKinematic;
			_rb.constraints = _originalConstraints;
			ToggleAllSelfMeshDisablers(enabled: false);
			if (_pc != null)
			{
				_pc.enabled = true;
			}
			if (_head != null)
			{
				_head.enabled = _wasHeadEnabled;
				_head.isLocked = false;
			}
		}
	}

	private void ToggleAllSelfMeshDisablers(bool enabled)
	{
		_selfMeshDisablers.Clear();
		SelfMeshDisabler[] array = UnityEngine.Object.FindObjectsByType<SelfMeshDisabler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		foreach (SelfMeshDisabler selfMeshDisabler in array)
		{
			if (selfMeshDisabler != null)
			{
				_selfMeshDisablers.Add(selfMeshDisabler);
				selfMeshDisabler.ToggleMesh(enabled);
			}
		}
	}

	private void DisableIdentifiedCanvases()
	{
		_identifiedCanvases.Clear();
		Canvas[] array = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		foreach (Canvas canvas in array)
		{
			if (canvas != null && canvas.GetComponent<CanvasIdentifier>() != null)
			{
				_identifiedCanvases.Add((canvas, canvas.enabled));
				canvas.enabled = false;
			}
		}
	}

	private void EnableIdentifiedCanvases()
	{
		foreach (var (canvas, flag) in _identifiedCanvases)
		{
			if (canvas != null)
			{
				canvas.enabled = flag;
			}
		}
		_identifiedCanvases.Clear();
	}

	public void ToggleCameramanCanvas()
	{
		if (base.isLocalPlayer && _cameramanCameraInstance != null)
		{
			CameramanNoclipCamera component = _cameramanCameraInstance.GetComponent<CameramanNoclipCamera>();
			if (component != null)
			{
				component.ToggleCanvas();
			}
		}
	}

	public bool IsCameramanCanvasActive()
	{
		if (!base.isLocalPlayer || _cameramanCameraInstance == null)
		{
			return false;
		}
		CameramanNoclipCamera component = _cameramanCameraInstance.GetComponent<CameramanNoclipCamera>();
		if (component != null)
		{
			return component.IsCanvasActive();
		}
		return false;
	}

	public CameramanMode()
	{
		_Mirror_SyncVarHookDelegate__isActive = OnCameramanModeChanged;
		_Mirror_SyncVarHookDelegate__isVisible = OnVisibilityChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdSetCameramanMode__Boolean(bool active)
	{
		Network_isActive = active;
	}

	protected static void InvokeUserCode_CmdSetCameramanMode__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSetCameramanMode called on client.");
		}
		else
		{
			((CameramanMode)obj).UserCode_CmdSetCameramanMode__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_CmdSetVisibility__Boolean(bool visible)
	{
		Network_isVisible = visible;
	}

	protected static void InvokeUserCode_CmdSetVisibility__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSetVisibility called on client.");
		}
		else
		{
			((CameramanMode)obj).UserCode_CmdSetVisibility__Boolean(reader.ReadBool());
		}
	}

	static CameramanMode()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(CameramanMode), "System.Void CameramanMode::CmdSetCameramanMode(System.Boolean)", InvokeUserCode_CmdSetCameramanMode__Boolean, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(CameramanMode), "System.Void CameramanMode::CmdSetVisibility(System.Boolean)", InvokeUserCode_CmdSetVisibility__Boolean, requiresAuthority: false);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteBool(_isActive);
			writer.WriteBool(_isVisible);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteBool(_isActive);
		}
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteBool(_isVisible);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _isActive, _Mirror_SyncVarHookDelegate__isActive, reader.ReadBool());
			GeneratedSyncVarDeserialize(ref _isVisible, _Mirror_SyncVarHookDelegate__isVisible, reader.ReadBool());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _isActive, _Mirror_SyncVarHookDelegate__isActive, reader.ReadBool());
		}
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _isVisible, _Mirror_SyncVarHookDelegate__isVisible, reader.ReadBool());
		}
	}
}

}
