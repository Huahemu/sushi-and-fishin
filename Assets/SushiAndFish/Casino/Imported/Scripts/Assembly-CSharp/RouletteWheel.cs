namespace SushiAndFish.Casino.Original
{
using global::System;
using global::DG.Tweening;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class RouletteWheel : Wheel
{
	[SerializeField]
	private Transform ballWheel;

	[SerializeField]
	private Transform ball;

	[SerializeField]
	private float ballDropDuration;

	[SerializeField]
	private EventReference ballLandSfx;

	[Server]
	public override void SpinTheWheel(global::System.Random rng)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void RouletteWheel::SpinTheWheel(System.Random)' called when server was not active");
		}
		else if (!_isSpinning)
		{
			_isSpinning = true;
			float num = 9.72973f;
			float num2 = (float)rng.Next(0, 37) * num;
			float num3 = (float)rng.Next(0, 37) * num;
			float num4 = (float)minTurnAmount * 360f + num2;
			float angle = (float)minTurnAmount * 360f + num3;
			if (spinDirection)
			{
				num4 *= -1f;
			}
			RpcSpinWheel(num4, spinDuration);
			RpcSpinBallWheel(angle, spinDuration);
			StartCoroutine(WaitAndStop());
		}
	}

	[ClientRpc]
	private void RpcSpinBallWheel(float angle, float duration)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(angle);
		writer.WriteFloat(duration);
		SendRPCInternal("System.Void RouletteWheel::RpcSpinBallWheel(System.Single,System.Single)", 2086220949, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSpinBallWheel__Single__Single(float angle, float duration)
	{
		ball.DOLocalMove(new Vector3(0f, 1.25f, 0.1f), 0.2f);
		ballWheel.DOLocalRotate(new Vector3(0f, 0f, 0f - angle), duration - ballDropDuration, RotateMode.FastBeyond360).SetEase(easing).OnComplete(() =>
		{
			ball.DOLocalMove(new Vector3(0f, 0.9f, 0f), ballDropDuration).SetEase(Ease.OutBounce);
			SFXManager.SFXOneShot(ballLandSfx, base.transform.position);
		});
	}

	protected static void InvokeUserCode_RpcSpinBallWheel__Single__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSpinBallWheel called on server.");
		}
		else
		{
			((RouletteWheel)obj).UserCode_RpcSpinBallWheel__Single__Single(reader.ReadFloat(), reader.ReadFloat());
		}
	}

	static RouletteWheel()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(RouletteWheel), "System.Void RouletteWheel::RpcSpinBallWheel(System.Single,System.Single)", InvokeUserCode_RpcSpinBallWheel__Single__Single);
	}
}

}
