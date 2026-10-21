using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "NewDialogueMain", menuName = "Data/Dialogue/Main")]
	public class DialogueMainSO : ScriptableObject
	{
		public string Id;

		public DialoguePartSO MainPart;
	}
}
