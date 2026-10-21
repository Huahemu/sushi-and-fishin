namespace SushiAndFish.Casino.Original
{
using global::Gilzoide.UpdateManager;
using global::TMPro;
using global::UnityEngine;

public class LookAtCamera : MonoBehaviour, ILateUpdatable, IManagedObject
{
	[Header("Look At Settings")]
	[Tooltip("Whether to look at the camera on every frame")]
	public bool lookAtOnUpdate = true;

	[Tooltip("Whether to use smooth rotation")]
	public bool useSmoothRotation;

	[Tooltip("Rotation speed when using smooth rotation")]
	[Range(1f, 20f)]
	public float rotationSpeed = 5f;

	[Header("Axis Locking")]
	[Tooltip("Lock rotation on specific axes")]
	public bool lockXAxis;

	public bool lockYAxis;

	public bool lockZAxis;

	[Header("Offset")]
	[Tooltip("Optional offset to add to the look direction")]
	public Vector3 lookOffset = Vector3.zero;

	[Header("Flip Options")]
	[Tooltip("Flip the object 180 degrees around Y-axis")]
	public bool flipY;

	[Tooltip("Flip the object 180 degrees around X-axis")]
	public bool flipX;

	[Tooltip("Flip the object 180 degrees around Z-axis")]
	public bool flipZ;

	[Header("Rotation Clamps")]
	[Tooltip("Enable rotation clamping to limit rotation angles")]
	public bool useRotationClamps;

	[Tooltip("Minimum rotation angle for X-axis (degrees)")]
	public float minRotationX = -180f;

	[Tooltip("Maximum rotation angle for X-axis (degrees)")]
	public float maxRotationX = 180f;

	[Tooltip("Minimum rotation angle for Y-axis (degrees)")]
	public float minRotationY = -180f;

	[Tooltip("Maximum rotation angle for Y-axis (degrees)")]
	public float maxRotationY = 180f;

	[Tooltip("Minimum rotation angle for Z-axis (degrees)")]
	public float minRotationZ = -180f;

	[Tooltip("Maximum rotation angle for Z-axis (degrees)")]
	public float maxRotationZ = 180f;

	[Header("Fade (TextMeshPro)")]
	[Tooltip("If enabled, fades a TextMeshPro component based on distance to the main camera")]
	public bool fadeTextByDistance;

	[Tooltip("TextMeshPro component to fade. If left null, will try to find one on this object (or its children).")]
	public TMP_Text textToFade;

	[Tooltip("Distance at which fading starts (near).")]
	[Min(0f)]
	public float fadeStartDistance = 2f;

	[Tooltip("Distance at which fading ends (far).")]
	[Min(0f)]
	public float fadeEndDistance = 10f;

	[Tooltip("Alpha at (or closer than) Fade Start Distance.")]
	[Range(0f, 1f)]
	public float nearAlpha = 1f;

	[Tooltip("Alpha at (or farther than) Fade End Distance.")]
	[Range(0f, 1f)]
	public float farAlpha;

	private Camera _cachedMainCamera;

	private void OnEnable()
	{
		CacheReferencesIfNeeded();
		this.RegisterInManager();
	}

	private void OnDisable()
	{
		this.UnregisterInManager();
	}

	public void ManagedLateUpdate()
	{
		if (lookAtOnUpdate)
		{
			LookAtCameraTransform();
		}
		if (fadeTextByDistance)
		{
			UpdateTextFade();
		}
	}

	private void LookAtCameraTransform()
	{
		Camera mainCamera = GetMainCamera();
		if (mainCamera == null)
		{
			return;
		}
		Vector3 vector = mainCamera.transform.position - base.transform.position + lookOffset;
		if (lockXAxis)
		{
			vector.x = 0f;
		}
		if (lockYAxis)
		{
			vector.y = 0f;
		}
		if (lockZAxis)
		{
			vector.z = 0f;
		}
		if (vector != Vector3.zero)
		{
			Quaternion quaternion = Quaternion.LookRotation(vector);
			if (flipY)
			{
				quaternion *= Quaternion.Euler(0f, 180f, 0f);
			}
			if (flipX)
			{
				quaternion *= Quaternion.Euler(180f, 0f, 0f);
			}
			if (flipZ)
			{
				quaternion *= Quaternion.Euler(0f, 0f, 180f);
			}
			if (useRotationClamps)
			{
				Vector3 eulerAngles = quaternion.eulerAngles;
				eulerAngles.x = ClampAngle(eulerAngles.x, minRotationX, maxRotationX);
				eulerAngles.y = ClampAngle(eulerAngles.y, minRotationY, maxRotationY);
				eulerAngles.z = ClampAngle(eulerAngles.z, minRotationZ, maxRotationZ);
				quaternion = Quaternion.Euler(eulerAngles);
			}
			if (useSmoothRotation)
			{
				base.transform.rotation = Quaternion.Slerp(base.transform.rotation, quaternion, rotationSpeed * Time.deltaTime);
			}
			else
			{
				base.transform.rotation = quaternion;
			}
		}
	}

	private void UpdateTextFade()
	{
		CacheReferencesIfNeeded();
		if (textToFade == null)
		{
			return;
		}
		Camera mainCamera = GetMainCamera();
		if (!(mainCamera == null))
		{
			float num = Vector3.Distance(mainCamera.transform.position, base.transform.position);
			float a;
			if (fadeEndDistance <= fadeStartDistance)
			{
				a = ((num <= fadeStartDistance) ? nearAlpha : farAlpha);
			}
			else
			{
				float t = Mathf.InverseLerp(fadeStartDistance, fadeEndDistance, num);
				a = Mathf.Lerp(nearAlpha, farAlpha, t);
			}
			Color color = textToFade.color;
			color.a = a;
			textToFade.color = color;
		}
	}

	private void CacheReferencesIfNeeded()
	{
		if (_cachedMainCamera == null)
		{
			_cachedMainCamera = Camera.main;
		}
		if (textToFade == null)
		{
			textToFade = GetComponent<TMP_Text>();
			if (textToFade == null)
			{
				textToFade = GetComponentInChildren<TMP_Text>();
			}
		}
	}

	private Camera GetMainCamera()
	{
		if (_cachedMainCamera == null)
		{
			_cachedMainCamera = Camera.main;
		}
		return _cachedMainCamera;
	}

	private float ClampAngle(float angle, float min, float max)
	{
		if (angle > 180f)
		{
			angle -= 360f;
		}
		return Mathf.Clamp(angle, min, max);
	}
}

}
