using Unity.Netcode.Components;
using UnityEngine;

namespace ECM2.Examples.Networking.Netcode
{
	[DisallowMultipleComponent]
	public class ClientNetworkTransform : NetworkTransform
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
			return "ClientNetworkTransform";
		}
	}
}
