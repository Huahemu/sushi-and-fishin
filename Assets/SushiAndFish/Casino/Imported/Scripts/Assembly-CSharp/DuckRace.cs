namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;
using global::DG.Tweening;
using global::FMOD.Studio;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class DuckRace : GameBase
{
	[Header("References")]
	public Transform startPoint;

	public Transform endPoint;

	[SerializeField]
	private Transform crown;

	[SerializeField]
	private List<DuckRaceDuck> ducks;

	[SerializeField]
	private List<Renderer> selectedDuckLights;

	[Header("SFX")]
	[SerializeField]
	private EventReference duckMusic;

	private EventInstance _duckMusicInstance;

	private List<MaterialPropertyBlock> _sdmpbs = new List<MaterialPropertyBlock>();

	public bool hasEnded;

	private int _placedBetIndex;

	[SyncVar]
	[SerializeField]
	private int _winningDuckIndex = -1;

	public int Network_winningDuckIndex
	{
		get
		{
			return _winningDuckIndex;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _winningDuckIndex, 8uL, null);
		}
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		foreach (Renderer selectedDuckLight in selectedDuckLights)
		{
			_ = selectedDuckLight;
			_sdmpbs.Add(new MaterialPropertyBlock());
		}
		PlaceBetFeedback(0);
	}

	[Command(requiresAuthority = false)]
	public void ServerPlaceBet(int duckIndex)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(duckIndex);
		SendCommandInternal("System.Void DuckRace::ServerPlaceBet(System.Int32)", 1419084891, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	protected override void StartGame()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void DuckRace::StartGame()' called when server was not active");
			return;
		}
		base.StartGame();
		for (int i = 0; i < ducks.Count; i++)
		{
			ducks[i].ServerStartRace(GetSeededRandom(i * 999));
		}
		RpcSetCrown(active: true);
		RpcDuckMusic(play: true);
	}

	private void Update()
	{
		if (_winningDuckIndex >= 0)
		{
			SetCrownPosition();
		}
		if ((!isPlaying && hasEnded) || !base.isServer)
		{
			return;
		}
		for (int i = 0; i < ducks.Count; i++)
		{
			if (_winningDuckIndex < 0 || ducks[i].transform.localPosition.z > ducks[_winningDuckIndex].transform.localPosition.z)
			{
				Network_winningDuckIndex = i;
			}
		}
	}

	[Server]
	public bool OnDuckFinish(DuckRaceDuck duck)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Boolean DuckRace::OnDuckFinish(DuckRaceDuck)' called when server was not active");
			return default;
		}
		if (!isPlaying)
		{
			return false;
		}
		if (hasEnded)
		{
			return false;
		}
		hasEnded = true;
		Network_winningDuckIndex = ducks.IndexOf(duck);
		double multiplier = ((_placedBetIndex == _winningDuckIndex) ? (4.0 * base.EstimatedValue) : 0.0);
		Dictionary<string, object> gameSpecificData = new Dictionary<string, object>
		{
			{ "winningDuckIndex", _winningDuckIndex },
			{ "betDuckIndex", _placedBetIndex }
		};
		Payout(multiplier, ChangeType.GameResult, gameSpecificData, -1L);
		RpcDuckMusic(play: false);
		StartCoroutine(ResetGameRoutine());
		return true;
	}

	private IEnumerator ResetGameRoutine()
	{
		yield return new WaitForSeconds(1f);
		foreach (DuckRaceDuck duck in ducks)
		{
			duck.ServerReturn();
		}
		yield return new WaitForSeconds(0.7f);
		RpcSetCrown(active: false);
		yield return new WaitForSeconds(0.3f);
		ResetGame();
	}

	protected override void ResetGame()
	{
		base.ResetGame();
		Network_winningDuckIndex = -1;
		hasEnded = false;
		RpcSetCrown(active: false);
		foreach (DuckRaceDuck duck in ducks)
		{
			duck.ResetDuck();
		}
	}

	[ClientRpc]
	private void RpcPlaceBetFeedback(int duckIndex)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(duckIndex);
		SendRPCInternal("System.Void DuckRace::RpcPlaceBetFeedback(System.Int32)", 638818872, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void PlaceBetFeedback(int duckIndex)
	{
		int num = duckIndex * 2;
		int num2 = num + 3;
		for (int i = 0; i < selectedDuckLights.Count; i++)
		{
			Renderer renderer = selectedDuckLights[i];
			MaterialPropertyBlock materialPropertyBlock = _sdmpbs[i];
			renderer.GetPropertyBlock(materialPropertyBlock, 1);
			if (i >= num && i <= num2)
			{
				materialPropertyBlock.SetColor("_EmissionColor", Color.greenYellow * 3f);
			}
			else
			{
				materialPropertyBlock.SetColor("_EmissionColor", Color.black);
			}
			renderer.SetPropertyBlock(materialPropertyBlock, 1);
		}
	}

	[ClientRpc]
	private void RpcSetCrown(bool active)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(active);
		SendRPCInternal("System.Void DuckRace::RpcSetCrown(System.Boolean)", -820960290, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void SetCrownPosition()
	{
		crown.position = Vector3.Lerp(crown.position, ducks[_winningDuckIndex].transform.position, 20f * Time.deltaTime);
	}

	[ClientRpc]
	private void RpcDuckMusic(bool play)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(play);
		SendRPCInternal("System.Void DuckRace::RpcDuckMusic(System.Boolean)", -923557929, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_ServerPlaceBet__Int32(int duckIndex)
	{
		if (!isPlaying)
		{
			_placedBetIndex = duckIndex;
			RpcPlaceBetFeedback(duckIndex);
		}
	}

	protected static void InvokeUserCode_ServerPlaceBet__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command ServerPlaceBet called on client.");
		}
		else
		{
			((DuckRace)obj).UserCode_ServerPlaceBet__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcPlaceBetFeedback__Int32(int duckIndex)
	{
		PlaceBetFeedback(duckIndex);
	}

	protected static void InvokeUserCode_RpcPlaceBetFeedback__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlaceBetFeedback called on server.");
		}
		else
		{
			((DuckRace)obj).UserCode_RpcPlaceBetFeedback__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcSetCrown__Boolean(bool active)
	{
		crown.DOScale(active ? Vector3.one : Vector3.zero, 0.3f).SetEase(Ease.InBack);
	}

	protected static void InvokeUserCode_RpcSetCrown__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetCrown called on server.");
		}
		else
		{
			((DuckRace)obj).UserCode_RpcSetCrown__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcDuckMusic__Boolean(bool play)
	{
		if (play)
		{
			_duckMusicInstance = RuntimeManager.CreateInstance(duckMusic);
			_duckMusicInstance.set3DAttributes(base.transform.position.To3DAttributes());
			_duckMusicInstance.start();
		}
		else
		{
			_duckMusicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			_duckMusicInstance.release();
		}
	}

	protected static void InvokeUserCode_RpcDuckMusic__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcDuckMusic called on server.");
		}
		else
		{
			((DuckRace)obj).UserCode_RpcDuckMusic__Boolean(reader.ReadBool());
		}
	}

	static DuckRace()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(DuckRace), "System.Void DuckRace::ServerPlaceBet(System.Int32)", InvokeUserCode_ServerPlaceBet__Int32, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(DuckRace), "System.Void DuckRace::RpcPlaceBetFeedback(System.Int32)", InvokeUserCode_RpcPlaceBetFeedback__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(DuckRace), "System.Void DuckRace::RpcSetCrown(System.Boolean)", InvokeUserCode_RpcSetCrown__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(DuckRace), "System.Void DuckRace::RpcDuckMusic(System.Boolean)", InvokeUserCode_RpcDuckMusic__Boolean);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteVarInt(_winningDuckIndex);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 8L) != 0L)
		{
			writer.WriteVarInt(_winningDuckIndex);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _winningDuckIndex, null, reader.ReadVarInt());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 8L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _winningDuckIndex, null, reader.ReadVarInt());
		}
	}
}

}
