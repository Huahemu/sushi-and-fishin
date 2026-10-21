namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::DG.Tweening;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;

public class HiLoGame : GameBase
{
	[Header("References")]
	[SerializeField]
	private HiLoSlider hiLoSlider;

	[SerializeField]
	private Transform rollResultIndicator;

	[SerializeField]
	private Transform rollResultDice;

	[SerializeField]
	private TextMeshPro rollResultText;

	[SerializeField]
	private Transform rollStartPoint;

	[SerializeField]
	private Transform rollEndPoint;

	[SerializeField]
	private TextMeshPro multiplierText;

	[SerializeField]
	private TextMeshPro potentialWinningText;

	[SerializeField]
	private MeshRenderer underIndicator;

	[SerializeField]
	private MeshRenderer overIndicator;

	[SerializeField]
	private Material activeIndicator;

	[SerializeField]
	private Material inactiveIndicator;

	[Header("SFX")]
	[SerializeField]
	private SFXLoopComponent _sfxLoopComponent;

	private bool _isOver;

	private void OnEnable()
	{
		HiLoSlider obj = hiLoSlider;
		obj.OnValueChangedAction = (Action<float>)Delegate.Combine(obj.OnValueChangedAction, new Action<float>(HandleValueChanged));
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		HiLoSlider obj = hiLoSlider;
		obj.OnValueChangedAction = (Action<float>)Delegate.Remove(obj.OnValueChangedAction, new Action<float>(HandleValueChanged));
	}

	private void HandleValueChanged(float value)
	{
		if (base.isServer)
		{
			RpcSetIsOver(value, _isOver);
		}
	}

	protected override void OnBetSet()
	{
		RpcSetIsOver(hiLoSlider.currentValue, _isOver);
	}

	[ClientRpc]
	private void RpcSetIsOver(float value, bool isOver)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(value);
		writer.WriteBool(isOver);
		SendRPCInternal("System.Void HiLoGame::RpcSetIsOver(System.Single,System.Boolean)", 1068155300, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void SetOver(bool isOver)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void HiLoGame::SetOver(System.Boolean)' called when server was not active");
		}
		else if (!isPlaying)
		{
			_isOver = isOver;
			RpcSetIsOver(hiLoSlider.currentValue, _isOver);
		}
	}

	protected override void StartGame()
	{
		base.StartGame();
		hiLoSlider.LockSlider(isLocked: true);
		RollDice();
	}

	private void RollDice()
	{
		float roll = (float)GetSeededRandom().NextDouble();
		RpcRollDice(roll, _isOver);
	}

	[ClientRpc]
	private void RpcRollDice(float roll, bool isOver)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(roll);
		writer.WriteBool(isOver);
		SendRPCInternal("System.Void HiLoGame::RpcRollDice(System.Single,System.Boolean)", -1523939456, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void RollDiceFeedbacks(float roll, bool isOver)
	{
		_sfxLoopComponent.LoopSFX(play: true);
		rollResultText.text = "";
		float startX = rollStartPoint.localPosition.x;
		float endX = rollEndPoint.localPosition.x;
		float endValue = Mathf.Lerp(startX, endX, roll);
		Sequence sequence = DOTween.Sequence();
		float endValue2 = (isOver ? startX : endX);
		float duration = (isOver ? roll : (1f - roll)) * 5f;
		sequence.Append(rollResultIndicator.DOLocalMoveX(endValue2, 0.5f).SetEase(Ease.InOutQuad));
		sequence.Append(rollResultIndicator.DOLocalMoveX(endValue, duration).SetEase(Ease.Linear));
		sequence.Join(rollResultDice.DOShakeRotation(duration, 45f, 0, 15f, fadeOut: true, ShakeRandomnessMode.Harmonic));
		sequence.Join(rollResultDice.parent.DOLocalRotate(UnityEngine.Random.insideUnitSphere * 1080f, duration, RotateMode.FastBeyond360).SetEase(Ease.OutSine));
		sequence.Join(rollResultDice.DOShakeScale(duration, 1f, 5, 15f, fadeOut: false));
		sequence.OnUpdate(() =>
		{
			rollResultText.text = (Mathf.InverseLerp(startX, endX, rollResultIndicator.localPosition.x) * 100f).ToString("0.0") + "%";
			_sfxLoopComponent.ModulatePitch(Mathf.InverseLerp(startX, endX, rollResultIndicator.localPosition.x) + 0.63f);
		});
		if (base.isServer)
		{
			sequence.OnComplete(() =>
			{
				rollResultDice.parent.DOPunchScale(Vector3.one, 0.5f, 1, 0.5f).SetEase(Ease.InOutSine);
				_sfxLoopComponent.LoopSFX(play: false);
				EndGame(roll);
			});
		}
		else
		{
			sequence.OnComplete(() =>
			{
				rollResultDice.parent.DOPunchScale(Vector3.one, 0.5f, 1, 0.5f).SetEase(Ease.InOutSine);
				_sfxLoopComponent.LoopSFX(play: false);
			});
		}
	}

	private void EndGame(float roll)
	{
		if (_isOver)
		{
			if (roll >= hiLoSlider.currentValue)
			{
				Win();
			}
			else
			{
				Lose();
			}
		}
		else if (roll <= hiLoSlider.currentValue)
		{
			Win();
		}
		else
		{
			Lose();
		}
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
		hiLoSlider.LockSlider(isLocked: false);
	}

	private void Win()
	{
		double num = (_isOver ? (1.0 - (double)hiLoSlider.currentValue) : ((double)hiLoSlider.currentValue));
		double multiplier = base.EstimatedValue / num;
		Payout(multiplier, ChangeType.GameResult, null, -1L);
	}

	private void Lose()
	{
		Payout(0.0, ChangeType.GameResult, null, -1L);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetIsOver__Single__Boolean(float value, bool isOver)
	{
		double num = 0.0;
		num = (isOver ? (1f - value) : value);
		multiplierText.text = (base.EstimatedValue / num).ToString("0.##") + "x";
		potentialWinningText.text = "$" + (long)Math.Round((double)currentBet * base.EstimatedValue / num);
		underIndicator.material = (isOver ? inactiveIndicator : activeIndicator);
		overIndicator.material = (isOver ? activeIndicator : inactiveIndicator);
	}

	protected static void InvokeUserCode_RpcSetIsOver__Single__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetIsOver called on server.");
		}
		else
		{
			((HiLoGame)obj).UserCode_RpcSetIsOver__Single__Boolean(reader.ReadFloat(), reader.ReadBool());
		}
	}

	protected void UserCode_RpcRollDice__Single__Boolean(float roll, bool isOver)
	{
		RollDiceFeedbacks(roll, isOver);
	}

	protected static void InvokeUserCode_RpcRollDice__Single__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcRollDice called on server.");
		}
		else
		{
			((HiLoGame)obj).UserCode_RpcRollDice__Single__Boolean(reader.ReadFloat(), reader.ReadBool());
		}
	}

	static HiLoGame()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(HiLoGame), "System.Void HiLoGame::RpcSetIsOver(System.Single,System.Boolean)", InvokeUserCode_RpcSetIsOver__Single__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(HiLoGame), "System.Void HiLoGame::RpcRollDice(System.Single,System.Boolean)", InvokeUserCode_RpcRollDice__Single__Boolean);
	}
}

}
