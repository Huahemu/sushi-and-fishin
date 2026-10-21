namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Runtime.InteropServices;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::TMPro;
using global::UnityEngine;

public class CasinoOffersManager : NetworkSingleton<CasinoOffersManager>
{
	[Header("Blackjack Offer Settings")]
	[SerializeField]
	public int blackjackGoalCountMax = 3;

	[SerializeField]
	[SyncVar(hook = "UpdateText")]
	public int currentBlackjackGoalCount;

	[SerializeField]
	public int blackjackMinHandRequirement = 18;

	[SerializeField]
	public int blackjackOfferReward = 1000;

	[SerializeField]
	private TextMeshProUGUI goalText;

	[Header("Slots Offer Settings")]
	[SerializeField]
	public int slotsMaxLoseForJackpot = 5;

	[SerializeField]
	[SyncVar(hook = "UpdateText")]
	public int currentLoseForJackpot;

	[SerializeField]
	[SyncVar(hook = "UpdateText")]
	public int slotsJackpotReward;

	public Action<int, int> _Mirror_SyncVarHookDelegate_currentBlackjackGoalCount;

	public Action<int, int> _Mirror_SyncVarHookDelegate_currentLoseForJackpot;

	public Action<int, int> _Mirror_SyncVarHookDelegate_slotsJackpotReward;

	public int NetworkcurrentBlackjackGoalCount
	{
		get
		{
			return currentBlackjackGoalCount;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref currentBlackjackGoalCount, 1uL, _Mirror_SyncVarHookDelegate_currentBlackjackGoalCount);
		}
	}

	public int NetworkcurrentLoseForJackpot
	{
		get
		{
			return currentLoseForJackpot;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref currentLoseForJackpot, 2uL, _Mirror_SyncVarHookDelegate_currentLoseForJackpot);
		}
	}

	public int NetworkslotsJackpotReward
	{
		get
		{
			return slotsJackpotReward;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref slotsJackpotReward, 4uL, _Mirror_SyncVarHookDelegate_slotsJackpotReward);
		}
	}

	private void Start()
	{
		UpdateText();
	}

	[Server]
	public void ServerCheckBlackjackOffer(bool isWin, int playerHand, int dealerHand)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void CasinoOffersManager::ServerCheckBlackjackOffer(System.Boolean,System.Int32,System.Int32)' called when server was not active");
		}
		else if (!isWin && playerHand >= blackjackMinHandRequirement && playerHand < 21)
		{
			NetworkcurrentBlackjackGoalCount = currentBlackjackGoalCount + 1;
			if (currentBlackjackGoalCount >= blackjackGoalCountMax)
			{
				NetworkcurrentBlackjackGoalCount = 0;
			}
		}
	}

	[Server]
	public void ServerCheckSlotsJackpotOffer(bool isWin, int betAmount)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void CasinoOffersManager::ServerCheckSlotsJackpotOffer(System.Boolean,System.Int32)' called when server was not active");
		}
		else if (!isWin)
		{
			NetworkcurrentLoseForJackpot = currentLoseForJackpot + 1;
			if (currentLoseForJackpot <= slotsMaxLoseForJackpot)
			{
				NetworkslotsJackpotReward = slotsJackpotReward + (betAmount + betAmount * currentLoseForJackpot);
				return;
			}
			NetworkcurrentLoseForJackpot = 0;
			NetworkslotsJackpotReward = 0;
		}
		else
		{
			_ = currentLoseForJackpot;
			_ = slotsMaxLoseForJackpot;
			NetworkcurrentLoseForJackpot = 0;
			NetworkslotsJackpotReward = 0;
		}
	}

	private void UpdateText(int oldValue = 0, int newValue = 0)
	{
		goalText.text = $"- [Blackjack] Lose {blackjackGoalCountMax} times ({currentBlackjackGoalCount}/{blackjackGoalCountMax}) with a hand of {blackjackMinHandRequirement}+, earn ${blackjackOfferReward}";
		goalText.text += $"\n- [Slots] Lose {slotsMaxLoseForJackpot} times consecutively ({currentLoseForJackpot}/{slotsMaxLoseForJackpot}) to increase the jackpot (${slotsJackpotReward})";
		goalText.DOFade(1f, 0.5f).SetEase(Ease.OutCubic).OnComplete(() =>
		{
			goalText.DOFade(0.1f, 2.5f).SetEase(Ease.OutCubic);
		});
	}

	public CasinoOffersManager()
	{
		_Mirror_SyncVarHookDelegate_currentBlackjackGoalCount = UpdateText;
		_Mirror_SyncVarHookDelegate_currentLoseForJackpot = UpdateText;
		_Mirror_SyncVarHookDelegate_slotsJackpotReward = UpdateText;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteVarInt(currentBlackjackGoalCount);
			writer.WriteVarInt(currentLoseForJackpot);
			writer.WriteVarInt(slotsJackpotReward);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteVarInt(currentBlackjackGoalCount);
		}
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteVarInt(currentLoseForJackpot);
		}
		if ((syncVarDirtyBits & 4L) != 0L)
		{
			writer.WriteVarInt(slotsJackpotReward);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref currentBlackjackGoalCount, _Mirror_SyncVarHookDelegate_currentBlackjackGoalCount, reader.ReadVarInt());
			GeneratedSyncVarDeserialize(ref currentLoseForJackpot, _Mirror_SyncVarHookDelegate_currentLoseForJackpot, reader.ReadVarInt());
			GeneratedSyncVarDeserialize(ref slotsJackpotReward, _Mirror_SyncVarHookDelegate_slotsJackpotReward, reader.ReadVarInt());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref currentBlackjackGoalCount, _Mirror_SyncVarHookDelegate_currentBlackjackGoalCount, reader.ReadVarInt());
		}
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref currentLoseForJackpot, _Mirror_SyncVarHookDelegate_currentLoseForJackpot, reader.ReadVarInt());
		}
		if ((num & 4L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref slotsJackpotReward, _Mirror_SyncVarHookDelegate_slotsJackpotReward, reader.ReadVarInt());
		}
	}
}

}
