using System;
using System.Collections;
using System.Collections.Generic;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.Restaurant.LateJoin;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

public sealed class TableLateJoinSyncModule : MonoBehaviour, IFrameSlicedBootstrapModule, IJoinBootstrapModule
{
	private sealed class PreparedTables : IPreparedBootstrapModule
	{
		private readonly TableSnapshotPacked[] snapshots;

		private Dictionary<ushort, RestaurantTable> map;

		public bool IsReady
		{
			get
			{
				RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
				if (restaurantManager == null)
				{
					return false;
				}
				map = BuildClientTableMap(restaurantManager);
				TableSnapshotPacked[] array = snapshots;
				for (int i = 0; i < array.Length; i++)
				{
					TableSnapshotPacked tableSnapshotPacked = array[i];
					if (!map.TryGetValue(tableSnapshotPacked.TableId, out var value) || value == null)
					{
						return false;
					}
				}
				return true;
			}
		}

		public PreparedTables(TableSnapshotPacked[] snapshots)
		{
			this.snapshots = snapshots;
		}

		public IEnumerator ApplySteps()
		{
			foreach (RestaurantTable value2 in map.Values)
			{
				if (value2 != null && !value2.IsServer)
				{
					value2.ClientResetForBootstrap();
				}
				yield return null;
			}
			TableSnapshotPacked[] array = snapshots;
			for (int i = 0; i < array.Length; i++)
			{
				TableSnapshotPacked s = array[i];
				if (!map.TryGetValue(s.TableId, out var value) || value == null)
				{
					throw new InvalidOperationException("Table snapshot target disappeared during apply.");
				}
				value.ClientApplySnapshotPacked(in s);
				yield return null;
			}
		}
	}

	private readonly Dictionary<byte, TableSnapshotPacked> _serverRuntime = new Dictionary<byte, TableSnapshotPacked>();

	public ushort ModuleId => 20;

	public int Priority => 210;

	public bool IsRequired => true;

	public bool Supports(JoinBootstrapMode mode)
	{
		return true;
	}

	public bool ServerCapture(ulong targetClientId, JoinBootstrapMode mode)
	{
		RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
		if (restaurantManager == null)
		{
			_serverRuntime.Clear();
			UtilityMethods.Log("<color=red>[TableLateJoinSyncModule]</color> RestaurantManager not found.", LogType.Error);
			return false;
		}
		if (restaurantManager.Tables == null || restaurantManager.Tables.Count == 0)
		{
			_serverRuntime.Clear();
			UtilityMethods.Log("<color=orange>[TableLateJoinSyncModule]</color> RestaurantManager.Tables is empty.", LogType.Warning);
			return false;
		}
		_serverRuntime.Clear();
		for (int i = 0; i < restaurantManager.Tables.Count; i++)
		{
			RestaurantTable restaurantTable = restaurantManager.Tables[i];
			if (!(restaurantTable == null) && restaurantTable.PhysicalStateController.IsPhysicallyUsable && TryBuildSnapshotForTable(restaurantTable, out var s))
			{
				_serverRuntime[s.TableId] = s;
			}
		}
		UtilityMethods.Log($"<color=green>[TableLateJoinSyncModule]</color> Built snapshot for {_serverRuntime.Count} tables.");
		return true;
	}

	public void ServerWrite(FastBufferWriter writer)
	{
		ushort value = (ushort)_serverRuntime.Count;
		writer.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
		if (value == 0)
		{
			return;
		}
		foreach (KeyValuePair<byte, TableSnapshotPacked> item in _serverRuntime)
		{
			TableSnapshotPacked value2 = item.Value;
			writer.WriteValueSafe(in value2.TableId, default(FastBufferWriter.ForPrimitives));
			writer.WriteValueSafe(in value2.CurrentState, default(FastBufferWriter.ForPrimitives));
			writer.WriteValueSafe(in value2.SeatedCount, default(FastBufferWriter.ForPrimitives));
			writer.WriteValueSafe(in value2.ArrivedCustomerCount, default(FastBufferWriter.ForPrimitives));
			byte value3 = (byte)value2.Flags;
			writer.WriteValueSafe(in value3, default(FastBufferWriter.ForPrimitives));
			writer.WriteValueSafe(in value2.TakingOrderSeed, default(FastBufferWriter.ForPrimitives));
			writer.WriteValueSafe(in value2.TotalPaymentFinal, default(FastBufferWriter.ForPrimitives));
			writer.WriteValueSafe(in value2.DeliveredSeatMask, default(FastBufferWriter.ForPrimitives));
			WriteSeat(writer, in value2.Seat0);
			WriteSeat(writer, in value2.Seat1);
			WriteSeat(writer, in value2.Seat2);
			WriteSeat(writer, in value2.Seat3);
		}
	}

	private static void WriteSeat(FastBufferWriter w, in SeatOrderPacked seat)
	{
		w.WriteValueSafe(in seat.MainId, default(FastBufferWriter.ForFixedStrings));
		w.WriteValueSafe(in seat.SideId, default(FastBufferWriter.ForFixedStrings));
		w.WriteValueSafe(in seat.BevId, default(FastBufferWriter.ForFixedStrings));
	}

	public bool ClientCanApply()
	{
		if (Singleton<GameProgressManager>.Instance != null)
		{
			return Singleton<GameProgressManager>.Instance.RestaurantManager != null;
		}
		return false;
	}

	public void ClientReadAndApply(FastBufferReader reader)
	{
		IPreparedBootstrapModule preparedBootstrapModule = ClientPrepare(reader);
		if (!preparedBootstrapModule.IsReady)
		{
			throw new InvalidOperationException("Table snapshot targets are not ready.");
		}
		IEnumerator enumerator = preparedBootstrapModule.ApplySteps();
		try
		{
			while (enumerator.MoveNext())
			{
			}
		}
		finally
		{
			IDisposable disposable = enumerator as IDisposable;
			if (disposable != null)
			{
				disposable.Dispose();
			}
		}
	}

	public IPreparedBootstrapModule ClientPrepare(FastBufferReader reader)
	{
		reader.ReadValueSafe(out ushort value, default(FastBufferWriter.ForPrimitives));
		if (value > 256)
		{
			throw new InvalidOperationException("Table snapshot count exceeds byte identity space.");
		}
		TableSnapshotPacked[] array = new TableSnapshotPacked[value];
		HashSet<byte> hashSet = new HashSet<byte>();
		for (int i = 0; i < value; i++)
		{
			ref TableSnapshotPacked reference = ref array[i];
			reader.ReadValueSafe(out reference.TableId, default(FastBufferWriter.ForPrimitives));
			reader.ReadValueSafe(out reference.CurrentState, default(FastBufferWriter.ForPrimitives));
			reader.ReadValueSafe(out reference.SeatedCount, default(FastBufferWriter.ForPrimitives));
			reader.ReadValueSafe(out reference.ArrivedCustomerCount, default(FastBufferWriter.ForPrimitives));
			reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
			reference.Flags = (TableSnapFlags)value2;
			reader.ReadValueSafe(out reference.TakingOrderSeed, default(FastBufferWriter.ForPrimitives));
			reader.ReadValueSafe(out reference.TotalPaymentFinal, default(FastBufferWriter.ForPrimitives));
			reader.ReadValueSafe(out reference.DeliveredSeatMask, default(FastBufferWriter.ForPrimitives));
			ReadSeat(reader, ref reference.Seat0);
			ReadSeat(reader, ref reference.Seat1);
			ReadSeat(reader, ref reference.Seat2);
			ReadSeat(reader, ref reference.Seat3);
			if (!hashSet.Add(reference.TableId))
			{
				throw new InvalidOperationException("Duplicate table snapshot identity.");
			}
		}
		return new PreparedTables(array);
	}

	private static void ReadSeat(FastBufferReader r, ref SeatOrderPacked seat)
	{
		r.ReadValueSafe(out seat.MainId, default(FastBufferWriter.ForFixedStrings));
		r.ReadValueSafe(out seat.SideId, default(FastBufferWriter.ForFixedStrings));
		r.ReadValueSafe(out seat.BevId, default(FastBufferWriter.ForFixedStrings));
	}

	public void ClientAfterAllApplied()
	{
	}

	private bool TryBuildSnapshotForTable(RestaurantTable table, out TableSnapshotPacked s)
	{
		s = default;
		if (table == null)
		{
			return false;
		}
		s = table.ServerBuildSnapshotPacked();
		return true;
	}

	private static Dictionary<ushort, RestaurantTable> BuildClientTableMap(RestaurantManager rm)
	{
		if (rm.Tables != null && rm.Tables.Count > 0)
		{
			Dictionary<ushort, RestaurantTable> dictionary = new Dictionary<ushort, RestaurantTable>(rm.Tables.Count);
			for (int i = 0; i < rm.Tables.Count; i++)
			{
				RestaurantTable restaurantTable = rm.Tables[i];
				if (!(restaurantTable == null))
				{
					dictionary[(ushort)restaurantTable.Id] = restaurantTable;
				}
			}
			return dictionary;
		}
		return new Dictionary<ushort, RestaurantTable>();
	}
}
