namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class Plinko : GameBase
{
	[Header("References")]
	[SerializeField]
	private PlinkoPuck puckPrefab;

	[SerializeField]
	private Transform spawnPosition;

	[SerializeField]
	private List<PlinkoPillar> pillars;

	[Header("Settings")]
	[SerializeField]
	private double[] slotMultipliers = new double[9] { 0.2, 0.5, 1.0, 2.0, 5.0, 2.0, 1.0, 0.5, 0.2 };

	[SerializeField]
	private float cooldown;

	private float _lastSpawnTime;

	private global::System.Random _rng;

	protected override void OnAwake()
	{
		base.OnAwake();
		_rng = GetSeededRandom();
	}

	protected override bool CanGameStart()
	{
		if (Time.time - _lastSpawnTime < cooldown)
		{
			return false;
		}
		_lastSpawnTime = Time.time;
		return true;
	}

	[Server]
	protected override void StartGame()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Plinko::StartGame()' called when server was not active");
			return;
		}
		base.StartGame();
		DropPuck();
	}

	[Server]
	private void DropPuck()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Plinko::DropPuck()' called when server was not active");
			return;
		}
		Vector3 position = spawnPosition.position;
		float num = ((_rng.NextDouble() < 0.5) ? (-0.1f + (float)_rng.NextDouble() * 0.095f) : (0.005f + (float)_rng.NextDouble() * 0.095f));
		position.x += num;
		position.z += -0.1f + (float)_rng.NextDouble() * 0.2f;
		PlinkoPuck plinkoPuck = UnityEngine.Object.Instantiate(puckPrefab, position, spawnPosition.rotation);
		plinkoPuck.Initialize(currentBet);
		NetworkServer.Spawn(plinkoPuck.gameObject);
		ResetGame();
	}

	[Server]
	public void OnPuckEnteredPocket(int slotIndex, PlinkoPuck puck)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Plinko::OnPuckEnteredPocket(System.Int32,PlinkoPuck)' called when server was not active");
			return;
		}
		double num = 0.0;
		if (slotMultipliers != null && slotIndex >= 0 && slotIndex < slotMultipliers.Length)
		{
			num = slotMultipliers[slotIndex];
		}
		Payout(num * base.EstimatedValue, ChangeType.GameResult, null, puck.betAmount);
	}

	public void ServerPlayPillarFeedbacks(PlinkoPillar pillar)
	{
		if (base.isServer)
		{
			int index = pillars.IndexOf(pillar);
			RpcPlayPillarFeedbacks(index);
		}
	}

	[ClientRpc]
	private void RpcPlayPillarFeedbacks(int index)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(index);
		SendRPCInternal("System.Void Plinko::RpcPlayPillarFeedbacks(System.Int32)", 1965725964, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcPlayPillarFeedbacks__Int32(int index)
	{
		pillars[index].PlayFeedbacks();
	}

	protected static void InvokeUserCode_RpcPlayPillarFeedbacks__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayPillarFeedbacks called on server.");
		}
		else
		{
			((Plinko)obj).UserCode_RpcPlayPillarFeedbacks__Int32(reader.ReadVarInt());
		}
	}

	static Plinko()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(Plinko), "System.Void Plinko::RpcPlayPillarFeedbacks(System.Int32)", InvokeUserCode_RpcPlayPillarFeedbacks__Int32);
	}
}

}
