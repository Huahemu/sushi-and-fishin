namespace PaperPixelGames.Core.Ocean
{
	public readonly struct PullDropTargetFinishedEvent : IEvent
	{
		public readonly HarpoonPullDropTarget Target;

		public readonly ulong PrimaryClientId;

		public PullDropTargetFinishedEvent(HarpoonPullDropTarget target, ulong primaryClientId)
		{
			Target = target;
			PrimaryClientId = primaryClientId;
		}
	}
}
