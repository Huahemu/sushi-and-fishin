using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using UnityEngine;
using UnityEngine.Localization.Components;

namespace PaperPixelGames.Core.Managers
{
    // Extra conversation pages use the shop's actual panel, layout and button
    // prefab. The original shop pages and their localized labels are retained.
    public sealed partial class DialoguePanelController
    {
        public readonly struct ConversationChoice
        {
            public readonly string Text;
            public readonly Action Selected;
            public ConversationChoice(string text, Action selected) { Text=text; Selected=selected; }
        }

        readonly List<BaseButton> _conversationButtons = new List<BaseButton>();
        ConversationChoice[] _conversationChoices = Array.Empty<ConversationChoice>();
        Action _conversationClosed;
        bool _customConversation;
        public bool HasCustomConversation => _customConversation && IsVisible;
        public string ConversationText { get; private set; }
        public IReadOnlyList<BaseButton> ConversationButtons => _conversationButtons;

        public void ShowCustomConversation(ResidentBillboardText billboard, string text, Action onClosed, params ConversationChoice[] choices)
        {
            if (!billboard || !_panelRoot || !_buttonsRoot || !_purchaseButton)
                throw new InvalidOperationException("The native shop dialogue and billboard must be available.");
            _customConversation=true;
            _conversationChoices=choices ?? Array.Empty<ConversationChoice>();
            _conversationClosed=onClosed;
            ConversationText=text;
            _billboard=billboard;
            _currentInteractedResident=null;
            _employee=null;
            _questForThisNpc=null;
            _mode=DialogueMode.Menu;
            while (_conversationButtons.Count < _conversationChoices.Length)
            {
                int index=_conversationButtons.Count;
                var button=Instantiate(_purchaseButton, _buttonsRoot.transform, false);
                button.name="ConversationChoice"+index;
                button.gameObject.SetActive(false);
                // A cloned purchase label must not later be overwritten by the
                // shop's localization event when this page becomes visible.
                foreach(var localizer in button.GetComponentsInChildren<LocalizeStringEvent>(true)) localizer.enabled=false;
                button.Clicked+=()=>SelectConversationChoice(index);
                _conversationButtons.Add(button);
            }
            for(int i=0;i<_conversationChoices.Length;i++) _conversationButtons[i].SetText(_conversationChoices[i].Text);
            var ui=Singleton<GameUIManager>.Instance;
            if(ui)ui.IsShopUIActive=true;
            PPG_Player player=Singleton<PlayersManager>.Instance?.LocalPlayer;
            if(player){player.SwitchTo("UI",cursorVisible:true);player.HideCursor();}
            RegisterCancelEvent();
            SetVisible(true);
            PlayBillboardText(text);
        }

        void SelectConversationChoice(int index)
        {
            if (!_customConversation || !IsVisible || index<0 || index>=_conversationChoices.Length || TrySkipTyping()) return;
            _conversationChoices[index].Selected?.Invoke();
        }
        void HideCustomButtons() { foreach(var b in _conversationButtons) if(b)b.gameObject.SetActive(false); }
        bool ApplyCustomButtons()
        {
            if(!_customConversation)return false;
            for(int i=0;i<_conversationChoices.Length;i++)_conversationButtons[i].gameObject.SetActive(true);
            return true;
        }
        Action EndCustomConversation()
        {
            if(!_customConversation)return null;
            _customConversation=false;
            _billboard?.Hide();
            HideCustomButtons();
            _conversationChoices=Array.Empty<ConversationChoice>();
            var closed=_conversationClosed;
            _conversationClosed=null;
            ConversationText=null;
            return closed;
        }
    }
}
