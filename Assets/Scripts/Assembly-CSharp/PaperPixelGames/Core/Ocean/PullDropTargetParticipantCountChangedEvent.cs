namespace PaperPixelGames.Core.Ocean
{
	public readonly struct PullDropTargetParticipantCountChangedEvent : IEvent
	{
		public readonly HarpoonPullDropTarget Target;

		public readonly int ParticipantCount;

		public PullDropTargetParticipantCountChangedEvent(HarpoonPullDropTarget target, int participantCount)
		{
			Target = target;
			ParticipantCount = participantCount;
		}
	}
}
