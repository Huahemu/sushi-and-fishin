namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Runtime.InteropServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class MoneyManager : NetworkSingleton<MoneyManager>
{
	[Header("Logic")]
	[SyncVar]
	public long balance;

	[SyncVar]
	public long ticketBalance;

	[SyncVar]
	public long dayStartBalance;

	public Action<BalanceChangeData> OnBalanceChanged;

	public Action<long> OnTicketBalanceChanged;

	private GameSettings _gs;

	public long Networkbalance
	{
		get
		{
			return balance;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref balance, 1uL, null);
		}
	}

	public long NetworkticketBalance
	{
		get
		{
			return ticketBalance;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref ticketBalance, 2uL, null);
		}
	}

	public long NetworkdayStartBalance
	{
		get
		{
			return dayStartBalance;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref dayStartBalance, 4uL, null);
		}
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		_gs = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
	}

	[Command(requiresAuthority = false)]
	public void CmdTryChangeBalance(long amount, PlayerProfile changer, ChangeType changeType)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarLong(amount);
		writer.WriteNetworkBehaviour(changer);
		GeneratedNetworkCode._Write_ChangeType(writer, changeType);
		SendCommandInternal("System.Void MoneyManager::CmdTryChangeBalance(System.Int64,PlayerProfile,ChangeType)", 1990209703, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public bool TryChangeBalance(long amount, PlayerProfile changer, ChangeType changeType)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Boolean MoneyManager::TryChangeBalance(System.Int64,PlayerProfile,ChangeType)' called when server was not active");
			return default;
		}
        if (CasinoRuntimeContext.Wallet != null)
        {
            bool npcActor = SushiAndFish.FriendsLife.ActorWalletRouter.IsNpcInvocation;
            if ((!npcActor && amount == 0) || !CasinoRuntimeContext.Wallet.TryChangeCoins(amount, out long actualBalance)) return false;
            // A native NPC round owns a personal wallet. Human island balances,
            // progression and their UI keep the original source branch below.
            if (npcActor) return true;
            HostSynchronizeBalance(actualBalance);
            OnBalanceChanged?.Invoke(new BalanceChangeData(amount, changer, changeType));
            RpcInvokeBalanceChanged(balance, amount, changer, changeType);
            return true;
        }

		if (amount == 0L)
		{
			return false;
		}
		if (amount > 0)
		{
			long num = Math.Clamp(balance + amount, long.MinValue, long.MaxValue);
			AddBalance(num - balance, changer, changeType);
		}
		else if (amount < 0)
		{
			if (balance + amount < 0)
			{
				return false;
			}
			RemoveBalance(amount, changer, changeType);
		}
		return true;
	}

	[Server]
	public void SetBalance(long amount, PlayerProfile changer, ChangeType changeType)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MoneyManager::SetBalance(System.Int64,PlayerProfile,ChangeType)' called when server was not active");
			return;
		}
        if (CasinoRuntimeContext.Wallet != null)
        {
            HostSynchronizeBalance(CasinoRuntimeContext.ReadCoins);
            return;
        }

		Networkbalance = amount;
		OnBalanceChanged?.Invoke(new BalanceChangeData(amount, changer, changeType));
		RpcInvokeBalanceChanged(balance, amount, changer, changeType);
	}

    public void HostSynchronizeBalance(long actualBalance)
    {
        Networkbalance = actualBalance;
        NetworkticketBalance = actualBalance;
    }

	[Server]
	private void AddBalance(long amount, PlayerProfile changer, ChangeType changeType)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MoneyManager::AddBalance(System.Int64,PlayerProfile,ChangeType)' called when server was not active");
			return;
		}
		Networkbalance = balance + Math.Abs(amount);
		BalanceChangeData obj = new BalanceChangeData(amount, changer, changeType);
		OnBalanceChanged?.Invoke(obj);
		RpcInvokeBalanceChanged(balance, amount, changer, changeType);
	}

	[Server]
	private void RemoveBalance(long amount, PlayerProfile changer, ChangeType changeType)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MoneyManager::RemoveBalance(System.Int64,PlayerProfile,ChangeType)' called when server was not active");
			return;
		}
		Networkbalance = balance - Math.Abs(amount);
		BalanceChangeData obj = new BalanceChangeData(amount, changer, changeType);
		OnBalanceChanged?.Invoke(obj);
		RpcInvokeBalanceChanged(balance, amount, changer, changeType);
	}

	[ClientRpc]
	private void RpcInvokeBalanceChanged(long finalBalance, long amount, PlayerProfile changer, ChangeType changeType)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarLong(finalBalance);
		writer.WriteVarLong(amount);
		writer.WriteNetworkBehaviour(changer);
		GeneratedNetworkCode._Write_ChangeType(writer, changeType);
		SendRPCInternal("System.Void MoneyManager::RpcInvokeBalanceChanged(System.Int64,System.Int64,PlayerProfile,ChangeType)", -708839733, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void SetDayStartBalance()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MoneyManager::SetDayStartBalance()' called when server was not active");
		}
		else
		{
			NetworkdayStartBalance = balance;
		}
	}

	[Command(requiresAuthority = false)]
	public void CmdTryChangeTicketBalance(long amount)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarLong(amount);
		SendCommandInternal("System.Void MoneyManager::CmdTryChangeTicketBalance(System.Int64)", 63865289, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public bool TryChangeTicketBalance(long amount)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Boolean MoneyManager::TryChangeTicketBalance(System.Int64)' called when server was not active");
			return default;
		}
        if (SushiAndFish.FriendsLife.ActorWalletRouter.IsNpcInvocation)
            return SushiAndFish.FriendsLife.ActorWalletRouter.TryChangeNativeTickets(amount, out _);
        if (CasinoRuntimeContext.Wallet != null)
        {
            if (amount == 0 || !CasinoRuntimeContext.Wallet.TryChangeCoins(amount, out long actualBalance)) return false;
            HostSynchronizeBalance(actualBalance);
            OnTicketBalanceChanged?.Invoke(amount);
            RpcInvokeTicketChanged(actualBalance, amount);
            return true;
        }
		if (amount > 0)
		{
			long num = Math.Clamp(ticketBalance + amount, long.MinValue, long.MaxValue);
			AddTicket(num - ticketBalance);
		}
		else if (amount < 0)
		{
			if (ticketBalance + amount < 0)
			{
				return false;
			}
			RemoveTicket(amount);
		}
		return true;
	}

	public bool TrySetTicketBalance(long amount)
	{
        if (CasinoRuntimeContext.Wallet != null) { HostSynchronizeBalance(CasinoRuntimeContext.ReadCoins); return true; }
		NetworkticketBalance = amount;
		OnTicketBalanceChanged?.Invoke(amount);
		RpcInvokeTicketChanged(ticketBalance, amount);
		return true;
	}

	[Server]
	private void AddTicket(long amount)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MoneyManager::AddTicket(System.Int64)' called when server was not active");
			return;
		}
		NetworkticketBalance = ticketBalance + Math.Abs(amount);
		OnTicketBalanceChanged?.Invoke(amount);
		RpcInvokeTicketChanged(ticketBalance, amount);
	}

	[Server]
	private void RemoveTicket(long amount)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MoneyManager::RemoveTicket(System.Int64)' called when server was not active");
			return;
		}
		NetworkticketBalance = ticketBalance - Math.Abs(amount);
		OnTicketBalanceChanged?.Invoke(-Math.Abs(amount));
		RpcInvokeTicketChanged(ticketBalance, -Math.Abs(amount));
	}

	[ClientRpc]
	private void RpcInvokeTicketChanged(long finalBalance, long amount)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarLong(finalBalance);
		writer.WriteVarLong(amount);
		SendRPCInternal("System.Void MoneyManager::RpcInvokeTicketChanged(System.Int64,System.Int64)", -97559143, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	public void CmdResetBalancesToDefault()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void MoneyManager::CmdResetBalancesToDefault()", -756986718, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	private void ServerResetBalancesToDefault()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MoneyManager::ServerResetBalancesToDefault()' called when server was not active");
			return;
		}
        if (CasinoRuntimeContext.Wallet != null) { HostSynchronizeBalance(CasinoRuntimeContext.ReadCoins); return; }
		long num = balance;
		Networkbalance = _gs.startingMoney;
		BalanceChangeData obj = new BalanceChangeData(balance - num, null, ChangeType.Misc);
		OnBalanceChanged?.Invoke(obj);
		RpcInvokeBalanceChanged(balance, balance - num, null, ChangeType.Misc);
		long num2 = ticketBalance;
		NetworkticketBalance = _gs.startingTicket;
		OnTicketBalanceChanged?.Invoke(ticketBalance - num2);
		RpcInvokeTicketChanged(ticketBalance, ticketBalance - num2);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdTryChangeBalance__Int64__PlayerProfile__ChangeType(long amount, PlayerProfile changer, ChangeType changeType)
	{
		TryChangeBalance(amount, changer, changeType);
	}

	protected static void InvokeUserCode_CmdTryChangeBalance__Int64__PlayerProfile__ChangeType(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdTryChangeBalance called on client.");
		}
		else
		{
			((MoneyManager)obj).UserCode_CmdTryChangeBalance__Int64__PlayerProfile__ChangeType(reader.ReadVarLong(), reader.ReadNetworkBehaviour<PlayerProfile>(), GeneratedNetworkCode._Read_ChangeType(reader));
		}
	}

	protected void UserCode_RpcInvokeBalanceChanged__Int64__Int64__PlayerProfile__ChangeType(long finalBalance, long amount, PlayerProfile changer, ChangeType changeType)
	{
		if (!base.isServer)
		{
			Networkbalance = finalBalance;
			BalanceChangeData obj = new BalanceChangeData(amount, changer, changeType);
			OnBalanceChanged?.Invoke(obj);
		}
	}

	protected static void InvokeUserCode_RpcInvokeBalanceChanged__Int64__Int64__PlayerProfile__ChangeType(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcInvokeBalanceChanged called on server.");
		}
		else
		{
			((MoneyManager)obj).UserCode_RpcInvokeBalanceChanged__Int64__Int64__PlayerProfile__ChangeType(reader.ReadVarLong(), reader.ReadVarLong(), reader.ReadNetworkBehaviour<PlayerProfile>(), GeneratedNetworkCode._Read_ChangeType(reader));
		}
	}

	protected void UserCode_CmdTryChangeTicketBalance__Int64(long amount)
	{
		TryChangeTicketBalance(amount);
	}

	protected static void InvokeUserCode_CmdTryChangeTicketBalance__Int64(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdTryChangeTicketBalance called on client.");
		}
		else
		{
			((MoneyManager)obj).UserCode_CmdTryChangeTicketBalance__Int64(reader.ReadVarLong());
		}
	}

	protected void UserCode_RpcInvokeTicketChanged__Int64__Int64(long finalBalance, long amount)
	{
		if (!base.isServer)
		{
			NetworkticketBalance = finalBalance;
			OnTicketBalanceChanged?.Invoke(amount);
		}
	}

	protected static void InvokeUserCode_RpcInvokeTicketChanged__Int64__Int64(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcInvokeTicketChanged called on server.");
		}
		else
		{
			((MoneyManager)obj).UserCode_RpcInvokeTicketChanged__Int64__Int64(reader.ReadVarLong(), reader.ReadVarLong());
		}
	}

	protected void UserCode_CmdResetBalancesToDefault()
	{
		ServerResetBalancesToDefault();
	}

	protected static void InvokeUserCode_CmdResetBalancesToDefault(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdResetBalancesToDefault called on client.");
		}
		else
		{
			((MoneyManager)obj).UserCode_CmdResetBalancesToDefault();
		}
	}

	static MoneyManager()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(MoneyManager), "System.Void MoneyManager::CmdTryChangeBalance(System.Int64,PlayerProfile,ChangeType)", InvokeUserCode_CmdTryChangeBalance__Int64__PlayerProfile__ChangeType, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(MoneyManager), "System.Void MoneyManager::CmdTryChangeTicketBalance(System.Int64)", InvokeUserCode_CmdTryChangeTicketBalance__Int64, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(MoneyManager), "System.Void MoneyManager::CmdResetBalancesToDefault()", InvokeUserCode_CmdResetBalancesToDefault, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(MoneyManager), "System.Void MoneyManager::RpcInvokeBalanceChanged(System.Int64,System.Int64,PlayerProfile,ChangeType)", InvokeUserCode_RpcInvokeBalanceChanged__Int64__Int64__PlayerProfile__ChangeType);
		RemoteProcedureCalls.RegisterRpc(typeof(MoneyManager), "System.Void MoneyManager::RpcInvokeTicketChanged(System.Int64,System.Int64)", InvokeUserCode_RpcInvokeTicketChanged__Int64__Int64);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteVarLong(balance);
			writer.WriteVarLong(ticketBalance);
			writer.WriteVarLong(dayStartBalance);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteVarLong(balance);
		}
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteVarLong(ticketBalance);
		}
		if ((syncVarDirtyBits & 4L) != 0L)
		{
			writer.WriteVarLong(dayStartBalance);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref balance, null, reader.ReadVarLong());
			GeneratedSyncVarDeserialize(ref ticketBalance, null, reader.ReadVarLong());
			GeneratedSyncVarDeserialize(ref dayStartBalance, null, reader.ReadVarLong());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref balance, null, reader.ReadVarLong());
		}
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref ticketBalance, null, reader.ReadVarLong());
		}
		if ((num & 4L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref dayStartBalance, null, reader.ReadVarLong());
		}
	}
}

}
