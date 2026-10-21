namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Runtime.InteropServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::TMPro;
using global::UnityEngine;

public class Bank : NetworkBehaviour
{
	[Header("References")]
	[SerializeField]
	private BankKnob knob;

	[SerializeField]
	private TextMeshPro bankBalanceLabel;

	[SerializeField]
	private TextMeshPro selectedAmountLabel;

	[SerializeField]
	private TextMeshPro modeLabel;

	[SerializeField]
	private TextMeshPro currentInterestLabel;

	[SerializeField]
	private TextMeshPro lastModificationLabel;

	[SerializeField]
	private string currencyFormat = "${0}";

	[Header("Bank Settings")]
	[SerializeField]
	private int stepAmount = 1;

	[Header("Stock Market Settings")]
	[Tooltip("The tier configuration for this bank")]
	[SerializeField]
	private BankTier bankTier;

	[SyncVar(hook = "OnBankTierNumberChanged")]
	private int bankTierNumber = 1;

	[SyncVar(hook = "OnBankBalanceChanged")]
	private long bankBalance;

	[SyncVar(hook = "OnModeChanged")]
	private BankMode currentMode;

	[SyncVar(hook = "OnSelectedAmountChanged")]
	private long selectedAmount;

	[SyncVar(hook = "OnLastModificationTimeChanged")]
	private float lastModificationTime;

	[SyncVar(hook = "OnNextModificationTimeChanged")]
	private float nextModificationTime;

	[SyncVar(hook = "OnLastModificationPercentChanged")]
	private float lastModificationPercent;

	[SyncVar(hook = "OnLastDepositTimeChanged")]
	private float lastDepositTime;

	public Action<long> OnBankBalanceChangedEvent;

	public Action<BankMode> OnModeChangedEvent;

	public Action<long> OnSelectedAmountChangedEvent;

	public Action<int, int> _Mirror_SyncVarHookDelegate_bankTierNumber;

	public Action<long, long> _Mirror_SyncVarHookDelegate_bankBalance;

	public Action<BankMode, BankMode> _Mirror_SyncVarHookDelegate_currentMode;

	public Action<long, long> _Mirror_SyncVarHookDelegate_selectedAmount;

	public Action<float, float> _Mirror_SyncVarHookDelegate_lastModificationTime;

	public Action<float, float> _Mirror_SyncVarHookDelegate_nextModificationTime;

	public Action<float, float> _Mirror_SyncVarHookDelegate_lastModificationPercent;

	public Action<float, float> _Mirror_SyncVarHookDelegate_lastDepositTime;

	public int NetworkbankTierNumber
	{
		get
		{
			return bankTierNumber;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref bankTierNumber, 1uL, _Mirror_SyncVarHookDelegate_bankTierNumber);
		}
	}

	public long NetworkbankBalance
	{
		get
		{
			return bankBalance;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref bankBalance, 2uL, _Mirror_SyncVarHookDelegate_bankBalance);
		}
	}

	public BankMode NetworkcurrentMode
	{
		get
		{
			return currentMode;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref currentMode, 4uL, _Mirror_SyncVarHookDelegate_currentMode);
		}
	}

	public long NetworkselectedAmount
	{
		get
		{
			return selectedAmount;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref selectedAmount, 8uL, _Mirror_SyncVarHookDelegate_selectedAmount);
		}
	}

	public float NetworklastModificationTime
	{
		get
		{
			return lastModificationTime;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref lastModificationTime, 16uL, _Mirror_SyncVarHookDelegate_lastModificationTime);
		}
	}

	public float NetworknextModificationTime
	{
		get
		{
			return nextModificationTime;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref nextModificationTime, 32uL, _Mirror_SyncVarHookDelegate_nextModificationTime);
		}
	}

	public float NetworklastModificationPercent
	{
		get
		{
			return lastModificationPercent;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref lastModificationPercent, 64uL, _Mirror_SyncVarHookDelegate_lastModificationPercent);
		}
	}

	public float NetworklastDepositTime
	{
		get
		{
			return lastDepositTime;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref lastDepositTime, 128uL, _Mirror_SyncVarHookDelegate_lastDepositTime);
		}
	}

	private void Awake()
	{
		if (knob != null)
		{
			BankKnob bankKnob = knob;
			bankKnob.OnKnobValueChanged = (Action<float>)Delegate.Combine(bankKnob.OnKnobValueChanged, new Action<float>(HandleKnobValueChanged));
		}
	}

	private void Start()
	{
		UpdateLastModificationLabel(lastModificationPercent);
		if (bankTier == null && bankTierNumber > 0)
		{
			LoadBankTier(bankTierNumber);
		}
	}

	private void OnBankTierNumberChanged(int oldValue, int newValue)
	{
		LoadBankTier(newValue);
	}

	private void LoadBankTier(int tierNumber)
	{
		BankTier bankTier = SushiAndFish.Casino.Original.CasinoResources.Load<BankTier>($"Tier_{tierNumber}");
		if (bankTier != null)
		{
			this.bankTier = bankTier;
		}
	}

	private void Update()
	{
		if (base.isServer && bankBalance > 0)
		{
			ProcessMarketModification();
		}
		UpdateMarketStatusLabel();
	}

	private void ProcessMarketModification()
	{
		double time = NetworkTime.time;
		if (nextModificationTime != 0f && bankBalance != 0L && time >= (double)nextModificationTime)
		{
			ApplyMarketModification((float)time);
		}
	}

	private void ApplyMarketModification(float currentTime)
	{
		float randomModification = bankTier.GetRandomModification();
		double d = (float)bankBalance * randomModification;
		NetworkbankBalance = Math.Max(0L, (long)Math.Floor(d));
		NetworklastModificationPercent = (randomModification - 1f) * 100f;
		NetworklastModificationTime = currentTime;
		NetworknextModificationTime = currentTime + ((bankTier != null) ? bankTier.modificationInterval : 60f);
		NotifyBankBalanceChanged();
	}

	private void OnBankBalanceChanged(long oldValue, long newValue)
	{
		UpdateBankBalanceLabel(newValue);
		OnBankBalanceChangedEvent?.Invoke(newValue);
		UpdateMaxKnobValue();
	}

	private void OnModeChanged(BankMode oldMode, BankMode newMode)
	{
		UpdateModeLabel(newMode);
		OnModeChangedEvent?.Invoke(newMode);
		UpdateMaxKnobValue();
		NetworkselectedAmount = 0L;
	}

	private void OnSelectedAmountChanged(long oldValue, long newValue)
	{
		UpdateSelectedAmountLabel(newValue);
		OnSelectedAmountChangedEvent?.Invoke(newValue);
	}

	private void OnLastModificationTimeChanged(float oldValue, float newValue)
	{
	}

	private void OnNextModificationTimeChanged(float oldValue, float newValue)
	{
	}

	private void OnLastModificationPercentChanged(float oldValue, float newValue)
	{
		UpdateLastModificationLabel(newValue);
	}

	private void OnLastDepositTimeChanged(float oldValue, float newValue)
	{
	}

	private void HandleKnobValueChanged(float normalizedValue)
	{
		if (base.isServer)
		{
			long maxAmount = GetMaxAmount();
			long minAmount = GetMinAmount();
			long num = maxAmount - minAmount;
			if (num <= 0)
			{
				NetworkselectedAmount = minAmount;
				return;
			}
			long num2 = minAmount + (long)Math.Round(normalizedValue * (float)num);
			num2 = (long)Math.Round((double)num2 / (double)stepAmount) * stepAmount;
			num2 = Math.Max(minAmount, Math.Min(num2, maxAmount));
			NetworkselectedAmount = num2;
		}
	}

	private long GetMinAmount()
	{
		if (currentMode == BankMode.Put)
		{
			if (bankTier != null)
			{
				return bankTier.minDepositAmount;
			}
			return 1L;
		}
		return 1L;
	}

	private long GetMaxAmount()
	{
		if (currentMode == BankMode.Put)
		{
			long val = 0L;
			if (NetworkSingleton<MoneyManager>.Instance != null)
			{
				val = NetworkSingleton<MoneyManager>.Instance.balance;
			}
			long val2 = long.MaxValue;
			if (bankTier != null)
			{
				val2 = bankTier.maxDepositAmount;
			}
			return Math.Min(val, val2);
		}
		return bankBalance;
	}

	private void UpdateMaxKnobValue()
	{
		if (knob != null)
		{
			long maxAmount = GetMaxAmount();
			knob.SetMaxValue(maxAmount);
		}
	}

	[Server]
	private void SetMode(BankMode mode)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Bank::SetMode(BankMode)' called when server was not active");
			return;
		}
		NetworkcurrentMode = mode;
		NetworkselectedAmount = 0L;
		if (knob != null)
		{
			knob.SetNormalizedValue(0f);
		}
	}

	[Server]
	public void SetModePut(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Bank::SetModePut(PlayerInteract)' called when server was not active");
		}
		else
		{
			SetMode(BankMode.Put);
		}
	}

	[Server]
	public void SetModePull(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Bank::SetModePull(PlayerInteract)' called when server was not active");
		}
		else
		{
			SetMode(BankMode.Pull);
		}
	}

	[Server]
	public void ConfirmTransaction(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Bank::ConfirmTransaction(PlayerInteract)' called when server was not active");
		}
		else
		{
			if (selectedAmount <= 0)
			{
				return;
			}
			if (currentMode == BankMode.Put)
			{
				long minAmount = GetMinAmount();
				long maxAmount = GetMaxAmount();
				NetworkselectedAmount = Math.Max(minAmount, Math.Min(selectedAmount, maxAmount));
				if (selectedAmount < minAmount)
				{
					return;
				}
			}
			else
			{
				if (!CanWithdraw())
				{
					return;
				}
				if (bankBalance >= selectedAmount)
				{
					NetworkbankBalance = bankBalance - selectedAmount;
					NotifyBankBalanceChanged();
					if (bankBalance == 0L)
					{
						NetworknextModificationTime = 0f;
						NetworklastModificationTime = 0f;
						NetworklastDepositTime = 0f;
					}
					else
					{
						NetworklastDepositTime = 0f;
					}
				}
			}
			NetworkselectedAmount = 0L;
			if (knob != null)
			{
				knob.SetNormalizedValue(0f);
			}
		}
	}

	private bool CanWithdraw()
	{
		if (lastDepositTime == 0f)
		{
			return true;
		}
		if (lastModificationTime > lastDepositTime && lastModificationTime > 0f)
		{
			return true;
		}
		return false;
	}

	private void NotifyBankBalanceChanged()
	{
		OnBankBalanceChangedEvent?.Invoke(bankBalance);
	}

	private void UpdateBankBalanceLabel(long value)
	{
		if (bankBalanceLabel != null)
		{
			bankBalanceLabel.text = string.Format(currencyFormat, value);
		}
	}

	private void UpdateSelectedAmountLabel(long value)
	{
		if (selectedAmountLabel != null)
		{
			selectedAmountLabel.text = string.Format(currencyFormat, value);
		}
	}

	private void UpdateModeLabel(BankMode mode)
	{
		if (modeLabel != null)
		{
			modeLabel.text = ((mode == BankMode.Put) ? "Deposit" : "Withdraw");
		}
	}

	private void UpdateMarketStatusLabel()
	{
		if (currentInterestLabel == null)
		{
			return;
		}
		if (bankBalance > 0 && nextModificationTime > 0f)
		{
			double time = NetworkTime.time;
			float num = (float)((double)nextModificationTime - time);
			if (num > 0f)
			{
				int num2 = Mathf.CeilToInt(num);
				int num3 = num2 / 60;
				num2 %= 60;
				if (num3 > 0)
				{
					currentInterestLabel.text = $"{num3}m {num2}s";
				}
				else
				{
					currentInterestLabel.text = $"{num2}s";
				}
			}
			else
			{
				currentInterestLabel.text = "Modifying...";
			}
		}
		else if (bankBalance > 0)
		{
			currentInterestLabel.text = "Waiting...";
		}
		else
		{
			string text = ((bankTier != null) ? bankTier.GetFluctuationDisplay() : "+0% / -0%");
			currentInterestLabel.text = text;
		}
	}

	private void UpdateLastModificationLabel(float percent)
	{
		if (!(lastModificationLabel == null))
		{
			if (lastModificationTime > 0f && percent != 0f)
			{
				string arg = ((percent >= 0f) ? "+" : "");
				lastModificationLabel.text = $"{arg}{percent:F1}%";
			}
			else
			{
				lastModificationLabel.text = "";
			}
		}
	}

	public long GetBankBalance()
	{
		return bankBalance;
	}

	public BankMode GetCurrentMode()
	{
		return currentMode;
	}

	public long GetSelectedAmount()
	{
		return selectedAmount;
	}

	public float GetTimeUntilNextModification()
	{
		if (nextModificationTime == 0f)
		{
			return 0f;
		}
		return Mathf.Max(0f, (float)((double)nextModificationTime - NetworkTime.time));
	}

	public bool CanWithdrawMoney()
	{
		return CanWithdraw();
	}

	public float GetLastModificationPercent()
	{
		return lastModificationPercent;
	}

	public int GetCurrentTier()
	{
		if (!(bankTier != null))
		{
			return 1;
		}
		return bankTier.tierNumber;
	}

	[Server]
	public void SetBankTier(BankTier newTier)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Bank::SetBankTier(BankTier)' called when server was not active");
		}
		else if (!(newTier == null))
		{
			bankTier = newTier;
			NetworkbankTierNumber = newTier.tierNumber;
		}
	}

	public Bank()
	{
		_Mirror_SyncVarHookDelegate_bankTierNumber = OnBankTierNumberChanged;
		_Mirror_SyncVarHookDelegate_bankBalance = OnBankBalanceChanged;
		_Mirror_SyncVarHookDelegate_currentMode = OnModeChanged;
		_Mirror_SyncVarHookDelegate_selectedAmount = OnSelectedAmountChanged;
		_Mirror_SyncVarHookDelegate_lastModificationTime = OnLastModificationTimeChanged;
		_Mirror_SyncVarHookDelegate_nextModificationTime = OnNextModificationTimeChanged;
		_Mirror_SyncVarHookDelegate_lastModificationPercent = OnLastModificationPercentChanged;
		_Mirror_SyncVarHookDelegate_lastDepositTime = OnLastDepositTimeChanged;
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
			writer.WriteVarInt(bankTierNumber);
			writer.WriteVarLong(bankBalance);
			GeneratedNetworkCode._Write_BankMode(writer, currentMode);
			writer.WriteVarLong(selectedAmount);
			writer.WriteFloat(lastModificationTime);
			writer.WriteFloat(nextModificationTime);
			writer.WriteFloat(lastModificationPercent);
			writer.WriteFloat(lastDepositTime);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteVarInt(bankTierNumber);
		}
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteVarLong(bankBalance);
		}
		if ((syncVarDirtyBits & 4L) != 0L)
		{
			GeneratedNetworkCode._Write_BankMode(writer, currentMode);
		}
		if ((syncVarDirtyBits & 8L) != 0L)
		{
			writer.WriteVarLong(selectedAmount);
		}
		if ((syncVarDirtyBits & 0x10L) != 0L)
		{
			writer.WriteFloat(lastModificationTime);
		}
		if ((syncVarDirtyBits & 0x20L) != 0L)
		{
			writer.WriteFloat(nextModificationTime);
		}
		if ((syncVarDirtyBits & 0x40L) != 0L)
		{
			writer.WriteFloat(lastModificationPercent);
		}
		if ((syncVarDirtyBits & 0x80L) != 0L)
		{
			writer.WriteFloat(lastDepositTime);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref bankTierNumber, _Mirror_SyncVarHookDelegate_bankTierNumber, reader.ReadVarInt());
			GeneratedSyncVarDeserialize(ref bankBalance, _Mirror_SyncVarHookDelegate_bankBalance, reader.ReadVarLong());
			GeneratedSyncVarDeserialize(ref currentMode, _Mirror_SyncVarHookDelegate_currentMode, GeneratedNetworkCode._Read_BankMode(reader));
			GeneratedSyncVarDeserialize(ref selectedAmount, _Mirror_SyncVarHookDelegate_selectedAmount, reader.ReadVarLong());
			GeneratedSyncVarDeserialize(ref lastModificationTime, _Mirror_SyncVarHookDelegate_lastModificationTime, reader.ReadFloat());
			GeneratedSyncVarDeserialize(ref nextModificationTime, _Mirror_SyncVarHookDelegate_nextModificationTime, reader.ReadFloat());
			GeneratedSyncVarDeserialize(ref lastModificationPercent, _Mirror_SyncVarHookDelegate_lastModificationPercent, reader.ReadFloat());
			GeneratedSyncVarDeserialize(ref lastDepositTime, _Mirror_SyncVarHookDelegate_lastDepositTime, reader.ReadFloat());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref bankTierNumber, _Mirror_SyncVarHookDelegate_bankTierNumber, reader.ReadVarInt());
		}
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref bankBalance, _Mirror_SyncVarHookDelegate_bankBalance, reader.ReadVarLong());
		}
		if ((num & 4L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref currentMode, _Mirror_SyncVarHookDelegate_currentMode, GeneratedNetworkCode._Read_BankMode(reader));
		}
		if ((num & 8L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref selectedAmount, _Mirror_SyncVarHookDelegate_selectedAmount, reader.ReadVarLong());
		}
		if ((num & 0x10L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref lastModificationTime, _Mirror_SyncVarHookDelegate_lastModificationTime, reader.ReadFloat());
		}
		if ((num & 0x20L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref nextModificationTime, _Mirror_SyncVarHookDelegate_nextModificationTime, reader.ReadFloat());
		}
		if ((num & 0x40L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref lastModificationPercent, _Mirror_SyncVarHookDelegate_lastModificationPercent, reader.ReadFloat());
		}
		if ((num & 0x80L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref lastDepositTime, _Mirror_SyncVarHookDelegate_lastDepositTime, reader.ReadFloat());
		}
	}
}

}
