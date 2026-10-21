namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Diagnostics;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.InteropServices;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;

public class Minesweeper : GameBase
{
	[CompilerGenerated]
	private sealed class _003CResetAfterDelay_003Ed__21 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public Minesweeper _003C_003E4__this;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CResetAfterDelay_003Ed__21(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
		}

		private bool MoveNext()
		{
			int num = _003C_003E1__state;
			Minesweeper minesweeper = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				minesweeper._hasEnded = true;
				_003C_003E2__current = new WaitForSeconds(2f);
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				minesweeper.ResetGame();
				return false;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	[Header("References")]
	[SerializeField]
	private TextMeshPro minesCountText;

	[SerializeField]
	private TextMeshPro multiplierText;

	[SerializeField]
	private TextMeshPro potentialWinningText;

	[SerializeField]
	private List<MinesweeperTile> tiles;

	[Header("Settings")]
	[SerializeField]
	private int minMines = 1;

	[SerializeField]
	private int maxMines = 24;

	[Header("SFX")]
	[SerializeField]
	private EventReference revealGreenTileSfx;

	[SyncVar(hook = "OnMinesCountChanged")]
	private int _currentMineCount = 3;

	private HashSet<int> _mineIndexes = new HashSet<int>();

	private HashSet<int> _revealedTiles = new HashSet<int>();

	private bool _hasEnded;

	public Action<int, int> _Mirror_SyncVarHookDelegate__currentMineCount;

	public int Network_currentMineCount
	{
		get
		{
			return _currentMineCount;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _currentMineCount, 8uL, _Mirror_SyncVarHookDelegate__currentMineCount);
		}
	}

	private void OnMinesCountChanged(int oldValue, int newValue)
	{
		minesCountText.text = newValue + " Mines";
	}

	[ClientRpc]
	private void RpcSetMultiplierText(double multiplier)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteDouble(multiplier);
		SendRPCInternal("System.Void Minesweeper::RpcSetMultiplierText(System.Double)", 291016282, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetPotentialWinningText(long winning)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarLong(winning);
		SendRPCInternal("System.Void Minesweeper::RpcSetPotentialWinningText(System.Int64)", -530662363, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void IncreaseMines()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void Minesweeper::IncreaseMines()' called when server was not active");
		}
		else if (!isPlaying)
		{
			Network_currentMineCount = Mathf.Clamp(_currentMineCount + 1, minMines, maxMines);
		}
	}

	[Server]
	public void DecreaseMines()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void Minesweeper::DecreaseMines()' called when server was not active");
		}
		else if (!isPlaying)
		{
			Network_currentMineCount = Mathf.Clamp(_currentMineCount - 1, minMines, maxMines);
		}
	}

	[Server]
	protected override void StartGame()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void Minesweeper::StartGame()' called when server was not active");
			return;
		}
		base.StartGame();
		SetMines();
		foreach (MinesweeperTile tile in tiles)
		{
			tile.ServerSetButtonColor(1);
		}
	}

	[Server]
	private void SetMines()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void Minesweeper::SetMines()' called when server was not active");
			return;
		}
		int count = tiles.Count;
		Network_currentMineCount = Mathf.Clamp(_currentMineCount, 1, count - 1);
		global::System.Random seededRandom = GetSeededRandom();
		int[] array = new int[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = i;
		}
		_mineIndexes.Clear();
		for (int j = 0; j < _currentMineCount; j++)
		{
			int num = seededRandom.Next(j, count);
			ref int reference = ref array[j];
			ref int reference2 = ref array[num];
			int num2 = array[num];
			int num3 = array[j];
			reference = num2;
			reference2 = num3;
			_mineIndexes.Add(array[j]);
		}
	}

	[Server]
	public void RevealTile(MinesweeperTile tile)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void Minesweeper::RevealTile(MinesweeperTile)' called when server was not active");
		}
		else
		{
			if (!isPlaying || _hasEnded)
			{
				return;
			}
			int item = tiles.IndexOf(tile);
			if (!_revealedTiles.Add(item))
			{
				return;
			}
			if (_mineIndexes.Contains(item))
			{
				tile.ServerSetMine(isEnabled: true);
				tile.ServerSetButtonColor(0);
				tile.ServerExplode();
				Lose();
				return;
			}
			tile.ServerSetButtonColor(2);
			double num = CalculateCurrentMultiplier();
			long winning = (long)Math.Round((double)currentBet * num);
			RpcPlayRevealGreenSFX(num, tile.transform.position);
			RpcSetMultiplierText(num);
			RpcSetPotentialWinningText(winning);
			if (_revealedTiles.Count >= tiles.Count - _currentMineCount)
			{
				CashOut();
			}
		}
	}

	[Server]
	private void Lose()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void Minesweeper::Lose()' called when server was not active");
			return;
		}
		RevealAllMines();
		Payout(0.0, ChangeType.GameResult, null, -1L);
		StartCoroutine(ResetAfterDelay());
	}

	[Server]
	public void CashOut()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void Minesweeper::CashOut()' called when server was not active");
		}
		else if (isPlaying && !_hasEnded)
		{
			double multiplier = CalculateCurrentMultiplier();
			Payout(multiplier, ChangeType.GameResult, null, -1L);
			RevealAllMines();
			StartCoroutine(ResetAfterDelay());
		}
	}

	[IteratorStateMachine(typeof(_003CResetAfterDelay_003Ed__21))]
	[Server]
	private IEnumerator ResetAfterDelay()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator Minesweeper::ResetAfterDelay()' called when server was not active");
			return null;
		}
		return new _003CResetAfterDelay_003Ed__21(0)
		{
			_003C_003E4__this = this
		};
	}

	[Server]
	protected override void ResetGame()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void Minesweeper::ResetGame()' called when server was not active");
			return;
		}
		_revealedTiles.Clear();
		_mineIndexes.Clear();
		foreach (MinesweeperTile tile in tiles)
		{
			tile.ServerSetButtonColor(0);
			tile.ServerSetMine(isEnabled: false);
		}
		RpcSetMultiplierText(1.0);
		RpcSetPotentialWinningText(0L);
		_hasEnded = false;
		base.ResetGame();
	}

	private void RevealAllMines()
	{
		for (int i = 0; i < tiles.Count; i++)
		{
			tiles[i].ServerSetMine(_mineIndexes.Contains(i));
		}
	}

	[ClientRpc]
	private void RpcPlayRevealGreenSFX(double currentMultiplier, Vector3 position)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteDouble(currentMultiplier);
		writer.WriteVector3(position);
		SendRPCInternal("System.Void Minesweeper::RpcPlayRevealGreenSFX(System.Double,UnityEngine.Vector3)", -1358049268, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private double CalculateCurrentMultiplier()
	{
		if (_revealedTiles.Count == 0)
		{
			return 1.0;
		}
		int count = tiles.Count;
		double num = 1.0;
		int num2 = count - _currentMineCount;
		for (int i = 0; i < _revealedTiles.Count; i++)
		{
			double num3 = (double)(num2 - i) / (double)(count - i);
			num *= 1.0 / num3;
		}
		return num * base.EstimatedValue;
	}

	public Minesweeper()
	{
		_Mirror_SyncVarHookDelegate__currentMineCount = OnMinesCountChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetMultiplierText__Double(double multiplier)
	{
		multiplierText.text = multiplier.ToString("0.##") + "x";
	}

	protected static void InvokeUserCode_RpcSetMultiplierText__Double(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcSetMultiplierText called on server.");
		}
		else
		{
			((Minesweeper)obj).UserCode_RpcSetMultiplierText__Double(reader.ReadDouble());
		}
	}

	protected void UserCode_RpcSetPotentialWinningText__Int64(long winning)
	{
		potentialWinningText.text = "$" + winning.ToString("0");
	}

	protected static void InvokeUserCode_RpcSetPotentialWinningText__Int64(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcSetPotentialWinningText called on server.");
		}
		else
		{
			((Minesweeper)obj).UserCode_RpcSetPotentialWinningText__Int64(reader.ReadVarLong());
		}
	}

	protected void UserCode_RpcPlayRevealGreenSFX__Double__Vector3(double currentMultiplier, Vector3 position)
	{
		SFXManager.SFXOneShotWithParameters(revealGreenTileSfx, null, position, 0.7f + (float)currentMultiplier * 0.1f);
	}

	protected static void InvokeUserCode_RpcPlayRevealGreenSFX__Double__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcPlayRevealGreenSFX called on server.");
		}
		else
		{
			((Minesweeper)obj).UserCode_RpcPlayRevealGreenSFX__Double__Vector3(reader.ReadDouble(), reader.ReadVector3());
		}
	}

	static Minesweeper()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(Minesweeper), "System.Void Minesweeper::RpcSetMultiplierText(System.Double)", InvokeUserCode_RpcSetMultiplierText__Double);
		RemoteProcedureCalls.RegisterRpc(typeof(Minesweeper), "System.Void Minesweeper::RpcSetPotentialWinningText(System.Int64)", InvokeUserCode_RpcSetPotentialWinningText__Int64);
		RemoteProcedureCalls.RegisterRpc(typeof(Minesweeper), "System.Void Minesweeper::RpcPlayRevealGreenSFX(System.Double,UnityEngine.Vector3)", InvokeUserCode_RpcPlayRevealGreenSFX__Double__Vector3);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteVarInt(_currentMineCount);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 8L) != 0L)
		{
			writer.WriteVarInt(_currentMineCount);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _currentMineCount, _Mirror_SyncVarHookDelegate__currentMineCount, reader.ReadVarInt());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 8L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _currentMineCount, _Mirror_SyncVarHookDelegate__currentMineCount, reader.ReadVarInt());
		}
	}
}

}
