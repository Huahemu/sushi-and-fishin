using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public sealed class QuestStateData
	{
		public ushort QuestId;

		public List<QuestStepStateData> StepStates = new List<QuestStepStateData>();
	}
}
