namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Collections.Generic;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::TMPro;
using global::UnityEngine;

public class Craps : GameBase
{
	[Header("References")]
	[SerializeField]
	private DiceManager diceManager;

	[SerializeField]
	private TextMeshPro winConditionText;

	[SerializeField]
	private TextMeshPro loseConditionText;

	[SerializeField]
	private TextMeshPro multiplierText;

	[SerializeField]
	private TextMeshPro potentialWinText;

	[SerializeField]
	private MMF_Player diceRollFeedback;

	[Header("Point Phase Rules")]
	[SerializeField]
	private int maxPointPhaseThrows = 5;

	private int _pointValue;

	private bool _isPointPhase;

	private int _lastRolledValue;

	private int _pointPhaseThrows;

	private void OnEnable()
	{
		diceManager.OnDiceRolled += HandleDiceRolled;
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		diceManager.OnDiceRolled -= HandleDiceRolled;
	}

	[Server]
	protected override void StartGame()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Craps::StartGame()' called when server was not active");
			return;
		}
		diceManager.ResetRound();
		RpcSetMultiplierAndWinningText("2x", MoneyFormatter.FormatWithDollar(currentBet * 2));
	}

	private void HandleDiceRolled(int result)
	{
		if (!isPlaying || !base.isServer)
		{
			return;
		}
		_lastRolledValue = result;
		Gradient gradient = new Gradient();
		GradientColorKey[] array = new GradientColorKey[2];
		GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2]
		{
			new GradientAlphaKey(1f, 0f),
			new GradientAlphaKey(1f, 1f)
		};
		if (!_isPointPhase)
		{
			switch (result)
			{
			case 7:
			case 11:
				array[0] = new GradientColorKey(new Color(0.2f, 0.6f, 0.2f), 0f);
				array[1] = new GradientColorKey(new Color(0.4f, 0.8f, 0.2f), 1f);
				Win(2.0);
				break;
			case 2:
			case 3:
			case 12:
				array[0] = new GradientColorKey(new Color(0.7f, 0.2f, 0.1f), 0f);
				array[1] = new GradientColorKey(new Color(0.9f, 0.4f, 0.1f), 1f);
				Lose();
				break;
			default:
				array[0] = new GradientColorKey(new Color(0.7f, 0.8f, 0.2f), 0f);
				array[1] = new GradientColorKey(new Color(0.9f, 0.8f, 0.2f), 1f);
				_isPointPhase = true;
				_pointValue = result;
				_pointPhaseThrows = 0;
				diceManager.ResetRound();
				RpcSetWinConditionText(_pointValue.ToString());
				RpcSetLoseConditionText("7");
				RpcSetMultiplierAndWinningText("4x", MoneyFormatter.FormatWithDollar(currentBet * 4));
				break;
			}
		}
		else
		{
			_pointPhaseThrows++;
			if (result == _pointValue)
			{
				array[0] = new GradientColorKey(new Color(0.2f, 0.6f, 0.2f), 0f);
				array[1] = new GradientColorKey(new Color(0.4f, 0.8f, 0.2f), 1f);
				Win(4.0);
			}
			else if (result == 7)
			{
				array[0] = new GradientColorKey(new Color(0.7f, 0.2f, 0.1f), 0f);
				array[1] = new GradientColorKey(new Color(0.9f, 0.4f, 0.1f), 1f);
				Lose();
			}
			else if (_pointPhaseThrows >= maxPointPhaseThrows)
			{
				array[0] = new GradientColorKey(new Color(0.7f, 0.8f, 0.2f), 0f);
				array[1] = new GradientColorKey(new Color(0.9f, 0.8f, 0.2f), 1f);
				RpcDiceRolledFeedback(gradient, "REFUND");
				PushRefund();
			}
			else
			{
				array[0] = new GradientColorKey(new Color(0.2f, 0.5f, 0.8f), 0f);
				array[1] = new GradientColorKey(new Color(0.2f, 0.7f, 0.9f), 1f);
				diceManager.ResetRound();
			}
		}
		gradient.SetKeys(array, alphaKeys);
		RpcDiceRolledFeedback(gradient, result.ToString());
	}

	[ClientRpc]
	private void RpcDiceRolledFeedback(Gradient gradient, string result)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_UnityEngine_002EGradient(writer, gradient);
		writer.WriteString(result);
		SendRPCInternal("System.Void Craps::RpcDiceRolledFeedback(UnityEngine.Gradient,System.String)", 868177736, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void Win(double multiplier)
	{
		Dictionary<string, object> gameSpecificData = new Dictionary<string, object>
		{
			{ "lastDiceRoll", _lastRolledValue },
			{ "pointValue", _pointValue }
		};
		Payout(multiplier * base.EstimatedValue, ChangeType.GameResult, gameSpecificData, -1L);
		StartCoroutine(ResetGameRoutine());
	}

	private void Lose()
	{
		Dictionary<string, object> gameSpecificData = new Dictionary<string, object>
		{
			{ "lastDiceRoll", _lastRolledValue },
			{ "pointValue", _pointValue }
		};
		Payout(0.0, ChangeType.GameResult, gameSpecificData, -1L);
		StartCoroutine(ResetGameRoutine());
	}

	private void PushRefund()
	{
		Dictionary<string, object> gameSpecificData = new Dictionary<string, object>
		{
			{ "lastDiceRoll", _lastRolledValue },
			{ "pointValue", _pointValue }
		};
		Payout(1.0, ChangeType.GameResult, gameSpecificData, -1L);
		StartCoroutine(ResetGameRoutine());
	}

	private IEnumerator ResetGameRoutine()
	{
		yield return new WaitForSeconds(1f);
		ResetGame();
	}

	protected override void ResetGame()
	{
		diceManager.LockDices(isLocked: true);
		_isPointPhase = false;
		_pointValue = 0;
		_pointPhaseThrows = 0;
		RpcSetLoseConditionText("2 / 3 / 12");
		RpcSetWinConditionText("7 / 11");
		RpcSetMultiplierAndWinningText("", "");
		base.ResetGame();
	}

	[ClientRpc]
	private void RpcSetLoseConditionText(string text)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(text);
		SendRPCInternal("System.Void Craps::RpcSetLoseConditionText(System.String)", 1778590780, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetWinConditionText(string text)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(text);
		SendRPCInternal("System.Void Craps::RpcSetWinConditionText(System.String)", -1225143097, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetMultiplierAndWinningText(string multText, string winText)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(multText);
		writer.WriteString(winText);
		SendRPCInternal("System.Void Craps::RpcSetMultiplierAndWinningText(System.String,System.String)", 1435110022, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcDiceRolledFeedback__Gradient__String(Gradient gradient, string result)
	{
		MMF_FloatingText feedbackOfType = diceRollFeedback.GetFeedbackOfType<MMF_FloatingText>();
		feedbackOfType.Value = result;
		feedbackOfType.AnimateColorGradient = gradient;
		diceRollFeedback.PlayFeedbacks();
	}

	protected static void InvokeUserCode_RpcDiceRolledFeedback__Gradient__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcDiceRolledFeedback called on server.");
		}
		else
		{
			((Craps)obj).UserCode_RpcDiceRolledFeedback__Gradient__String(GeneratedNetworkCode._Read_UnityEngine_002EGradient(reader), reader.ReadString());
		}
	}

	protected void UserCode_RpcSetLoseConditionText__String(string text)
	{
		loseConditionText.text = text;
	}

	protected static void InvokeUserCode_RpcSetLoseConditionText__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetLoseConditionText called on server.");
		}
		else
		{
			((Craps)obj).UserCode_RpcSetLoseConditionText__String(reader.ReadString());
		}
	}

	protected void UserCode_RpcSetWinConditionText__String(string text)
	{
		winConditionText.text = text;
	}

	protected static void InvokeUserCode_RpcSetWinConditionText__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetWinConditionText called on server.");
		}
		else
		{
			((Craps)obj).UserCode_RpcSetWinConditionText__String(reader.ReadString());
		}
	}

	protected void UserCode_RpcSetMultiplierAndWinningText__String__String(string multText, string winText)
	{
		multiplierText.text = multText;
		potentialWinText.text = winText;
	}

	protected static void InvokeUserCode_RpcSetMultiplierAndWinningText__String__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetMultiplierAndWinningText called on server.");
		}
		else
		{
			((Craps)obj).UserCode_RpcSetMultiplierAndWinningText__String__String(reader.ReadString(), reader.ReadString());
		}
	}

	static Craps()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(Craps), "System.Void Craps::RpcDiceRolledFeedback(UnityEngine.Gradient,System.String)", InvokeUserCode_RpcDiceRolledFeedback__Gradient__String);
		RemoteProcedureCalls.RegisterRpc(typeof(Craps), "System.Void Craps::RpcSetLoseConditionText(System.String)", InvokeUserCode_RpcSetLoseConditionText__String);
		RemoteProcedureCalls.RegisterRpc(typeof(Craps), "System.Void Craps::RpcSetWinConditionText(System.String)", InvokeUserCode_RpcSetWinConditionText__String);
		RemoteProcedureCalls.RegisterRpc(typeof(Craps), "System.Void Craps::RpcSetMultiplierAndWinningText(System.String,System.String)", InvokeUserCode_RpcSetMultiplierAndWinningText__String__String);
	}
}

}
