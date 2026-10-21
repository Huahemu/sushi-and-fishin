namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class PlayerParticles : MonoBehaviour
{
	[SerializeField]
	private PlayerController playerController;

	[SerializeField]
	private ParticleSystem moveTrail;

	[SerializeField]
	private ParticleSystem moveParticles;

	[SerializeField]
	private ParticleSystem jumpParticles;

	[SerializeField]
	private ParticleSystem landParticles;

	private bool _isGrounded;

	private bool _hasBody;

	private void OnEnable()
	{
		playerController.OnClientJumped += OnJump;
		playerController.OnClientLanded += OnLand;
	}

	private void OnDisable()
	{
		playerController.OnClientJumped -= OnJump;
		playerController.OnClientLanded -= OnLand;
	}

	private void OnJump(bool wasGrounded)
	{
		if (wasGrounded)
		{
			jumpParticles.Play();
		}
	}

	private void OnLand(float fallImpact)
	{
		landParticles.Play();
	}

	private void Update()
	{
		SetHasBody();
		SetIsGrounded();
		SetMoveParticles();
	}

	private void SetHasBody()
	{
		bool hasBody = playerController.hasBody;
		if (hasBody != _hasBody)
		{
			_hasBody = hasBody;
			OnHasBodyChanged();
		}
	}

	private void OnHasBodyChanged()
	{
		moveParticles.gameObject.SetActive(_hasBody);
		moveTrail.gameObject.SetActive(_hasBody);
	}

	private void SetIsGrounded()
	{
		bool isGrounded = playerController.isGrounded;
		if (isGrounded != _isGrounded)
		{
			_isGrounded = isGrounded;
			OnGroundedChange();
		}
	}

	private void OnGroundedChange()
	{
		if (_isGrounded)
		{
			moveParticles.Play();
			moveTrail.Play();
		}
		else
		{
			moveParticles.Stop();
			moveTrail.Stop();
		}
	}

	private void SetMoveParticles()
	{
		if (_isGrounded)
		{
			Vector3 serverVelocity = playerController.serverVelocity;
			if (!(serverVelocity.sqrMagnitude < 0.01f))
			{
				moveParticles.transform.rotation = FathF.LookRotationUpPriority(-serverVelocity.normalized, base.transform.up);
			}
		}
	}
}

}
