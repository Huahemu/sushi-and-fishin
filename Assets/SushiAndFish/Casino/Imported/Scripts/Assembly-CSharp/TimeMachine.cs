namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Diagnostics;
using global::System.Runtime.CompilerServices;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::TMPro;
using global::UnityEngine;

public class TimeMachine : ConsumableItem
{
	[CompilerGenerated]
	private sealed class _003CRollbackRoutine_003Ed__7 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public TimeMachine _003C_003E4__this;

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
		public _003CRollbackRoutine_003Ed__7(int _003C_003E1__state)
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
			TimeMachine timeMachine = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003C_003E2__current = new WaitForSeconds(0.5f);
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				timeMachine.ServerRewindTime();
				_003C_003E2__current = null;
				_003C_003E1__state = 2;
				return true;
			case 2:
				_003C_003E1__state = -1;
				timeMachine.destroySfx.RpcPlayOneShotWith3DPos();
				timeMachine.DestroyItem();
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

	[Header("Settings")]
	[SerializeField]
	private float rollbackSeconds = 60f;

	[Header("References")]
	[SerializeField]
	private TextMeshPro screenText;

	[SerializeField]
	private Animator anim;

	[Header("SFX")]
	[SerializeField]
	private SFXComponent rewindSfx;

	[SerializeField]
	private SFXComponent destroySfx;

	private bool _isActivated;

	protected override void OnUseItem(bool isPressed)
	{
		base.OnUseItem(isPressed);
		if (isPressed && !_isActivated && (NetworkSingleton<GameManager>.Instance.state != GameState.Game || NetworkSingleton<GameManager>.Instance.HasDayStarted))
		{
			_isActivated = true;
			rewindSfx.PlayOneShotWith3DPos();
			anim.SetTrigger("Rewind");
			DOVirtual.Float(0f, 1f, 0.5f, (float t) =>
			{
				float f = rollbackSeconds - rollbackSeconds * t;
				screenText.text = Mathf.RoundToInt(f) + "s";
			}).SetEase(Ease.OutCubic);
			if (base.isServer)
			{
				StartCoroutine(RollbackRoutine());
			}
		}
	}

	[IteratorStateMachine(typeof(_003CRollbackRoutine_003Ed__7))]
	[Server]
	private IEnumerator RollbackRoutine()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator TimeMachine::RollbackRoutine()' called when server was not active");
			return null;
		}
		return new _003CRollbackRoutine_003Ed__7(0)
		{
			_003C_003E4__this = this
		};
	}

	[Server]
	private void ServerRewindTime()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void TimeMachine::ServerRewindTime()' called when server was not active");
			return;
		}
		List<PayoutRecord> list = null;
		if ((bool)NetworkSingleton<PayoutTracker>.Instance)
		{
			list = NetworkSingleton<PayoutTracker>.Instance.RollbackLastSeconds(rollbackSeconds);
		}
		if (list != null && list.Count > 0)
		{
			foreach (PayoutRecord item in list)
			{
				if (item != null && item.profit != 0L)
				{
					NetworkSingleton<MoneyManager>.Instance.TryChangeBalance(-item.profit, item.playerProfile, ChangeType.GameResult);
				}
			}
			NetworkSingleton<GameResultsManager>.Instance.RollbackResults(list);
		}
		NetworkSingleton<GameManager>.Instance.ServerAdjustTimer(0f - rollbackSeconds);
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
