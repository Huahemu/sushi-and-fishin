namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class FloatingObjectPhysics : MonoBehaviour
{
	[Header("Physics Settings")]
	[SerializeField]
	private float forceMultiplier = 10f;

	[SerializeField]
	private float returnLerpSpeed = 2f;

	[SerializeField]
	private float rotationLerpSpeed = 2f;

	[SerializeField]
	private float drag = 2f;

	[SerializeField]
	private float angularDrag = 2f;

	[Header("Return Threshold")]
	[SerializeField]
	private float positionThreshold = 0.01f;

	[SerializeField]
	private float rotationThreshold = 0.1f;

	[SerializeField]
	private float velocityThreshold = 0.1f;

	[Header("Hover Movement")]
	[SerializeField]
	private bool enableHover = true;

	[SerializeField]
	private float hoverAmplitude = 0.1f;

	[SerializeField]
	private float hoverSpeed = 1f;

	[SerializeField]
	private Vector3 hoverDirection = Vector3.up;

	[Tooltip("Additional random offset per object for variation")]
	[SerializeField]
	private float hoverRandomOffset;

	[Header("Visual Effects")]
	[SerializeField]
	private ParticleSystem clickParticle;

	[Tooltip("If true, will instantiate a new particle system for each click. Otherwise, moves the existing one.")]
	[SerializeField]
	private bool instantiateParticles = true;

	[SerializeField]
	private bool debugParticles;

	private Rigidbody rb;

	private Collider col;

	private Camera mainCamera;

	private Vector3 originalPosition;

	private Quaternion originalRotation;

	private bool isReturning;

	private float hoverTimeOffset;

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
		col = GetComponent<Collider>();
		rb.useGravity = false;
		rb.linearDamping = drag;
		rb.angularDamping = angularDrag;
		rb.isKinematic = false;
		originalPosition = base.transform.position;
		originalRotation = base.transform.rotation;
		hoverTimeOffset = UnityEngine.Random.Range(0f, MathF.PI * 2f);
		if (hoverRandomOffset > 0f)
		{
			hoverTimeOffset += UnityEngine.Random.Range(0f - hoverRandomOffset, hoverRandomOffset);
		}
	}

	private void Start()
	{
		if (mainCamera == null)
		{
			mainCamera = Camera.main;
		}
	}

	private void Update()
	{
		if (enableHover)
		{
			ApplyHoverMovement();
		}
		ReturnToOriginal();
	}

	public void HandleClick(RaycastHit hit, Camera camera)
	{
		if (!(camera == null))
		{
			Vector3 point = hit.point;
			Vector3 forward = camera.transform.forward;
			rb.AddForceAtPosition(forward * forceMultiplier, point, ForceMode.Impulse);
			Vector3 right = camera.transform.right;
			Vector3 up = camera.transform.up;
			Vector3 torque = (right + up * 0.5f) * forceMultiplier * 0.1f;
			rb.AddTorque(torque, ForceMode.Impulse);
			PlayParticleAtPoint(point, hit.normal);
			isReturning = false;
		}
	}

	private void PlayParticleAtPoint(Vector3 position, Vector3 normal)
	{
		if (clickParticle == null)
		{
			if (debugParticles)
			{
				Debug.LogWarning("[FloatingObjectPhysics] No particle system assigned on " + base.gameObject.name);
			}
			return;
		}
		if (instantiateParticles)
		{
			ParticleSystem particleSystem = UnityEngine.Object.Instantiate(clickParticle, position, Quaternion.LookRotation(normal));
			particleSystem.gameObject.SetActive(value: true);
			ParticleSystem.MainModule main = particleSystem.main;
			main.playOnAwake = false;
			particleSystem.Play();
			if (debugParticles)
			{
				Debug.Log($"[FloatingObjectPhysics] Instantiated and played particle at {position}");
			}
			if (main.duration > 0f)
			{
				float num = ((main.startLifetime.constantMax > 0f) ? main.startLifetime.constantMax : main.startLifetime.constant);
				UnityEngine.Object.Destroy(particleSystem.gameObject, main.duration + num + 1f);
			}
			else
			{
				UnityEngine.Object.Destroy(particleSystem.gameObject, 5f);
			}
			return;
		}
		if (!clickParticle.gameObject.activeInHierarchy)
		{
			clickParticle.gameObject.SetActive(value: true);
		}
		if (clickParticle.isPlaying)
		{
			clickParticle.Stop();
			clickParticle.Clear();
		}
		clickParticle.transform.position = position;
		clickParticle.transform.rotation = Quaternion.LookRotation(normal);
		clickParticle.Play();
		if (debugParticles)
		{
			Debug.Log($"[FloatingObjectPhysics] Played particle at {position}");
		}
	}

	private void ApplyHoverMovement()
	{
		if (!(Vector3.Distance(base.transform.position, originalPosition) > 0.5f) && !(rb.linearVelocity.magnitude > velocityThreshold * 2f))
		{
			float num = Mathf.Sin(Time.time * hoverSpeed + hoverTimeOffset) * hoverAmplitude;
			Vector3 force = (originalPosition + hoverDirection.normalized * num - base.transform.position) * returnLerpSpeed * 0.5f;
			rb.AddForce(force, ForceMode.Force);
		}
	}

	private void ReturnToOriginal()
	{
		if (!isReturning && rb.linearVelocity.magnitude < velocityThreshold)
		{
			isReturning = true;
		}
		Vector3 vector = originalPosition;
		if (enableHover && Vector3.Distance(base.transform.position, originalPosition) <= 0.5f && rb.linearVelocity.magnitude <= velocityThreshold * 2f)
		{
			float num = Mathf.Sin(Time.time * hoverSpeed + hoverTimeOffset) * hoverAmplitude;
			vector = originalPosition + hoverDirection.normalized * num;
		}
		if (!isReturning && !(Vector3.Distance(base.transform.position, originalPosition) < 1f))
		{
			return;
		}
		Vector3 vector2 = vector - base.transform.position;
		float magnitude = vector2.magnitude;
		if (magnitude > positionThreshold)
		{
			Vector3 force = vector2.normalized * returnLerpSpeed * magnitude;
			rb.AddForce(force, ForceMode.Force);
		}
		else
		{
			Vector3 b = (vector - base.transform.position) / Time.deltaTime;
			rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, b, Time.deltaTime * returnLerpSpeed);
		}
		(originalRotation * Quaternion.Inverse(base.transform.rotation)).ToAngleAxis(out var angle, out var axis);
		if (angle > rotationThreshold)
		{
			if (angle > 180f)
			{
				angle -= 360f;
			}
			Vector3 torque = axis * (angle * (MathF.PI / 180f) * rotationLerpSpeed);
			rb.AddTorque(torque, ForceMode.Force);
		}
		else
		{
			base.transform.rotation = Quaternion.Lerp(base.transform.rotation, originalRotation, Time.deltaTime * rotationLerpSpeed);
			rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, Time.deltaTime * rotationLerpSpeed);
		}
	}

	private void OnDrawGizmosSelected()
	{
		if (Application.isPlaying)
		{
			Gizmos.color = Color.green;
			Gizmos.DrawWireSphere(originalPosition, 0.1f);
			Gizmos.DrawLine(base.transform.position, originalPosition);
		}
	}

	public void ResetOriginalTransform()
	{
		originalPosition = base.transform.position;
		originalRotation = base.transform.rotation;
	}
}

}
