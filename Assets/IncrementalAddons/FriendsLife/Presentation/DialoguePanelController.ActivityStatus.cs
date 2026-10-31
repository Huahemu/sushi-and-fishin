using PaperPixelGames.Core.Ai.NPCs;

namespace PaperPixelGames.Core.Managers
{
    public sealed partial class DialoguePanelController
    {
        // Refresh only the known visible root page's status text. Do not
        // rebuild choices, change focus, or reopen a dismissed conversation.
        public bool TryRefreshActivityStatus(ResidentBillboardText expectedBillboard,string previousText,string currentText)
        {
            if(!HasCustomConversation||_billboard!=expectedBillboard||ConversationText!=previousText)return false;
            if(previousText==currentText)return true;
            ConversationText=currentText;PlayBillboardText(currentText);return true;
        }
    }
}
