using System;
using System.Collections.Generic;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendTabletPresenter
    {
        FriendsLifeNetwork actionTransport;
        readonly Dictionary<string,UiReturnFrame> actionFrames=new Dictionary<string,UiReturnFrame>();
        string actionFeedback;
        UiReturnFrame feedbackFrame;
        int feedbackReceivedFrame;
        readonly HashSet<string> inlineActions=new HashSet<string>();
        string inlineText;UiReturnFrame inlineFrame;bool feedbackInline;

        void BindActionFeedback()
        {
            if(actionTransport==FriendsLifeNetwork.Instance)return;
            UnbindActionFeedback();actionTransport=FriendsLifeNetwork.Instance;
            if(actionTransport)actionTransport.ResponseReceived+=ReceiveActionFeedback;
        }
        void UnbindActionFeedback()
        {
            if(actionTransport)actionTransport.ResponseReceived-=ReceiveActionFeedback;
            actionTransport=null;
        }
        void ClearActionFeedback(){actionFrames.Clear();inlineActions.Clear();actionFeedback=null;feedbackFrame=null;inlineText=null;inlineFrame=null;}
        void SendTrackedAction(string action,string option)
        {SendTrackedCommand("action",action,option,action.StartsWith("minigame",StringComparison.Ordinal));}
        bool SendTrackedHousing(string action,HousingPlanCommand plan=null,string targetPersonId=null)
        {return SendTrackedCommand("housing",action,plan==null?null:JsonUtility.ToJson(plan),targetPersonId:targetPersonId);}
        bool SendTrackedCommand(string kind,string action,string option,bool inline=false,string targetPersonId=null)
        {
            BindActionFeedback();var human=FriendsLifeRuntime.Instance?.LocalHuman();
            if(!actionTransport||human==null){Status("正在连接，请稍后再试。");return false;}
            var command=new FriendsWorldCommand{commandId=Guid.NewGuid().ToString("N"),kind=kind,
                actorPersonId=human.personId,targetPersonId=targetPersonId??personId,actionId=action,optionId=option};
            actionFrames[command.commandId]=Capture();
            if(inline)inlineActions.Add(command.commandId);
            string result=actionTransport.Submit(command);if(inline)ShowInlineFeedback(result);else Status(result);
            return true;
        }
        void ReceiveActionFeedback(FriendsWorldResponse response)
        {
            if(response==null||!actionFrames.TryGetValue(response.commandId,out var frame))return;
            actionFrames.Remove(response.commandId);
            feedbackInline=inlineActions.Remove(response.commandId);
            if(!isActiveAndEnabled||page!=frame.page||personId!=frame.personId)return;
            actionFeedback=response.message;feedbackFrame=frame;feedbackReceivedFrame=Time.frameCount;
        }
        void UpdateActionFeedback()
        {
            BindActionFeedback();
            // The host sends its snapshot immediately after the response. Render
            // feedback on the following frame so that snapshot refresh cannot erase it.
            if(string.IsNullOrEmpty(actionFeedback)||Time.frameCount<=feedbackReceivedFrame)return;
            string message=actionFeedback;actionFeedback=null;
            if(isActiveAndEnabled&&feedbackFrame!=null&&page==feedbackFrame.page&&personId==feedbackFrame.personId){if(feedbackInline)ShowInlineFeedback(message);else Status(message);}
            feedbackFrame=null;
        }
        void ShowInlineFeedback(string message){inlineText=message;inlineFrame=Capture();RestoreInlineFeedback();}
        void RestoreInlineFeedback()
        {
            if(!view||string.IsNullOrEmpty(inlineText)||inlineFrame==null||page!=inlineFrame.page||personId!=inlineFrame.personId)return;
            // One line in the footer's left slot, cut with an ellipsis.
            if(current!=null&&current.status)current.status.text=FriendUi.PlayerText(inlineText,"没有完成，请稍后再试。");
        }
    }
}
