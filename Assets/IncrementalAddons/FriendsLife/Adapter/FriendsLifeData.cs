using System;
using System.Collections.Generic;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public enum PersonControllerKind { Npc, Human }
    [Serializable] public sealed class ActorWalletDelta
    {
        public string personId, provenance;
        public long deltaCents;
        public int sourceSlot, sourceAmountRaw;
    }

    [Serializable]
    public sealed class PersonBinding
    {
        public string worldId, personId, nativeResidentId, displayName, platformAccountKey = "", originCommandId = "";
        public PersonControllerKind controllerKind;
        public int hostResidentId, appearanceRevision;
        public int sourceFamilyAppearanceRevision = -1;
        public string sourceFamilyStage;
        public string ownedHomeId = "", currentHomeId = "", returnHomeId = "";
        public bool arrivalGrantUsed, existingHostResident, addedByPlayer;
        public string appearanceJson = "";
        public List<LifeSimulationInventoryStack> inventory = new List<LifeSimulationInventoryStack>();
    }

    [Serializable]
    public sealed class SourceEpisode
    {
        public string episodeId, sourceKind, sourceId, eventName, graph, command, demoParam;
        public string focusPersonId, targetPersonId, anotherPersonId;
        public int branchIndex = -1;
        public string demoResult, status = "Queued", fault = "", opId;
        public double sourceMinute;
        public LifeSimulationPendingEvent nativePending;
        public LifeFamilyChoice familyChoice;
        public string optionId, inputOwnerPersonId, inputChildName;
        public bool inputAccepted;
        public int inputCursor;
        public bool committed, presentationRequested;
        public string runnerJson = "";
        public string rejectedContext = "";
        public bool worldArrivalVerified;
        public Vector3 focusCommitPosition, targetCommitPosition;
        public float participantDistanceAtCommit;
    }

    [Serializable]
    public sealed class ActivityLease
    {
        public string leaseId, personId, ownerEpisode, facilityId, phase = "Reserved", resumeToken = "";
        public bool safeBoundary;
        public Vector3 destination;
        public double untilGameMinute;
        public float visualElapsed;
    }

    [Serializable]
    public sealed class FriendsKernelReceipt
    {
        public string opId, payerPersonId, causeEpisode, domain, message;
        public bool success;
        public long walletDeltaCents, revision;
        public double gameMinute;
        public List<ActorWalletDelta> allocations = new List<ActorWalletDelta>();
    }

    [Serializable]
    public sealed class NativeCommittedHistory
    {
        public string episodeId, eventName, focusNativeId, targetNativeId;
        public uint eventId;
        public double gameMinute;
    }

    [Serializable]
    public sealed class FriendsLifeSave
    {
        public int schema = 1;
        public string worldId, coreJson, economyJson = "", savedUtc;
        public string nativeSourceContextJson = "";
        public long revision, activeMicros;
        public double hostGameMinute;
        public FriendHostClockCursor hostCalendarCursor;
        public List<PersonBinding> people = new List<PersonBinding>();
        public List<SourceEpisode> episodes = new List<SourceEpisode>();
        public List<ActivityLease> leases = new List<ActivityLease>();
        public List<OriginalAmbientActivityRecord> originalActivities = new List<OriginalAmbientActivityRecord>();
        public List<FriendWorldTalkRequest> worldTalks = new List<FriendWorldTalkRequest>();
        public List<FriendsKernelReceipt> receipts = new List<FriendsKernelReceipt>();
        public List<NativeCommittedHistory> nativeHistory = new List<NativeCommittedHistory>();
    }
}
