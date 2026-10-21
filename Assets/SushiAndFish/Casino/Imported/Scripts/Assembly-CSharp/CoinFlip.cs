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
using global::UnityEngine;

public class CoinFlip : NetworkBehaviour
{
	[CompilerGenerated]
	private sealed class _003CCheckCoinStoppedRoutine_003Ed__17 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public CoinFlip _003C_003E4__this;

		private float _003CstartTime_003E5__2;

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
		public _003CCheckCoinStoppedRoutine_003Ed__17(int _003C_003E1__state)
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
			CoinFlip coinFlip = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003C_003E2__current = new WaitForSeconds(coinFlip.firstCheckDelay);
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				_003CstartTime_003E5__2 = Time.time;
				goto IL_00dc;
			case 2:
				{
					_003C_003E1__state = -1;
					if (!(coinFlip.coin.transform.localPosition.y > coinFlip.coinHeightThreshold) && !(coinFlip.coin.linearVelocity.sqrMagnitude > coinFlip.stopVelocityThreshold * coinFlip.stopVelocityThreshold) && !(coinFlip.coin.angularVelocity.sqrMagnitude > coinFlip.stopAngularVelocityThreshold * coinFlip.stopAngularVelocityThreshold))
					{
						break;
					}
					goto IL_00dc;
				}
				IL_00dc:
				if (Time.time - _003CstartTime_003E5__2 < coinFlip.maxDuration)
				{
					_003C_003E2__current = new WaitForSeconds(coinFlip.checkInterval);
					_003C_003E1__state = 2;
					return true;
				}
				break;
			}
			coinFlip.loopComponent.RpcLoopSFX(play: false);
			coinFlip.DecideWinner();
			return false;
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
	private Rigidbody coin;

	[Header("Throw Settings")]
	[SerializeField]
	private float minFlipForce = 10f;

	[SerializeField]
	private float maxFlipForce = 20f;

	[SerializeField]
	private float minFlipTorque = 2f;

	[SerializeField]
	private float maxFlipTorque = 4f;

	[Header("Stop Check Settings")]
	[SerializeField]
	private float firstCheckDelay = 1f;

	[SerializeField]
	private float checkInterval = 0.1f;

	[SerializeField]
	private float maxDuration = 10f;

	[SerializeField]
	private float stopVelocityThreshold = 0.1f;

	[SerializeField]
	private float stopAngularVelocityThreshold = 0.1f;

	[SerializeField]
	private float coinHeightThreshold = 1.25f;

	[Header("SFX")]
	[SerializeField]
	private SFXLoopComponent loopComponent;

	[SerializeField]
	private SFXLocalPlayer winVoiceLineSfx;

	[SerializeField]
	private SFXLocalPlayer loseVoiceLineSfx;

	private Coroutine _checkStopCoroutine;

	[Server]
	public void ServerPlayCoinFlip()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void CoinFlip::ServerPlayCoinFlip()' called when server was not active");
			return;
		}
		FlipCoin();
		StartCoroutine(CheckCoinStoppedRoutine());
	}

	[Server]
	private void FlipCoin()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void CoinFlip::FlipCoin()' called when server was not active");
			return;
		}
		global::System.Random seededRandom = GetSeededRandom();
		float num = Mathf.Lerp(minFlipForce, maxFlipForce, (float)seededRandom.NextDouble());
		coin.isKinematic = false;
		coin.AddForce(Vector3.up * num, ForceMode.VelocityChange);
		float f = (float)(seededRandom.NextDouble() * Math.PI * 2.0);
		Vector3 normalized = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)).normalized;
		float num2 = Mathf.Lerp(minFlipTorque, maxFlipTorque, (float)seededRandom.NextDouble());
		coin.AddTorque(normalized * num2, ForceMode.VelocityChange);
		loopComponent.RpcLoopSFX(play: true);
	}

	[IteratorStateMachine(typeof(_003CCheckCoinStoppedRoutine_003Ed__17))]
	[Server]
	private IEnumerator CheckCoinStoppedRoutine()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator CoinFlip::CheckCoinStoppedRoutine()' called when server was not active");
			return null;
		}
		return new _003CCheckCoinStoppedRoutine_003Ed__17(0)
		{
			_003C_003E4__this = this
		};
	}

	[Server]
	private void DecideWinner()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void CoinFlip::DecideWinner()' called when server was not active");
		}
		else if (Vector3.Angle(coin.transform.up, Vector3.up) > 90f)
		{
			NetworkSingleton<WinSceneManager>.Instance.ServerConcludeCoinFlip(isWin: true);
			RpcPlayJeffVoiceLine(isWin: true);
		}
		else
		{
			NetworkSingleton<WinSceneManager>.Instance.ServerConcludeCoinFlip(isWin: false);
			RpcPlayJeffVoiceLine(isWin: false);
		}
	}

	private global::System.Random GetSeededRandom(int additionalContext = 0)
	{
		if (NetworkSingleton<SeededRandomManager>.Instance == null || NetworkSingleton<GameManager>.Instance == null)
		{
			return new global::System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
		}
		long num = NetworkSingleton<SeededRandomManager>.Instance.CurrentSeed ^ (additionalContext * 2246822519u);
		long num2 = (num ^ (num >> 32)) * 2246822507u;
		long num3 = (num2 ^ (num2 >> 16)) * 3266489917u;
		return new global::System.Random((int)(num3 ^ (num3 >> 13)));
	}

	[Server]
	public void TestCoinFlip()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void CoinFlip::TestCoinFlip()' called when server was not active");
			return;
		}
		global::System.Random seededRandom = GetSeededRandom(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
		float num = Mathf.Lerp(minFlipForce, maxFlipForce, (float)seededRandom.NextDouble());
		coin.AddForce(Vector3.up * num, ForceMode.VelocityChange);
		float f = (float)(seededRandom.NextDouble() * Math.PI * 2.0);
		Vector3 normalized = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)).normalized;
		float num2 = Mathf.Lerp(minFlipTorque, maxFlipTorque, (float)seededRandom.NextDouble());
		coin.AddTorque(normalized * num2, ForceMode.VelocityChange);
		StartCoroutine(CheckCoinStoppedRoutine());
	}

	[ClientRpc]
	private void RpcPlayJeffVoiceLine(bool isWin)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isWin);
		SendRPCInternal("System.Void CoinFlip::RpcPlayJeffVoiceLine(System.Boolean)", 750501188, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcPlayJeffVoiceLine__Boolean(bool isWin)
	{
		if (!(winVoiceLineSfx == null) && !(loseVoiceLineSfx == null))
		{
			if (isWin)
			{
				winVoiceLineSfx.PlayOneShotWith3DPos();
			}
			else
			{
				loseVoiceLineSfx.PlayOneShotWith3DPos();
			}
		}
	}

	protected static void InvokeUserCode_RpcPlayJeffVoiceLine__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcPlayJeffVoiceLine called on server.");
		}
		else
		{
			((CoinFlip)obj).UserCode_RpcPlayJeffVoiceLine__Boolean(reader.ReadBool());
		}
	}

	static CoinFlip()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(CoinFlip), "System.Void CoinFlip::RpcPlayJeffVoiceLine(System.Boolean)", InvokeUserCode_RpcPlayJeffVoiceLine__Boolean);
	}
}

}
