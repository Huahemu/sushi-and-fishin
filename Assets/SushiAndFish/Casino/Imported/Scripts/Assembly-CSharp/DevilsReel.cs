namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;

public class DevilsReel : ConsumableItem
{
	[SerializeField]
	private TextMeshPro screenText;

	[SerializeField]
	private float chanceOfWin;

	[SerializeField]
	private NetworkAnimator anim;

	[SerializeField]
	private ParticleSystem spinVfxGood;

	[SerializeField]
	private ParticleSystem spinVfxBad;

	private bool _isSpinning;

	[SyncVar]
	private long _lastProfit;

	private PlayerProfile _holderProfile;

	[Header("SFX")]
	[SerializeField]
	private SFXComponent spinSfx;

	[SerializeField]
	private SFXComponent numChangeSfx;

	[SerializeField]
	private SFXComponent winSfx;

	[SerializeField]
	private SFXComponent loseSfx;

	[SerializeField]
	private SFXComponent destroySfx;

	[SerializeField]
	private SFXComponent invalidSfx;

	public long Network_lastProfit
	{
		get
		{
			return _lastProfit;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _lastProfit, 2uL, null);
		}
	}

	protected override void SubscribeToEvents(bool isSubscribed)
	{
		base.SubscribeToEvents(isSubscribed);
		if (isSubscribed)
		{
			NetworkSingleton<GameResultsManager>.Instance.OnResultRegistered += OnResultRegistered;
		}
		else
		{
			NetworkSingleton<GameResultsManager>.Instance.OnResultRegistered -= OnResultRegistered;
		}
	}

	private void OnResultRegistered(long bet, long payout, PlayerProfile playerProfile, CasinoGameType gameType, Vector3 position, bool hadTipsyFortune, bool hadInspiringMelody, bool hadImmunity, Dictionary<string, object> gameSpecificData)
	{
		if (base.isServer && (bool)base.NetworkHolder && !(_holderProfile != playerProfile) && NetworkSingleton<GameResultsManager>.Instance.lastResults.TryGetValue(_holderProfile, out var value))
		{
			Network_lastProfit = value.NetProfit;
			string text = MoneyFormatter.FormatWithDollar((long)((double)(_lastProfit * 2) * (double)NetworkSingleton<UpgradeManager>.Instance.GetUpgradeData(_holderProfile.steamId, PlayerUpgradeType.Stakeholder)));
			string text2 = MoneyFormatter.FormatWithDollar(_lastProfit);
			if (_lastProfit > 0)
			{
				RpcSetText("-" + text2 + " | +" + text, punch: false);
			}
			else
			{
				RpcSetText("-", punch: false);
			}
		}
	}

	protected override void OnPickedUp(PlayerInventory playerInventory)
	{
		base.OnPickedUp(playerInventory);
		if (_isSpinning)
		{
			return;
		}
		_holderProfile = playerInventory.GetComponent<PlayerProfile>();
		if (NetworkSingleton<GameResultsManager>.Instance.lastResults.TryGetValue(_holderProfile, out var value))
		{
			Network_lastProfit = value.NetProfit;
			string text = MoneyFormatter.FormatWithDollar((long)((double)(_lastProfit * 2) * (double)NetworkSingleton<UpgradeManager>.Instance.GetUpgradeData(_holderProfile.steamId, PlayerUpgradeType.Stakeholder)));
			string text2 = MoneyFormatter.FormatWithDollar(_lastProfit);
			if (_lastProfit > 0)
			{
				RpcSetText("-" + text2 + " | +" + text, punch: false);
			}
			else
			{
				RpcSetText("-", punch: false);
			}
		}
	}

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		base.OnDropped(playerInventory);
		if (!_isSpinning)
		{
			_holderProfile = null;
			Network_lastProfit = 0L;
			RpcSetText("-", punch: false);
		}
	}

	protected override void OnUseItem(bool isPressed)
	{
		base.OnUseItem(isPressed);
		if (!isPressed || _isSpinning)
		{
			return;
		}
		if (_lastProfit <= 0)
		{
			PlayUnableToUseFeedback();
			return;
		}
		if (NetworkSingleton<MoneyManager>.Instance.balance < _lastProfit)
		{
			PlayUnableToUseFeedback();
			return;
		}
		_isSpinning = true;
		if (base.isServer)
		{
			StartCoroutine(SpinRoutine());
		}
	}

	private IEnumerator SpinRoutine()
	{
		anim.SetTrigger("Spin");
		yield return new WaitForSeconds(1f);
		spinSfx.RpcPlayOneShotAttached();
		float totalDuration = 3f;
		float minInterval = 0.1f;
		float maxInterval = 0.35f;
		PlayerProfile holder = _holderProfile;
		long lose = -_lastProfit;
		long win = (long)((double)(_lastProfit * 2) * (double)NetworkSingleton<UpgradeManager>.Instance.GetUpgradeData(holder.steamId, PlayerUpgradeType.Stakeholder));
		float interval;
		for (float elapsed = 0f; elapsed < totalDuration; elapsed += interval)
		{
			float num = elapsed / totalDuration;
			float t = 1f - Mathf.Pow(1f - num, 3f);
			interval = Mathf.Lerp(minInterval, maxInterval, t);
			bool num2 = UnityEngine.Random.value > 0.5f;
			long amount = (num2 ? win : lose);
			RpcSetText(MoneyFormatter.FormatWithDollar(amount), punch: true);
			if (num2)
			{
				RpcPlayVFX(isWin: true, isEnd: false);
			}
			else
			{
				RpcPlayVFX(isWin: false, isEnd: false);
			}
			numChangeSfx.RpcPlayOneShotAttached();
			yield return new WaitForSeconds(interval);
		}
		bool flag = (float)GetSeededRandom().NextDouble() < chanceOfWin;
		long finalValue = (flag ? win : lose);
		RpcSetText(MoneyFormatter.FormatWithDollar(finalValue), punch: true);
		if (flag)
		{
			winSfx.RpcPlayOneShotAttached();
			RpcPlayVFX(isWin: true, isEnd: true);
		}
		else
		{
			loseSfx.RpcPlayOneShotAttached();
			RpcPlayVFX(isWin: false, isEnd: true);
		}
		yield return new WaitForSeconds(1f);
		NetworkSingleton<MoneyManager>.Instance.TryChangeBalance(finalValue, holder, ChangeType.Misc);
		destroySfx.RpcPlayOneShotWith3DPos();
		DestroyItem();
	}

	[ClientRpc]
	private void RpcSetText(string text, bool punch)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(text);
		writer.WriteBool(punch);
		SendRPCInternal("System.Void DevilsReel::RpcSetText(System.String,System.Boolean)", -1982567001, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcPlayVFX(bool isWin, bool isEnd)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isWin);
		writer.WriteBool(isEnd);
		SendRPCInternal("System.Void DevilsReel::RpcPlayVFX(System.Boolean,System.Boolean)", 751801301, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void PlayUnableToUseFeedback()
	{
		invalidSfx.PlayOneShotAttached();
		screenText.transform.DOPunchScale(screenText.transform.localScale * 0.5f, 0.1f, 1);
	}

	private global::System.Random GetSeededRandom()
	{
		if (!NetworkSingleton<SeededRandomManager>.Instance || !NetworkSingleton<GameManager>.Instance)
		{
			return new global::System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
		}
		int currentSeed = NetworkSingleton<SeededRandomManager>.Instance.CurrentSeed;
		int daysPassed = NetworkSingleton<GameManager>.Instance.daysPassed;
		int devilsReelCounter = NetworkSingleton<SeededRandomManager>.Instance.DevilsReelCounter;
		long num = (((currentSeed * 2654435761u + daysPassed) * 2654435761u + devilsReelCounter) * 2654435761u) ^ (devilsReelCounter << 13) ^ (devilsReelCounter >> 7);
		long num2 = (num ^ (num >> 32)) * 2246822507u;
		long num3 = (num2 ^ (num2 >> 16)) * 3266489917u;
		return new global::System.Random((int)(num3 ^ (num3 >> 13)));
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetText__String__Boolean(string text, bool punch)
	{
		screenText.text = text;
		if (punch)
		{
			screenText.transform.DOPunchScale(screenText.transform.localScale * 0.2f, 0.1f, 1, 0f);
		}
	}

	protected static void InvokeUserCode_RpcSetText__String__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetText called on server.");
		}
		else
		{
			((DevilsReel)obj).UserCode_RpcSetText__String__Boolean(reader.ReadString(), reader.ReadBool());
		}
	}

	protected void UserCode_RpcPlayVFX__Boolean__Boolean(bool isWin, bool isEnd)
	{
		ParticleSystem particleSystem = spinVfxGood;
		if (!isWin)
		{
			particleSystem = spinVfxBad;
		}
		if (isEnd)
		{
			ParticleSystem.MainModule main = particleSystem.main;
			main.duration = 1f;
			main.startLifetime = 1f;
			ParticleSystem.MainModule main2 = particleSystem.transform.GetChild(0).GetComponent<ParticleSystem>().main;
			main2.duration = 1f;
		}
		particleSystem.Play();
	}

	protected static void InvokeUserCode_RpcPlayVFX__Boolean__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayVFX called on server.");
		}
		else
		{
			((DevilsReel)obj).UserCode_RpcPlayVFX__Boolean__Boolean(reader.ReadBool(), reader.ReadBool());
		}
	}

	static DevilsReel()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(DevilsReel), "System.Void DevilsReel::RpcSetText(System.String,System.Boolean)", InvokeUserCode_RpcSetText__String__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(DevilsReel), "System.Void DevilsReel::RpcPlayVFX(System.Boolean,System.Boolean)", InvokeUserCode_RpcPlayVFX__Boolean__Boolean);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteVarLong(_lastProfit);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteVarLong(_lastProfit);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _lastProfit, null, reader.ReadVarLong());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _lastProfit, null, reader.ReadVarLong());
		}
	}
}

}
