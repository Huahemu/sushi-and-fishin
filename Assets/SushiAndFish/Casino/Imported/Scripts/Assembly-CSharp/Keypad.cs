namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Runtime.InteropServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class Keypad : NetworkBehaviour
{
	[SerializeField]
	private UIColorPalette palette;

	[Header("References")]
	[SerializeField]
	private TextMeshPro displayText;

	[SerializeField]
	private TextMeshPro errorText;

	[SerializeField]
	private TextMeshPro minMaxBetText;

	[SerializeField]
	private ParticleSystem goldenChipParticles;

	[SerializeField]
	private MMF_Player invalidBetAmountFb;

	[SerializeField]
	private RawImage keypadRenderTarget;

	public Camera keypadCamera;

	private bool _needsRender;

	[SyncVar(hook = "OnCasinoGameSet")]
	public GameBase casinoGame;

	[SyncVar(hook = "OnInputValueChanged")]
	private string _currentInput = "";

	[SyncVar(hook = "OnErrorTextChanged")]
	private string _errorMessage = "";

	[Header("SFX")]
	[SerializeField]
	private SFXLoopComponent goldenChipSFX;

	public bool isGoldenChipApplied;

	protected NetworkBehaviourSyncVar ___casinoGameNetId;

	public Action<GameBase, GameBase> _Mirror_SyncVarHookDelegate_casinoGame;

	public Action<string, string> _Mirror_SyncVarHookDelegate__currentInput;

	public Action<string, string> _Mirror_SyncVarHookDelegate__errorMessage;

	public GameBase NetworkcasinoGame
	{
		get
		{
			return GetSyncVarNetworkBehaviour(___casinoGameNetId, ref casinoGame);
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter_NetworkBehaviour(value, ref casinoGame, 1uL, _Mirror_SyncVarHookDelegate_casinoGame, ref ___casinoGameNetId);
		}
	}

	public string Network_currentInput
	{
		get
		{
			return _currentInput;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _currentInput, 2uL, _Mirror_SyncVarHookDelegate__currentInput);
		}
	}

	public string Network_errorMessage
	{
		get
		{
			return _errorMessage;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _errorMessage, 4uL, _Mirror_SyncVarHookDelegate__errorMessage);
		}
	}

	public override void OnStartClient()
	{
		UpdateDisplay();
	}

	private void OnEnable()
	{
		palette = SushiAndFish.Casino.Original.CasinoResources.Load<UIColorPalette>("ColorSettings");
		MoneyManager instance = NetworkSingleton<MoneyManager>.Instance;
		instance.OnBalanceChanged = (Action<BalanceChangeData>)Delegate.Combine(instance.OnBalanceChanged, new Action<BalanceChangeData>(OnBalanceChanged));
	}

	private void OnDisable()
	{
		MoneyManager instance = NetworkSingleton<MoneyManager>.Instance;
		instance.OnBalanceChanged = (Action<BalanceChangeData>)Delegate.Remove(instance.OnBalanceChanged, new Action<BalanceChangeData>(OnBalanceChanged));
	}

	private void Start()
	{
		if ((bool)keypadCamera && (bool)keypadRenderTarget)
		{
			RenderTexture renderTexture = new RenderTexture(450, 600, 24);
			keypadCamera.targetTexture = renderTexture;
			keypadRenderTarget.texture = renderTexture;
			keypadCamera.enabled = false;
		}
		if ((bool)NetworkcasinoGame)
		{
			base.transform.SetParent(NetworkcasinoGame.keypadSpawnPoint, worldPositionStays: false);
			UpdateMinMaxBetText(NetworkcasinoGame.MinBet, NetworkcasinoGame.MaxBet);
		}
		if ((bool)displayText)
		{
			displayText.text = "$0";
		}
		if ((bool)errorText)
		{
			errorText.text = "";
		}
		if(CasinoRuntimeContext.Active && isServer && NetworkcasinoGame && string.IsNullOrEmpty(_currentInput)) SetBetMinimum();
		RequestRender();
	}

	private void LateUpdate()
	{
		if (base.isClient && (bool)keypadCamera && _needsRender)
		{
			_needsRender = false;
			if(CasinoRuntimeContext.Active) SushiAndFish.Casino.CasinoHostRepair.RepairKeypad(this);
			keypadCamera.Render();
		}
	}

	public void SetCasinoGame(GameBase game)
	{
		NetworkcasinoGame = game;
	}

	private void OnCasinoGameSet(GameBase oldGame, GameBase newGame)
	{
		if ((bool)newGame)
		{
			base.transform.SetParent(newGame.keypadSpawnPoint, worldPositionStays: false);
			UpdateMinMaxBetText(NetworkcasinoGame.MinBet, NetworkcasinoGame.MaxBet);
		}
	}

	private void OnInputValueChanged(string oldValue, string newValue)
	{
		UpdateDisplay();
	}

	private void OnErrorTextChanged(string oldValue, string newValue)
	{
		errorText.text = newValue;
		RequestRender();
	}

	private void OnBalanceChanged(BalanceChangeData changeData)
	{
		if (base.isServer && !isGoldenChipApplied)
		{
			ApplyInput(_currentInput, validate: true);
		}
	}

	[Server]
	public void AppendDigit(string digit)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Keypad::AppendDigit(System.String)' called when server was not active");
		}
		else
		{
			if (isGoldenChipApplied)
			{
				return;
			}
			long maxBet = NetworkcasinoGame.MaxBet;
			Network_errorMessage = "";
			int num = int.Parse(digit);
			if (!(_currentInput == "0") || num != 0)
			{
				string text = ((!(_currentInput == "0") || num <= 0) ? (_currentInput + digit) : digit);
				if (long.TryParse(text, out var result) && result > maxBet)
				{
					Network_errorMessage = "Max Bet: " + MoneyFormatter.FormatWithDollar(maxBet);
				}
				else
				{
					ApplyInput(text, validate: true);
				}
			}
		}
	}

	[Server]
	public void SetBetMinimum()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Keypad::SetBetMinimum()' called when server was not active");
		}
		else if (!isGoldenChipApplied)
		{
			ApplyInput(NetworkcasinoGame.MinBet.ToString(), validate: true);
		}
	}

	[Server]
	public void SetPercentageBet(float percentage)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Keypad::SetPercentageBet(System.Single)' called when server was not active");
		}
		else if (!isGoldenChipApplied)
		{
			string input = GetPercentageBet(percentage).ToString();
			ApplyInput(input, validate: true);
		}
	}

	[Server]
	public void ClearInput()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Keypad::ClearInput()' called when server was not active");
		}
		else if (!isGoldenChipApplied)
		{
			ApplyInput("0", validate: true);
		}
	}

	[Server]
	public void SetGoldenChip(bool apply, float multiplier = 1f)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Keypad::SetGoldenChip(System.Boolean,System.Single)' called when server was not active");
			return;
		}
		isGoldenChipApplied = apply;
		goldenChipSFX.RpcLoopSFX(apply);
		if (apply)
		{
			string input = GetPercentageBet(multiplier, limitToBalance: false).ToString();
			ApplyInput(input, validate: false);
			Network_errorMessage = "Golden Chip!";
			RpcSetDisplayColor(palette.ticketYellow);
			RpcSetGoldenChipParticles(isEnabled: true);
		}
		else
		{
			ClearInput();
			RpcSetGoldenChipParticles(isEnabled: false);
		}
	}

	[Server]
	private void ApplyInput(string input, bool validate)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Keypad::ApplyInput(System.String,System.Boolean)' called when server was not active");
			return;
		}
		Network_currentInput = input;
		long num = NetworkcasinoGame.MinBet;
		long maxBet = NetworkcasinoGame.MaxBet;
		if (NetworkcasinoGame is Roulette)
		{
			num = 1L;
		}
		if (!long.TryParse(_currentInput, out var result))
		{
			Network_errorMessage = "Invalid amount";
			return;
		}
		if (!validate)
		{
			Network_errorMessage = "";
			RpcSetDisplayColor(palette.profitGreen);
			NetworkcasinoGame.ServerSetBet(result);
			return;
		}
		if (result < num)
		{
			Network_errorMessage = "Min: " + MoneyFormatter.FormatWithDollar(num);
			RpcSetDisplayColor(palette.lossRed);
		}
		else if (result > maxBet)
		{
			Network_errorMessage = "Max: " + MoneyFormatter.FormatWithDollar(maxBet);
			RpcSetDisplayColor(palette.lossRed);
		}
		else if (result > (SushiAndFish.FriendsLife.ActorWalletRouter.IsNpcInvocation?SushiAndFish.Casino.Original.CasinoRuntimeContext.ReadCoins:NetworkSingleton<MoneyManager>.Instance.balance))
		{
			Network_errorMessage = "Not Enough Money";
			RpcSetDisplayColor(palette.lossRed);
		}
		else
		{
			Network_errorMessage = "";
			RpcSetDisplayColor(palette.profitGreen);
		}
		NetworkcasinoGame.ServerSetBet(result);
	}

	[Server]
	private long GetPercentageBet(double percentage, bool limitToBalance = true)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Int64 Keypad::GetPercentageBet(System.Double,System.Boolean)' called when server was not active");
			return default;
		}
		long balance = (SushiAndFish.FriendsLife.ActorWalletRouter.IsNpcInvocation?SushiAndFish.Casino.Original.CasinoRuntimeContext.ReadCoins:NetworkSingleton<MoneyManager>.Instance.balance);
		long num = NetworkcasinoGame.MaxBet;
		if (limitToBalance)
		{
			num = Math.Min(NetworkcasinoGame.MaxBet, balance);
		}
		return (long)Math.Round((double)num * percentage);
	}

	public long GetCurrentInput()
	{
		if (!long.TryParse(_currentInput, out var result))
		{
			return 0L;
		}
		return result;
	}

	[Server]
	public void ServerUpdateMinMaxBetText(long minBet, long maxBet)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Keypad::ServerUpdateMinMaxBetText(System.Int64,System.Int64)' called when server was not active");
		}
		else
		{
			RpcUpdateMinMaxBetText(minBet, maxBet);
		}
	}

	[ClientRpc]
	private void RpcUpdateMinMaxBetText(long minBet, long maxBet)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarLong(minBet);
		writer.WriteVarLong(maxBet);
		SendRPCInternal("System.Void Keypad::RpcUpdateMinMaxBetText(System.Int64,System.Int64)", -784711597, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void UpdateMinMaxBetText(long minBet, long maxBet)
	{
		minMaxBetText.text = "Min: " + MoneyFormatter.FormatWithDollar(minBet) + " \nMax: " + MoneyFormatter.FormatWithDollar(maxBet);
	}

	[ClientRpc]
	private void RpcSetDisplayColor(Color color)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteColor(color);
		SendRPCInternal("System.Void Keypad::RpcSetDisplayColor(UnityEngine.Color)", 827791925, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetGoldenChipParticles(bool isEnabled)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		SendRPCInternal("System.Void Keypad::RpcSetGoldenChipParticles(System.Boolean)", -154841549, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	public void RpcUpdateDisplay()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Keypad::RpcUpdateDisplay()", 1846554495, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void UpdateDisplay()
	{
		long result;
		if (string.IsNullOrEmpty(_currentInput))
		{
			displayText.text = "$0";
		}
		else if (long.TryParse(_currentInput, out result))
		{
			displayText.text = "$" + result.ToString("N0");
		}
		else
		{
			displayText.text = "$" + _currentInput;
		}
		RequestRender();
	}

	[Server]
	public void ServerInvalidBetAmountFb()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Keypad::ServerInvalidBetAmountFb()' called when server was not active");
			return;
		}
		Network_errorMessage = "Invalid bet amount";
		RpcPlayInvalidBetAmountFb();
	}

	[ClientRpc]
	private void RpcPlayInvalidBetAmountFb()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Keypad::RpcPlayInvalidBetAmountFb()", -526309948, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void RequestRender()
	{
		if (base.isClient && (bool)keypadCamera)
		{
			_needsRender = true;
		}
	}

	public Keypad()
	{
		_Mirror_SyncVarHookDelegate_casinoGame = OnCasinoGameSet;
		_Mirror_SyncVarHookDelegate__currentInput = OnInputValueChanged;
		_Mirror_SyncVarHookDelegate__errorMessage = OnErrorTextChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcUpdateMinMaxBetText__Int64__Int64(long minBet, long maxBet)
	{
		UpdateMinMaxBetText(minBet, maxBet);
		RequestRender();
	}

	protected static void InvokeUserCode_RpcUpdateMinMaxBetText__Int64__Int64(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcUpdateMinMaxBetText called on server.");
		}
		else
		{
			((Keypad)obj).UserCode_RpcUpdateMinMaxBetText__Int64__Int64(reader.ReadVarLong(), reader.ReadVarLong());
		}
	}

	protected void UserCode_RpcSetDisplayColor__Color(Color color)
	{
		displayText.color = color;
		RequestRender();
	}

	protected static void InvokeUserCode_RpcSetDisplayColor__Color(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetDisplayColor called on server.");
		}
		else
		{
			((Keypad)obj).UserCode_RpcSetDisplayColor__Color(reader.ReadColor());
		}
	}

	protected void UserCode_RpcSetGoldenChipParticles__Boolean(bool isEnabled)
	{
		if (isEnabled)
		{
			goldenChipParticles.Play();
		}
		else
		{
			goldenChipParticles.Stop();
		}
	}

	protected static void InvokeUserCode_RpcSetGoldenChipParticles__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetGoldenChipParticles called on server.");
		}
		else
		{
			((Keypad)obj).UserCode_RpcSetGoldenChipParticles__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcUpdateDisplay()
	{
		UpdateDisplay();
	}

	protected static void InvokeUserCode_RpcUpdateDisplay(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcUpdateDisplay called on server.");
		}
		else
		{
			((Keypad)obj).UserCode_RpcUpdateDisplay();
		}
	}

	protected void UserCode_RpcPlayInvalidBetAmountFb()
	{
		invalidBetAmountFb.PlayFeedbacks();
		RequestRender();
	}

	protected static void InvokeUserCode_RpcPlayInvalidBetAmountFb(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayInvalidBetAmountFb called on server.");
		}
		else
		{
			((Keypad)obj).UserCode_RpcPlayInvalidBetAmountFb();
		}
	}

	static Keypad()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(Keypad), "System.Void Keypad::RpcUpdateMinMaxBetText(System.Int64,System.Int64)", InvokeUserCode_RpcUpdateMinMaxBetText__Int64__Int64);
		RemoteProcedureCalls.RegisterRpc(typeof(Keypad), "System.Void Keypad::RpcSetDisplayColor(UnityEngine.Color)", InvokeUserCode_RpcSetDisplayColor__Color);
		RemoteProcedureCalls.RegisterRpc(typeof(Keypad), "System.Void Keypad::RpcSetGoldenChipParticles(System.Boolean)", InvokeUserCode_RpcSetGoldenChipParticles__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(Keypad), "System.Void Keypad::RpcUpdateDisplay()", InvokeUserCode_RpcUpdateDisplay);
		RemoteProcedureCalls.RegisterRpc(typeof(Keypad), "System.Void Keypad::RpcPlayInvalidBetAmountFb()", InvokeUserCode_RpcPlayInvalidBetAmountFb);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteNetworkBehaviour(NetworkcasinoGame);
			writer.WriteString(_currentInput);
			writer.WriteString(_errorMessage);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteNetworkBehaviour(NetworkcasinoGame);
		}
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteString(_currentInput);
		}
		if ((syncVarDirtyBits & 4L) != 0L)
		{
			writer.WriteString(_errorMessage);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize_NetworkBehaviour(ref casinoGame, _Mirror_SyncVarHookDelegate_casinoGame, reader, ref ___casinoGameNetId);
			GeneratedSyncVarDeserialize(ref _currentInput, _Mirror_SyncVarHookDelegate__currentInput, reader.ReadString());
			GeneratedSyncVarDeserialize(ref _errorMessage, _Mirror_SyncVarHookDelegate__errorMessage, reader.ReadString());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize_NetworkBehaviour(ref casinoGame, _Mirror_SyncVarHookDelegate_casinoGame, reader, ref ___casinoGameNetId);
		}
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _currentInput, _Mirror_SyncVarHookDelegate__currentInput, reader.ReadString());
		}
		if ((num & 4L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _errorMessage, _Mirror_SyncVarHookDelegate__errorMessage, reader.ReadString());
		}
	}
}

}
