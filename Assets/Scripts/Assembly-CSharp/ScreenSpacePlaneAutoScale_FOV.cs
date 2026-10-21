using System;
using UnityEngine;

[ExecuteAlways]
public class ScreenSpacePlaneAutoScale_FOV : MonoBehaviour
{
	[Header("Camera")]
	public Camera targetCamera;

	[Header("Coverage")]
	[Tooltip("1 = exactly fills the camera frustum at this distance. >1 overscales.")]
	public float overScaling = 10f;

	[Tooltip("If true and this object is parented to the camera, uses localPosition.z as distance.")]
	public bool useLocalZDistanceIfCameraParent = true;

	[Header("Axis Mapping")]
	[Tooltip("If your plane is rotated (e.g., lies on XZ), enable this to swap Y/Z usage.")]
	public bool planeUsesXZInsteadOfXY;

	private float _lastFov = -1f;

	private float _lastAspect = -1f;

	private float _lastDist = -1f;

	private void OnEnable()
	{
		ResolveCamera();
		Apply();
	}

	private void LateUpdate()
	{
		ResolveCamera();
		if (!(targetCamera == null))
		{
			float fieldOfView = targetCamera.fieldOfView;
			float aspect = targetCamera.aspect;
			float distanceFromCamera = GetDistanceFromCamera();
			if (!Mathf.Approximately(fieldOfView, _lastFov) || !Mathf.Approximately(aspect, _lastAspect) || !Mathf.Approximately(distanceFromCamera, _lastDist))
			{
				Apply();
			}
		}
	}

	private void ResolveCamera()
	{
		if (!(targetCamera != null))
		{
			targetCamera = GetComponentInParent<Camera>();
			if (targetCamera == null)
			{
				targetCamera = Camera.main;
			}
		}
	}

	private float GetDistanceFromCamera()
	{
		if (targetCamera == null)
		{
			return 1f;
		}
		if (useLocalZDistanceIfCameraParent && base.transform.IsChildOf(targetCamera.transform))
		{
			return Mathf.Max(0.0001f, base.transform.localPosition.z);
		}
		float b = Vector3.Distance(base.transform.position, targetCamera.transform.position);
		return Mathf.Max(0.0001f, b);
	}

	private void Apply()
	{
		if (!(targetCamera == null))
		{
			float distanceFromCamera = GetDistanceFromCamera();
			float num = targetCamera.fieldOfView * (MathF.PI / 180f);
			float num2 = 2f * Mathf.Tan(0.5f * num) * distanceFromCamera;
			float num3 = num2 * targetCamera.aspect;
			num2 *= overScaling;
			num3 *= overScaling;
			if (!planeUsesXZInsteadOfXY)
			{
				base.transform.localScale = new Vector3(num3, num2, base.transform.localScale.z);
			}
			else
			{
				base.transform.localScale = new Vector3(num3, base.transform.localScale.y, num2);
			}
			_lastFov = targetCamera.fieldOfView;
			_lastAspect = targetCamera.aspect;
			_lastDist = distanceFromCamera;
		}
	}
}
