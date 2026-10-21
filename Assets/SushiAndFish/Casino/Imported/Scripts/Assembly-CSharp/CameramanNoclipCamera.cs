namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class CameramanNoclipCamera : MonoBehaviour
{
	[Header("Movement Settings")]
	[SerializeField]
	private float speed = 600f;

	[SerializeField]
	private float sprintSpeed = 1000f;

	[SerializeField]
	private float smoothness = 0.3f;

	private Camera _camera;

	private CameraSettings _cs;

	private Canvas _canvas;

	private Vector2 _horizontalInput;

	private Vector2 _lookInput;

	private Vector3 _currentVelocity;

	private float _yaw;

	private float _pitch;

	[Header("Canvas Settings")]
	[SerializeField]
	private float planeDistanceStep = 10f;

	[SerializeField]
	private float minPlaneDistance = 10f;

	[SerializeField]
	private float maxPlaneDistance = 1000f;

	private void Awake()
	{
		_camera = GetComponent<Camera>();
		if (_camera == null)
		{
			_camera = GetComponentInChildren<Camera>();
		}
		_canvas = GetComponentInChildren<Canvas>();
		_cs = SushiAndFish.Casino.Original.CasinoResources.Load<CameraSettings>("CameraSettings");
	}

	private void Start()
	{
		if (_camera != null)
		{
			Camera main = Camera.main;
			if (main != null && main != _camera)
			{
				main.gameObject.SetActive(value: false);
			}
			_camera.tag = "MainCamera";
		}
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	private void OnEnable()
	{
		InputEvents.OnMoveEvent = (Action<Vector2>)Delegate.Combine(InputEvents.OnMoveEvent, new Action<Vector2>(OnMove));
		InputEvents.OnAimEvent = (Action<Vector2>)Delegate.Combine(InputEvents.OnAimEvent, new Action<Vector2>(OnLook));
		InputEvents.OnScrollEvent = (Action<int>)Delegate.Combine(InputEvents.OnScrollEvent, new Action<int>(OnScroll));
	}

	private void OnDisable()
	{
		InputEvents.OnMoveEvent = (Action<Vector2>)Delegate.Remove(InputEvents.OnMoveEvent, new Action<Vector2>(OnMove));
		InputEvents.OnAimEvent = (Action<Vector2>)Delegate.Remove(InputEvents.OnAimEvent, new Action<Vector2>(OnLook));
		InputEvents.OnScrollEvent = (Action<int>)Delegate.Remove(InputEvents.OnScrollEvent, new Action<int>(OnScroll));
	}

	private void OnDestroy()
	{
		if (MonoSingleton<LocalManager>.Instance != null && MonoSingleton<LocalManager>.Instance.mainCamera != null)
		{
			MonoSingleton<LocalManager>.Instance.mainCamera.gameObject.SetActive(value: true);
		}
	}

	private void OnMove(Vector2 input)
	{
		_horizontalInput = input;
	}

	private void OnLook(Vector2 input)
	{
		_lookInput = input;
	}

	private void OnScroll(int scrollValue)
	{
		if (_canvas != null && _canvas.gameObject.activeSelf)
		{
			float value = _canvas.planeDistance + (float)scrollValue * planeDistanceStep;
			_canvas.planeDistance = Mathf.Clamp(value, minPlaneDistance, maxPlaneDistance);
		}
	}

	public void ToggleCanvas()
	{
		if (_canvas != null)
		{
			_canvas.gameObject.SetActive(!_canvas.gameObject.activeSelf);
		}
	}

	public bool IsCanvasActive()
	{
		if (_canvas != null)
		{
			return _canvas.gameObject.activeSelf;
		}
		return false;
	}

	private void Update()
	{
		if (!(_camera == null))
		{
			HandleRotation();
			HandleMovement();
		}
	}

	private void HandleRotation()
	{
		float num = (InputEvents.IsZoomPressed ? _cs.zoomSensitivity : _cs.sensitivity.value);
		_yaw += _lookInput.x * num * Time.deltaTime;
		_pitch -= _lookInput.y * num * Time.deltaTime;
		_pitch = Mathf.Clamp(_pitch, -89f, 89f);
		Quaternion b = Quaternion.Euler(_pitch, _yaw, 0f);
		float t = _cs.cameraLerp * Time.deltaTime;
		base.transform.rotation = Quaternion.Slerp(base.transform.rotation, b, t);
	}

	private void HandleMovement()
	{
		int num = 0;
		if (InputEvents.IsJumpPressed)
		{
			num++;
		}
		if (InputEvents.IsCrouchPressed)
		{
			num--;
		}
		Vector3 normalized = Vector3.ProjectOnPlane(base.transform.forward, Vector3.up).normalized;
		Vector3 normalized2 = Vector3.ProjectOnPlane(base.transform.right, Vector3.up).normalized;
		Vector3 normalized3 = (normalized * _horizontalInput.y + normalized2 * _horizontalInput.x + Vector3.up * num).normalized;
		float num2 = (InputEvents.IsSprintPressed ? sprintSpeed : speed);
		Vector3 vector = Vector3.SmoothDamp(Vector3.zero, normalized3, ref _currentVelocity, smoothness);
		base.transform.position += vector * (num2 * Time.unscaledDeltaTime);
	}

	public void InitializeRotation(Vector3 position, Quaternion rotation)
	{
		base.transform.position = position;
		base.transform.rotation = rotation;
		Vector3 eulerAngles = rotation.eulerAngles;
		_yaw = eulerAngles.y;
		_pitch = eulerAngles.x;
		if (_pitch > 180f)
		{
			_pitch -= 360f;
		}
	}
}

}
