namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::DG.Tweening;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class DuckRaceDuck : NetworkBehaviour
{
	[SerializeField]
	private float minStepDelay = 0.5f;

	[SerializeField]
	private float maxStepDelay = 1f;

	[SerializeField]
	private float minStepDistance = 0.25f;

	[SerializeField]
	private float maxStepDistance = 1f;

	[SerializeField]
	private float stepTweenDuration = 0.4f;

	[SerializeField]
	private DuckRace duckRace;

	[SerializeField]
	private Animator animator;

	[SerializeField]
	private ParticleSystem dustVfx;

	[SerializeField]
	private EventReference duckQuackSfx;

	[SerializeField]
	private EventReference duckWinSfx;

	[Server]
	public void ServerStartRace(global::System.Random rng)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void DuckRaceDuck::ServerStartRace(System.Random)' called when server was not active");
			return;
		}
		StartCoroutine(DuckRaceRoutine(rng));
		StartCoroutine(DuckQuackRoutine(rng));
	}

	private IEnumerator DuckRaceRoutine(global::System.Random rng)
	{
		RpcSetRunningAnimation(isRunning: true);
		while (!duckRace.hasEnded)
		{
			float num = Mathf.Lerp(minStepDistance, maxStepDistance, (float)rng.NextDouble());
			float targetZ = Mathf.Min(base.transform.localPosition.z + num, duckRace.endPoint.localPosition.z);
			RpcStep(targetZ);
			yield return new WaitForSeconds(stepTweenDuration);
			if (Mathf.Approximately(targetZ, duckRace.endPoint.localPosition.z))
			{
				if (duckRace.OnDuckFinish(this))
				{
					RpcWinFeedback();
				}
				RpcSetRunningAnimation(isRunning: false);
				yield break;
			}
			float seconds = Mathf.Lerp(minStepDelay, maxStepDelay, (float)rng.NextDouble());
			yield return new WaitForSeconds(seconds);
		}
		RpcSetRunningAnimation(isRunning: false);
	}

	[ClientRpc]
	private void RpcSetRunningAnimation(bool isRunning)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isRunning);
		SendRPCInternal("System.Void DuckRaceDuck::RpcSetRunningAnimation(System.Boolean)", -684728799, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private IEnumerator DuckQuackRoutine(global::System.Random rng)
	{
		while (!duckRace.hasEnded)
		{
			float seconds = Mathf.Lerp(0.3f, 1f, (float)rng.NextDouble());
			RpcDuckQuack();
			yield return new WaitForSeconds(seconds);
		}
	}

	[ClientRpc]
	private void RpcDuckQuack()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void DuckRaceDuck::RpcDuckQuack()", 1749117279, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcStep(float targetZ)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(targetZ);
		SendRPCInternal("System.Void DuckRaceDuck::RpcStep(System.Single)", 1439858130, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcWinFeedback()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void DuckRaceDuck::RpcWinFeedback()", 1853282982, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ServerReturn()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void DuckRaceDuck::ServerReturn()' called when server was not active");
		}
		else
		{
			RpcReturn();
		}
	}

	[ClientRpc]
	private void RpcReturn()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void DuckRaceDuck::RpcReturn()", -1424841539, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ResetDuck()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void DuckRaceDuck::ResetDuck()' called when server was not active");
			return;
		}
		RpcResetDuck();
		RpcSetRunningAnimation(isRunning: false);
	}

	[ClientRpc]
	private void RpcResetDuck()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void DuckRaceDuck::RpcResetDuck()", -931685591, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetRunningAnimation__Boolean(bool isRunning)
	{
		animator.SetBool("isRunning", isRunning);
		if (isRunning)
		{
			dustVfx.Play();
		}
		else
		{
			dustVfx.Stop();
		}
	}

	protected static void InvokeUserCode_RpcSetRunningAnimation__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetRunningAnimation called on server.");
		}
		else
		{
			((DuckRaceDuck)obj).UserCode_RpcSetRunningAnimation__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcDuckQuack()
	{
		SFXManager.SFXOneShot(duckQuackSfx, base.transform.position);
	}

	protected static void InvokeUserCode_RpcDuckQuack(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcDuckQuack called on server.");
		}
		else
		{
			((DuckRaceDuck)obj).UserCode_RpcDuckQuack();
		}
	}

	protected void UserCode_RpcStep__Single(float targetZ)
	{
		base.transform.DOLocalMoveZ(targetZ, stepTweenDuration).SetEase(Ease.Linear).WaitForCompletion();
	}

	protected static void InvokeUserCode_RpcStep__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcStep called on server.");
		}
		else
		{
			((DuckRaceDuck)obj).UserCode_RpcStep__Single(reader.ReadFloat());
		}
	}

	protected void UserCode_RpcWinFeedback()
	{
		base.transform.DOLocalJump(base.transform.localPosition, 0.25f, 3, 1f).SetEase(Ease.Linear);
		SFXManager.SFXOneShot(duckWinSfx, base.transform.position);
	}

	protected static void InvokeUserCode_RpcWinFeedback(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcWinFeedback called on server.");
		}
		else
		{
			((DuckRaceDuck)obj).UserCode_RpcWinFeedback();
		}
	}

	protected void UserCode_RpcReturn()
	{
		base.transform.DOLocalMoveZ(duckRace.startPoint.localPosition.z, 1f).SetEase(Ease.OutQuad);
	}

	protected static void InvokeUserCode_RpcReturn(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcReturn called on server.");
		}
		else
		{
			((DuckRaceDuck)obj).UserCode_RpcReturn();
		}
	}

	protected void UserCode_RpcResetDuck()
	{
		base.transform.DOKill();
		base.transform.localPosition = new Vector3(base.transform.localPosition.x, base.transform.localPosition.y, duckRace.startPoint.localPosition.z);
	}

	protected static void InvokeUserCode_RpcResetDuck(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcResetDuck called on server.");
		}
		else
		{
			((DuckRaceDuck)obj).UserCode_RpcResetDuck();
		}
	}

	static DuckRaceDuck()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(DuckRaceDuck), "System.Void DuckRaceDuck::RpcSetRunningAnimation(System.Boolean)", InvokeUserCode_RpcSetRunningAnimation__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(DuckRaceDuck), "System.Void DuckRaceDuck::RpcDuckQuack()", InvokeUserCode_RpcDuckQuack);
		RemoteProcedureCalls.RegisterRpc(typeof(DuckRaceDuck), "System.Void DuckRaceDuck::RpcStep(System.Single)", InvokeUserCode_RpcStep__Single);
		RemoteProcedureCalls.RegisterRpc(typeof(DuckRaceDuck), "System.Void DuckRaceDuck::RpcWinFeedback()", InvokeUserCode_RpcWinFeedback);
		RemoteProcedureCalls.RegisterRpc(typeof(DuckRaceDuck), "System.Void DuckRaceDuck::RpcReturn()", InvokeUserCode_RpcReturn);
		RemoteProcedureCalls.RegisterRpc(typeof(DuckRaceDuck), "System.Void DuckRaceDuck::RpcResetDuck()", InvokeUserCode_RpcResetDuck);
	}
}

}
