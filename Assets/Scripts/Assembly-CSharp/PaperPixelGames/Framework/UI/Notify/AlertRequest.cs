namespace PaperPixelGames.Framework.UI.Notify
{
	public readonly struct AlertRequest
	{
		public readonly string Header;

		public readonly string Description;

		public readonly float Duration;

		public readonly bool AutoClose;

		public readonly AlertPriority Priority;

		public readonly bool UsePriorityPulse;

		public AlertRequest(string description, float duration = 2f, AlertPriority priority = AlertPriority.Normal, bool autoClose = true, bool usePriorityPulse = true)
		{
			Header = string.Empty;
			Description = description;
			Duration = duration;
			AutoClose = autoClose;
			Priority = priority;
			UsePriorityPulse = usePriorityPulse;
		}

		public AlertRequest(string header, string description, float duration = 2f, AlertPriority priority = AlertPriority.Normal, bool autoClose = true, bool usePriorityPulse = true)
		{
			Header = header;
			Description = description;
			Duration = duration;
			AutoClose = autoClose;
			Priority = priority;
			UsePriorityPulse = usePriorityPulse;
		}
	}
}
