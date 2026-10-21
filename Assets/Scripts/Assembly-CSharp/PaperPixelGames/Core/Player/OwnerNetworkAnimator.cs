using Unity.Netcode.Components;

namespace PaperPixelGames.Core.Player
{
	public class OwnerNetworkAnimator : NetworkAnimator
	{
		protected override bool OnIsServerAuthoritative()
		{
			return false;
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "OwnerNetworkAnimator";
		}
	}
}
