namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Linq;
using global::System.Runtime.InteropServices;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;

public class Keno : GameBase
{
	[Header("Game Settings")]
	[SerializeField]
	private float revealDelay = 0.1f;

	[SerializeField]
	private int diamondCount = 10;

	[SerializeField]
	private int maxSelectionCount = 10;

	[SerializeField]
	private float riskRewardRatio = 2f;

	[Header("References")]
	[SerializeField]
	private TextMeshPro multiplierText;

	[SerializeField]
	private TextMeshPro potentialWinningText;

	[SerializeField]
	private Transform buttonsParent;

	private KenoButton[] _buttons;

	[Header("SFX")]
	[SerializeField]
	private EventReference sfxWarning;

	[SerializeField]
	private EventReference sfxOnHit;

	[SerializeField]
	private EventReference sfxOnMiss;

	[SyncVar(hook = "OnMultiplierChanged")]
	private double _currentMultiplier;

	private string _playerName;

	private List<KenoButton> _selectedButtons = new List<KenoButton>();

	public Action<double, double> _Mirror_SyncVarHookDelegate__currentMultiplier;

	private KenoButton[] Buttons
	{
		get
		{
			if (_buttons == null || _buttons.Length == 0)
			{
				_buttons = buttonsParent.GetComponentsInChildren<KenoButton>();
			}
			return _buttons;
		}
	}

	public double Network_currentMultiplier
	{
		get
		{
			return _currentMultiplier;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _currentMultiplier, 8uL, _Mirror_SyncVarHookDelegate__currentMultiplier);
		}
	}

	private void OnMultiplierChanged(double oldMult, double newMult)
	{
		multiplierText.text = newMult.ToString("0.##") + "x";
		potentialWinningText.text = "$" + Math.Round((double)currentBet * newMult).ToString("N0");
	}

	protected override bool CanGameStart()
	{
		if (_selectedButtons.Count <= 0)
		{
			KenoButton[] buttons = Buttons;
			for (int i = 0; i < buttons.Length; i++)
			{
				buttons[i].ServerWarningFeedback();
			}
			RpcWarningFeedback();
			return false;
		}
		return true;
	}

	[Server]
	protected override void StartGame()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Keno::StartGame()' called when server was not active");
			return;
		}
		base.StartGame();
		StartCoroutine(GameRoutine());
	}

	private IEnumerator GameRoutine()
	{
		List<int> selectedNumbers = FathF.GetUniqueRandomNumbers(diamondCount, 0, Buttons.Length - 1, shuffle: true);
		int hitCount = 0;
		for (int i = 0; i < selectedNumbers.Count; i++)
		{
			KenoButton kenoButton = Buttons[selectedNumbers[i]];
			kenoButton.ServerRevealDiamond(isTrue: true);
			if (_selectedButtons.Contains(kenoButton))
			{
				hitCount++;
				Network_currentMultiplier = GetMultiplier(hitCount);
				RpcDiamondRevealFeedback(isHit: true, kenoButton.transform.position, i);
			}
			else
			{
				RpcDiamondRevealFeedback(isHit: false, kenoButton.transform.position, i);
			}
			yield return new WaitForSeconds(revealDelay);
		}
		EndGame();
	}

	private void EndGame()
	{
		Payout(_currentMultiplier, ChangeType.GameResult, null, -1L);
		StartCoroutine(ResetGameRoutine());
	}

	private IEnumerator ResetGameRoutine()
	{
		yield return new WaitForSeconds(1f);
		ResetGame();
	}

	protected override void ResetGame()
	{
		base.ResetGame();
		KenoButton[] buttons = Buttons;
		for (int i = 0; i < buttons.Length; i++)
		{
			buttons[i].ServerRevealDiamond(isTrue: false);
		}
		Network_currentMultiplier = 0.0;
	}

	private double GetMultiplier(int hitCount)
	{
		int n = Buttons.Length;
		int count = _selectedButtons.Count;
		int m = diamondCount;
		return GetBalancedMultipliers(n, count, m)[hitCount] * base.EstimatedValue;
	}

	private Dictionary<int, double> GetBalancedMultipliers(int N, int k, int m)
	{
		Dictionary<int, double> dictionary = new Dictionary<int, double>();
		double num = 0.0;
		for (int i = 0; i <= Math.Min(k, m); i++)
		{
			double num2 = (dictionary[i] = Hypergeometric(N, m, k, i));
			num += num2;
		}
		Dictionary<int, float> rawMultipliers = dictionary.ToDictionary((KeyValuePair<int, double> kv) => kv.Key, (KeyValuePair<int, double> kv) => (float)Math.Pow(kv.Key, riskRewardRatio));
		rawMultipliers[0] = 0f;
		double num4 = dictionary.Sum((KeyValuePair<int, double> kv) => kv.Value * (double)rawMultipliers[kv.Key]);
		double scale = 1.0 / num4;
		return rawMultipliers.ToDictionary((KeyValuePair<int, float> kv) => kv.Key, (KeyValuePair<int, float> kv) => (double)kv.Value * scale);
	}

	private double Hypergeometric(int N, int m, int k, int x)
	{
		return Binomial(m, x) * Binomial(N - m, k - x) / Binomial(N, k);
	}

	private double Binomial(int n, int k)
	{
		if (k < 0 || k > n)
		{
			return 0.0;
		}
		if (k == 0 || k == n)
		{
			return 1.0;
		}
		double num = 1.0;
		for (int i = 1; i <= k; i++)
		{
			num *= (double)(n - (k - i)) / (double)i;
		}
		return num;
	}

	public bool SelectButton(KenoButton button)
	{
		if (_selectedButtons.Contains(button))
		{
			_selectedButtons.Remove(button);
			return false;
		}
		if (_selectedButtons.Count >= maxSelectionCount)
		{
			return false;
		}
		_selectedButtons.Add(button);
		return true;
	}

	[ClientRpc]
	private void RpcDiamondRevealFeedback(bool isHit, Vector3 position, int idx)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isHit);
		writer.WriteVector3(position);
		writer.WriteVarInt(idx);
		SendRPCInternal("System.Void Keno::RpcDiamondRevealFeedback(System.Boolean,UnityEngine.Vector3,System.Int32)", -94606144, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcWarningFeedback()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Keno::RpcWarningFeedback()", 189043358, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public Keno()
	{
		_Mirror_SyncVarHookDelegate__currentMultiplier = OnMultiplierChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcDiamondRevealFeedback__Boolean__Vector3__Int32(bool isHit, Vector3 position, int idx)
	{
		float num = (float)idx / (float)Buttons.Length * 3f;
		if (isHit)
		{
			SFXManager.SFXOneShotWithParameters(sfxOnHit, null, position, 0.7f + num);
		}
		else
		{
			SFXManager.SFXOneShotWithParameters(sfxOnMiss, null, position, 0.7f + num);
		}
	}

	protected static void InvokeUserCode_RpcDiamondRevealFeedback__Boolean__Vector3__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcDiamondRevealFeedback called on server.");
		}
		else
		{
			((Keno)obj).UserCode_RpcDiamondRevealFeedback__Boolean__Vector3__Int32(reader.ReadBool(), reader.ReadVector3(), reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcWarningFeedback()
	{
		SFXManager.SFXOneShot(sfxWarning, base.transform.position);
	}

	protected static void InvokeUserCode_RpcWarningFeedback(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcWarningFeedback called on server.");
		}
		else
		{
			((Keno)obj).UserCode_RpcWarningFeedback();
		}
	}

	static Keno()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(Keno), "System.Void Keno::RpcDiamondRevealFeedback(System.Boolean,UnityEngine.Vector3,System.Int32)", InvokeUserCode_RpcDiamondRevealFeedback__Boolean__Vector3__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(Keno), "System.Void Keno::RpcWarningFeedback()", InvokeUserCode_RpcWarningFeedback);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteDouble(_currentMultiplier);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 8L) != 0L)
		{
			writer.WriteDouble(_currentMultiplier);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _currentMultiplier, _Mirror_SyncVarHookDelegate__currentMultiplier, reader.ReadDouble());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 8L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _currentMultiplier, _Mirror_SyncVarHookDelegate__currentMultiplier, reader.ReadDouble());
		}
	}
}

}
