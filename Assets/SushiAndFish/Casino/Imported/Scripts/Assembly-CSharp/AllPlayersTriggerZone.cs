namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Diagnostics;
using global::System.Runtime.CompilerServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.Events;

public class AllPlayersTriggerZone : NetworkBehaviour
{
	[CompilerGenerated]
	private sealed class _003CCountdownRoutine_003Ed__19 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public AllPlayersTriggerZone _003C_003E4__this;

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
		public _003CCountdownRoutine_003Ed__19(int _003C_003E1__state)
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
			AllPlayersTriggerZone allPlayersTriggerZone = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				allPlayersTriggerZone.RpcUpdateCountdownText(start: true);
				_003C_003E2__current = new WaitForSeconds(allPlayersTriggerZone.delayBeforeEvent);
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				allPlayersTriggerZone._hasTriggered = true;
				allPlayersTriggerZone.RpcOnCountdownEnd();
				if (NetworkSingleton<GameManager>.Instance.state == GameState.Game)
				{
					allPlayersTriggerZone.RpcOnLeaveCasinoSoundEffect();
				}
				else
				{
					allPlayersTriggerZone.RpcOnSoundEffect();
				}
				allPlayersTriggerZone.onCountDownEnd?.Invoke();
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
	private float delayBeforeEvent = 1f;

	[SerializeField]
	private float colliderCheckInterval = 0.1f;

	[Header("References")]
	[SerializeField]
	private TextMeshPro countdownText;

	[SerializeField]
	private Collider checkCollider;

	[SerializeField]
	private Animator animator;

	[Header("Events")]
	[SerializeField]
	private UnityEvent onCountDownEnd;

	[SerializeField]
	private UnityEvent onSoundEffect;

	[SerializeField]
	private UnityEvent onLeaveCasinoSoundEffect;

	private Coroutine _countdownRoutine;

	private Coroutine _countdownTextRoutine;

	private bool _hasTriggered;

	private float _lastCheckTime;

	[SerializeField]
	private bool isActive;

	public bool IsActive
	{
		get
		{
			return isActive;
		}
		set
		{
			if (isActive != value)
			{
				isActive = value;
				if (!isActive && _countdownRoutine != null)
				{
					StopCoroutine(_countdownRoutine);
					_countdownRoutine = null;
					RpcUpdateCountdownText(start: false);
				}
			}
		}
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		if (!base.isServer)
		{
			base.enabled = false;
		}
	}

	private void Update()
	{
		CheckPlayers();
	}

	private void CheckPlayers()
	{
		if (!IsActive || _hasTriggered || !MonoSingleton<LocalManager>.Instance || MonoSingleton<LocalManager>.Instance.players == null || MonoSingleton<LocalManager>.Instance.players.Count <= 0 || Time.time - _lastCheckTime < colliderCheckInterval)
		{
			return;
		}
		_lastCheckTime = Time.time;
		Bounds bounds = checkCollider.bounds;
		foreach (PlayerReferences player in MonoSingleton<LocalManager>.Instance.players)
		{
			if (!bounds.Contains(player.transform.position))
			{
				if (_countdownRoutine != null)
				{
					StopCoroutine(_countdownRoutine);
					_countdownRoutine = null;
					RpcUpdateCountdownText(start: false);
				}
				return;
			}
		}
		if (_countdownRoutine == null)
		{
			_countdownRoutine = StartCoroutine(CountdownRoutine());
		}
	}

	[IteratorStateMachine(typeof(_003CCountdownRoutine_003Ed__19))]
	[Server]
	private IEnumerator CountdownRoutine()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator AllPlayersTriggerZone::CountdownRoutine()' called when server was not active");
			return null;
		}
		return new _003CCountdownRoutine_003Ed__19(0)
		{
			_003C_003E4__this = this
		};
	}

	[ClientRpc]
	private void RpcOnCountdownEnd()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void AllPlayersTriggerZone::RpcOnCountdownEnd()", 517119164, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcUpdateCountdownText(bool start)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(start);
		SendRPCInternal("System.Void AllPlayersTriggerZone::RpcUpdateCountdownText(System.Boolean)", 1163986795, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private IEnumerator CountdownTextRoutine()
	{
		countdownText.text = delayBeforeEvent.ToString("0.0");
		float elapsed = 0f;
		while (elapsed < delayBeforeEvent)
		{
			elapsed += Time.deltaTime;
			float num = delayBeforeEvent - elapsed;
			countdownText.text = num.ToString("0.0");
			yield return null;
		}
	}

	[ClientRpc]
	private void RpcOnSoundEffect()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void AllPlayersTriggerZone::RpcOnSoundEffect()", 572427076, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcOnLeaveCasinoSoundEffect()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void AllPlayersTriggerZone::RpcOnLeaveCasinoSoundEffect()", 692876386, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcOnCountdownEnd()
	{
		if ((bool)animator)
		{
			animator.SetTrigger("isReady");
		}
	}

	protected static void InvokeUserCode_RpcOnCountdownEnd(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcOnCountdownEnd called on server.");
		}
		else
		{
			((AllPlayersTriggerZone)obj).UserCode_RpcOnCountdownEnd();
		}
	}

	protected void UserCode_RpcUpdateCountdownText__Boolean(bool start)
	{
		if ((bool)countdownText)
		{
			if (_countdownTextRoutine != null)
			{
				StopCoroutine(_countdownTextRoutine);
				_countdownTextRoutine = null;
				countdownText.text = delayBeforeEvent.ToString("0.0");
			}
			if (start)
			{
				_countdownTextRoutine = StartCoroutine(CountdownTextRoutine());
			}
		}
	}

	protected static void InvokeUserCode_RpcUpdateCountdownText__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcUpdateCountdownText called on server.");
		}
		else
		{
			((AllPlayersTriggerZone)obj).UserCode_RpcUpdateCountdownText__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcOnSoundEffect()
	{
		onSoundEffect?.Invoke();
	}

	protected static void InvokeUserCode_RpcOnSoundEffect(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcOnSoundEffect called on server.");
		}
		else
		{
			((AllPlayersTriggerZone)obj).UserCode_RpcOnSoundEffect();
		}
	}

	protected void UserCode_RpcOnLeaveCasinoSoundEffect()
	{
		onLeaveCasinoSoundEffect?.Invoke();
	}

	protected static void InvokeUserCode_RpcOnLeaveCasinoSoundEffect(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcOnLeaveCasinoSoundEffect called on server.");
		}
		else
		{
			((AllPlayersTriggerZone)obj).UserCode_RpcOnLeaveCasinoSoundEffect();
		}
	}

	static AllPlayersTriggerZone()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(AllPlayersTriggerZone), "System.Void AllPlayersTriggerZone::RpcOnCountdownEnd()", InvokeUserCode_RpcOnCountdownEnd);
		RemoteProcedureCalls.RegisterRpc(typeof(AllPlayersTriggerZone), "System.Void AllPlayersTriggerZone::RpcUpdateCountdownText(System.Boolean)", InvokeUserCode_RpcUpdateCountdownText__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(AllPlayersTriggerZone), "System.Void AllPlayersTriggerZone::RpcOnSoundEffect()", InvokeUserCode_RpcOnSoundEffect);
		RemoteProcedureCalls.RegisterRpc(typeof(AllPlayersTriggerZone), "System.Void AllPlayersTriggerZone::RpcOnLeaveCasinoSoundEffect()", InvokeUserCode_RpcOnLeaveCasinoSoundEffect);
	}
}

}
