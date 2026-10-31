using System;
using PaperPixelGames.Core.Ai.NPCs;
using TMPro;
using UnityEngine;
using PaperPixelGames.Framework.UI;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendTabletPresenter
    {
        public void OpenOriginalActivity(string id)
        {if(!FriendsLifeFeatureFlags.DebugActivityPage)return;Push();personId=id;page="OriginalActivity";Render();}
        void RenderOriginalActivity()
        {
            var runtime=FriendsLifeRuntime.Instance;var person=runtime?.GetPerson(personId);
            // Debug page: hidden while DebugActivityPage is off.
            if(!FriendsLifeFeatureFlags.DebugActivityPage){page=person!=null?"Detail":"List";if(person!=null)RenderDetail();else RenderList();return;}
            var p=Page("FriendsOriginalActivity","当前活动");var body=p.body;float y=0;
            var name=FriendUi.Text("Person",body,0,y,1088,52,person?.displayName??"朋友",30);name.textWrappingMode=TMPro.TextWrappingModes.NoWrap;y+=68;
            var text=FriendUi.Text("Status",body,0,y,1088,120,FriendOriginalActivityUi.Header(personId),27);text.alignment=TMPro.TextAlignmentOptions.TopLeft;y+=136;
            FriendUi.Flow(body,ref y,"ActivityHelp","可以让朋友结束当前活动。工作、吃饭、聊天中不能结束。",24,1088);
            var stop=FriendUi.Button("SafeReturn",body,0,y,530,64,"结束活动",()=>
            {if(!FriendOriginalActivityUi.TryCancelCurrent(personId,out var reason))Status(FriendUi.PlayerText(reason,"请稍等。"));},true);
            stop.interactable=FriendOriginalActivityUi.CanCancelCurrent(personId);
            var observer=p.paper.gameObject.AddComponent<FriendOriginalActivityPageStatus>();observer.Bind(personId,text,stop);
            BackButton();
        }
    }
    // Text/availability only. No player focus, choices, clock or body mutation.
    public sealed class FriendOriginalActivityPageStatus:MonoBehaviour
    {
        string person;TMP_Text text;BaseButton stop;float next;
        public void Bind(string id,TMP_Text value,BaseButton button){person=id;text=value;stop=button;}
        void Update()
        {
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+.25f;
            if(text){string current=FriendOriginalActivityUi.Header(person);if(text.text!=current)text.text=current;}
            if(stop)stop.interactable=FriendOriginalActivityUi.CanCancelCurrent(person);
        }
    }
}
