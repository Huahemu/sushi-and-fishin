using System;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendWorldTalkAuthority
    {
        public FriendWorldTalkRequest EnsureNpcOffer(FriendsLifeRuntime runtime,SourceEpisode episode)
        {
            if(episode?.familyChoice==null||episode.inputAccepted||episode.committed||
                runtime.GetPerson(episode.focusPersonId)?.controllerKind!=PersonControllerKind.Npc||
                runtime.GetPerson(episode.targetPersonId)?.controllerKind!=PersonControllerKind.Human)return null;
            Observe(runtime);string id="social/"+episode.sourceId;
            var row=Find(runtime,id);if(row!=null)return row;
            row=new FriendWorldTalkRequest{requestId=id,npcInitiated=true,personId=episode.focusPersonId,humanId=episode.targetPersonId,
                sourceEpisodeId=episode.episodeId,sourceChoiceId=episode.sourceId,phase="InvitePending"};
            runtime.State.worldTalks.Add(row);runtime.NotifyChanged();runtime.SaveNow();return row;
        }
        public static bool HasNpcApproachBody(FriendsLifeRuntime runtime,string episodeId)
            =>runtime?.State.worldTalks.Any(r=>r.npcInitiated&&r.sourceEpisodeId==episodeId&&
                (r.phase=="Approaching"||r.phase=="ApproachReleasing"))==true;
        public static FriendWorldTalkRequest PendingNpcOffer(FriendsLifeRuntime runtime,string npc,string human)
            =>runtime?.State.worldTalks.Where(r=>r.npcInitiated&&r.personId==npc&&r.humanId==human&&r.phase=="Offered")
                .OrderBy(r=>r.requestId,StringComparer.Ordinal).FirstOrDefault(r=>runtime.PendingNpcSocialChoice(npc,human)?.episodeId==r.sourceEpisodeId);
        public static void RecordSocialResult(FriendsLifeRuntime runtime,SourceEpisode episode,string originalMessage)
        {
            if(string.IsNullOrEmpty(originalMessage)||runtime.State.worldTalks.Any(r=>r.sourceResult&&r.sourceEpisodeId==episode.episodeId))return;
            runtime.State.worldTalks.Add(new FriendWorldTalkRequest{requestId="result/"+episode.sourceId,sourceResult=true,
                sourceEpisodeId=episode.episodeId,sourceChoiceId=episode.sourceId,personId=episode.focusPersonId,phase="Done",actionAccepted=true,resultMessage=originalMessage});
            // Both friends show one short line (Tomodachi pair moment); the
            // bubbles hide again by themselves.
            FriendInteractionPresenter.Say(episode.focusPersonId,originalMessage,0f,5f);
            FriendInteractionPresenter.Say(episode.targetPersonId,FriendPlayerInteractions.PairReply(episode.familyChoice?.action),2.5f,3.5f);
            runtime.NotifyChanged();
        }
        bool ExecuteNpcOffer(FriendsLifeRuntime runtime,string human,FriendWorldTalkRequest row,FriendsWorldCommand command,out string message)
        {
            message="请求尚未受理。";
            if(!WorldTalkRules.SameOwner(row,human,command.targetPersonId))return false;
            if(command.actionId=="offer-ignore"||command.actionId=="end")
            {row.ignored=true;message="暂不回应。";runtime.NotifyChanged();runtime.SaveNow();return true;}
            if(command.actionId!="offer-input")return false;
            string action="family:"+row.sourceChoiceId;
            if(row.inputDispatched)
            {
                bool same=WorldTalkRules.SameCompletion(row,command.commandId,action,command.nativeProfileJson);
                ReconcileInput(runtime,row);message=same?row.resultMessage??"暂未收到操作结果。":"这次输入已经提交。";return same&&row.actionAccepted;
            }
            var episode=runtime.State.episodes.FirstOrDefault(e=>e.episodeId==row.sourceEpisodeId&&!e.committed&&!e.inputAccepted);
            if(row.phase!="Offered"||episode?.familyChoice==null||!WithinReach(runtime,human,row.personId)||
                !episode.familyChoice.options.Any(o=>o.enabled&&o.id==command.nativeProfileJson)||!runtime.CanSubmitHumanAction(human,row.personId,action))return false;
            // Durable intent precedes the sole original input call. An unknown
            // outcome is local to this row and never creates a fresh nonce.
            row.completedCommandId=command.commandId;row.completedAction=action;row.completedOption=command.nativeProfileJson;
            row.inputDispatched=true;row.phase="Unknown";runtime.NotifyChanged();runtime.SaveNow();
            try
            {
                string op="world-talk/"+row.requestId+"/input";
                message=DispatchSourceInput(runtime,row);
                row.actionAccepted=InputRecorded(runtime,row);row.resultMessage=message;row.phase="Done";
                runtime.NotifyChanged();runtime.SaveNow();return row.actionAccepted;
            }
            catch(Exception error)
            {row.resultMessage=null;row.phase="Unknown";message="暂未收到操作结果。";Debug.LogWarning("[FriendsLife] Social input outcome unresolved: "+error.Message);return false;}
        }
        void AdvanceNpcOffer(FriendsLifeRuntime runtime,FriendWorldTalkRequest row)
        {
            if(row.phase=="Unknown"){ReconcileInput(runtime,row);return;}
            var episode=runtime.State.episodes.FirstOrDefault(e=>e.episodeId==row.sourceEpisodeId);
            if(row.phase=="Approaching"||row.phase=="ApproachReleasing")
            {
                var actor=runtime.ActorOf(row.personId);var agent=actor?actor.GetComponent<NavMeshAgent>():null;
                if(!FriendsBodyAuthority.IsOwned(runtime,row.personId,row.sourceEpisodeId))
                {row.phase="Offered";if(episode!=null&&!episode.inputAccepted)episode.status="AwaitHuman";runtime.NotifyChanged();return;}
                var transit=FriendsPrivateCommonTransit.Instance?.Observe(row.personId);
                bool transitMoving=transit!=null&&transit.ownerEpisode==row.sourceEpisodeId&&transit.phase!="Completed"&&transit.phase!="Failed";
                bool arrived=actor&&agent&&agent.enabled&&agent.isOnNavMesh&&!agent.pathPending&&actor.Brain.Movement.HasArrived()&&
                    Vector3.Distance(actor.transform.position,row.approachDestination)<=Mathf.Max(.15f,agent.stoppingDistance+.05f)&&
                    Vector3.Distance(actor.transform.position,agent.nextPosition)<=.25f;
                bool pathFailed=!transitMoving&&actor&&agent&&agent.enabled&&agent.isOnNavMesh&&!agent.pathPending&&
                    (agent.pathStatus!=NavMeshPathStatus.PathComplete||!agent.hasPath&&!arrived);
                if(pathFailed&&row.phase=="Approaching")
                {
                    actor.Brain.Movement.Stop();row.approachFailure="The actual NPC approach no longer has a complete path to its planned endpoint";
                    row.phase="ApproachReleasing";
                }
                if(transitMoving||row.phase=="Approaching"&&!arrived&&transit?.phase!="Failed")return;
                row.phase="ApproachReleasing";
                if(episode!=null&&!episode.inputAccepted)episode.status="NpcApproachReleasing";
                if(!TryReleaseTalkBody(runtime,row))return;
                row.phase=episode==null||episode.committed||episode.inputAccepted?"Done":"Offered";
                if(episode!=null&&!episode.inputAccepted)episode.status="AwaitHuman";
                if(row.phase=="Offered")AnnounceNpcOffer(runtime,row,episode);
                runtime.NotifyChanged();runtime.SaveNow();return;
            }
            if(episode==null||episode.committed||episode.inputAccepted){row.phase="Done";runtime.NotifyChanged();return;}
            if(row.phase=="Offered")return;
            if(row.approachAttempted){row.phase="Offered";return;}
            if(WithinReach(runtime,row.humanId,row.personId))
            {row.approachAttempted=true;row.phase="Offered";AnnounceNpcOffer(runtime,row,episode);runtime.NotifyChanged();runtime.SaveNow();return;}
            if(!runtime.IsWorldRunning)return; // shared TryAcquire requests and waits for the original safe exit.
            if(!FriendsBodyAuthority.TryAcquire(runtime,row.personId,row.sourceEpisodeId,"SocialApproach",out var lease,out var reason))return;
            row.approachAttempted=true;episode.status="NpcApproach";
            if(!runtime.TryPlanNpcSocialApproach(row.personId,row.humanId,row.sourceEpisodeId,out var target,out reason)||
                !runtime.TryMoveWorldActor(row.personId,target,out var actual,.05f))
            {
                row.approachFailure=reason??runtime.LastWorldMoveDiagnostic;row.phase="ApproachReleasing";
                if(TryReleaseTalkBody(runtime,row)){row.phase="Offered";episode.status="AwaitHuman";}
                runtime.NotifyChanged();runtime.SaveNow();return;
            }
            row.approachDestination=actual;lease.destination=actual;lease.safeBoundary=false;row.phase="Approaching";
            runtime.NotifyChanged();runtime.SaveNow();
        }
        static void AnnounceNpcOffer(FriendsLifeRuntime runtime,FriendWorldTalkRequest row,SourceEpisode episode)
        {
            if(row.ignored||episode?.familyChoice==null)return;
            var actor=runtime.ActorOf(row.personId);var text=actor?actor.GetComponentInChildren<ResidentBillboardText>(true):null;
            if(text&&text.isActiveAndEnabled)text.PlayTypewriter(episode.familyChoice.title);
        }
    }
}
