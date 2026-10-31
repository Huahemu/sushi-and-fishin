using System;
using System.Linq;
using System.Collections.Generic;
using PaperPixelGames.Core.Player;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsLifeRuntime
    {
        const string SocialStagePrefix="host-social-stage/v1|";
        // Host geometry readiness only. No relationship value, RNG, source
        // clock or original qualification is changed by this waiting record.
        string SocialStageBindingKey(SourceEpisode episode)
        {
            var rows=new List<string>{State.worldId,episode.sourceKind,episode.familyChoice?.action??episode.eventName};
            foreach(string id in new[]{episode.focusPersonId,episode.targetPersonId,episode.anotherPersonId}.Where(x=>!string.IsNullOrEmpty(x)).Distinct().OrderBy(x=>x,StringComparer.Ordinal))
            {
                var p=GetPerson(id);var actor=ActorOf(id);var agent=actor?actor.GetComponent<NavMeshAgent>():null;
                var room=FriendsHomeInteriors.Instance?.RoomFor(id);var house=room?room.house:FriendsEconomyService.Instance?.Plot(p?.currentHomeId)?.house;
                string key=id+"/"+p?.currentHomeId+"/actor="+(actor?actor.GetInstanceID():0)+"/room="+(room?room.GetInstanceID():0)+
                    "/house="+(house?house.GetInstanceID():0)+"/children="+(house?house.transform.childCount:0)+
                    "/nav="+(agent?agent.agentTypeID:0)+":"+(agent?agent.areaMask:0)+":"+(agent&&agent.enabled&&agent.isOnNavMesh);
                if(p?.controllerKind==PersonControllerKind.Human)
                {
                    var body=TransformOf(id);var player=body?body.GetComponent<PPG_Player>():null;
                    float step=player&&player.Interactor?Mathf.Max(.25f,player.Interactor.InteractRange):1;
                    if(body)key+="/human-stage="+Mathf.RoundToInt(body.position.x/step)+","+Mathf.RoundToInt(body.position.y/step)+","+Mathf.RoundToInt(body.position.z/step);
                }
                if(house)key+="/house-pose="+house.transform.position.ToString("R")+"/"+house.transform.rotation.eulerAngles.ToString("R");
                rows.Add(key);
            }
            return string.Join("|",rows);
        }
        bool SocialStageMayAttempt(SourceEpisode episode)
        {
            if(episode.status!="Suspended"||episode.rejectedContext?.StartsWith(SocialStagePrefix,StringComparison.Ordinal)!=true)return true;
            // A partially released operation retains only its own unresolved
            // body claim. Never reacquire another pair while release is pending.
            if(State.leases.Any(l=>l.ownerEpisode==episode.episodeId))
            {ReleaseEpisodeLeases(episode.episodeId);return false;}
            if(episode.rejectedContext==SocialStagePrefix+SocialStageBindingKey(episode))return false;
            episode.rejectedContext="";episode.status="Queued";return true;
        }
        void RememberSocialStageFailure(SourceEpisode episode)
        {
            if(episode.status!="Suspended")return;
            string context=SocialStagePrefix+SocialStageBindingKey(episode);
            if(episode.rejectedContext==context)return;
            episode.rejectedContext=context;NotifyChanged();SaveNow();
        }
        public bool RetrySocialAfterBodyBindingChange(string episodeId)
        {
            var episode=State?.episodes.FirstOrDefault(e=>e.episodeId==episodeId&&!e.committed&&e.sourceKind=="Family");
            if(episode==null||episode.rejectedContext?.StartsWith(SocialStagePrefix,StringComparison.Ordinal)!=true)return false;
            episode.rejectedContext="";episode.status="Queued";NotifyChanged();SaveNow();return true;
        }
        void WaitForHumanSocialInput(SourceEpisode episode)
        {
            // The approach is an active, NPC-only action. Waiting for the
            // player's decision begins only after that body is safely returned.
            if(FriendWorldTalkAuthority.HasNpcApproachBody(this,episode.episodeId))
            {
                var offer=State.worldTalks.First(r=>r.npcInitiated&&r.sourceEpisodeId==episode.episodeId);
                episode.status=offer.phase=="ApproachReleasing"?"NpcApproachReleasing":"NpcApproach";return;
            }
            // AwaitHuman is a durable input handle, never a body reservation.
            if(State.leases.Any(l=>l.ownerEpisode==episode.episodeId))
            {
                ReleaseEpisodeLeases(episode.episodeId);
                if(State.leases.Any(l=>l.ownerEpisode==episode.episodeId))
                {episode.status="HumanBodyReleasing";return;}
            }
            if(episode.status!="AwaitHuman")
            {episode.status="AwaitHuman";PendingInputRequested?.Invoke(episode);NotifyChanged();}
            FriendWorldTalkAuthority.Instance.EnsureNpcOffer(this,episode);
        }
        public SourceEpisode PendingNpcSocialChoice(string npcId,string humanId)
            =>State?.episodes.Where(e=>!e.committed&&!e.inputAccepted&&e.sourceKind=="Family"&&e.familyChoice!=null&&
                e.focusPersonId==npcId&&(e.targetPersonId==humanId||e.anotherPersonId==humanId)&&
                !FriendWorldTalkAuthority.FamilyInputUnresolved(this,humanId,e.sourceId))
                .OrderBy(e=>e.sourceMinute).ThenBy(e=>e.episodeId,StringComparer.Ordinal).FirstOrDefault();
        public bool HasExecutingSocialBody(string personId)
            =>State?.leases.Any(l=>l.personId==personId)==true;
        void AdvanceSocialReturns()
        {
            foreach(var episode in State.episodes.Where(e=>e.sourceKind=="Family"&&e.committed&&e.status=="Returning").ToArray())
            {
                ReleaseEpisodeLeases(episode.episodeId);
                if(State.leases.Any(l=>l.ownerEpisode==episode.episodeId))continue;
                episode.status="Done";NotifyChanged();SaveNow();
            }
        }
    }
}
