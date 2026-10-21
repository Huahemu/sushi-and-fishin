namespace PaperPixelGames.Core.Ocean
{
	public readonly struct PullDropTargetCancelledEvent : IEvent
	{
		public readonly HarpoonPullDropTarget Target;

		public readonly ulong PrimaryClientId;

		public readonly PullDropTargetCancelReason Reason;

		public PullDropTargetCancelledEvent(HarpoonPullDropTarget target, ulong primaryClientId, PullDropTargetCancelReason reason)
		{
			Target = target;
			PrimaryClientId = primaryClientId;
			Reason = reason;
		}
	}
}
