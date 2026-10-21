namespace SushiAndFish.Casino.Original
{
using global::FMOD.Studio;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class SFXPhysicsObject : NetworkBehaviour
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
		CmdPlayHit(magnitude);
		hitCooldownTimer = Time.time + hitCooldownTime * Random.Range(0.9f, 1f);
	}

	[Server]
	private void CmdPlayHit(float magnitude)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void SFXPhysicsObject::CmdPlayHit(System.Single)' called when server was not active");
		}
		else
		{
			RpcPlayHit(magnitude);
		}
	}

	[ClientRpc]
	private void RpcPlayHit(float magnitude)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(magnitude);
		SendRPCInternal("System.Void SFXPhysicsObject::RpcPlayHit(System.Single)", 1505502791, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void HandlePlayerHit(float magnitude)
	{
		CmdPlayPlayerHit(magnitude);
		playerHitCooldownTimer = Time.time + playerHitCooldownTime;
	}

	[Server]
	private void CmdPlayPlayerHit(float magnitude)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void SFXPhysicsObject::CmdPlayPlayerHit(System.Single)' called when server was not active");
		}
		else
		{
			RpcPlayPlayerHit(magnitude);
		}
	}

	[ClientRpc]
	private void RpcPlayPlayerHit(float magnitude)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(magnitude);
		SendRPCInternal("System.Void SFXPhysicsObject::RpcPlayPlayerHit(System.Single)", 270337566, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public void SetPlayerThrowCooldown()
	{
		playerHitCooldownTimer = Time.time + playerThrowCooldown;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcPlayHit__Single(float magnitude)
	{
		SFXParams[] sFXParams = new SFXParams[2]
		{
			new SFXParams("PhysicsObjectType", 0f),
			new SFXParams("Magnitude", magnitude)
		};
		SFXManager.SFXOneShotWithParameters(eventRef, sFXParams, base.gameObject.transform.position, pitchMod);
	}

	protected static void InvokeUserCode_RpcPlayHit__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayHit called on server.");
		}
		else
		{
			((SFXPhysicsObject)obj).UserCode_RpcPlayHit__Single(reader.ReadFloat());
		}
	}

	protected void UserCode_RpcPlayPlayerHit__Single(float magnitude)
	{
		SFXParams[] sFXParams = new SFXParams[1]
		{
			new SFXParams("Magnitude", magnitude)
		};
		SFXManager.SFXOneShotWithParameters(playerHitReference, sFXParams, base.gameObject.transform.position);
	}

	protected static void InvokeUserCode_RpcPlayPlayerHit__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayPlayerHit called on server.");
		}
		else
		{
			((SFXPhysicsObject)obj).UserCode_RpcPlayPlayerHit__Single(reader.ReadFloat());
		}
	}

	static SFXPhysicsObject()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(SFXPhysicsObject), "System.Void SFXPhysicsObject::RpcPlayHit(System.Single)", InvokeUserCode_RpcPlayHit__Single);
		RemoteProcedureCalls.RegisterRpc(typeof(SFXPhysicsObject), "System.Void SFXPhysicsObject::RpcPlayPlayerHit(System.Single)", InvokeUserCode_RpcPlayPlayerHit__Single);
	}
}

}
