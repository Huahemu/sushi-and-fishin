using System;
using System.Collections;
using System.Collections.Generic;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentsLateJoinSyncModule : MonoBehaviour, IFrameSlicedBootstrapModule, IJoinBootstrapModule
	{
		private sealed class PreparedResidents : IPreparedBootstrapModule
		{
			private readonly ResidentSnapshotPacked[] snapshots;

			private ResidentManager manager;

			public bool IsReady
			{
				get
				{
					manager = Singleton<ResidentManager>.Instance;
					if (manager == null)
					{
						return false;
					}
					ResidentSnapshotPacked[] array = snapshots;
					for (int i = 0; i < array.Length; i++)
					{
						ResidentSnapshotPacked residentSnapshotPacked = array[i];
						if (!manager.TryGetResident(residentSnapshotPacked.ResidentId, out var _))
						{
							return false;
						}
					}
					return true;
				}
			}

			public PreparedResidents(ResidentSnapshotPacked[] snapshots)
			{
				this.snapshots = snapshots;
			}

			public IEnumerator ApplySteps()
			{
				bool ended = false;
				try
				{
					foreach (object item in manager.BeginLateJoinApplySteps())
					{
						yield return item;
					}
					ResidentSnapshotPacked[] array = snapshots;
					for (int i = 0; i < array.Length; i++)
					{
						ResidentSnapshotPacked snapshot = array[i];
						if (manager == null || !manager.TryGetResident(snapshot.ResidentId, out var _))
						{
							throw new InvalidOperationException("Resident snapshot target disappeared during apply.");
						}
						manager.ApplySnapshotPacked(snapshot);
						yield return null;
					}
					foreach (object item2 in manager.EndLateJoinApplySteps())
					{
						yield return item2;
					}
					ended = true;
				}
				finally
				{
					PreparedResidents preparedResidents = this;
					if (!ended && preparedResidents.manager != null)
					{
						preparedResidents.manager.EndLateJoinApply();
					}
				}
			}
		}

		private readonly Dictionary<byte, ResidentSnapshotPacked> _serverRuntime = new Dictionary<byte, ResidentSnapshotPacked>();

		public ushort ModuleId => 10;

		public int Priority => 200;

		public bool IsRequired => true;

		public bool Supports(JoinBootstrapMode mode)
		{
			return true;
		}

		public bool ServerCapture(ulong targetClientId, JoinBootstrapMode mode)
		{
			ResidentManager instance = Singleton<ResidentManager>.Instance;
			_serverRuntime.Clear();
			if (instance == null)
			{
				UtilityMethods.Log("<color=red>[ResidentsLateJoinSyncModulePacked]</color> ResidentManager not found.", LogType.Error);
				return false;
			}
			for (int i = 0; i < instance.Residents.Count; i++)
			{
				ResidentController residentController = instance.Residents[i];
				if (!(residentController == null))
				{
					if (!residentController.TryBuildSnapshotPacked(out var snapshot))
					{
						return false;
					}
					_serverRuntime[snapshot.ResidentId] = snapshot;
				}
			}
			UtilityMethods.Log($"<color=green>[ResidentsLateJoinSyncModulePacked]</color> Built snapshot for {_serverRuntime.Count} residents.");
			return true;
		}

		public void ServerWrite(FastBufferWriter writer)
		{
			writer.WriteValueSafe<ushort>((ushort)_serverRuntime.Count, default(FastBufferWriter.ForPrimitives));
			foreach (KeyValuePair<byte, ResidentSnapshotPacked> item in _serverRuntime)
			{
				ResidentSnapshotPacked value = item.Value;
				writer.WriteValueSafe(in value.ResidentId, default(FastBufferWriter.ForPrimitives));
				writer.WriteValueSafe(in value.Position);
				writer.WriteValueSafe(in value.YawQ, default(FastBufferWriter.ForPrimitives));
				writer.WriteValueSafe(in value.CurrentState, default(FastBufferWriter.ForPrimitives));
				writer.WriteValueSafe(in value.PreviousState, default(FastBufferWriter.ForPrimitives));
				writer.WriteValueSafe(in value.Flags, default(FastBufferWriter.ForPrimitives));
				writer.WriteValueSafe(in value.Index0, default(FastBufferWriter.ForPrimitives));
				writer.WriteValueSafe(in value.Index1, default(FastBufferWriter.ForPrimitives));
				writer.WriteValueSafe(in value.TableId, default(FastBufferWriter.ForPrimitives));
				writer.WriteValueSafe(in value.SeatIndex, default(FastBufferWriter.ForPrimitives));
				writer.WriteValueSafe(in value.RestaurantState, default(FastBufferWriter.ForPrimitives));
			}
		}

		public bool ClientCanApply()
		{
			return Singleton<ResidentManager>.Instance != null;
		}

		public void ClientReadAndApply(FastBufferReader reader)
		{
			IPreparedBootstrapModule preparedBootstrapModule = ClientPrepare(reader);
			if (!preparedBootstrapModule.IsReady)
			{
				throw new InvalidOperationException("Resident snapshot targets are not ready.");
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
				throw new InvalidOperationException("Resident snapshot count exceeds byte identity space.");
			}
			ResidentSnapshotPacked[] array = new ResidentSnapshotPacked[value];
			HashSet<byte> hashSet = new HashSet<byte>();
			for (int i = 0; i < value; i++)
			{
				ref ResidentSnapshotPacked reference = ref array[i];
				reader.ReadValueSafe(out reference.ResidentId, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out reference.Position);
				reader.ReadValueSafe(out reference.YawQ, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out reference.CurrentState, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out reference.PreviousState, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out reference.Flags, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out reference.Index0, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out reference.Index1, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out reference.TableId, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out reference.SeatIndex, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out reference.RestaurantState, default(FastBufferWriter.ForPrimitives));
				if (!hashSet.Add(reference.ResidentId))
				{
					throw new InvalidOperationException("Duplicate resident snapshot identity.");
				}
			}
			return new PreparedResidents(array);
		}

		public void ClientAfterAllApplied()
		{
			if (Singleton<ResidentManager>.Instance != null)
			{
				Singleton<ResidentManager>.Instance.AfterAllLateJoinModulesApplied();
			}
		}
	}
}
