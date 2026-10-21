namespace SushiAndFish.Casino.Original
{
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class PlinkoPuckSFX : NetworkBehaviour
{
	public LayerMask allowedLayers;

	[SerializeField]
	private EventReference eventRef;

	[SerializeField]
	private float SensitivityThreshold = 3f;

	[SerializeField]
	private float hitCooldownTime = 0.3f;

	private float hitCooldownTimer;

	[SerializeField]
	private float pitchMod = 1f;

	private void OnCollisionEnter(Collision other)
	{
		if (!eventRef.IsNull && !(hitCooldownTimer >= Time.time))
		{
			Vector3 relativeVelocity = other.relativeVelocity;
			if (!(relativeVelocity.magnitude < SensitivityThreshold))
			{
				float num = Mathf.Max(0f, relativeVelocity.magnitude - SensitivityThreshold);
				num = Mathf.Clamp01(num * 0.07f);
				HandleHit(num);
				pitchMod += Random.Range(0.02f, 0.15f);
			}
		}
	}

	private void HandleHit(float magnitude)
	{
		CmdPlayHit(magnitude);
		hitCooldownTimer = Time.time + hitCooldownTime;
	}

	[Server]
	private void CmdPlayHit(float magnitude)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlinkoPuckSFX::CmdPlayHit(System.Single)' called when server was not active");
		}
		else if (!eventRef.IsNull)
		{
			RpcPlayHit(magnitude);
		}
	}

	[ClientRpc]
	private void RpcPlayHit(float magnitude)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(magnitude);
		SendRPCInternal("System.Void PlinkoPuckSFX::RpcPlayHit(System.Single)", -1859393961, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcPlayHit__Single(float magnitude)
	{
		SFXParams[] sFXParams = new SFXParams[1]
		{
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
			((PlinkoPuckSFX)obj).UserCode_RpcPlayHit__Single(reader.ReadFloat());
		}
	}

	static PlinkoPuckSFX()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(PlinkoPuckSFX), "System.Void PlinkoPuckSFX::RpcPlayHit(System.Single)", InvokeUserCode_RpcPlayHit__Single);
	}
}

}
