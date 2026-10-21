using System;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.UI;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Economy
{
	public partial class EconomyHandler : NetworkBehaviour
	{
		[SerializeField]
		private EconomyUI _economyUI;

		[SerializeField]
		private AudioClipSO _earnCoinSfx;

		[SerializeField]
		private AudioClipSO _spendCoinSfx;

		[SerializeField]
		private AudioClipSO _earnScrapSfx;

		[SerializeField]
		private int _divingSuitLevelTwoMoneyCostDollars;

		[SerializeField]
		private int _divingSuitLevelTwoScrapCost;

		[SerializeField]
		private int _conveyorRepairCostDollars;

		[NonSerialized]
		public NetworkVariable<int> ShopBalance = new NetworkVariable<int>(0);

		[NonSerialized]
		public NetworkVariable<int> ScrapBalance = new NetworkVariable<int>(0);

		private const string LOG = "<b><color=#8FD14F>[Economy]</color></b>";

		private const string WARN = "<b><color=#E57373>[Economy]</color></b>";

		private const int MONEY_MULTIPLIER = 100;

		public EconomyUI EconomyUI => _economyUI;

		private bool CanWriteQuestRequirements
		{
			get
			{
				if (!(NetworkManager.Singleton == null) && NetworkManager.Singleton.IsListening)
				{
					return base.IsServer;
				}
				return true;
			}
		}

		public void SetBalance(int money, int scrap)
		{
			UtilityMethods.Log($"[EconomyHandler] SetBalance Money:{money} - Scrap:{scrap}");
			SetExactGold(money == int.MaxValue ? 0 : money, money == int.MaxValue);
			ScrapBalance.Value = scrap;
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
            InitializeExactGold();
			UtilityMethods.Log($"[EconomyHandler] OnNetworkSpawn Money:{ShopBalance.Value} - Scrap:{ScrapBalance.Value}");
			NetworkVariable<int> shopBalance = ShopBalance;
			shopBalance.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(shopBalance.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnShopBalanceChanged));
			NetworkVariable<int> scrapBalance = ScrapBalance;
			scrapBalance.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(scrapBalance.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnScrapBalanceChanged));
			ServiceLocator.ForSceneOf(this).Register(typeof(EconomyHandler), this);
			UpdateExactGoldHud();
			_economyUI.ScrapUpdate(ScrapBalance.Value);
			RefreshEconomyQuestRequirements();
		}

		public override void OnNetworkDespawn()
		{
            DisposeExactGold();
			NetworkVariable<int> shopBalance = ShopBalance;
			shopBalance.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(shopBalance.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnShopBalanceChanged));
			NetworkVariable<int> scrapBalance = ScrapBalance;
			scrapBalance.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(scrapBalance.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnScrapBalanceChanged));
			base.OnNetworkDespawn();
		}

		private void OnShopBalanceChanged(int prev, int next)
		{
			UtilityMethods.Log($"[EconomyHandler] OnShopBalanceChanged prev:{prev} - next:{next}");
			AcceptLegacyGoldWrite(next);
            UpdateExactGoldHud();
			RefreshMoneyQuestRequirements(next);
		}

		private void OnScrapBalanceChanged(int prev, int next)
		{
			UtilityMethods.Log($"[EconomyHandler] OnScrapBalanceChanged prev:{prev} - next:{next}");
			_economyUI?.ScrapUpdate(next);
			RefreshScrapQuestRequirements(next);
		}

		public void RefreshEconomyQuestRequirements()
		{
			RefreshMoneyQuestRequirements(ShopBalance.Value);
			RefreshScrapQuestRequirements(ScrapBalance.Value);
		}

		private void RefreshMoneyQuestRequirements(int currentMoney)
		{
			if (!CanWriteQuestRequirements)
			{
				return;
			}
			QuestReferenceHub instance = QuestReferenceHub.Instance;
			if (!(instance == null))
			{
				int num = currentMoney / 100;
				if (num > 0)
				{
					instance.SetProgressAtLeast(QuestId.EarnOneThousandDollars, num);
					instance.SetProgressAtLeast(QuestId.EarnThreeThousandDollars, num);
				}
			}
		}

		private void RefreshScrapQuestRequirements(int currentScrap)
		{
			if (!CanWriteQuestRequirements)
			{
				return;
			}
			QuestReferenceHub instance = QuestReferenceHub.Instance;
			if (!(instance == null))
			{
				int num = currentScrap / 100;
				if (num > 0)
				{
					instance.SetProgressAtLeast(QuestId.CollectScrapForDivingSuit, num);
				}
				if (currentScrap >= _divingSuitLevelTwoScrapCost * 100)
				{
					instance.CompleteIfActive(QuestId.CollectScrapForDivingSuit);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerEarnMoneyRpc(int amount)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3670956760u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, amount);
				__endSendRpc(ref bufferWriter, 3670956760u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (amount <= 0)
				{
					UtilityMethods.Log(string.Format("{0} Earn ignored. Amount must be > 0 (got {1}).", "<b><color=#E57373>[Economy]</color></b>", amount));
					return;
				}
				ServerTryChangeStoredGold(amount);
				UtilityMethods.Log(string.Format("{0} Earned +{1}. <b>{2}</b>", "<b><color=#8FD14F>[Economy]</color></b>", amount, ShopBalance.Value));
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerSpendMoneyRpc(int amount, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(918770312u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, amount);
				__endSendRpc(ref bufferWriter, 918770312u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (amount <= 0)
				{
					UtilityMethods.Log(string.Format("{0} Spend ignored. Amount must be > 0 (got {1}).", "<b><color=#E57373>[Economy]</color></b>", amount));
					return;
				}
				if (!CanSpendStoredGold(amount))
				{
					UtilityMethods.Log(string.Format("{0} Insufficient funds. Need {1}, have {2}.", "<b><color=#E57373>[Economy]</color></b>", amount, ShopBalance.Value));
					return;
				}
				ServerTryChangeStoredGold(-((decimal)amount));
				GrantMoneySpentProgressionXp(amount, rpcParams.Receive.SenderClientId);
				UtilityMethods.Log(string.Format("{0} Spent -{1}. <b>{2}</b>", "<b><color=#8FD14F>[Economy]</color></b>", amount, ShopBalance.Value));
			}
		}

		private void GrantMoneySpentProgressionXp(int spentStoredAmount, ulong? feedbackClientId = null, bool staffPurchase = false)
		{
			int num = spentStoredAmount / 100;
			if (num > 0)
			{
				ProgressionManager progressionManager = Singleton<GameProgressManager>.Instance?.ProgressionManager;
				if (staffPurchase)
				{
					progressionManager?.ServerGrantStaffMoneySpentXp(num, "money_spent");
				}
				else
				{
					progressionManager?.ServerGrantMoneySpentXp(num, "money_spent", feedbackClientId);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerEarnScrapRpc(int amount)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1042822047u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, amount);
				__endSendRpc(ref bufferWriter, 1042822047u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (amount <= 0)
				{
					UtilityMethods.Log(string.Format("{0} Scrap earn ignored. Amount must be > 0 (got {1}).", "<b><color=#E57373>[Economy]</color></b>", amount));
					return;
				}
				ScrapBalance.Value += amount;
				UtilityMethods.Log(string.Format("{0} Scrap +{1}. <b>{2}</b>", "<b><color=#8FD14F>[Economy]</color></b>", amount, ScrapBalance.Value));
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerSpendScrapRpc(int amount)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(794075632u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, amount);
				__endSendRpc(ref bufferWriter, 794075632u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (amount <= 0)
				{
					UtilityMethods.Log(string.Format("{0} Scrap spend ignored. Amount must be > 0 (got {1}).", "<b><color=#E57373>[Economy]</color></b>", amount));
					return;
				}
				if (ScrapBalance.Value < amount)
				{
					UtilityMethods.Log(string.Format("{0} Not enough scrap. Need {1}, have {2}.", "<b><color=#E57373>[Economy]</color></b>", amount, ScrapBalance.Value));
					return;
				}
				ScrapBalance.Value -= amount;
				UtilityMethods.Log(string.Format("{0} Scrap -{1}. <b>{2}</b>", "<b><color=#8FD14F>[Economy]</color></b>", amount, ScrapBalance.Value));
			}
		}

		public void EarnMoney(int amount, ulong clientID = ulong.MaxValue)
		{
			int num = amount * 100;
			ServerEarnMoneyRpc(num);
			if (num > 0)
			{
				UnlockFirstMoneyAchievement(clientID);
			}
			ServerPlayEarnMoneyFeedbackRpc(num, clientID);
		}

		public void SpendMoney(int amount)
		{
			if (amount * 100 > ShopBalance.Value)
			{
				_economyUI.FlashShopInsufficient();
			}
			else
			{
				ServerSpendMoneyRpc(amount * 100);
			}
		}

		public void EarnScrap(int amount, ulong clientID = ulong.MaxValue)
		{
			int num = amount * 100;
			ServerEarnScrapRpc(num);
			ServerPlayEarnScrapFeedbackRpc(num, clientID);
		}

		public void SpendScrap(int amount)
		{
			if (amount * 100 > ScrapBalance.Value)
			{
				_economyUI.FlashScrapInsufficient();
			}
			else
			{
				ServerSpendScrapRpc(amount * 100);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerPlayEarnMoneyFeedbackRpc(int storedAmount, ulong clientId)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1015357547u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, storedAmount);
				BytePacker.WriteValueBitPacked(bufferWriter, clientId);
				__endSendRpc(ref bufferWriter, 1015357547u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (storedAmount > 0)
			{
				if (clientId == ulong.MaxValue)
				{
					ClientPlayEarnMoneyFeedbackEveryoneRpc(storedAmount);
				}
				else
				{
					ClientPlayEarnMoneyFeedbackRpc(storedAmount, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerPlayEarnScrapFeedbackRpc(int storedAmount, ulong clientId)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2911805794u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, storedAmount);
				BytePacker.WriteValueBitPacked(bufferWriter, clientId);
				__endSendRpc(ref bufferWriter, 2911805794u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (storedAmount > 0)
			{
				if (clientId == ulong.MaxValue)
				{
					ClientPlayEarnScrapFeedbackEveryoneRpc(storedAmount);
				}
				else
				{
					ClientPlayEarnScrapFeedbackRpc(storedAmount, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPlayEarnMoneyFeedbackEveryoneRpc(int storedAmount)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3110032479u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, storedAmount);
				__endSendRpc(ref bufferWriter, 3110032479u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlayEarnMoneyFeedbackLocal(storedAmount);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPlayEarnScrapFeedbackEveryoneRpc(int storedAmount)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(13191110u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, storedAmount);
				__endSendRpc(ref bufferWriter, 13191110u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlayEarnScrapFeedbackLocal(storedAmount);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientPlayEarnMoneyFeedbackRpc(int storedAmount, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(4260540246u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, storedAmount);
				__endSendRpc(ref bufferWriter, 4260540246u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlayEarnMoneyFeedbackLocal(storedAmount);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientPlayEarnScrapFeedbackRpc(int storedAmount, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1088902707u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, storedAmount);
				__endSendRpc(ref bufferWriter, 1088902707u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlayEarnScrapFeedbackLocal(storedAmount);
			}
		}

		private void PlayEarnMoneyFeedbackLocal(int storedAmount)
		{
			if (_economyUI != null)
			{
				_economyUI.PlayMoneyGainFeedback(storedAmount, PlayEarnMoneySfxLocal);
			}
			else
			{
				PlayEarnMoneySfxLocal();
			}
		}

		private void PlayEarnScrapFeedbackLocal(int storedAmount)
		{
			if (_economyUI != null)
			{
				_economyUI.PlayScrapGainFeedback(storedAmount, PlayEarnScrapSfxLocal);
			}
			else
			{
				PlayEarnScrapSfxLocal();
			}
		}

		private void PlayEarnMoneySfxLocal()
		{
			if (!(Singleton<SoundManager>.Instance == null) && !(_earnCoinSfx == null))
			{
				Singleton<SoundManager>.Instance.PlaySFX(_earnCoinSfx);
			}
		}

		private void PlaySpendMoneyFeedbackLocal(int storedAmount)
		{
			if (_economyUI != null)
			{
				_economyUI.PlayMoneySpendFeedback(storedAmount, PlaySpendMoneySfxLocal);
			}
			else
			{
				PlaySpendMoneySfxLocal();
			}
		}

		private void PlaySpendMoneySfxLocal()
		{
			AudioClipSO audioClipSO = ((_spendCoinSfx != null) ? _spendCoinSfx : _earnCoinSfx);
			if (!(Singleton<SoundManager>.Instance == null) && !(audioClipSO == null))
			{
				Singleton<SoundManager>.Instance.PlaySFX(audioClipSO);
			}
		}

		private void PlayEarnScrapSfxLocal()
		{
			if (!(Singleton<SoundManager>.Instance == null) && !(_earnScrapSfx == null))
			{
				Singleton<SoundManager>.Instance.PlaySFX(_earnScrapSfx);
			}
		}

		public bool HasSufficientBalance(int amount)
		{
			return CanSpendStoredGold((decimal)amount * 100m);
		}

		public bool HasSufficientScrap(int amount)
		{
			return ScrapBalance.Value >= amount * 100;
		}

		public bool ServerStaffEarn(int dollars)
		{
			if (!base.IsServer || dollars < 0)
			{
				return false;
			}
			if (!MoneyHelper.IsInfinite(ShopBalance.Value))
			{
				ServerTryChangeStoredGold((decimal)dollars * 100m);
			}
			if (dollars > 0)
			{
				SendEarnMoneyFeedback(dollars * 100, ulong.MaxValue);
			}
			return true;
		}

		public bool ServerTryStaffSpend(int storedAmount, bool purchase, out double awardedXp)
		{
			awardedXp = 0.0;
			if (!base.IsServer || storedAmount <= 0 || !CanSpendStoredGold(storedAmount))
			{
				return false;
			}
			ProgressionManager progressionManager = Singleton<GameProgressManager>.Instance?.ProgressionManager;
			double num = progressionManager?.TotalXp ?? 0.0;
			if (!MoneyHelper.IsInfinite(ShopBalance.Value))
			{
				ServerTryChangeStoredGold(-((decimal)storedAmount));
			}
			if (purchase)
			{
				GrantMoneySpentProgressionXp(storedAmount, null, staffPurchase: true);
			}
			awardedXp = (progressionManager?.TotalXp ?? num) - num;
			return true;
		}

		public bool ServerTrySpendStoredMoney(int storedAmount, ulong feedbackClientId = ulong.MaxValue)
		{
			if (!base.IsServer || storedAmount <= 0 || !CanSpendStoredGold(storedAmount))
			{
				return false;
			}
			ServerTryChangeStoredGold(-((decimal)storedAmount));
			GrantMoneySpentProgressionXp(storedAmount);
			SendSpendMoneyFeedback(storedAmount, feedbackClientId);
			UtilityMethods.Log(string.Format("{0} Server transaction spent -{1}. <b>{2}</b>", "<b><color=#8FD14F>[Economy]</color></b>", storedAmount, ShopBalance.Value));
			return true;
		}

		public bool ServerTryEarnStoredMoney(int storedAmount, ulong feedbackClientId = ulong.MaxValue)
		{
			if (!base.IsServer || storedAmount <= 0)
			{
				return false;
			}
			ServerTryChangeStoredGold(storedAmount);
			UnlockFirstMoneyAchievement(feedbackClientId);
			SendEarnMoneyFeedback(storedAmount, feedbackClientId);
			UtilityMethods.Log(string.Format("{0} Server transaction earned +{1}. <b>{2}</b>", "<b><color=#8FD14F>[Economy]</color></b>", storedAmount, ShopBalance.Value));
			return true;
		}

		private void UnlockFirstMoneyAchievement(ulong clientId)
		{
			Singleton<GameProgressManager>.Instance?.RpcRelay?.ServerUnlockSteamAchievementForClient("ACH_FIRST_MONEY_EARNED", clientId);
		}

		private void SendSpendMoneyFeedback(int storedAmount, ulong clientId)
		{
			if (clientId == ulong.MaxValue)
			{
				ClientPlaySpendMoneyFeedbackEveryoneRpc(storedAmount);
			}
			else
			{
				ClientPlaySpendMoneyFeedbackRpc(storedAmount, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
			}
		}

		private void SendEarnMoneyFeedback(int storedAmount, ulong clientId)
		{
			if (clientId == ulong.MaxValue)
			{
				ClientPlayEarnMoneyFeedbackEveryoneRpc(storedAmount);
			}
			else
			{
				ClientPlayEarnMoneyFeedbackRpc(storedAmount, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPlaySpendMoneyFeedbackEveryoneRpc(int storedAmount)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1071354877u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, storedAmount);
				__endSendRpc(ref bufferWriter, 1071354877u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlaySpendMoneyFeedbackLocal(storedAmount);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientPlaySpendMoneyFeedbackRpc(int storedAmount, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(154141791u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, storedAmount);
				__endSendRpc(ref bufferWriter, 154141791u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlaySpendMoneyFeedbackLocal(storedAmount);
			}
		}

		protected override void __initializeVariables()
		{
			if (ShopBalance == null)
			{
				throw new Exception("EconomyHandler.ShopBalance cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			ShopBalance.Initialize(this);
			__nameNetworkVariable(ShopBalance, "ShopBalance");
			NetworkVariableFields.Add(ShopBalance);
			if (ScrapBalance == null)
			{
				throw new Exception("EconomyHandler.ScrapBalance cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			ScrapBalance.Initialize(this);
			__nameNetworkVariable(ScrapBalance, "ScrapBalance");
			NetworkVariableFields.Add(ScrapBalance);
            ExactGoldStorage.Initialize(this);
            __nameNetworkVariable(ExactGoldStorage, "ExactGoldStorage");
            NetworkVariableFields.Add(ExactGoldStorage);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3670956760u, __rpc_handler_3670956760, "ServerEarnMoneyRpc", RpcInvokePermission.Everyone);
			__registerRpc(918770312u, __rpc_handler_918770312, "ServerSpendMoneyRpc", RpcInvokePermission.Everyone);
			__registerRpc(1042822047u, __rpc_handler_1042822047, "ServerEarnScrapRpc", RpcInvokePermission.Everyone);
			__registerRpc(794075632u, __rpc_handler_794075632, "ServerSpendScrapRpc", RpcInvokePermission.Everyone);
			__registerRpc(1015357547u, __rpc_handler_1015357547, "ServerPlayEarnMoneyFeedbackRpc", RpcInvokePermission.Everyone);
			__registerRpc(2911805794u, __rpc_handler_2911805794, "ServerPlayEarnScrapFeedbackRpc", RpcInvokePermission.Everyone);
			__registerRpc(3110032479u, __rpc_handler_3110032479, "ClientPlayEarnMoneyFeedbackEveryoneRpc", RpcInvokePermission.Everyone);
			__registerRpc(13191110u, __rpc_handler_13191110, "ClientPlayEarnScrapFeedbackEveryoneRpc", RpcInvokePermission.Everyone);
			__registerRpc(4260540246u, __rpc_handler_4260540246, "ClientPlayEarnMoneyFeedbackRpc", RpcInvokePermission.Everyone);
			__registerRpc(1088902707u, __rpc_handler_1088902707, "ClientPlayEarnScrapFeedbackRpc", RpcInvokePermission.Everyone);
			__registerRpc(1071354877u, __rpc_handler_1071354877, "ClientPlaySpendMoneyFeedbackEveryoneRpc", RpcInvokePermission.Everyone);
			__registerRpc(154141791u, __rpc_handler_154141791, "ClientPlaySpendMoneyFeedbackRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3670956760(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((EconomyHandler)target).ServerEarnMoneyRpc(value);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_918770312(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				RpcParams ext = rpcParams.Ext;
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((EconomyHandler)target).ServerSpendMoneyRpc(value, ext);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1042822047(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((EconomyHandler)target).ServerEarnScrapRpc(value);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_794075632(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((EconomyHandler)target).ServerSpendScrapRpc(value);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1015357547(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((EconomyHandler)target).ServerPlayEarnMoneyFeedbackRpc(value, value2);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2911805794(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((EconomyHandler)target).ServerPlayEarnScrapFeedbackRpc(value, value2);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3110032479(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((EconomyHandler)target).ClientPlayEarnMoneyFeedbackEveryoneRpc(value);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_13191110(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((EconomyHandler)target).ClientPlayEarnScrapFeedbackEveryoneRpc(value);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4260540246(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				RpcParams ext = rpcParams.Ext;
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((EconomyHandler)target).ClientPlayEarnMoneyFeedbackRpc(value, ext);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1088902707(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				RpcParams ext = rpcParams.Ext;
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((EconomyHandler)target).ClientPlayEarnScrapFeedbackRpc(value, ext);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1071354877(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((EconomyHandler)target).ClientPlaySpendMoneyFeedbackEveryoneRpc(value);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_154141791(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				RpcParams ext = rpcParams.Ext;
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((EconomyHandler)target).ClientPlaySpendMoneyFeedbackRpc(value, ext);
				((EconomyHandler)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "EconomyHandler";
		}
	}
}
