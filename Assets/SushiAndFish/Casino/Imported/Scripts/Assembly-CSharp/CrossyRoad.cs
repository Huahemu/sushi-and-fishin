namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Diagnostics;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.InteropServices;
using global::DG.Tweening;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;

public class CrossyRoad : GameBase
{
	[CompilerGenerated]
	private sealed class _003CResetAfterDelay_003Ed__30 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public CrossyRoad _003C_003E4__this;

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
		public _003CResetAfterDelay_003Ed__30(int _003C_003E1__state)
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
			CrossyRoad crossyRoad = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003C_003E2__current = new WaitForSeconds(2f);
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				crossyRoad.RpcResetDuckPosition();
				_003C_003E2__current = new WaitForSeconds(1f);
				_003C_003E1__state = 2;
				return true;
			case 2:
				_003C_003E1__state = -1;
				crossyRoad.ResetGame();
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
	private TextMeshPro multiplierText;

	[Header("References")]
	[SerializeField]
	private TextMeshPro potentialWinningsText;

	[SerializeField]
	private Transform startPoint;

	[SerializeField]
	private Transform endPoint;

	[SerializeField]
	private Transform penguin;

	[SerializeField]
	private Animator penguinAnim;

	[SerializeField]
	private Animator[] sidePenguinAnims;

	[SerializeField]
	private Transform[] iceCubes;

	[SerializeField]
	private ParticleSystem waterVfx;

	[Header("Settings")]
	[SerializeField]
	private float stepDistance = 0.2f;

	[SerializeField]
	private float jumpHeight = 0.1f;

	[SerializeField]
	private float stepTweenDuration = 0.4f;

	[SerializeField]
	private int maxSteps = 10;

	[SerializeField]
	private double[] stepMultipliers;

	[SerializeField]
	[SyncVar(hook = "OnMultiplierChanged")]
	private double currentMultiplier;

	[Header("SFX")]
	[SerializeField]
	private EventReference duckSFX;

	[SerializeField]
	private EventReference crossyWinSFX;

	[SerializeField]
	private EventReference crossyLoseSFX;

	[SerializeField]
	private EventReference crossyDuckLoseSFX;

	private int _currentStep;

	private float _currentCrashChance;

	private float _lastStepTime;

	private bool _hasEnded;

	public Action<double, double> _Mirror_SyncVarHookDelegate_currentMultiplier;

	public double NetworkcurrentMultiplier
	{
		get
		{
			return currentMultiplier;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref currentMultiplier, 8uL, _Mirror_SyncVarHookDelegate_currentMultiplier);
		}
	}

	private void OnMultiplierChanged(double oldMultiplier, double newMultiplier)
	{
		if ((bool)multiplierText)
		{
			multiplierText.text = $"{newMultiplier:F1}x";
		}
		if ((bool)potentialWinningsText)
		{
			long num = (long)Math.Round((double)currentBet * newMultiplier);
			potentialWinningsText.text = $"${num:N0}";
		}
	}

	private void Start()
	{
		Animator[] array = sidePenguinAnims;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].speed = UnityEngine.Random.Range(0.8f, 1.2f);
		}
	}

	[Server]
	protected override void StartGame()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void CrossyRoad::StartGame()' called when server was not active");
			return;
		}
		_currentStep = 0;
		NetworkcurrentMultiplier = stepMultipliers[_currentStep];
		_currentCrashChance = GetCrashChanceForStep(_currentStep);
	}

	[Server]
	public void TakeStep()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void CrossyRoad::TakeStep()' called when server was not active");
		}
		else if (isPlaying && !_hasEnded && _currentStep < maxSteps && !(Time.time - _lastStepTime < stepTweenDuration + 0.1f))
		{
			_lastStepTime = Time.time;
			StartCoroutine(StepRoutine());
		}
	}

	private IEnumerator StepRoutine()
	{
		float num = (float)GetSeededRandom(_currentStep * 9999).NextDouble();
		_currentStep++;
		if (num < _currentCrashChance)
		{
			RpcPenguinStep(_currentStep, (float)stepMultipliers[_currentStep]);
			_hasEnded = true;
			yield return new WaitForSeconds(stepTweenDuration);
			Crash();
			yield break;
		}
		NetworkcurrentMultiplier = stepMultipliers[_currentStep];
		_currentCrashChance = GetCrashChanceForStep(_currentStep);
		RpcPenguinStep(_currentStep, (float)currentMultiplier);
		if (_currentStep >= maxSteps)
		{
			yield return new WaitForSeconds(stepTweenDuration);
			CashOut();
		}
	}

	[Server]
	private void Crash()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void CrossyRoad::Crash()' called when server was not active");
			return;
		}
		RpcCrashFeedback(_currentStep, (float)currentMultiplier);
		Payout(0.0, ChangeType.GameResult, null, -1L);
		StartCoroutine(ResetAfterDelay());
	}

	[Server]
	public void CashOut()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void CrossyRoad::CashOut()' called when server was not active");
		}
		else if (isPlaying && !_hasEnded && _currentStep > 0)
		{
			_hasEnded = true;
			Payout(currentMultiplier, ChangeType.GameResult, null, -1L);
			RpcWinFeedback((float)currentMultiplier);
			StartCoroutine(ResetAfterDelay());
		}
	}

	[IteratorStateMachine(typeof(_003CResetAfterDelay_003Ed__30))]
	[Server]
	private IEnumerator ResetAfterDelay()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator CrossyRoad::ResetAfterDelay()' called when server was not active");
			return null;
		}
		return new _003CResetAfterDelay_003Ed__30(0)
		{
			_003C_003E4__this = this
		};
	}

	[Server]
	protected override void ResetGame()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void CrossyRoad::ResetGame()' called when server was not active");
			return;
		}
		_hasEnded = false;
		_currentStep = 0;
		NetworkcurrentMultiplier = stepMultipliers[_currentStep];
		_currentCrashChance = GetCrashChanceForStep(_currentStep);
		base.ResetGame();
	}

	private float GetCrashChanceForStep(int step)
	{
		if (step < 0)
		{
			return 0f;
		}
		if (step >= maxSteps)
		{
			return 1f;
		}
		double num = stepMultipliers[step];
		double num2 = stepMultipliers[step + 1];
		double num3 = base.EstimatedValue / num;
		double num4 = base.EstimatedValue / num2 / num3;
		return Mathf.Clamp01((float)(1.0 - num4));
	}

	[ClientRpc]
	private void RpcPenguinStep(int step, float mult)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(step);
		writer.WriteFloat(mult);
		SendRPCInternal("System.Void CrossyRoad::RpcPenguinStep(System.Int32,System.Single)", -2082101845, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcResetDuckPosition()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void CrossyRoad::RpcResetDuckPosition()", 614803394, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcWinFeedback(float mult)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(mult);
		SendRPCInternal("System.Void CrossyRoad::RpcWinFeedback(System.Single)", 1987450247, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcCrashFeedback(int step, float mult)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(step);
		writer.WriteFloat(mult);
		SendRPCInternal("System.Void CrossyRoad::RpcCrashFeedback(System.Int32,System.Single)", 2077413361, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public CrossyRoad()
	{
		_Mirror_SyncVarHookDelegate_currentMultiplier = OnMultiplierChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcPenguinStep__Int32__Single(int step, float mult)
	{
		Vector3 endValue = startPoint.position + (endPoint.position - startPoint.position).normalized * (stepDistance * (float)step);
		penguin.DOJump(endValue, jumpHeight, 1, stepTweenDuration).SetEase(Ease.Linear);
		penguinAnim.SetTrigger("Jump");
		SFXManager.SFXOneShotWithParameters(duckSFX, null, penguin.transform.position, 0.8f + mult * 0.04f);
	}

	protected static void InvokeUserCode_RpcPenguinStep__Int32__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcPenguinStep called on server.");
		}
		else
		{
			((CrossyRoad)obj).UserCode_RpcPenguinStep__Int32__Single(reader.ReadVarInt(), reader.ReadFloat());
		}
	}

	protected void UserCode_RpcResetDuckPosition()
	{
		penguin.DOMove(startPoint.position, stepTweenDuration).SetEase(Ease.OutQuad);
	}

	protected static void InvokeUserCode_RpcResetDuckPosition(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcResetDuckPosition called on server.");
		}
		else
		{
			((CrossyRoad)obj).UserCode_RpcResetDuckPosition();
		}
	}

	protected void UserCode_RpcWinFeedback__Single(float mult)
	{
		penguinAnim.SetTrigger("Win");
		Animator[] array = sidePenguinAnims;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].SetTrigger("Win");
		}
		SFXManager.SFXOneShotWithParameters(crossyWinSFX, null, penguin.transform.position, 0.8f + mult * 0.04f);
	}

	protected static void InvokeUserCode_RpcWinFeedback__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcWinFeedback called on server.");
		}
		else
		{
			((CrossyRoad)obj).UserCode_RpcWinFeedback__Single(reader.ReadFloat());
		}
	}

	protected void UserCode_RpcCrashFeedback__Int32__Single(int step, float mult)
	{
		waterVfx.transform.position = iceCubes[step].transform.position;
		Transform target = iceCubes[step];
		Sequence s = DOTween.Sequence();
		s.Append(target.DOShakePosition(0.2f, 0.1f).SetEase(Ease.OutCirc));
		s.Append(target.DOLocalMoveY(-0.2f, 0.8f).SetEase(Ease.InCirc));
		s.AppendInterval(0.5f);
		s.AppendCallback(() =>
		{
			waterVfx.Play();
		});
		s.AppendInterval(0.5f);
		s.Append(target.DOLocalMoveY(0f, 0.5f).SetEase(Ease.OutBack));
		penguinAnim.SetTrigger("Crash");
		SFXManager.SFXOneShotWithParameters(crossyDuckLoseSFX, null, penguin.transform.position, 0.8f + mult * 0.04f);
		SFXManager.SFXOneShotWithParameters(crossyLoseSFX, null, base.transform.position);
	}

	protected static void InvokeUserCode_RpcCrashFeedback__Int32__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcCrashFeedback called on server.");
		}
		else
		{
			((CrossyRoad)obj).UserCode_RpcCrashFeedback__Int32__Single(reader.ReadVarInt(), reader.ReadFloat());
		}
	}

	static CrossyRoad()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(CrossyRoad), "System.Void CrossyRoad::RpcPenguinStep(System.Int32,System.Single)", InvokeUserCode_RpcPenguinStep__Int32__Single);
		RemoteProcedureCalls.RegisterRpc(typeof(CrossyRoad), "System.Void CrossyRoad::RpcResetDuckPosition()", InvokeUserCode_RpcResetDuckPosition);
		RemoteProcedureCalls.RegisterRpc(typeof(CrossyRoad), "System.Void CrossyRoad::RpcWinFeedback(System.Single)", InvokeUserCode_RpcWinFeedback__Single);
		RemoteProcedureCalls.RegisterRpc(typeof(CrossyRoad), "System.Void CrossyRoad::RpcCrashFeedback(System.Int32,System.Single)", InvokeUserCode_RpcCrashFeedback__Int32__Single);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteDouble(currentMultiplier);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 8L) != 0L)
		{
			writer.WriteDouble(currentMultiplier);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref currentMultiplier, _Mirror_SyncVarHookDelegate_currentMultiplier, reader.ReadDouble());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 8L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref currentMultiplier, _Mirror_SyncVarHookDelegate_currentMultiplier, reader.ReadDouble());
		}
	}
}

}
