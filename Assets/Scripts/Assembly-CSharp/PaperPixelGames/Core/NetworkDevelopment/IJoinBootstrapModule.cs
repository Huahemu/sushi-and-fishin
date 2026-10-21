using Unity.Netcode;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public interface IJoinBootstrapModule
	{
		ushort ModuleId { get; }

		int Priority { get; }

		bool IsRequired { get; }

		bool Supports(JoinBootstrapMode mode);

		bool ServerCapture(ulong targetClientId, JoinBootstrapMode mode);

		void ServerWrite(FastBufferWriter writer);

		bool ClientCanApply();

		void ClientReadAndApply(FastBufferReader reader);

		void ClientAfterAllApplied();
	}
}
