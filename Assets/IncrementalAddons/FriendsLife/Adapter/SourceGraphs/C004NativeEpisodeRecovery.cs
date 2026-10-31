using System;
using System.Linq;
using UnityEngine;
using SushiAndFish.FriendsLife.SourceGraphs;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsLifeRuntime
    {
        const string C004RecoveryTravel="SourceRecoveryTravel";

        // Explicit host repair after an actual binding change. This never calls
        // Trigger/ChooseBranch/Complete, creates a new episode, or clears the
        // persisted runner. Its own Travel status is the durable permission to
        // finish this one requested recovery after normal save/load.
        public bool RecoverNativeEpisodeAfterBindingChange(string episodeId)
        {
            if(!Ready||!IsAuthority||!IsWorldRunning||string.IsNullOrEmpty(episodeId))return false;
            var episode=State.episodes.SingleOrDefault(e=>e.episodeId==episodeId);
            if(episode==null||episode.sourceKind!="MiiEvent"||episode.committed)return false;
            if(episode.status==C004RecoveryTravel)return true;
            if(episode.status!="BindingFault"&&episode.status!="Suspended")return false;
            if(episode.nativePending==null||episode.branchIndex<0||episode.nativePending.usesReconstructedEligibility)
            {LastError="The same selected original pending/branch is unavailable for source recovery.";return false;}
            var bridge=OriginalGraphBridge.Instance;
            string cursorFailure=null;
            if(!bridge||!bridge.CanRecoverSameNativeCursor(this,episode,out cursorFailure))
            {LastError=bridge?cursorFailure:"Original graph bridge is unavailable for source recovery.";return false;}
            if(!HumanParticipantsReadyForStage(episode))
            {LastError="Actual Human source participants are not ready for the same-episode stage.";return false;}
            // Exactly one acquisition attempt per explicit request. Contention
            // leaves the original BindingFault/Suspended cursor parked; this
            // method does not create an automatic acquisition/retry loop.
            if(!TryAcquirePairLease(episode))
            {LastError=string.IsNullOrEmpty(episode.fault)?"Actual source participants are busy; same episode remains parked.":episode.fault;return false;}
            episode.status=C004RecoveryTravel;episode.worldArrivalVerified=false;
            MoveEpisodeParticipantsTogether(episode);
            if(episode.status!=C004RecoveryTravel)
            {
                string failure=string.IsNullOrEmpty(episode.fault)?"Actual same-episode navigation was rejected.":episode.fault;
                UpdateEpisodeStatus(episode.episodeId,"BindingFault",failure);LastError=failure;return false;
            }
            LastError="";NotifyChanged();return true;
        }

        // Pumped by the existing graph bridge, not a second gameplay Update.
        // Only an already explicitly requested recovery can enter this phase.
        internal void AdvanceC004NativeEpisodeRecovery()
        {
            if(!Ready||!IsAuthority||!IsWorldRunning)return;
            foreach(var episode in State.episodes.Where(e=>e.sourceKind=="MiiEvent"&&!e.committed&&e.status==C004RecoveryTravel).ToArray())
            {
                try
                {
                    var participants=new[]{episode.focusPersonId,episode.targetPersonId,episode.anotherPersonId}.Where(id=>!string.IsNullOrEmpty(id)).Distinct().ToArray();
                    bool allLeases=participants.Length>0&&participants.All(id=>State.leases.Any(l=>l.personId==id&&l.ownerEpisode==episode.episodeId));
                    if(!allLeases)
                    {
                        // A normal restored snapshot may need actual bodies and
                        // the saved source leases rebound once. Lost/contested
                        // resources park the source; never retry a fault forever.
                        if(!HumanParticipantsReadyForStage(episode)||!TryAcquirePairLease(episode))
                        {UpdateEpisodeStatus(episode.episodeId,"BindingFault","Actual recovery participants/leases were lost or are busy; explicit host recovery is required again.");continue;}
                        MoveEpisodeParticipantsTogether(episode);
                        if(episode.status!=C004RecoveryTravel)
                        {UpdateEpisodeStatus(episode.episodeId,"BindingFault",episode.fault);continue;}
                    }
                    if(!EpisodeParticipantsArrived(episode))continue;
                    var bridge=OriginalGraphBridge.Instance;
                    string failure=null;
                    if(!bridge||!bridge.ResumeRecoveredNativeCursor(this,episode,out failure))
                        UpdateEpisodeStatus(episode.episodeId,"BindingFault",bridge?failure:"Original source bridge disappeared during same-cursor recovery.");
                }
                catch(Exception error){UpdateEpisodeStatus(episode.episodeId,"BindingFault",error.Message);}
            }
        }
    }
}
