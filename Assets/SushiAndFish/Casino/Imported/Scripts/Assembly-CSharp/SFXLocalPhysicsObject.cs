namespace SushiAndFish.Casino.Original
{
using global::FMOD.Studio;
using global::FMODUnity;
using global::UnityEngine;

public class SFXLocalPhysicsObject : MonoBehaviour
{
	[SerializeField]
	private EventReference eventRef;

	[SerializeField]
	private float SensitivityThreshold = 3f;

	[Header("Stay Collision")]
	[SerializeField]
	private bool stayCollision = true;

	[SerializeField]
	private float staySensitivityMultiplier = 0.2f;

	[Header("Other")]
	[SerializeField]
	private float hitCooldownTime = 0.3f;

	private float hitCooldownTimer;

	[SerializeField]
	private float pitchMod = 1f;

	private EventInstance movementInstance;

	private Rigidbody rb;

	private int playerLayer = 6;

	private bool wasSleeping;

	[SerializeField]
	private EventReference playerHitReference;

	private float playerHitCooldownTime = 0.8f;

	private float playerHitCooldownTimer;

	private float playerHitThresholdMultiplier = 3f;

	private float playerThrowCooldown = 0.3f;

	private float startSleepTime = 0.3f;

	[SerializeField]
	private bool canHitPlayer = true;

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
	}

	private void Start()
	{
		hitCooldownTimer = Time.time + startSleepTime;
		playerHitCooldownTimer = Time.time + startSleepTime;
	}

	private void OnCollisionEnter(Collision other)
	{
		if (!base.enabled || eventRef.IsNull || hitCooldownTimer >= Time.time)
		{
			return;
		}
		if (other.gameObject.layer == playerLayer)
		{
			if (canHitPlayer)
			{
				OnPlayerCollision(other);
			}
			return;
		}
		Vector3 relativeVelocity = other.relativeVelocity;
		if (!(relativeVelocity.magnitude < SensitivityThreshold))
		{
			float num = Mathf.Max(0f, relativeVelocity.magnitude - SensitivityThreshold);
			num = Mathf.Clamp01(num * 0.07f);
			HandleHit(num);
		}
	}

	private void OnPlayerCollision(Collision other)
	{
		if (!playerHitReference.IsNull && !wasSleeping && !(other.relativeVelocity.magnitude < 6.5f) && !(playerHitCooldownTimer >= Time.time))
		{
			Vector3 relativeVelocity = other.relativeVelocity;
			if (!(relativeVelocity.magnitude < SensitivityThreshold * playerHitThresholdMultiplier))
			{
				float num = Mathf.Max(0f, relativeVelocity.magnitude - SensitivityThreshold);
				num = Mathf.Clamp01(num * 0.07f);
				HandlePlayerHit(num);
			}
		}
	}

	private void OnCollisionStay(Collision other)
	{
		if (base.enabled && stayCollision && !eventRef.IsNull && !wasSleeping && !(hitCooldownTimer >= Time.time) && other.gameObject.layer != playerLayer)
		{
			Vector3 impulse = other.impulse;
			if (!(impulse.magnitude < SensitivityThreshold * staySensitivityMultiplier))
			{
				float magnitude = Mathf.Max(0f, impulse.magnitude - SensitivityThreshold);
				HandleHit(magnitude);
			}
		}
	}

	private void LateUpdate()
	{
		if (base.enabled)
		{
			wasSleeping = rb.IsSleeping();
		}
	}

	private void HandleHit(float magnitude)
	{
		PlayHit(magnitude);
		hitCooldownTimer = Time.time + hitCooldownTime * Random.Range(0.9f, 1f);
	}

	private void PlayHit(float magnitude)
	{
		SFXParams[] sFXParams = new SFXParams[2]
		{
			new SFXParams("PhysicsObjectType", 0f),
			new SFXParams("Magnitude", magnitude)
		};
		SFXManager.SFXOneShotWithParameters(eventRef, sFXParams, base.gameObject.transform.position, pitchMod);
	}

	private void HandlePlayerHit(float magnitude)
	{
		PlayPlayerHit(magnitude);
		playerHitCooldownTimer = Time.time + playerHitCooldownTime;
	}

	private void PlayPlayerHit(float magnitude)
	{
		SFXParams[] sFXParams = new SFXParams[1]
		{
			new SFXParams("Magnitude", magnitude)
		};
		SFXManager.SFXOneShotWithParameters(playerHitReference, sFXParams, base.gameObject.transform.position);
	}

	public void SetPlayerThrowCooldown()
	{
		playerHitCooldownTimer = Time.time + playerThrowCooldown;
	}
}

}
