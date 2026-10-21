using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;

namespace PaperPixelGames.Core.Dialogue
{
	public class DialogueManager : Singleton<DialogueManager>
	{
		[SerializeField]
		private Canvas _chatCanvas;

		[SerializeField]
		private TextMeshProUGUI _agentNameText;

		[SerializeField]
		private TextMeshProUGUI _agentChatText;

		[SerializeField]
		private LocalizeStringEvent _agentChatTextStringEvent;

		[SerializeField]
		private List<DialogueOption> _options;

		[Space]
		[SerializeField]
		private List<DialogueMainSO> _dialogueMains;

		[SerializeField]
		private List<DialogueMainSO> _childDialogueMains;

		[SerializeField]
		private List<DialogueMainSO> _pedestrianDialogueMains;

		private DialogueMainSO _currentDialogue;

		private DialoguePartSO _currentPart;

		private Action _callBack;

		public override SingletonScope Scope => SingletonScope.Scene;

		public List<DialogueMainSO> ChildDialogueMain => _childDialogueMains;

		public List<DialogueMainSO> PedestrianDialogueMains => _pedestrianDialogueMains;

		private void Start()
		{
			_chatCanvas.enabled = false;
			_agentNameText.text = "";
			_agentChatText.text = "";
			_options.ForEach((DialogueOption x) =>
			{
				x.ResetOption();
			});
		}
	}
}
