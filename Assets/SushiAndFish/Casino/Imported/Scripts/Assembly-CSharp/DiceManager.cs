namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::Mirror;
using global::UnityEngine;

public class DiceManager : NetworkBehaviour
{
	[SerializeField]
	private int diceCount;

	[SerializeField]
	private Dice dicePrefab;

	[SerializeField]
	private Transform diceZone;

	[SerializeField]
	private Transform diceSpawnPoint;

	private int _currentResult;

	private List<Dice> _diceList = new List<Dice>();

	private List<Dice> _rolledDice = new List<Dice>();

	[SerializeField]
	private SFXComponent resetSfx;

	[SerializeField]
	private SFXComponent diceStoppedSfx;

	public event Action<int> OnDiceRolled;

	public override void OnStartServer()
	{
		base.OnStartServer();
		for (int i = 0; i < diceCount; i++)
		{
			Dice dice = UnityEngine.Object.Instantiate(dicePrefab, diceSpawnPoint.position, diceSpawnPoint.rotation);
			NetworkServer.Spawn(dice.gameObject);
			_diceList.Add(dice);
		}
		foreach (Dice dice2 in _diceList)
		{
			dice2.OnDiceStopped += HandleDiceStopped;
		}
	}

	public override void OnStopServer()
	{
		base.OnStopServer();
		Dice[] array = _diceList.ToArray();
		foreach (Dice dice in array)
		{
			if (!(dice == null))
			{
				dice.OnDiceStopped -= HandleDiceStopped;
				if (dice.gameObject != null)
				{
					NetworkServer.Destroy(dice.gameObject);
				}
			}
		}
		_diceList.Clear();
	}

	private void OnEnable()
	{
		if (!base.isServer)
		{
			return;
		}
		foreach (Dice dice in _diceList)
		{
			dice.ServerSetEnabled(isEnabled: true);
		}
	}

	private void OnDisable()
	{
		if (!base.isServer)
		{
			return;
		}
		foreach (Dice dice in _diceList)
		{
			dice.ServerSetEnabled(isEnabled: false);
		}
	}

	private void HandleDiceStopped(Dice dice, int result)
	{
		if (!_rolledDice.Contains(dice))
		{
			if (!IsInZone(dice.transform.position))
			{
				ResetDice(dice);
				return;
			}
			_rolledDice.Add(dice);
			dice.LockDice(isLocked: true);
			_currentResult += result;
			CheckAllDices();
		}
	}

	private void CheckAllDices()
	{
		if (_rolledDice.Count >= _diceList.Count)
		{
			diceStoppedSfx.RpcPlayOneShotWith3DPos();
			OnDiceRolled?.Invoke(_currentResult);
		}
	}

	public void ResetRound()
	{
		foreach (Dice dice in _diceList)
		{
			ResetDice(dice);
		}
		_currentResult = 0;
		_rolledDice.Clear();
	}

	private bool IsInZone(Vector3 position)
	{
		Vector3 vector = diceZone.InverseTransformPoint(position);
		if (Mathf.Abs(vector.x) <= 0.5f && Mathf.Abs(vector.y) <= 0.5f)
		{
			return Mathf.Abs(vector.z) <= 0.5f;
		}
		return false;
	}

	private void ResetDice(Dice dice)
	{
		dice.ServerResetDice(diceSpawnPoint.position);
		dice.LockDice(isLocked: false);
		resetSfx.RpcPlayOneShotWith3DPos();
	}

	public void LockDices(bool isLocked)
	{
		foreach (Dice dice in _diceList)
		{
			dice.LockDice(isLocked);
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
