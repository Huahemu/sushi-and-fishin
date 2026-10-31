using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed class FriendDialoguePresenter : MonoBehaviour
    {
        static FriendDialoguePresenter instance;static SourceTextResolver resolver;
        public static event Action<string,string,string,string,IReadOnlyDictionary<string,string>> SourceControlRequested;
        readonly Dictionary<string,Coroutine> running=new Dictionary<string,Coroutine>();
        public static SourceTextResolver Texts=>resolver??(resolver=new SourceTextResolver());
        static FriendDialoguePresenter Instance
        {get{if(!instance){var go=new GameObject("Friends Source Presentation");DontDestroyOnLoad(go);instance=go.AddComponent<FriendDialoguePresenter>();}return instance;}}
        public static void ShowSourceLine(string episodeId,string nodeKey,string textRef,string speakerPersonId,Action completed)
        {ShowSourceLine(episodeId,nodeKey,textRef,speakerPersonId,null,completed);}
        public static void ShowSourceLine(string episodeId,string nodeKey,string textRef,string speakerPersonId,IReadOnlyDictionary<string,string> slots,Action completed)
        {ShowSourceLine(episodeId,nodeKey,textRef,speakerPersonId,slots,completed,null);}
        public static void ShowSourceLine(string episodeId,string nodeKey,string textRef,string speakerPersonId,IReadOnlyDictionary<string,string> slots,Action completed,Action<string> failed)
        {
            BindPersonSlots(speakerPersonId,slots);var runs=Texts.Play(textRef);var self=Instance;string key=episodeId+"/"+nodeKey;
            if(self.running.ContainsKey(key))return;
            self.running[key]=null;
            string archive=slots!=null&&slots.TryGetValue("@SourceMotionArchive",out var suppliedArchive)?suppliedArchive:"";
            var coroutine=self.StartCoroutine(self.PlayLine(key,episodeId,nodeKey,runs,speakerPersonId,archive,completed,failed));
            // An empty original line may complete synchronously before
            // StartCoroutine returns. Do not reinsert its finished handle.
            if(self.running.ContainsKey(key))self.running[key]=coroutine;
        }
        static void BindPersonSlots(string id,IReadOnlyDictionary<string,string> slots)
        {
            Texts.BindContext(slots);
            string Person(IReadOnlyDictionary<string,string> args,string key="mii")
            {string slot=args.TryGetValue(key,out var s)?s:"SlotMii0";return slots!=null&&slots.TryGetValue(slot,out var p)?p:slot=="Talker"?id:null;}
            foreach(string tag in new[]{"Nickname","Nickname_Ex_JPKR"})Texts.BindSlots(tag,args=>FriendsLifeRuntime.Instance?.GetPerson(Person(args))?.displayName);
            foreach(string tag in new[]{"PlayerNicknameCommon","PlayerNicknameByMii"})Texts.BindSlots(tag,args=>FriendsLifeRuntime.Instance?.LocalHuman()?.displayName);
            Texts.BindSlots("Relation",args=>Relation(Person(args,"mii0")??id,Person(args,"mii1")??(slots!=null&&slots.TryGetValue("SlotMii1",out var other)?other:null),false));
            Texts.BindSlots("RelationLong",args=>Relation(Person(args,"mii0")??id,Person(args,"mii1")??(slots!=null&&slots.TryGetValue("SlotMii1",out var other)?other:null),false));
            Texts.BindSlots("RelationMeterRank",args=>Relation(id,slots!=null&&slots.TryGetValue("SlotMii1",out var other)?other:null,true));
        }
        static string Relation(string from,string to,bool rank)
        {
            var runtime=FriendsLifeRuntime.Instance;var a=runtime?.GetPerson(from);var b=runtime?.GetPerson(to);
            if(a==null||b==null)return null;
            var relationship=runtime.Core.State.relationships.FirstOrDefault(r=>r.fromId==a.nativeResidentId&&r.toId==b.nativeResidentId);
            return relationship==null?null:rank?Texts.RelationRank(relationship.kind,relationship.nativeRank,relationship.fighting,relationship.oneSideLove):Texts.RelationName(relationship.kind);
        }
        IEnumerator PlayLine(string key,string episodeId,string nodeKey,List<SourceTextResolver.Run> runs,string speaker,string archive,Action complete,Action<string> failed)
        {
            var actor=FriendsLifeRuntime.Instance?.ActorOf(speaker);var candidate=actor?actor.GetComponentInChildren<ResidentBillboardText>(true):null;
            var billboard=candidate&&candidate.isActiveAndEnabled?candidate:null;
            var controls=actor?actor.GetComponent<FriendSourceTextControl>():null;if(actor&&!controls)controls=actor.gameObject.AddComponent<FriendSourceTextControl>();
            string text="";foreach(var run in runs)
            {
                Texts.EmitControl(run);
                if(run.control!=null)
                {
                    SourceControlRequested?.Invoke(episodeId,nodeKey,speaker,run.control,run.args);
                    if(controls&&!controls.Apply(run,episodeId+"/"+nodeKey,ref text,archive,out var error))
                    {controls.EndLine();running.Remove(key);failed?.Invoke(error);yield break;}
                    if(!controls&&run.control!="Speed"&&run.control!="SpeedDirect"&&run.control!="vtml_speed")
                    {running.Remove(key);failed?.Invoke("Original text control has no actual speaker owner: "+run.control);yield break;}
                }
                if(run.pause>0){float remaining=run.pause;while(remaining>0){if(Time.timeScale>0)remaining-=Time.unscaledDeltaTime;yield return null;}}
                if(run.pageBreak){text="";if(billboard)billboard.Hide();continue;}
                if(run.text==null)continue;int previouslyVisible=billboard?FriendSourceBillboard.GetOrAdd(billboard).SourceCharacterCount:0;text+=run.text;
                if(billboard){bool finished=false;FriendSourceBillboard.GetOrAdd(billboard).PlaySourceTypewriter(text,run.speed,run.charSeconds,previouslyVisible,()=>finished=true);while(!finished&&billboard){yield return null;}}
                // Offscreen presentation still uses the host typewriter's .01s character clock.
                else{float left=run.text.Length*(run.charSeconds>=0?run.charSeconds:.01f/Mathf.Max(.01f,run.speed));while(left>0){if(Time.timeScale>0)left-=Time.unscaledDeltaTime;yield return null;}}
            }
            if(controls)controls.EndLine();running.Remove(key);complete?.Invoke();
        }
        public static void ShowSourceChoice(string episodeId,string nodeKey,string textRef,string speakerPersonId,string[] sourceIds,Action<string> selected)
        {
            BindPersonSlots(speakerPersonId,null);var labels=Texts.Choose(textRef);if(labels.Length!=sourceIds.Length)throw new InvalidOperationException("Source choice/TextRef count mismatch: "+textRef);
            var actor=FriendsLifeRuntime.Instance?.ActorOf(speakerPersonId);var billboard=actor?actor.GetComponentInChildren<ResidentBillboardText>(true):null;var panel=Singleton<GameUIManager>.Instance?.DialoguePanelController;
            if(!billboard||!panel)throw new InvalidOperationException("Source choice requires the real actor and original dialogue panel");
            var choices=labels.Select((label,index)=>new DialoguePanelController.ConversationChoice(label,()=>{panel.Hide();selected(sourceIds[index]);})).ToArray();panel.ShowCustomConversation(billboard,"",null,choices);
        }
        public static void ShowWorldInteraction(string personId) { if(!FriendSocialDialogueController.TryOpenNpcOffer(personId))FriendWorldTalkController.Open(personId); }
        internal static void ShowWorldInteractionMenu(string personId, ResidentBillboardText billboard, Action closed, Action<string> selected, bool sourceChoiceOnly)
        {
            var panel=Singleton<GameUIManager>.Instance?.DialoguePanelController;if(!billboard||!panel)return;
            // Tomodachi E menu: talk, help with a problem (only when there is
            // one), give a gift, offer a job. 活动 is cut (empty page).
            var items=new List<DialoguePanelController.ConversationChoice>{new DialoguePanelController.ConversationChoice("交谈",()=>selected("talk"))};
            if(!sourceChoiceOnly)
            {
                if(FriendPlayerInteractions.Problem(FriendsLifeRuntime.Instance,personId)!=null)items.Add(new DialoguePanelController.ConversationChoice("帮忙",()=>selected("help")));
                items.Add(new DialoguePanelController.ConversationChoice("送礼",()=>selected("items")));
                items.Add(new DialoguePanelController.ConversationChoice("工作",()=>selected("jobs")));
            }
            items.Add(new DialoguePanelController.ConversationChoice("离开",()=>selected("leave")));
            panel.ShowCustomConversation(billboard,FriendOriginalActivityUi.Header(personId),closed,items.ToArray());
            FriendOriginalActivityUi.Track(panel,billboard,personId);
        }
        internal static void ShowWorldInteractionSource(string person,string page,ResidentBillboardText billboard,Action closed,Action back,Action<string,string> submit,Action<PaperPixelGames.Core.Staff.StaffRole> job,Action<string> open)
        {
            // Navigation stays in the native fish-shop panel. Domain input is
            // submitted only by the owner of the authority talk request.
            var runtime=FriendsLifeRuntime.Instance;var panel=Singleton<GameUIManager>.Instance?.DialoguePanelController;if(!runtime||!billboard||!panel)return;
            var choices=new List<DialoguePanelController.ConversationChoice>();var human=runtime.LocalHuman();string text="";
            if(page=="items")
            {
                // In-person gift: the Deep Blue Sushi item in hand, or a small
                // gift bought on the spot with the player's money.
                text="要送什么？";
                string held=FriendPlayerInteractions.HeldGiftKind(FriendWorldTalkController.HeldItem());
                if(held!=null)choices.Add(new DialoguePanelController.ConversationChoice("送出手里的"+TomodachiLife.Simulation.LifeSimulationRuntime.PlayerGiftName(held),()=>submit(FriendPlayerInteractions.HeldPrefix+held,null)));
                foreach(var kind in FriendPlayerInteractions.BuyKinds)
                {
                    string action=FriendPlayerInteractions.BuyPrefix+kind;
                    choices.Add(new DialoguePanelController.ConversationChoice("买"+TomodachiLife.Simulation.LifeSimulationRuntime.PlayerGiftName(kind)+"（"+FriendPlayerInteractions.BuyPriceRaw(kind)/100+" 金币）",()=>submit(action,null)));
                }
            }
            else if(page=="help")
            {
                var request=FriendPlayerInteractions.Problem(runtime,person);
                if(request==null)text="已经没事了，谢谢你！";
                else
                {
                    text=request.description;string action=FriendPlayerInteractions.HelpPrefix+request.id;
                    foreach(var answer in FriendPlayerInteractions.HelpChoices(runtime,request))
                    {
                        string option=answer.Value;
                        choices.Add(new DialoguePanelController.ConversationChoice(answer.Key,()=>{if(option==null)open("items");else submit(action,option);}));
                    }
                }
            }
            else if(page=="jobs")
            {
                // Only roles the shop has unlocked and still has room for.
                var staff=PaperPixelGames.Core.Staff.StaffManager.Instance;text="想去寿司店帮忙吗？";
                foreach(PaperPixelGames.Core.Staff.StaffRole role in Enum.GetValues(typeof(PaperPixelGames.Core.Staff.StaffRole)))
                {
                    if(staff==null||!staff.IsRoleUnlocked(role)||staff.IsRoleFull(role))continue;
                    var selectedRole=role;string title=role==PaperPixelGames.Core.Staff.StaffRole.SushiChef?"寿司师傅":role==PaperPixelGames.Core.Staff.StaffRole.PrepCook?"备料员":role==PaperPixelGames.Core.Staff.StaffRole.Waitress?"服务员":"补货员";
                    choices.Add(new DialoguePanelController.ConversationChoice(title,()=>job(selectedRole)));
                }
                if(choices.Count==0)text="店里现在没有空缺的岗位。";
            }
            else if(human!=null)
            {
                var pending=runtime.GetEpisodes(person).Where(e=>!e.committed&&!e.inputAccepted&&e.status=="AwaitHuman"&&(e.focusPersonId==human.personId||e.targetPersonId==human.personId||e.anotherPersonId==human.personId)&&e.familyChoice!=null).ToArray();
                foreach(var episode in pending)foreach(var option in episode.familyChoice.options.Where(o=>o.enabled))
                {
                    string action="family:"+episode.familyChoice.id,id=option.id;
                    choices.Add(new DialoguePanelController.ConversationChoice(FriendTabletPresenter.SourceMenuLabel(option.id),()=>submit(action,id)));
                }
            }
            if(page!="items"&&page!="help"&&page!="jobs"&&choices.Count==0)text="现在没有要回应的事。";
            choices.Add(new DialoguePanelController.ConversationChoice("返回",back));panel.ShowCustomConversation(billboard,text,closed,choices.ToArray());
        }
    }
}
