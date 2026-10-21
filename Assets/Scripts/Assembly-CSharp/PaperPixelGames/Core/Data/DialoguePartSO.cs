using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "NewDialoguePart", menuName = "Data/Dialogue/Part")]
	public class DialoguePartSO : ScriptableObject
	{
		public StringTable agentSpeechTable;

		public string PreviewOfTextId;

		public string TextId;

		public List<DialogueOptionData> Options;
	}
}
