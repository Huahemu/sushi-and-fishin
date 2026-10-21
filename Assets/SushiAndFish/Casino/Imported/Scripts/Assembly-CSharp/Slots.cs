namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Diagnostics;
using global::System.Runtime.CompilerServices;
using global::DG.Tweening;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class Slots : GameBase
{
	private class SlotsWinData
	{
		public float Multiplier;

		public SlotPattern Pattern;

		public SlotsWinData(float multiplier, SlotPattern pattern)
		{
			Multiplier = multiplier;
			Pattern = pattern;
		}
	}

	[CompilerGenerated]
	private sealed class _003CSpinReelsCoroutine_003Ed__14 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public Slots _003C_003E4__this;

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
		public _003CSpinReelsCoroutine_003Ed__14(int _003C_003E1__state)
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
			Slots slots = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
			{
				_003C_003E1__state = -1;
				for (int i = 0; i < slots.reels.Count; i++)
				{
					float num2 = slots.spinDuration + (float)i * slots.delayBetweenReels;
					int turnCount = slots.spinCount + Mathf.RoundToInt((float)i * num2 / slots.spinDuration);
					int seed = slots.GetSeededRandom(i * 10000).Next(int.MinValue, int.MaxValue);
					slots.reels[i].ServerStartScrolling(num2, turnCount, seed);
					slots.RpcPlaySpinReelSFX(num2, slots.reels[i]);
				}
				_003C_003E2__current = new WaitForSeconds(slots.spinDuration + (float)(slots.reels.Count - 1) * slots.delayBetweenReels);
				_003C_003E1__state = 1;
				return true;
			}
			case 1:
				_003C_003E1__state = -1;
				slots.StartCoroutine(slots.EndGameRoutine());
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
	private TextMeshPro payoutText;

	[SerializeField]
	private MMF_Player payoutFb;

	[SerializeField]
	private List<SlotReel> reels;

	[SerializeField]
	private List<Image> symbolLocations;

	[SerializeField]
	private List<SlotPattern> patterns;

	[Header("Slots Settings")]
	[SerializeField]
	private float spinDuration = 5f;

	[SerializeField]
	private int spinCount = 15;

	[SerializeField]
	private float delayBetweenReels = 1f;

	[Header("SFX")]
	[SerializeField]
	private EventReference spinReelSFX;

	[SerializeField]
	private EventReference spinDoneSFX;

	[SerializeField]
	private EventReference multiplierSFX;

	[SerializeField]
	private EventReference finaltextSFX;

	private float _finalMultiplier;

	[Server]
	protected override void StartGame()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void Slots::StartGame()' called when server was not active");
			return;
		}
		base.StartGame();
		StartCoroutine(SpinReelsCoroutine());
	}

	[IteratorStateMachine(typeof(_003CSpinReelsCoroutine_003Ed__14))]
	[Server]
	private IEnumerator SpinReelsCoroutine()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator Slots::SpinReelsCoroutine()' called when server was not active");
			return null;
		}
		return new _003CSpinReelsCoroutine_003Ed__14(0)
		{
			_003C_003E4__this = this
		};
	}

	private IEnumerator EndGameRoutine()
	{
		RpcMultiplierAnimation(isEnabled: true, 0f);
		yield return new WaitForSeconds(0.5f);
		List<SlotsWinData> winningPatterns = GetWinningPatterns();
		foreach (SlotsWinData item in winningPatterns)
		{
			_finalMultiplier += item.Pattern.multiplier * item.Multiplier;
			RpcSetMultiplier(item.Pattern.GetPatternIndexes(), _finalMultiplier);
			yield return new WaitForSeconds(0.5f);
		}
		RpcMultiplierAnimation(isEnabled: false, _finalMultiplier);
		yield return new WaitForSeconds(1f);
		Payout((double)_finalMultiplier * base.EstimatedValue, ChangeType.GameResult, null, -1L);
		yield return new WaitForSeconds(1f);
		ResetGame();
	}

	protected override void ResetGame()
	{
		_finalMultiplier = 0f;
		foreach (SlotReel reel in reels)
		{
			reel.ServerReset();
		}
		base.ResetGame();
	}

	[ClientRpc]
	private void RpcSetMultiplier(List<int> pattern, float multiplier)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_System_002ECollections_002EGeneric_002EList_00601_003CSystem_002EInt32_003E(writer, pattern);
		writer.WriteFloat(multiplier);
		SendRPCInternal("System.Void Slots::RpcSetMultiplier(System.Collections.Generic.List`1<System.Int32>,System.Single)", -1876413610, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcMultiplierAnimation(bool isEnabled, float mult)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		writer.WriteFloat(mult);
		SendRPCInternal("System.Void Slots::RpcMultiplierAnimation(System.Boolean,System.Single)", -783525406, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private List<SlotsWinData> GetWinningPatterns()
	{
		List<SlotsWinData> list = new List<SlotsWinData>();
		List<int> list2 = new List<int>();
		foreach (SlotReel reel in reels)
		{
			list2.AddRange(reel.GetResult());
		}
		foreach (SlotPattern pattern in patterns)
		{
			List<int> patternIndexes = pattern.GetPatternIndexes();
			int num = list2[patternIndexes[0]];
			bool flag = true;
			foreach (int item in patternIndexes)
			{
				if (list2[item] != num)
				{
					flag = false;
				}
			}
			if (flag)
			{
				list.Add(new SlotsWinData(1f - ((float)num - 1.5f) * 0.5f, pattern));
				UnityEngine.Debug.LogWarning("pattern match: " + pattern);
			}
		}
		return list;
	}

	[ClientRpc]
	private void RpcPlaySpinReelSFX(float duration, SlotReel reel)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(duration);
		writer.WriteNetworkBehaviour(reel);
		SendRPCInternal("System.Void Slots::RpcPlaySpinReelSFX(System.Single,SlotReel)", 1195037631, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void PlaySpinDoneSFX()
	{
		SFXManager.SFXOneShot(spinDoneSFX, base.transform.position);
	}

	private void PlayMultiplierSFX(float mult)
	{
		SFXParams[] sFXParams = new SFXParams[1]
		{
			new SFXParams("Multiplier", Mathf.Clamp(mult, 0f, 1000f))
		};
		SFXManager.SFXOneShotWithParameters(multiplierSFX, sFXParams, base.transform.position);
	}

	private void PlayFinalTextSFX(float mult)
	{
		SFXParams[] sFXParams = new SFXParams[1]
		{
			new SFXParams("Multiplier", Mathf.Clamp(mult, 0f, 1000f))
		};
		SFXManager.SFXOneShotWithParameters(finaltextSFX, sFXParams, base.transform.position);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetMultiplier__List_00601__Single(List<int> pattern, float multiplier)
	{
		foreach (int item in pattern)
		{
			symbolLocations[item].transform.DOPunchScale(Vector3.one * 0.05f, 0.4f).SetEase(Ease.OutCubic);
			symbolLocations[item].DOColor(new Color(1f, 0.9f, 0.9f, 0f), 0.5f).From(new Color(1f, 0.9f, 0.9f, 1f)).SetEase(Ease.OutCubic);
		}
		payoutText.text = $"x{multiplier}\n";
		PlayMultiplierSFX(multiplier);
		Color endValue = UnityEngine.Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);
		payoutText.DOColor(endValue, 0.3f).SetEase(Ease.OutCubic);
		payoutFb.PlayFeedbacks();
	}

	protected static void InvokeUserCode_RpcSetMultiplier__List_00601__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcSetMultiplier called on server.");
		}
		else
		{
			((Slots)obj).UserCode_RpcSetMultiplier__List_00601__Single(GeneratedNetworkCode._Read_System_002ECollections_002EGeneric_002EList_00601_003CSystem_002EInt32_003E(reader), reader.ReadFloat());
		}
	}

	protected void UserCode_RpcMultiplierAnimation__Boolean__Single(bool isEnabled, float mult)
	{
		if (isEnabled)
		{
			PlayMultiplierSFX(mult);
			payoutText.color = Color.white;
			payoutText.text = $"x{mult}";
			payoutText.transform.DOScale(Vector3.one, 0.3f).From(Vector3.zero);
			return;
		}
		PlayFinalTextSFX(mult);
		if (mult < 10f)
		{
			payoutText.text = $"${(float)currentBet * mult}";
			payoutText.transform.DOPunchScale(Vector3.one * 0.5f, 0.5f, 1).OnComplete(() =>
			{
				payoutText.transform.DOScale(Vector3.zero, 0.3f).From(Vector3.one);
			});
			return;
		}
		payoutText.text = "Jackpot!\n" + $"${(float)currentBet * mult}";
		payoutText.transform.DOShakeScale(1.5f, new Vector3(0.02f, 0.02f, 0f), 20, 0f, fadeOut: false, ShakeRandomnessMode.Harmonic).OnComplete(() =>
		{
			payoutText.transform.DOScale(Vector3.zero, 0.3f).From(Vector3.one);
		});
		payoutText.rectTransform.DOShakeAnchorPos(1.5f, new Vector3(100f, 100f, 0f), 50, 90f, snapping: false, fadeOut: false, ShakeRandomnessMode.Harmonic);
	}

	protected static void InvokeUserCode_RpcMultiplierAnimation__Boolean__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcMultiplierAnimation called on server.");
		}
		else
		{
			((Slots)obj).UserCode_RpcMultiplierAnimation__Boolean__Single(reader.ReadBool(), reader.ReadFloat());
		}
	}

	protected void UserCode_RpcPlaySpinReelSFX__Single__SlotReel(float duration, SlotReel reel)
	{
		SFXParams[] sFXParams = new SFXParams[1]
		{
			new SFXParams("spinDuration", duration * 1000f)
		};
		SFXManager.SFXOneShotWithParameters(spinReelSFX, sFXParams, reel.transform.position);
		Invoke("PlaySpinDoneSFX", duration);
	}

	protected static void InvokeUserCode_RpcPlaySpinReelSFX__Single__SlotReel(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcPlaySpinReelSFX called on server.");
		}
		else
		{
			((Slots)obj).UserCode_RpcPlaySpinReelSFX__Single__SlotReel(reader.ReadFloat(), reader.ReadNetworkBehaviour<SlotReel>());
		}
	}

	static Slots()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(Slots), "System.Void Slots::RpcSetMultiplier(System.Collections.Generic.List`1<System.Int32>,System.Single)", InvokeUserCode_RpcSetMultiplier__List_00601__Single);
		RemoteProcedureCalls.RegisterRpc(typeof(Slots), "System.Void Slots::RpcMultiplierAnimation(System.Boolean,System.Single)", InvokeUserCode_RpcMultiplierAnimation__Boolean__Single);
		RemoteProcedureCalls.RegisterRpc(typeof(Slots), "System.Void Slots::RpcPlaySpinReelSFX(System.Single,SlotReel)", InvokeUserCode_RpcPlaySpinReelSFX__Single__SlotReel);
	}
}

}
