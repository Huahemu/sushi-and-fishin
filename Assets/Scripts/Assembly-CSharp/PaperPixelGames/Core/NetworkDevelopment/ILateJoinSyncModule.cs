using Unity.Netcode;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public interface ILateJoinSyncModule
	{
		ushort ModuleId { get; }

		int Priority { get; }

		void ServerCaptureSnapshot(ulong forClientId);

		void ServerWriteCachedSnapshot(FastBufferWriter writer);

		bool ClientCanApply();

		void ClientReadAndApplySnapshot(FastBufferReader reader);

		void ClientAfterAllApplied();
	}
}
