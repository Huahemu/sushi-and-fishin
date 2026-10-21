namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Linq;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;

public class Roulette : GameBase
{
	[Header("References")]
	[SerializeField]
	private Wheel wheel;

	[SerializeField]
	private RouletteButton[] buttons;

	[SerializeField]
	private TextMeshPro totalBetText;

	private Dictionary<string, long> _bets = new Dictionary<string, long>();

	private string _goldenBetOption;

	private static readonly int[] RedNumbers;

	private static readonly int[] BlackNumbers;

	private static readonly int[] Column1;

	private static readonly int[] Column2;

	private static readonly int[] Column3;

	private void OnEnable()
	{
		wheel.OnWheelStopped += HandleWheelStopped;
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		wheel.OnWheelStopped -= HandleWheelStopped;
	}

	[Server]
	public override void TryStartGame(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Roulette::TryStartGame(PlayerInteract)' called when server was not active");
		}
		else
		{
			if (!CanGameStart() || isPlaying)
			{
				return;
			}
			if (playerInteract.TryGetComponent<PlayerProfile>(out var component))
			{
				interactingPlayer = component;
			}
			long totalBet = GetTotalBet();
			if (totalBet < base.MinBet)
			{
				RouletteButton[] array = buttons;
				for (int i = 0; i < array.Length; i++)
				{
					array[i].ServerWarningFeedback();
				}
				return;
			}
			if (!isGoldenChipApplied)
			{
				if (!NetworkSingleton<MoneyManager>.Instance.TryChangeBalance(-totalBet, interactingPlayer, ChangeType.Bet))
				{
					keypad.ServerInvalidBetAmountFb();
					return;
				}
			}
			else
			{
				isGoldenBet = true;
			}
			isPlaying = true;
			canBet = false;
			StartGame();
		}
	}

	[Server]
	protected override void StartGame()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Roulette::StartGame()' called when server was not active");
			return;
		}
		base.StartGame();
		wheel.SpinTheWheel(GetSeededRandom());
	}

	[Server]
	public void ResetBets()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Roulette::ResetBets()' called when server was not active");
		}
		else if (!isPlaying)
		{
			_bets.Clear();
			_goldenBetOption = null;
			RouletteButton[] array = buttons;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].ServerSetBets(base.MaxBet, 0L);
			}
			RpcSetTotalBetText("$0");
		}
	}

	[ClientRpc]
	private void RpcSetTotalBetText(string text)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(text);
		SendRPCInternal("System.Void Roulette::RpcSetTotalBetText(System.String)", -1387118594, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void SelectBettingOption(string option, RouletteButton button)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Roulette::SelectBettingOption(System.String,RouletteButton)' called when server was not active");
		}
		else
		{
			if (isPlaying)
			{
				return;
			}
			long currentInput = keypad.GetCurrentInput();
			if (currentInput <= 0)
			{
				return;
			}
			if (isGoldenChipApplied)
			{
				if (string.IsNullOrEmpty(_goldenBetOption))
				{
					_goldenBetOption = option;
				}
				else if (_goldenBetOption != option)
				{
					button.ServerWarningFeedback();
					return;
				}
			}
			long balance = NetworkSingleton<MoneyManager>.Instance.balance;
			long num = Math.Min(base.MaxBet, balance);
			long num2 = (_bets.ContainsKey(option) ? _bets[option] : 0);
			long num3;
			if (!isGoldenChipApplied)
			{
				num3 = num2 + currentInput;
				if (num3 > num)
				{
					num3 = num;
					currentInput = num3 - num2;
					if (currentInput <= 0)
					{
						return;
					}
				}
			}
			else
			{
				ResetBets();
				num3 = currentInput;
			}
			_bets[option] = num3;
			long totalBet = GetTotalBet();
			ServerSetBet(totalBet);
			RpcSetTotalBetText(MoneyFormatter.FormatWithDollar(totalBet));
			button.ServerSetBets(base.MaxBet, num3);
		}
	}

	private long GetTotalBet()
	{
		long num = 0L;
		foreach (long value in _bets.Values)
		{
			num += value;
		}
		return num;
	}

	protected override void SetGoldenChip(bool apply, float multiplier = 1f)
	{
		if (isGoldenChipApplied != apply)
		{
			base.NetworkisGoldenChipApplied = apply;
			if (!apply)
			{
				isGoldenBet = false;
			}
			if (!apply)
			{
				_goldenBetOption = null;
			}
			ResetBets();
			keypad.SetGoldenChip(apply, multiplier);
		}
	}

	private void HandleWheelStopped(string result)
	{
		if (!base.isServer || !isPlaying)
		{
			return;
		}
		if (!int.TryParse(result, out var result2))
		{
			EndGame(0.0);
			return;
		}
		double num = 0.0;
		foreach (KeyValuePair<string, long> bet in _bets)
		{
			string key = bet.Key;
			long value = bet.Value;
			if (value > 0)
			{
				double num2 = CheckBetWin(key, result2);
				if (num2 > 0.0)
				{
					num += (double)value * num2;
				}
			}
		}
		long totalBet = GetTotalBet();
		double multiplier = ((totalBet > 0) ? (num / (double)totalBet) : 0.0);
		EndGame(multiplier);
	}

	private float CheckBetWin(string betOption, int resultNumber)
	{
		if (string.IsNullOrEmpty(betOption))
		{
			return 0f;
		}
		if (int.TryParse(betOption, out var result))
		{
			if (resultNumber != result)
			{
				return 0f;
			}
			return 36f;
		}
		switch (betOption)
		{
		case "Red":
			if (!RedNumbers.Contains(resultNumber))
			{
				return 0f;
			}
			return 2f;
		case "Black":
			if (!BlackNumbers.Contains(resultNumber))
			{
				return 0f;
			}
			return 2f;
		case "Odd":
			if (resultNumber == 0 || resultNumber % 2 != 1)
			{
				return 0f;
			}
			return 2f;
		case "Even":
			if (resultNumber == 0 || resultNumber % 2 != 0)
			{
				return 0f;
			}
			return 2f;
		case "Low":
			if (resultNumber < 1 || resultNumber > 18)
			{
				return 0f;
			}
			return 2f;
		case "High":
			if (resultNumber < 19 || resultNumber > 36)
			{
				return 0f;
			}
			return 2f;
		case "Dozen1":
			if (resultNumber < 1 || resultNumber > 12)
			{
				return 0f;
			}
			return 3f;
		case "Dozen2":
			if (resultNumber < 13 || resultNumber > 24)
			{
				return 0f;
			}
			return 3f;
		case "Dozen3":
			if (resultNumber < 25 || resultNumber > 36)
			{
				return 0f;
			}
			return 3f;
		case "Column1":
			if (!Column1.Contains(resultNumber))
			{
				return 0f;
			}
			return 3f;
		case "Column2":
			if (!Column2.Contains(resultNumber))
			{
				return 0f;
			}
			return 3f;
		case "Column3":
			if (!Column3.Contains(resultNumber))
			{
				return 0f;
			}
			return 3f;
		default:
			return 0f;
		}
	}

	private void EndGame(double multiplier)
	{
		Payout(multiplier, ChangeType.GameResult, null, GetTotalBet());
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
		_goldenBetOption = null;
		wheel.ResetWheel();
	}

	static Roulette()
	{
		RedNumbers = new int[18]
		{
			1, 3, 5, 7, 9, 12, 14, 16, 18, 19,
			21, 23, 25, 27, 30, 32, 34, 36
		};
		BlackNumbers = new int[18]
		{
			2, 4, 6, 8, 10, 11, 13, 15, 17, 20,
			22, 24, 26, 28, 29, 31, 33, 35
		};
		Column1 = new int[12]
		{
			1, 4, 7, 10, 13, 16, 19, 22, 25, 28,
			31, 34
		};
		Column2 = new int[12]
		{
			2, 5, 8, 11, 14, 17, 20, 23, 26, 29,
			32, 35
		};
		Column3 = new int[12]
		{
			3, 6, 9, 12, 15, 18, 21, 24, 27, 30,
			33, 36
		};
		RemoteProcedureCalls.RegisterRpc(typeof(Roulette), "System.Void Roulette::RpcSetTotalBetText(System.String)", InvokeUserCode_RpcSetTotalBetText__String);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetTotalBetText__String(string text)
	{
		totalBetText.text = text;
	}

	protected static void InvokeUserCode_RpcSetTotalBetText__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetTotalBetText called on server.");
		}
		else
		{
			((Roulette)obj).UserCode_RpcSetTotalBetText__String(reader.ReadString());
		}
	}
}

}
