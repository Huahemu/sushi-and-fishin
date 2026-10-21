using System.Collections;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public interface IPreparedBootstrapModule
	{
		bool IsReady { get; }

		IEnumerator ApplySteps();
	}
}
