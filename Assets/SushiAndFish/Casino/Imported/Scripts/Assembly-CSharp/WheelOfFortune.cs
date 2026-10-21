namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Globalization;
using global::Mirror;
using global::UnityEngine;

public class WheelOfFortune : GameBase
{
	[Header("References")]
	[SerializeField]
	private Wheel wheel;

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
			Debug.LogWarning("[Server] function 'System.Void WheelOfFortune::StartGame()' called when server was not active");
			return;
		}
		base.StartGame();
		wheel.SpinTheWheel(GetSeededRandom());
	}

	private void HandleWheelStopped(string result)
	{
		if (base.isServer && isPlaying)
		{
			decimal result2;
			if (result == "Spin")
			{
				gameTurn++;
				wheel.SpinTheWheel(GetSeededRandom());
			}
			else if (decimal.TryParse(result, NumberStyles.Float, CultureInfo.InvariantCulture, out result2))
			{
				EndGame((double)result2);
			}
			else
			{
				EndGame(0.0);
			}
		}
	}

	private void EndGame(double multiplier)
	{
		Payout(multiplier * base.EstimatedValue, ChangeType.GameResult, null, -1L);
		StartCoroutine(ResetGameRoutine());
	}

	private IEnumerator ResetGameRoutine()
	{
		yield return new WaitForSeconds(1f);
		ResetGame();
	}

	protected override void ResetGame()
	{
		wheel.ResetWheel();
		base.ResetGame();
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
