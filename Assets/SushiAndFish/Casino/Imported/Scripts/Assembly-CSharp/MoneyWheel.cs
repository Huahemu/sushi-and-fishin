namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class MoneyWheel : GameBase
{
	[Header("References")]
	[SerializeField]
	private Wheel wheel;

	[SerializeField]
	private MoneyWheelButton[] buttons;

	[SerializeField]
	private Transform betIndicator;

	private string _currentBettingOption = "Green";

	public override void OnStartServer()
	{
		base.OnStartServer();
		SelectBettingOption("Green");
	}

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
	protected override void StartGame()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MoneyWheel::StartGame()' called when server was not active");
			return;
		}
		base.StartGame();
		wheel.SpinTheWheel(GetSeededRandom());
	}

	[Server]
	public void SelectBettingOption(string option)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MoneyWheel::SelectBettingOption(System.String)' called when server was not active");
		}
		else
		{
			if (isPlaying)
			{
				return;
			}
			_currentBettingOption = option;
			MoneyWheelButton[] array = buttons;
			foreach (MoneyWheelButton moneyWheelButton in array)
			{
				moneyWheelButton.SelectFeedBack(option == moneyWheelButton.betOption);
				if (option == moneyWheelButton.betOption)
				{
					RpcSetBetIndicatorPosition(moneyWheelButton.transform.position);
				}
			}
		}
	}

	private void HandleWheelStopped(string result)
	{
		if (!base.isServer || !isPlaying)
		{
			return;
		}
		if (result == _currentBettingOption)
		{
			switch (result)
			{
			case "Green":
				EndGame(2f);
				break;
			case "Blue":
				EndGame(3f);
				break;
			case "Red":
				EndGame(5f);
				break;
			case "Orange":
				EndGame(10f);
				break;
			}
		}
		else
		{
			EndGame(0f);
		}
	}

	private void EndGame(float multiplier)
	{
		Payout((double)multiplier * base.EstimatedValue, ChangeType.GameResult, null, -1L);
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
		wheel.ResetWheel();
	}

	[ClientRpc]
	private void RpcSetBetIndicatorPosition(Vector3 position)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVector3(position);
		SendRPCInternal("System.Void MoneyWheel::RpcSetBetIndicatorPosition(UnityEngine.Vector3)", -1398401581, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetBetIndicatorPosition__Vector3(Vector3 position)
	{
		betIndicator.transform.position = position;
	}

	protected static void InvokeUserCode_RpcSetBetIndicatorPosition__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetBetIndicatorPosition called on server.");
		}
		else
		{
			((MoneyWheel)obj).UserCode_RpcSetBetIndicatorPosition__Vector3(reader.ReadVector3());
		}
	}

	static MoneyWheel()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(MoneyWheel), "System.Void MoneyWheel::RpcSetBetIndicatorPosition(UnityEngine.Vector3)", InvokeUserCode_RpcSetBetIndicatorPosition__Vector3);
	}
}

}
