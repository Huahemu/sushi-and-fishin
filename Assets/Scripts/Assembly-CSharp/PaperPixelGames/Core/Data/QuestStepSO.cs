using PaperPixelGames.Framework.Enums;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "QuestStep", menuName = "Data/QuestPipeline/Quest Step")]
	public class QuestStepSO : ScriptableObject
	{
		public ushort Id;

		public string Text;

		public QuestStepType StepType;

		public ushort Target = 1;

		public bool PreventOnDisabled;
	}
}
