using Unity.Netcode;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public interface IFrameSlicedBootstrapModule : IJoinBootstrapModule
	{
		IPreparedBootstrapModule ClientPrepare(FastBufferReader reader);
	}
}
