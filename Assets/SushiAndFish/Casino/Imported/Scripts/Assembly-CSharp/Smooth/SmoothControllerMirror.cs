namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using global::UnityEngine;

namespace Smooth
{
	public class SmoothControllerMirror : MonoBehaviour
	{
		public static bool isHandlerRegistered;

		private void Awake()
		{
			RegisterHandlers();
		}

		private void Update()
		{
			if ((NetworkServer.active || NetworkClient.active) && !isHandlerRegistered)
			{
				RegisterHandlers();
			}
			if (!NetworkServer.active && !NetworkClient.active && isHandlerRegistered)
			{
				isHandlerRegistered = false;
			}
		}

		public static void RegisterHandlers()
		{
			NetworkServer.ReplaceHandler<NetworkStateMirror>(SmoothSyncMirror.HandleSyncServer);
			NetworkClient.ReplaceHandler<NetworkStateMirror>(SmoothSyncMirror.HandleSyncClient);
			isHandlerRegistered = true;
		}
	}
}

}
