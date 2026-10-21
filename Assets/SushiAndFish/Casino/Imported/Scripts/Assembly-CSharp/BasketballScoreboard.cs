namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Runtime.InteropServices;
using global::Mirror;
using global::MoreMountains.Feedbacks;
using global::TMPro;
using global::UnityEngine;

public class BasketballScoreboard : NetworkBehaviour
{
	[Header("References")]
	[SerializeField]
	private TextMeshPro scoreTextA;

	[SerializeField]
	private TextMeshPro scoreTextB;

	[SerializeField]
	private MMF_Player teamAScoreFb;

	[SerializeField]
	private MMF_Player teamBScoreFb;

	[Header("Ball")]
	[SerializeField]
	private Basketball basketballPrefab;

	[SerializeField]
	private Basketball basketball;

	[SerializeField]
	private Transform spawnPos;

	[SerializeField]
	private Transform initialSpawnPos;

	[SyncVar(hook = "OnScoreAChanged")]
	private int _scoreA;

	[SyncVar(hook = "OnScoreBChanged")]
	private int _scoreB;

	public Action<int, int> _Mirror_SyncVarHookDelegate__scoreA;

	public Action<int, int> _Mirror_SyncVarHookDelegate__scoreB;

	public int Network_scoreA
	{
		get
		{
			return _scoreA;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _scoreA, 1uL, _Mirror_SyncVarHookDelegate__scoreA);
		}
	}

	public int Network_scoreB
	{
		get
		{
			return _scoreB;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _scoreB, 2uL, _Mirror_SyncVarHookDelegate__scoreB);
		}
	}

	public override void OnStartServer()
	{
		base.OnStartServer();
		basketball = UnityEngine.Object.Instantiate(basketballPrefab, initialSpawnPos.position, initialSpawnPos.rotation);
		NetworkServer.Spawn(basketball.gameObject);
	}

	private void OnScoreAChanged(int oldScore, int newScore)
	{
		scoreTextA.text = newScore.ToString();
	}

	private void OnScoreBChanged(int oldScore, int newScore)
	{
		scoreTextB.text = newScore.ToString();
	}

	[Server]
	public void RetrieveBasketball()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BasketballScoreboard::RetrieveBasketball()' called when server was not active");
			return;
		}
		basketball.DestroyItem();
		basketball = UnityEngine.Object.Instantiate(basketballPrefab, spawnPos.position, spawnPos.rotation);
		NetworkServer.Spawn(basketball.gameObject);
	}

	[Server]
	public void IncreaseScore(bool isTeamA)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BasketballScoreboard::IncreaseScore(System.Boolean)' called when server was not active");
		}
		else if (isTeamA)
		{
			Network_scoreA = _scoreA + 1;
			teamAScoreFb.PlayFeedbacks();
		}
		else
		{
			Network_scoreB = _scoreB + 1;
			teamBScoreFb.PlayFeedbacks();
		}
	}

	[Server]
	public void ResetScores()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BasketballScoreboard::ResetScores()' called when server was not active");
			return;
		}
		Network_scoreA = 0;
		Network_scoreB = 0;
	}

	public BasketballScoreboard()
	{
		_Mirror_SyncVarHookDelegate__scoreA = OnScoreAChanged;
		_Mirror_SyncVarHookDelegate__scoreB = OnScoreBChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteVarInt(_scoreA);
			writer.WriteVarInt(_scoreB);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteVarInt(_scoreA);
		}
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteVarInt(_scoreB);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _scoreA, _Mirror_SyncVarHookDelegate__scoreA, reader.ReadVarInt());
			GeneratedSyncVarDeserialize(ref _scoreB, _Mirror_SyncVarHookDelegate__scoreB, reader.ReadVarInt());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _scoreA, _Mirror_SyncVarHookDelegate__scoreA, reader.ReadVarInt());
		}
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _scoreB, _Mirror_SyncVarHookDelegate__scoreB, reader.ReadVarInt());
		}
	}
}

}
