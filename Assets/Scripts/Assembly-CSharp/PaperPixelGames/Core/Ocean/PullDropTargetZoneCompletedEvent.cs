namespace PaperPixelGames.Core.Ocean
{
	public readonly struct PullDropTargetZoneCompletedEvent : IEvent
	{
		public readonly HarpoonPullDropTarget Target;

		public readonly int ZoneIndex;

		public readonly float GlobalProgress01;

		public PullDropTargetZoneCompletedEvent(HarpoonPullDropTarget target, int zoneIndex, float globalProgress01)
		{
			Target = target;
			ZoneIndex = zoneIndex;
			GlobalProgress01 = globalProgress01;
		}
	}
}
