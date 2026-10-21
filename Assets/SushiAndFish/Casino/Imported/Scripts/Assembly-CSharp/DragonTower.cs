namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;

public class DragonTower : GameBase
{
	[Serializable]
	private class Floor
	{
		public List<DragonTowerButton> buttons = new List<DragonTowerButton>();

		public int eggIndex;
	}

	[Header("References")]
	[SerializeField]
	private Animator animator;

	[SerializeField]
	private ParticleSystem fireVfx;

	[SerializeField]
	private TextMeshPro multiplierText;

	[SerializeField]
	private TextMeshPro potentialWinningText;

	[SerializeField]
	private List<Floor> floors = new List<Floor>();

	private bool _hasEnded;

	private int _currentFloor;

	[Header("SFX")]
	[SerializeField]
	private SFXLoopComponent sfxLoopComponent;

	[SerializeField]
	private SFXComponent loseSfx;

	[SerializeField]
	private SFXComponent winSfx;

	[SerializeField]
	private EventReference stepSfx;

	[Server]
	protected override void StartGame()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void DragonTower::StartGame()' called when server was not active");
			return;
		}
		base.StartGame();
		sfxLoopComponent.RpcLoopSFX(play: true);
		SetEggs();
		RpcSetInteractableButtons(0);
		RpcSetMultiplierText(1.0);
	}

	private void SetEggs()
	{
		global::System.Random seededRandom = GetSeededRandom();
		foreach (Floor floor in floors)
		{
			floor.eggIndex = seededRandom.Next(0, 4);
		}
	}

	[Server]
	public void OnPressButton(int floorIndex, int buttonIndex, DragonTowerButton button)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void DragonTower::OnPressButton(System.Int32,System.Int32,DragonTowerButton)' called when server was not active");
		}
		else if (isPlaying && _currentFloor == floorIndex && !_hasEnded)
		{
			if (buttonIndex == floors[floorIndex].eggIndex)
			{
				button.ServerSetButtonState(DragonTowerButton.ButtonState.Red);
				Lose();
			}
			else
			{
				button.ServerSetButtonState(DragonTowerButton.ButtonState.Green);
				ProgressGame();
			}
		}
	}

	private void ProgressGame()
	{
		_currentFloor++;
		RpcSetMultiplierText(GetMultiplier(_currentFloor));
		if (_currentFloor > floors.Count - 1)
		{
			Win();
			return;
		}
		RpcStepSfx();
		RpcSetInteractableButtons(_currentFloor);
	}

	[Server]
	public void Cashout(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void DragonTower::Cashout(PlayerInteract)' called when server was not active");
		}
		else if (isPlaying && !_hasEnded && _currentFloor > 0)
		{
			Win();
		}
	}

	private void Win()
	{
		_hasEnded = true;
		sfxLoopComponent.RpcLoopSFX(play: false);
		double multiplier = GetMultiplier(_currentFloor);
		RpcSetAnimator(isWin: true);
		RevealEggs();
		Payout(multiplier, ChangeType.GameResult, null, -1L);
		winSfx.PlayOneShotWith3DPos();
		StartCoroutine(ResetGameRoutine());
	}

	private void Lose()
	{
		_hasEnded = true;
		sfxLoopComponent.RpcLoopSFX(play: false);
		RpcSetAnimator(isWin: false);
		RevealEggs();
		Payout(0.0, ChangeType.GameResult, null, -1L);
		loseSfx.PlayOneShotWith3DPos();
		StartCoroutine(ResetGameRoutine());
	}

	private void RevealEggs()
	{
		for (int i = 0; i < _currentFloor; i++)
		{
			Floor floor = floors[i];
			floor.buttons[floor.eggIndex].ServerSetButtonState(DragonTowerButton.ButtonState.RevealEgg);
		}
	}

	private double GetMultiplier(int stage)
	{
		if (stage <= 0)
		{
			return 1.0;
		}
		double num = Math.Pow(0.75, stage);
		return 1.0 / num * base.EstimatedValue;
	}

	private IEnumerator ResetGameRoutine()
	{
		yield return new WaitForSeconds(0.8f);
		RpcSetInteractableButtons(-1);
		yield return new WaitForSeconds(0.2f);
		ResetGame();
	}

	protected override void ResetGame()
	{
		RpcSetInteractableButtons(-1);
		RpcSetMultiplierText(0.0);
		base.ResetGame();
		_hasEnded = false;
		_currentFloor = 0;
	}

	[ClientRpc]
	private void RpcSetInteractableButtons(int floorIndex)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(floorIndex);
		SendRPCInternal("System.Void DragonTower::RpcSetInteractableButtons(System.Int32)", 2121373762, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetMultiplierText(double multiplier)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteDouble(multiplier);
		SendRPCInternal("System.Void DragonTower::RpcSetMultiplierText(System.Double)", -1489341436, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetAnimator(bool isWin)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isWin);
		SendRPCInternal("System.Void DragonTower::RpcSetAnimator(System.Boolean)", 751021246, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void PlayFireVfx()
	{
		fireVfx.Play();
	}

	[ClientRpc]
	private void RpcStepSfx()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void DragonTower::RpcStepSfx()", -1454015945, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetInteractableButtons__Int32(int floorIndex)
	{
		for (int i = 0; i < floors.Count; i++)
		{
			foreach (DragonTowerButton button in floors[i].buttons)
			{
				if (i < floorIndex)
				{
					if (button.buttonState == DragonTowerButton.ButtonState.Clickable)
					{
						button.ServerSetButtonState(DragonTowerButton.ButtonState.Inactive);
					}
				}
				else if (i == floorIndex)
				{
					button.ServerSetButtonState(DragonTowerButton.ButtonState.Clickable);
				}
				else
				{
					button.ServerSetButtonState(DragonTowerButton.ButtonState.Inactive);
				}
			}
		}
	}

	protected static void InvokeUserCode_RpcSetInteractableButtons__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetInteractableButtons called on server.");
		}
		else
		{
			((DragonTower)obj).UserCode_RpcSetInteractableButtons__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcSetMultiplierText__Double(double multiplier)
	{
		multiplierText.text = multiplier.ToString("0.##") + "x";
		potentialWinningText.text = "$" + ((long)Math.Round((double)currentBet * multiplier)).ToString("N0");
	}

	protected static void InvokeUserCode_RpcSetMultiplierText__Double(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetMultiplierText called on server.");
		}
		else
		{
			((DragonTower)obj).UserCode_RpcSetMultiplierText__Double(reader.ReadDouble());
		}
	}

	protected void UserCode_RpcSetAnimator__Boolean(bool isWin)
	{
		if (isWin)
		{
			animator.SetTrigger("Win");
		}
		else
		{
			animator.SetTrigger("Lose");
		}
	}

	protected static void InvokeUserCode_RpcSetAnimator__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetAnimator called on server.");
		}
		else
		{
			((DragonTower)obj).UserCode_RpcSetAnimator__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcStepSfx()
	{
		SFXManager.SFXOneShotWithParameters(stepSfx, null, base.transform.position, 1f + (float)_currentFloor / (float)floors.Count);
	}

	protected static void InvokeUserCode_RpcStepSfx(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcStepSfx called on server.");
		}
		else
		{
			((DragonTower)obj).UserCode_RpcStepSfx();
		}
	}

	static DragonTower()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(DragonTower), "System.Void DragonTower::RpcSetInteractableButtons(System.Int32)", InvokeUserCode_RpcSetInteractableButtons__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(DragonTower), "System.Void DragonTower::RpcSetMultiplierText(System.Double)", InvokeUserCode_RpcSetMultiplierText__Double);
		RemoteProcedureCalls.RegisterRpc(typeof(DragonTower), "System.Void DragonTower::RpcSetAnimator(System.Boolean)", InvokeUserCode_RpcSetAnimator__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(DragonTower), "System.Void DragonTower::RpcStepSfx()", InvokeUserCode_RpcStepSfx);
	}
}

}
