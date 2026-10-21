using Unity.Netcode.Components;

namespace Dissonance.Integrations.Unity_NFGO.Demo
{
	public class NfgoClientNetworkTransform : NetworkTransform
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
			return "NfgoClientNetworkTransform";
		}
	}
}
