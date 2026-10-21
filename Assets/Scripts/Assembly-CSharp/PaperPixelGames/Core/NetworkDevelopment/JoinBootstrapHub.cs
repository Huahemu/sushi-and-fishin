using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public sealed class JoinBootstrapHub
	{
		private sealed class AtomicApplication : IPreparedBootstrapModule
		{
			private readonly IJoinBootstrapModule module;

			private readonly byte[] payload;

			public bool IsReady => module.ClientCanApply();

			public AtomicApplication(IJoinBootstrapModule module, byte[] payload)
			{
				this.module = module;
				this.payload = payload;
			}

			public IEnumerator ApplySteps()
			{
				using (FastBufferReader reader = new FastBufferReader(payload, Allocator.Temp))
				{
					module.ClientReadAndApply(reader);
					if (reader.Position != reader.Length)
					{
						throw new InvalidOperationException("Module payload boundary mismatch.");
					}
				}
				yield return null;
			}
		}

		private const ushort SNAPSHOT_VERSION = 5;

		private const int MAX_SNAPSHOT_SIZE_BYTES = 1048576;

		private readonly List<IJoinBootstrapModule> _modules;

		private readonly StringBuilder _debugLog = new StringBuilder();

		public JoinBootstrapHub(IEnumerable<IJoinBootstrapModule> modules)
		{
			_modules = modules?.Where((IJoinBootstrapModule x) => x != null).OrderBy((IJoinBootstrapModule x) => x.Priority).ThenBy((IJoinBootstrapModule x) => x.ModuleId)
				.ToList() ?? new List<IJoinBootstrapModule>();
			IGrouping<ushort, IJoinBootstrapModule> grouping = (from x in _modules
				group x by x.ModuleId).FirstOrDefault((IGrouping<ushort, IJoinBootstrapModule> group) => group.Count() > 1);
			if (grouping != null)
			{
				throw new InvalidOperationException($"Duplicate bootstrap ModuleId={grouping.Key}.");
			}
		}

		public bool ServerCaptureAll(ulong targetClientId, JoinBootstrapMode mode, out string failReason)
		{
			failReason = string.Empty;
			for (int i = 0; i < _modules.Count; i++)
			{
				IJoinBootstrapModule joinBootstrapModule = _modules[i];
				if (!joinBootstrapModule.Supports(mode))
				{
					continue;
				}
				try
				{
					if (!joinBootstrapModule.ServerCapture(targetClientId, mode))
					{
						if (joinBootstrapModule.IsRequired)
						{
							failReason = $"Required bootstrap module capture failed. ModuleId={joinBootstrapModule.ModuleId}";
							return false;
						}
						UtilityMethods.Log($"<color=orange>[JoinBootstrapHub]</color> Optional module capture failed. ModuleId={joinBootstrapModule.ModuleId}", LogType.Warning);
					}
				}
				catch (Exception ex)
				{
					if (!joinBootstrapModule.IsRequired)
					{
						UtilityMethods.Log($"<color=orange>[JoinBootstrapHub]</color> Optional module capture exception. ModuleId={joinBootstrapModule.ModuleId}, Exception={ex.Message}", LogType.Warning);
						continue;
					}
					failReason = $"Required bootstrap module capture exception. ModuleId={joinBootstrapModule.ModuleId}, Exception={ex.Message}";
					return false;
				}
			}
			return true;
		}

		public byte[] ServerBuildBlob(JoinSessionContext context)
		{
			using FastBufferWriter writer = new FastBufferWriter(16384, Allocator.Temp, 1048576);
			List<IJoinBootstrapModule> list = _modules.Where((IJoinBootstrapModule x) => x.Supports(context.Mode)).ToList();
			WriteHeader(writer, context, (ushort)list.Count);
			for (int num = 0; num < list.Count; num++)
			{
				IJoinBootstrapModule joinBootstrapModule = list[num];
				writer.WriteValueSafe<ushort>(joinBootstrapModule.ModuleId, default(FastBufferWriter.ForPrimitives));
				writer.WriteValueSafe<byte>((byte)(joinBootstrapModule.IsRequired ? 1u : 0u), default(FastBufferWriter.ForPrimitives));
				int position = writer.Position;
				writer.WriteValueSafe<int>(0, default(FastBufferWriter.ForPrimitives));
				int position2 = writer.Position;
				joinBootstrapModule.ServerWrite(writer);
				int value = writer.Position - position2;
				int position3 = writer.Position;
				writer.Seek(position);
				writer.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				writer.Seek(position3);
			}
			return writer.ToArray();
		}

		public BootstrapApplyResult ClientApplyBlob(byte[] blob)
		{
			using BootstrapApplyOperation bootstrapApplyOperation = CreateClientApply(blob, 0uL, null, null, double.PositiveInfinity, int.MaxValue);
			bootstrapApplyOperation.AdvanceFrame();
			return bootstrapApplyOperation.Result;
		}

		public BootstrapApplyOperation CreateClientApply(byte[] blob, ulong expectedEpoch = 0uL, Func<bool> isCurrent = null, Func<double> clock = null, double frameBudgetMs = 2.0, int stepsPerFrame = 8)
		{
			BootstrapApplyResult result = default;
			List<BootstrapApplyOperation.PreparedModule> list = new List<BootstrapApplyOperation.PreparedModule>();
			try
			{
				if (MultiplayerFaultInjection.ConsumeForcedBootstrapFailure())
				{
					throw new InvalidOperationException("Bootstrap failure injected by the multiplayer development test harness.");
				}
				if (isCurrent != null && !isCurrent())
				{
					throw new InvalidOperationException("Inactive bootstrap session.");
				}
				if (blob == null || blob.Length == 0 || blob.Length > 1048576)
				{
					throw new InvalidOperationException("Bootstrap blob is empty or exceeds the size limit.");
				}
				using FastBufferReader reader = new FastBufferReader(blob, Allocator.Temp);
				if (!TryReadHeader(reader, out var header))
				{
					throw new InvalidOperationException("Failed to read bootstrap header.");
				}
				if (header.Version != 5)
				{
					throw new InvalidOperationException($"Unsupported bootstrap snapshot version. Received={header.Version}");
				}
				if (!Enum.IsDefined(typeof(JoinBootstrapMode), header.Mode))
				{
					throw new InvalidOperationException("Invalid bootstrap mode.");
				}
				result.Context = new JoinSessionContext
				{
					HostSteamId = header.HostSteamId.ToString(),
					SlotId = header.SlotId,
					Mode = (JoinBootstrapMode)header.Mode,
					SessionEpoch = header.SessionEpoch,
					SnapshotId = header.SnapshotId,
					WorldRevision = header.WorldRevision
				};
				if (!result.Context.HasSnapshotIdentity || (expectedEpoch != 0L && header.SessionEpoch != expectedEpoch))
				{
					throw new InvalidOperationException("Bootstrap context or session epoch is invalid.");
				}
				HashSet<ushort> hashSet = new HashSet<ushort>();
				int num = int.MinValue;
				ushort num2 = 0;
				for (int i = 0; i < header.ModuleCount; i++)
				{
					reader.ReadValueSafe(out ushort id, default(FastBufferWriter.ForPrimitives));
					reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
					reader.ReadValueSafe(out int value2, default(FastBufferWriter.ForPrimitives));
					if (value > 1 || !hashSet.Add(id))
					{
						throw new InvalidOperationException($"Invalid or duplicate module header. ModuleId={id}");
					}
					if (value2 < 0 || value2 > 1048576 || !reader.TryBeginRead(value2))
					{
						throw new InvalidOperationException($"Invalid bootstrap payload length. ModuleId={id}");
					}
					int position = reader.Position;
					reader.Seek(position + value2);
					IJoinBootstrapModule joinBootstrapModule = _modules.FirstOrDefault((IJoinBootstrapModule x) => x.ModuleId == id);
					if (joinBootstrapModule == null)
					{
						if (value == 1)
						{
							throw new InvalidOperationException($"Required bootstrap module is not registered. ModuleId={id}");
						}
						continue;
					}
					if (joinBootstrapModule.IsRequired != (value == 1) || !joinBootstrapModule.Supports(result.Context.Mode))
					{
						throw new InvalidOperationException($"Bootstrap module contract mismatch. ModuleId={id}");
					}
					if (joinBootstrapModule.Priority < num || (joinBootstrapModule.Priority == num && id <= num2))
					{
						throw new InvalidOperationException($"Bootstrap module dependency order is invalid. ModuleId={id}");
					}
					num = joinBootstrapModule.Priority;
					num2 = id;
					byte[] array = new byte[value2];
					Array.Copy(blob, position, array, 0, value2);
					IPreparedBootstrapModule preparedBootstrapModule;
					if (joinBootstrapModule is IFrameSlicedBootstrapModule frameSlicedBootstrapModule)
					{
						using FastBufferReader reader2 = new FastBufferReader(array, Allocator.Temp);
						try
						{
							preparedBootstrapModule = frameSlicedBootstrapModule.ClientPrepare(reader2);
							if (preparedBootstrapModule == null || reader2.Position != value2)
							{
								throw new InvalidOperationException($"Bootstrap module payload was not consumed exactly. ModuleId={id}");
							}
						}
						catch (Exception ex) when (!joinBootstrapModule.IsRequired)
						{
							Debug.LogWarning($"Optional bootstrap module {id} preparation failed: {ex.Message}");
							continue;
						}
					}
					else
					{
						preparedBootstrapModule = new AtomicApplication(joinBootstrapModule, array);
					}
					list.Add(new BootstrapApplyOperation.PreparedModule
					{
						Module = joinBootstrapModule,
						Application = preparedBootstrapModule
					});
				}
				if (reader.Position != reader.Length)
				{
					throw new InvalidOperationException("Trailing bootstrap bytes.");
				}
				foreach (IJoinBootstrapModule module in _modules)
				{
					if (module.IsRequired && module.Supports(result.Context.Mode) && !hashSet.Contains(module.ModuleId))
					{
						throw new InvalidOperationException($"Required bootstrap module is missing. ModuleId={module.ModuleId}");
					}
				}
			}
			catch (Exception ex2)
			{
				result.FailReason = ex2.Message;
				list = null;
			}
			return new BootstrapApplyOperation(result, list, isCurrent, clock, frameBudgetMs, stepsPerFrame);
		}

		private static void WriteHeader(FastBufferWriter writer, JoinSessionContext context, ushort moduleCount)
		{
			writer.WriteValueSafe<ushort>((ushort)5, default(FastBufferWriter.ForPrimitives));
			byte value = (byte)context.Mode;
			writer.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			writer.WriteValueSafe<FixedString64Bytes>(new FixedString64Bytes(context.HostSteamId ?? string.Empty), default(FastBufferWriter.ForFixedStrings));
			writer.WriteValueSafe(in context.SlotId, default(FastBufferWriter.ForPrimitives));
			writer.WriteValueSafe(in context.SessionEpoch, default(FastBufferWriter.ForPrimitives));
			writer.WriteValueSafe(in context.SnapshotId, default(FastBufferWriter.ForPrimitives));
			writer.WriteValueSafe(in context.WorldRevision, default(FastBufferWriter.ForPrimitives));
			writer.WriteValueSafe(in moduleCount, default(FastBufferWriter.ForPrimitives));
		}

		public static bool TryReadContext(byte[] blob, out JoinSessionContext context)
		{
			context = default;
			if (blob == null || blob.Length == 0 || blob.Length > 1048576)
			{
				return false;
			}
			using FastBufferReader reader = new FastBufferReader(blob, Allocator.Temp);
			if (!TryReadHeader(reader, out var header) || header.Version != 5)
			{
				return false;
			}
			context = new JoinSessionContext
			{
				HostSteamId = header.HostSteamId.ToString(),
				SlotId = header.SlotId,
				SessionEpoch = header.SessionEpoch,
				Mode = (JoinBootstrapMode)header.Mode,
				SnapshotId = header.SnapshotId,
				WorldRevision = header.WorldRevision
			};
			return context.HasSnapshotIdentity;
		}

		private static bool TryReadHeader(FastBufferReader reader, out BootstrapSnapshotHeader header)
		{
			header = default;
			try
			{
				reader.ReadValueSafe(out ushort value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out FixedString64Bytes value3, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out int value4, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out ulong value5, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out ulong value6, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out ulong value7, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out ushort value8, default(FastBufferWriter.ForPrimitives));
				header = new BootstrapSnapshotHeader
				{
					Version = value,
					Mode = value2,
					HostSteamId = value3,
					SlotId = value4,
					SessionEpoch = value5,
					SnapshotId = value6,
					WorldRevision = value7,
					ModuleCount = value8
				};
			}
			catch
			{
				return false;
			}
			return true;
		}
	}
}
