namespace SushiAndFish.Casino.Original
{
using global::Mirror;

namespace Smooth
{
	public struct NetworkStateMirror : NetworkMessage
	{
		public SmoothSyncMirror smoothSync;

		public StateMirror state;

		public void copyFromSmoothSync(SmoothSyncMirror smoothSyncScript)
		{
			smoothSync = smoothSyncScript;
			state.copyFromSmoothSync(smoothSyncScript);
		}
	}
}

}
