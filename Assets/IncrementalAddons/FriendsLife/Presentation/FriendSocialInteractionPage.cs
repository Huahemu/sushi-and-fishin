using System;
using System.Linq;
using System.Collections.Generic;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Friends profile -> pending request -> actual native E -> original source
    // options -> queued physical action -> source result -> original life.
    public sealed partial class FriendTabletPresenter
    {
        void RenderSocialProfileSummary(Transform content,ref float y)
        {
            var runtime=FriendsLifeRuntime.Instance;if(!runtime)return;
            int pending=runtime.GetEpisodes(personId).Count(e=>!e.committed&&e.sourceKind=="Family");
            if(pending>0)Field(content,ref y,"社交",pending+"件正在进行的关系互动");
            Menu(content,ref y,"社交消息","social");
        }
        bool RenderSourceSocialPage(string category,Transform content,ref float y)
        {
            if(category!="social")return false;
            var runtime=FriendsLifeRuntime.Instance;var person=runtime?.GetPerson(personId);if(person==null)return true;
            // Recent friend-with-friend chats (journal), newest first.
            var chats=runtime.Core.State.journal.Where(j=>j.kind=="chat"&&(j.focusResidentId==person.nativeResidentId||j.targetResidentId==person.nativeResidentId))
                .Reverse().Take(5).ToArray();
            foreach(var chat in chats)Field(content,ref y,"聊天",chat.message);
            var episodes=runtime.GetEpisodes(personId).Where(e=>e.sourceKind=="Family"&&e.familyChoice!=null)
                .OrderByDescending(e=>e.sourceMinute).Take(24).ToArray();
            if(episodes.Length==0&&chats.Length==0)Field(content,ref y,"社交消息","暂时没有新的关系互动。");
            foreach(var episode in episodes)
            {
                var other=runtime.GetPerson(episode.focusPersonId==personId?episode.targetPersonId:episode.focusPersonId);
                string phase=episode.status=="AwaitHuman"?"等待回应":episode.status=="NpcApproach"?"正在走近":episode.status=="NpcApproachReleasing"||episode.status=="Returning"?"正在恢复活动":
                    episode.status=="Travel"||episode.status=="HumanInputQueued"?"正在前往":episode.status=="Done"?"已经结束":
                    episode.status=="WorldPresentation"||episode.status=="SourceActivity"?"正在互动":
                    episode.status=="BindingFault"||episode.status=="Suspended"?"请稍等":"正在安排";
                var offer=runtime.State.worldTalks.LastOrDefault(r=>r.npcInitiated&&r.sourceEpisodeId==episode.episodeId);
                if(offer?.phase=="Unknown")phase="回应结果待确认";
                else if(offer?.phase=="InvitePending")phase="等待当前活动结束";
                else if(offer?.phase=="Offered"&&!string.IsNullOrEmpty(offer.approachFailure))phase="暂时无法过来，可以靠近后交谈";
                else if(offer?.phase=="Offered"&&offer.ignored)phase="暂未回应，朋友已继续自己的活动";
                else if(episode.status=="SourceRejected")phase="没能完成，请稍后再试";
                string result=runtime.State.worldTalks.LastOrDefault(r=>r.sourceResult&&r.sourceEpisodeId==episode.episodeId)?.resultMessage;
                Field(content,ref y,other?.displayName??"关系互动",episode.familyChoice.title+"\n"+phase+(string.IsNullOrEmpty(result)?"":"\n"+result));
                if(!episode.committed&&!episode.inputAccepted&&episode.status=="AwaitHuman")
                    Field(content,ref y,"回应方式","走到朋友身边，按“交谈”回应。");
            }
            return true;
        }
    }

    // This entry is called only by the existing DBS E interaction route. It
    // uses the original dialogue panel, billboard and original R option labels.
    public sealed class FriendSocialDialogueController:MonoBehaviour
    {
        static FriendSocialDialogueController instance;
        FriendsLifeNetwork network;string world,human,person,request,pending;
        bool submitted,showing,closing,replacing;
        sealed class PendingIntent
        {public string world,human,person,request,command,option;}
        readonly Dictionary<string,PendingIntent> intents=new Dictionary<string,PendingIntent>();
        readonly Dictionary<string,int> attempts=new Dictionary<string,int>();
        ResidentBillboardText ownedBillboard;
        public int OpenCount{get;private set;}
        public int SourceInputCount{get;private set;}
        public int PendingUnresolvedCount=>intents.Count;
        public string CurrentRequestId=>request;
        public string CurrentPersonId=>person;
        public static string ActivePersonId=>instance&&instance.showing?instance.person:null;
        public static bool TryOpenNpcOffer(string npc)
        {
            var runtime=FriendsLifeRuntime.Instance;var player=runtime?.LocalHuman();
            if(!runtime||!runtime.Ready||player==null||!FriendWorldTalkAuthority.WithinReach(runtime,player.personId,npc))return false;
            var row=FriendWorldTalkAuthority.PendingNpcOffer(runtime,npc,player.personId);if(row==null)return false;
            if(!instance){var go=new GameObject("Friend social dialogue");DontDestroyOnLoad(go);instance=go.AddComponent<FriendSocialDialogueController>();}
            return instance.Open(runtime,row);
        }
        void BindNetwork()
        {
            var current=FriendsLifeNetwork.Instance;if(network==current)return;
            if(network)network.ResponseReceived-=Response;network=current;if(network)network.ResponseReceived+=Response;
        }
        DialoguePanelController Panel=>Singleton<GameUIManager>.TryGetInstance(out var ui)?ui.DialoguePanelController:null;
        ResidentBillboardText Billboard(FriendsLifeRuntime runtime)
        {
            var actor=runtime.ActorOf(person);var original=actor?actor.GetComponentInChildren<ResidentBillboardText>(true):null;
            var body=FriendWorldTalkAuthority.VisibleBody(runtime,person);if(!original||!body)return null;
            if(original.isActiveAndEnabled)return original;
            if(ownedBillboard)return ownedBillboard;
            if(original.transform==actor.transform||original.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true).Length!=0)return null;
            ownedBillboard=Instantiate(original,body,false);ownedBillboard.gameObject.SetActive(true);return ownedBillboard;
        }
        bool Open(FriendsLifeRuntime runtime,FriendWorldTalkRequest row)
        {
            var panel=Panel;if(!panel||panel.HasCustomConversation&&!showing)return false;
            if(request!=null)Close(!submitted);
            world=runtime.State.worldId;human=row.humanId;person=row.personId;request=row.requestId;submitted=false;BindNetwork();
            var episode=runtime.PendingNpcSocialChoice(person,human);var billboard=Billboard(runtime);
            if(episode?.familyChoice==null||!billboard)return false;
            if(intents.TryGetValue(world+"/"+request,out var prior))
            {pending=prior.command;submitted=true;ShowUnresolved(runtime);OpenCount++;return true;}
            var choices=episode.familyChoice.options.Where(o=>o.enabled).Select(option=>
            {
                string id=option.id;return new DialoguePanelController.ConversationChoice(option.label,()=>Submit(id));
            }).ToList();
            choices.Add(new DialoguePanelController.ConversationChoice("暂不回应",()=>Close(true)));
            showing=true;OpenCount++;panel.ShowCustomConversation(billboard,episode.familyChoice.title,()=>{if(!replacing)Close(true);},choices.ToArray());return true;
        }
        void Send(string verb,string command,string option=null)
        {
            BindNetwork();var runtime=FriendsLifeRuntime.Instance;
            var data=new FriendsWorldCommand{kind="world-talk",worldId=world,actorPersonId=human,targetPersonId=person,
                optionId=request,commandId=command,actionId=verb,nativeProfileJson=option};
            if(network)network.Submit(data);
            else if(runtime&&runtime.IsAuthority)
            {
                bool accepted=FriendWorldTalkAuthority.Instance.Execute(runtime,human,data,out var message);
                Response(new FriendsWorldResponse{commandId=command,accepted=accepted,unresolved=FriendWorldTalkAuthority.Find(runtime,request)?.phase=="Unknown",message=message});
            }
        }
        void Submit(string option)
        {
            if(submitted||pending!=null||request==null)return;
            var runtime=FriendsLifeRuntime.Instance;
            if(!runtime||runtime.State.worldId!=world||!FriendWorldTalkAuthority.WithinReach(runtime,human,person))return;
            string key=world+"/"+request;int attempt=attempts.TryGetValue(key,out var previous)?previous+1:1;attempts[key]=attempt;
            submitted=true;pending="s8/"+world.Substring(0,8)+"/"+request.Substring(request.LastIndexOf('/')+1)+"/i/"+attempt;
            intents[key]=new PendingIntent{world=world,human=human,person=person,request=request,command=pending,option=option};
            SourceInputCount++;Send("offer-input",pending,option);
        }
        void Response(FriendsWorldResponse response)
        {
            var pair=intents.FirstOrDefault(p=>p.Value.command==response.commandId);
            if(pair.Value==null)return;
            bool current=pending==response.commandId&&world==pair.Value.world&&request==pair.Value.request;
            if(response.unresolved)
            {
                if(current&&showing)ShowUnresolved(FriendsLifeRuntime.Instance);
                return;
            }
            intents.Remove(pair.Key);if(current){pending=null;Close(false);}
        }
        void ShowUnresolved(FriendsLifeRuntime runtime)
        {
            var panel=Panel;var billboard=Billboard(runtime);if(!panel||!billboard)return;
            replacing=true;
            try{panel.ShowCustomConversation(billboard,"暂未收到这次回应的结果。",()=>{if(!replacing)Close(false);},
                new[]{new DialoguePanelController.ConversationChoice("返回",()=>Close(false))});showing=true;}
            finally{replacing=false;}
        }
        void Close(bool ignore)
        {
            if(closing)return;closing=true;
            try
            {
            if(ignore&&!submitted&&request!=null)Send("offer-ignore",request+"/ignore");
            bool hide=showing;showing=false;if(hide&&Panel&&Panel.HasCustomConversation)Panel.Hide();
            if(ownedBillboard)Destroy(ownedBillboard.gameObject);ownedBillboard=null;
            // A detached uncertain request retains its exact command/payload in
            // intents, while unrelated NPC requests remain independently usable.
            world=human=person=request=pending=null;submitted=false;
            }
            finally{closing=false;}
        }
        void Update()
        {
            var runtime=FriendsLifeRuntime.Instance;if(!runtime||!runtime.Ready)return;
            foreach(var pair in intents.Where(p=>p.Value.world==runtime.State.worldId).ToArray())
                if(FriendWorldTalkAuthority.Find(runtime,pair.Value.request)?.phase=="Done")intents.Remove(pair.Key);
            if(request==null)return;
            if(runtime.State.worldId!=world){pending=null;Close(false);return;}
            var row=FriendWorldTalkAuthority.Find(runtime,request);
            if(row?.phase=="Done"){pending=null;Close(false);}
            else if(showing&&!FriendWorldTalkAuthority.WithinReach(runtime,human,person))Close(!submitted);
        }
        void OnDestroy(){if(network)network.ResponseReceived-=Response;Close(false);if(instance==this)instance=null;}
    }
}
