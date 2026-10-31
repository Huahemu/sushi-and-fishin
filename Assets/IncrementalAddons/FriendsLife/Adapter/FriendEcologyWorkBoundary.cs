using System;
using System.Linq;
namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsLifeRuntime
    {
        // The existing source job owns its service until its real Handover ends.
        // This waiting lease is a reservation, never permission to pause a worker.
        private bool PrepareEcologyWorkBoundary(SourceEpisode episode)
        {
            var economy = FriendsEconomyService.Instance;
            if (!economy) return true;
            var ids = new[] { episode.focusPersonId, episode.targetPersonId, episode.anotherPersonId }
                .Where(id => !string.IsNullOrEmpty(id) && GetPerson(id)?.controllerKind == PersonControllerKind.Npc)
                .Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray();
            // Wait without reserving or requesting an existing worker to leave.
            if (ids.Any(id => HasActiveInPlaceTalk(id) && !State.leases.Any(l => l.personId == id && l.ownerEpisode == episode.episodeId && l.phase != "JobHandover"))) return false;
            if (ids.Any(id => State.leases.Any(l => l.personId == id && l.ownerEpisode != episode.episodeId))) return false;
            bool ready = true;
            bool changed = false;
            foreach (string id in ids)
            {
                if (!economy.HasActiveJob(id))
                {
                    // Restore() destroys Dormancy at the end of the frame. Do
                    // not start the source on a still-visible service proxy.
                    if (ActorOf(id)?.GetComponent<FriendResidentDormancy>()) ready = false;
                    continue;
                }
                if (!State.leases.Any(l => l.personId == id && l.ownerEpisode == episode.episodeId))
                {
                    State.leases.Add(new ActivityLease { leaseId = "source-work-wait/" + episode.episodeId + "/" + id,
                        personId = id, ownerEpisode = episode.episodeId, facilityId = "SourceWorkBoundary", phase = "JobHandover", safeBoundary = false });
                    changed = true;
                }
                economy.RequestLeaveJob(id);
                ready = false;
            }
            if (changed) NotifyChanged();
            return ready;
        }
    }
}
