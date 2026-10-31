using System;
using System.Linq;

namespace SushiAndFish.FriendsLife
{
    [Serializable]
    public sealed class SourceWorkYieldResult
    {public bool accepted,ready;public string phase,reason;}
    public sealed partial class FriendsEconomyService
    {
        // Explicit recovery of an existing parked original event. Its pending,
        // branch, RNG and cursor stay intact. The source reservation prevents
        // the savings queue from immediately taking the person back after the
        // original station has acknowledged safe handover.
        public SourceWorkYieldResult RequestWorkYieldForSourceRecovery(string personId,string sourceEpisodeId)
        {
            var result=new SourceWorkYieldResult{phase="Rejected"};
            if(!Authority||!runtime||!runtime.Ready||!runtime.IsWorldRunning)return result;
            var episode=runtime.State.episodes.FirstOrDefault(e=>e.episodeId==sourceEpisodeId&&e.sourceKind=="MiiEvent"&&!e.committed);
            if(episode==null||episode.nativePending==null||episode.nativePending.usesReconstructedEligibility||
                (episode.status!="BindingFault"&&episode.status!="Suspended"&&episode.status!="SourceRecoveryTravel")||
                !new[]{episode.focusPersonId,episode.targetPersonId,episode.anotherPersonId}.Contains(personId)||
                runtime.GetPerson(personId)?.controllerKind!=PersonControllerKind.Npc)
            {result.reason="The same parked original source episode/participant is required";return result;}
            if(runtime.State.leases.Any(l=>l.personId==personId&&l.ownerEpisode!=sourceEpisodeId))
            {result.reason="Another source owns the actual person";return result;}
            string id="source-job-yield/"+sourceEpisodeId+"/"+personId;
            var lease=runtime.State.leases.FirstOrDefault(l=>l.personId==personId&&l.ownerEpisode==sourceEpisodeId);
            if(lease==null)
            {
                lease=new ActivityLease{leaseId=id,personId=personId,ownerEpisode=sourceEpisodeId,phase="JobHandover",facilityId="SourceRecovery",safeBoundary=false};
                runtime.State.leases.Add(lease);
            }
            RequestLeaveJob(personId);
            result.accepted=true;result.ready=!HasActiveJob(personId);result.phase=result.ready?"ReadyForSameSourceRecovery":"WaitingForOriginalJobHandover";
            if(lease.leaseId==id)lease.safeBoundary=result.ready;
            var actor=runtime.ActorOf(personId);
            if(result.ready&&actor?.Brain!=null&&actor.Runtime!=null&&!actor.Runtime.IsSnapshotApplying)actor.Brain.Pause();
            runtime.NotifyChanged();return result;
        }
    }
}
