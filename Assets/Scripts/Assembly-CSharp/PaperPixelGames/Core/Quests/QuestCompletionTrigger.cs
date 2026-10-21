using UnityEngine;

namespace PaperPixelGames.Core.Quests
{
	public class QuestCompletionTrigger : MonoBehaviour
	{
		[SerializeField]
		private QuestSO _quest;

		public void Complete()
		{
			if (_quest == null)
			{
				Debug.LogWarning("[QuestCompletionTrigger] Quest is null.", this);
			}
			else
			{
				QuestManager.Instance.RequestCompleteQuest(_quest);
			}
		}
	}
}
