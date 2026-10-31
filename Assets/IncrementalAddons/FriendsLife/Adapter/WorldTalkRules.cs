using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Host navigation requests only. No source activity, RNG, result or bill is
    // copied into this record; their existing owners remain authoritative.
    [Serializable]
    public sealed class FriendWorldTalkRequest
    {
        public string requestId, humanId, personId, phase = "Queued", sourceChoiceId;
        public string completedCommandId, completedAction, completedOption, resultMessage;
        public bool actionAccepted;
        public bool npcInitiated,approachAttempted,ignored,inputDispatched,sourceResult;
        public string sourceEpisodeId,pendingVerb,approachFailure;
        public Vector3 approachDestination;
        // inPlace: a seated or anchored friend answers without giving up its
        // seat (no body lease). busyLine: the one line a busy friend says.
        public bool inPlace;
        // Durable request marker, not an assertion of an inventory/save ACK.
        public bool heldGiftConsumptionRequested,heldGiftConsumptionObserved,heldGiftRewardAttempted,heldGiftCompletionSaved;
        public string heldGiftSessionId,heldGiftStatus;
        public ulong heldGiftObjectId;
        public string busyLine;
    }

    public static class WorldTalkRules
    {
        public static bool SameOwner(FriendWorldTalkRequest row, string human, string person)
            => row != null && row.humanId == human && row.personId == person;
        public static bool Terminal(FriendWorldTalkRequest row) => row != null && row.phase == "Done";
        public static bool CanBegin(IEnumerable<FriendWorldTalkRequest> rows, string human, string person, string request)
            => !rows.Any(r => !Terminal(r) && r.phase != "Unknown" && (!r.npcInitiated || r.phase == "Approaching" || r.phase == "ApproachReleasing") && r.requestId != request && (r.personId == person || !r.npcInitiated && r.humanId == human));
        public static bool CanSuspend(bool hasBody, bool existingLease, bool job, bool nativeActivity,
            bool working, bool restaurant, bool assigned, bool interacting, bool swimming, bool snapshot, bool stateAllows)
            => hasBody && !existingLease && !job && !nativeActivity && !working && !restaurant && !assigned &&
                !interacting && !swimming && !snapshot && stateAllows;
        public static bool SameCompletion(FriendWorldTalkRequest row, string command, string action, string option)
            => row != null && row.completedCommandId == command && row.completedAction == action && row.completedOption == option;
    }
}
