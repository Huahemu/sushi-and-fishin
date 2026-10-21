using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Ai.NPCs.Dialogue
{
	[CreateAssetMenu(menuName = "Data/Dialogue/Quest Dialogue", fileName = "QuestDialogue")]
	public sealed class QuestDialogueSO : ScriptableObject
	{
		public string QuestId;

		public ActorType OwnerActorType;

		[Header("Menu Option Label")]
		public string OptionLabel = "Ask about the quest";

		[Header("Quest Info Text")]
		[TextArea(2, 10)]
		public string InfoText = "Quest info...";

		[Header("Quest Info Text")]
		[TextArea(2, 10)]
		public string AnswerText = "Quest info...";

		[Header("Continue Button Label")]
		public string ContinueLabel = "Continue";

		[Header("Continue Button Label")]
		public string EndTalkLabel = "Aight!";

		public string OptionLocalizationKey => "dialogue.quest." + QuestId + ".option";

		public string InfoLocalizationKey => "dialogue.quest." + QuestId + ".info";

		public string AnswerLocalizationKey => "dialogue.quest." + QuestId + ".answer";

		public string ContinueLocalizationKey => "dialogue.quest." + QuestId + ".continue";

		public string EndTalkLocalizationKey => "dialogue.quest." + QuestId + ".end";
	}
}
