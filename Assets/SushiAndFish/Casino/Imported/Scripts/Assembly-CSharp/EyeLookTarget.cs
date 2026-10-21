namespace SushiAndFish.Casino.Original
{
using global::System;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class EyeLookTarget : MonoBehaviour
{
	[Header("Smooth Rotation Settings")]
	[SerializeField]
	private float smoothTime = 0.12f;

	[Header("Left Eye Rotation Limits")]
	[SerializeField]
	private float leftEyeMinDegrees = -45f;

	[SerializeField]
	private float leftEyeMaxDegrees = 45f;

	[Header("Right Eye Rotation Limits")]
	[SerializeField]
	private float rightEyeMinDegrees = -45f;

	[SerializeField]
	private float rightEyeMaxDegrees = 45f;

	private PlayerOrgans playerOrgans;

	public bool isDestroying;

	private float leftEyeYawVelocity;

	private float leftEyePitchVelocity;

	private float rightEyeYawVelocity;

	private float rightEyePitchVelocity;

	private Transform leftEye => playerOrgans?.LeftEye;

	private Transform rightEye => playerOrgans?.RightEye;

	private void Start()
	{
		playerOrgans = MonoSingleton<LocalManager>.Instance.players.Find((PlayerReferences player) => player.identity.isLocalPlayer).organs;
	}

	private void LateUpdate()
	{
		if (!isDestroying)
		{
			Vector3 position = base.transform.position;
			if (leftEye != null)
			{
				SmoothRotateEye(leftEye, ref leftEyeYawVelocity, ref leftEyePitchVelocity, leftEyeMinDegrees, leftEyeMaxDegrees, position);
			}
			if (rightEye != null)
			{
				SmoothRotateEye(rightEye, ref rightEyeYawVelocity, ref rightEyePitchVelocity, rightEyeMinDegrees, rightEyeMaxDegrees, position);
			}
		}
	}

	private Quaternion ClampRotation(Transform eye, Quaternion targetRotation, float minDegrees, float maxDegrees)
	{
		if (eye.parent == null)
		{
			return targetRotation;
		}
		_ = eye.parent.forward;
		Vector3 direction = targetRotation * Vector3.forward;
		Vector3 vector = eye.parent.InverseTransformDirection(direction);
		float value = Mathf.Atan2(vector.x, vector.z) * 57.29578f;
		float value2 = Mathf.Asin(vector.y) * 57.29578f;
		value = Mathf.Clamp(value, minDegrees, maxDegrees);
		float num = Mathf.Clamp(value2, minDegrees, maxDegrees);
		float f = value * (MathF.PI / 180f);
		float f2 = num * (MathF.PI / 180f);
		Vector3 direction2 = new Vector3(Mathf.Sin(f) * Mathf.Cos(f2), Mathf.Sin(f2), Mathf.Cos(f) * Mathf.Cos(f2));
		return Quaternion.LookRotation(eye.parent.TransformDirection(direction2));
	}

	public void SmoothEyesToForwardAndDestroy()
	{
		if (isDestroying)
		{
			return;
		}
		isDestroying = true;
		if (playerOrgans == null)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		Transform transform = leftEye?.parent ?? rightEye?.parent;
		if (transform == null)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		Quaternion rotation = transform.rotation;
		float duration = 0.3f;
		int completed = 0;
		int total = ((leftEye != null) ? 1 : 0) + ((rightEye != null) ? 1 : 0);
		if (total == 0)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		TweenCallback action = () =>
		{
			completed++;
			if (completed >= total)
			{
				UnityEngine.Object.Destroy(base.gameObject);
			}
		};
		if (leftEye != null)
		{
			leftEye.DORotateQuaternion(rotation, duration).OnComplete(action);
		}
		if (rightEye != null)
		{
			rightEye.DORotateQuaternion(rotation, duration).OnComplete(action);
		}
	}

	private void SmoothRotateEye(Transform eye, ref float yawVelocity, ref float pitchVelocity, float minDegrees, float maxDegrees, Vector3 targetPoint)
	{
		if (!(eye.parent == null))
		{
			Vector3 vector = targetPoint - eye.position;
			if (!(vector == Vector3.zero))
			{
				Vector3 vector2 = eye.parent.InverseTransformDirection(vector.normalized);
				float value = Mathf.Atan2(vector2.x, vector2.z) * 57.29578f;
				float value2 = Mathf.Asin(vector2.y) * 57.29578f;
				value = Mathf.Clamp(value, minDegrees, maxDegrees);
				value2 = Mathf.Clamp(value2, minDegrees, maxDegrees);
				Vector3 localEulerAngles = eye.localEulerAngles;
				float current = NormalizeAngle(localEulerAngles.y);
				float current2 = NormalizeAngle(localEulerAngles.x);
				float y = Mathf.SmoothDampAngle(current, value, ref yawVelocity, smoothTime);
				float x = Mathf.SmoothDampAngle(current2, value2, ref pitchVelocity, smoothTime);
				eye.localRotation = Quaternion.Euler(x, y, 0f);
			}
		}
	}

	private float NormalizeAngle(float angle)
	{
		if (angle > 180f)
		{
			angle -= 360f;
		}
		return angle;
	}
}

}
