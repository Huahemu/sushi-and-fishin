using System;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;

namespace PaperPixelGames.Core.Dialogue
{
	public class DialogueOption : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _optionText;

		[SerializeField]
		private LocalizeStringEvent _optionTextStringEvent;

		[SerializeField]
		private BaseButton _button;

		[SerializeField]
		private GameObject _selectedObject;

		private Action _optionAction;

		private void OnEnable()
		{
			_selectedObject.SetActive(value: false);
		}

		public void SetOption(DialogueOptionData part, Action action = null)
		{
			base.gameObject.SetActive(value: true);
			_optionTextStringEvent.StringReference.TableEntryReference = part.TextId;
			_optionAction = action;
			_button.Clicked += OnOptionAction;
			_button.HoverChanged += OnHover;
		}

		private void OnHover(bool status)
		{
			_selectedObject.SetActive(status);
		}

		public void ResetOption()
		{
			base.gameObject.SetActive(value: false);
			_optionText.text = "";
			_optionAction = null;
		}

		public void OnOptionAction()
		{
			_optionAction?.Invoke();
		}
	}
}
