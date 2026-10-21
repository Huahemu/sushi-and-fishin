using System;
using PaperPixelGames.Core.Dialogue;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[Serializable]
	public class DialogueOptionData
	{
		public string PreviewOfTextId;

		public string TextId;

		public DialoguePartSO NextPart;

		[SerializeReference]
		public IDialogueAction OptionAction;
	}
}
