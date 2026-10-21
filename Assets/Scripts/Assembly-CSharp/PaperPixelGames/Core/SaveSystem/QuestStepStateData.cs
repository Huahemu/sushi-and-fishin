using System;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public sealed class QuestStepStateData
	{
		public ushort StepId;

		public ushort CurrentValue;
	}
}
