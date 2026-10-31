using System;
using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.ResidentialIsland;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.AI;
using UnityEngine.Playables;

namespace SushiAndFish.FriendsLife
{
    /// <summary>Projects the R sleep/dream intent into the person's own real bed. No need, money or family result is added here.</summary>
    public sealed class FriendsNpcRestController : MonoBehaviour
    {
        sealed class Pose
        {
            public ResidentController actor;
            public HousingRestInteractable bed;
            public HousingPrivateRoom room;
            public ActivityLease lease;
            public Animator animator;
            public SkinnedMeshRenderer actualBody;
            public readonly Dictionary<GameObject,bool> visibilityChanges=new Dictionary<GameObject,bool>();
            public PlayableGraph graph;
            public AnimationMixerPlayable mixer;
            public AnimationClipPlayable[] clips;
            public Vector3 startHip, modelLocalPosition;
            public Quaternion modelLocalRotation, worldRotation;
            public bool applyRootMotion;
            public AnimatorCullingMode culling;
            public float exitElapsed, exitPoseTime;
            public bool exiting, paused, visibilityHeld, enteredPose;
            public string worldId;
            public Vector3 lastExitPosition;
            public IResidentState originalState;
            public RestBlock exitBlock;
        }
        // A failed navigation binding is retried after a real context change or
        // an explicit binding-repair signal, never because a frame/time changed.
        sealed class RestBlock
        {
            public string worldId, homeId, activity, reason;
            public ResidentController actor;
            public NavMeshAgent agent;
            public HousingRestInteractable bed;
            public HousingPrivateRoom room;
            public Transform exit;
            public Vector3 body, agentPosition, destination;
            public int agentType, areaMask;
            public bool enabled, onMesh;
            public bool Same(FriendsLifeRuntime runtime, string personId, ResidentController current,
                HousingRestInteractable furniture, HousingPrivateRoom privateRoom, Vector3 target)
            {
                var nav = current ? current.GetComponent<NavMeshAgent>() : null;
                var person = runtime.GetPerson(personId);
                return person != null && worldId == runtime.State.worldId && homeId == person.currentHomeId &&
                    activity == runtime.Core.FindResident(person.nativeResidentId)?.activity &&
                    actor == current && agent == nav && bed == furniture && room == privateRoom && exit == (furniture ? furniture.exit : null) &&
                    (!nav || agentType == nav.agentTypeID && areaMask == nav.areaMask && enabled == nav.enabled &&
                    onMesh == (nav.enabled && nav.isOnNavMesh)) && Vector3.Distance(destination, target) <= .01f;
            }
            public static RestBlock Capture(FriendsLifeRuntime runtime, string personId, ResidentController actor,
                HousingRestInteractable bed, HousingPrivateRoom room, Vector3 destination, string reason)
            {
                var nav = actor ? actor.GetComponent<NavMeshAgent>() : null; var person = runtime.GetPerson(personId);
                return new RestBlock { worldId = runtime.State.worldId, homeId = person?.currentHomeId,
                    activity = person == null ? null : runtime.Core.FindResident(person.nativeResidentId)?.activity,
                    actor = actor, agent = nav, bed = bed, room = room, exit = bed ? bed.exit : null, destination = destination, reason = reason,
                    body = actor ? actor.transform.position : default, enabled = nav && nav.enabled,
                    onMesh = nav && nav.enabled && nav.isOnNavMesh, agentPosition = nav && nav.enabled && nav.isOnNavMesh ? nav.nextPosition : default,
                    agentType = nav ? nav.agentTypeID : 0, areaMask = nav ? nav.areaMask : 0 };
            }
        }
        readonly Dictionary<string, Pose> poses = new Dictionary<string, Pose>();
        readonly Dictionary<string, RestBlock> blocked = new Dictionary<string, RestBlock>();
        const float CameraLead = .65f, SitDuration = 1.8f, HandDuration = 1.4f, TuckDuration = 1f, PivotDuration = 1.1f, ReclineDuration = 1.7f;
        const float EnterDuration = CameraLead + SitDuration + HandDuration + TuckDuration + PivotDuration + ReclineDuration;
        float nextDiscovery;
        // Friends sleep through the original Home state of their night schedule
        // (hidden indoors), like original residents. The bed pose graph is off
        // (#13): no new bed starts; a running pose exits and an orphan rest
        // lease from an old save is removed below.
        static readonly bool OwnBedPose = false;

        void Awake(){if(!GetComponent<FriendsHomeWorldActions>())gameObject.AddComponent<FriendsHomeWorldActions>();}
        public bool IsResting(string personId)=>poses.ContainsKey(personId??"");
        public string RestBlockReason(string personId)
        { return blocked.TryGetValue(personId??"", out var block) ? block.reason : poses.TryGetValue(personId??"", out var pose) ? pose.exitBlock?.reason : null; }
        // Call only after repairing the actual scene/navigation binding. Every
        // original-owner and movement precondition still runs on the next pass.
        public void RetryRestAfterBindingChange(string personId)
        { blocked.Remove(personId??""); if(poses.TryGetValue(personId??"",out var pose))pose.exitBlock=null; }
        public bool RequestSourceExit(string personId)
        {if(!poses.TryGetValue(personId??"",out var pose))return false;RequestExit(pose);return true;}

        void Update()
        {
            var runtime = FriendsLifeRuntime.Instance;
            if (runtime == null || !runtime.IsWorldRunning) return;
            if (Time.unscaledTime >= nextDiscovery)
            {
                nextDiscovery = Time.unscaledTime + .5f;
                foreach (var person in runtime.GetPeople().Where(p => p.controllerKind == PersonControllerKind.Npc))
                {
                    var native = runtime.Core.FindResident(person.nativeResidentId);
                    bool wantsRest = OwnBedPose && native != null && (native.activity == "睡觉" || native.activity == "梦中") && !FriendsHomeWorldActions.Controls(person.personId);
                    if (poses.TryGetValue(person.personId, out var active))
                    {
                        var expected=FriendsHomeInteriors.Instance?.RoomFor(person.personId);
                        var expectedHouse=expected?expected.house:FriendsEconomyService.Instance?.Plot(person.currentHomeId)?.house;
                        if (!wantsRest || active.bed == null || active.actor == null || active.room!=expected || active.bed.GetComponentInParent<HousingHouse>()!=expectedHouse) RequestExit(active);
                        continue;
                    }
                    string owner = "rest/" + person.personId;
                    if (!wantsRest)
                    {
                        blocked.Remove(person.personId);
                        foreach (var orphan in runtime.State.leases.Where(l => l.personId == person.personId && l.ownerEpisode == owner).ToArray()) runtime.State.leases.Remove(orphan);
                        continue;
                    }
                    if (FriendsEconomyService.Instance?.HasActiveJob(person.personId) == true) continue;
                    var actor = runtime.ActorOf(person.personId);
                    if (!FriendOriginalNpcActivityBoundary.CanAcquire(actor, out _)) continue;
                    var actorAgent = actor.GetComponent<NavMeshAgent>();
                    if (actorAgent == null || !actorAgent.enabled || !actorAgent.isOnNavMesh) continue;
                    // Original anchored activities deliberately separate body
                    // and agent. Rest never repairs somebody else's pose.
                    if (Vector3.Distance(actor.transform.position, actorAgent.nextPosition) > .25f) continue;
                    if (runtime.State.leases.Any(l => l.personId == person.personId && l.ownerEpisode != owner)) continue;
                    var privateRoom = FriendsHomeInteriors.Instance?.RoomFor(person.personId);
                    var shared=FriendsEconomyService.Instance.State.groups.FirstOrDefault(g=>g.homeId==person.currentHomeId&&g.members.Contains(person.personId)&&g.members.Count>1);
                    if(shared!=null&&(!FriendsHomeInteriors.Instance||!FriendsHomeInteriors.Instance.HasAllPrivateRooms(shared.groupId)||!privateRoom))continue;
                    var house = privateRoom ? privateRoom.house : FriendsEconomyService.Instance?.Plot(person.currentHomeId)?.house;
                    if (house == null || privateRoom == null && FriendsEconomyService.Instance.MembersOf(person.currentHomeId).Count() > 1) continue;
                    var bed = house.GetComponentsInChildren<HousingRestInteractable>(true).FirstOrDefault(r => r.kind == HousingRestKind.Bed && r.exit && r.sleepHip && r.seatHip &&
                        (string.IsNullOrEmpty(FriendRestOccupancy.For(r).friendOccupantPersonId) || FriendRestOccupancy.For(r).friendOccupantPersonId == person.personId) && (r.area == null || !r.area.IsOccupied || FriendRestOccupancy.For(r).friendOccupantPersonId == person.personId));
                    var actualAnimator=ActualRestAnimator(actor,out _);
                    if (bed == null || actualAnimator == null || !actualAnimator.isHuman || actor.IsCustomizationApplying || FriendResidentBindings.IsApplyingAppearance(actor)) continue;
                    // Reserve/travel only when the actual host furniture clips
                    // needed by this presentation are already available.
                    if (!HasRestClips(actualAnimator)) continue;
                    var portal = privateRoom ? FriendInstances<HousingRoomPortal>.Active().FirstOrDefault(p => p.allowedPersonId == person.personId && !p.leavingPrivateRoom) : null;
                    Vector3 destination = portal ? portal.transform.position : bed.exit.position;
                    if (blocked.TryGetValue(person.personId, out var previous) && previous.Same(runtime,person.personId,actor,bed,privateRoom,destination)) continue;
                    blocked.Remove(person.personId);
                    var lease = runtime.State.leases.FirstOrDefault(l => l.ownerEpisode == owner && l.personId == person.personId);
                    if (lease == null)
                    {
                        lease = new ActivityLease { leaseId = owner, personId = person.personId, ownerEpisode = owner,
                            facilityId = "bed/" + person.currentHomeId + "/" + person.personId, phase = "BedTravel", safeBoundary = false };
                        runtime.State.leases.Add(lease);
                    }
                    // A serialized Sleeping/BedExit phase is not a physical
                    // room-entry receipt. Reacquire the actual bed and portal;
                    // the original core sleep/dream state is untouched.
                    if(lease.phase!="BedTravel"){lease.phase="BedTravel";lease.visualElapsed=0;lease.safeBoundary=false;}
                    var pose = new Pose { actor = actor, bed = bed, room = privateRoom, lease = lease,
                        worldId = runtime.State.worldId, originalState = actor.Brain.StateMachine.Current, lastExitPosition = bed.exit.position };
                    actor.Brain.Pause(); pose.paused = true; actor.Brain.BeginModelVisibilityHold(); pose.visibilityHeld = true;
                    FriendRestOccupancy.For(bed).friendOccupantPersonId = person.personId; SetOccupied(bed, true);
                    poses.Add(person.personId, pose);
                    if (lease.phase == "BedTravel")
                    {
                        if (runtime.TryMoveWorldActor(person.personId, destination, out var resolved,.25f)) lease.destination = resolved;
                        else
                        {
                            string reason = runtime.LastWorldMoveDiagnostic;
                            poses.Remove(person.personId); ReleaseReservation(runtime, pose);
                            blocked[person.personId] = RestBlock.Capture(runtime,person.personId,actor,bed,privateRoom,destination,reason);
                            continue;
                        }
                    }
                    runtime.NotifyChanged();
                }
            }
            foreach (var item in poses.ToArray())
            {
                var pose = item.Value;
                if (pose.actor == null || pose.bed == null || !ControlsPose(runtime,pose)) { Finish(runtime, item.Key, pose); continue; }
                if (pose.lease.phase == "BedTravel")
                {
                    if (pose.exiting) { Finish(runtime, item.Key, pose); continue; }
                    var agent = pose.actor.GetComponent<NavMeshAgent>();
                    float tolerance = Mathf.Max(.15f, (agent ? agent.stoppingDistance : 0) + .05f);
                    if (!pose.actor.Brain.Movement.HasArrived() || Vector3.Distance(pose.actor.transform.position, pose.lease.destination) > tolerance ||
                        Vector3.Distance(pose.actor.Brain.Movement.Destination, pose.lease.destination) > tolerance) continue;
                    // A private room is an existing internal portal destination,
                    // entered only after the actor actually reaches its door.
                    if(pose.room&&(!FriendsHomeInteriors.Instance||!FriendsHomeInteriors.Instance.EnterPrivateRoom(item.Key)))continue;
                    pose.actor.Brain.Movement.StopAtPose(pose.bed.exit.position, Quaternion.Euler(0,pose.bed.exit.eulerAngles.y,0));
                    StartPose(pose);
                    runtime.NotifyChanged();
                }
                if (!pose.graph.IsValid())
                {
                    // Cancelling BedTravel never created a PlayableGraph. It
                    // still owns a reservation/lease which must be released.
                    if (pose.exiting) Finish(runtime, item.Key, pose);
                    continue;
                }
                if (pose.exiting)
                {
                    pose.exitElapsed += Time.unscaledDeltaTime;
                    float duration = 6.5f * Mathf.Clamp(pose.exitPoseTime / EnterDuration, .2f, 1);
                    if (pose.exitElapsed >= duration) { Finish(runtime, item.Key, pose); continue; }
                    pose.lease.visualElapsed = pose.exitPoseTime * (1 - pose.exitElapsed / duration);
                }
                else pose.lease.visualElapsed += Time.unscaledDeltaTime;
            }
        }
        static bool HasRestClips(Animator animator)
        {
            var catalog = HousingIsland.Instance?.Catalog;
            return animator != null && animator.isHuman && catalog != null && catalog.restIdle && catalog.restSit && catalog.restLay && catalog.restTuck && catalog.restBrace && catalog.restRecline&&FriendsHostRestRootRotation.Ready;
        }
        void StartPose(Pose pose)
        {
            var catalog = HousingIsland.Instance?.Catalog;
            var animator = ActualRestAnimator(pose.actor,out var actualBody);
            if (!HasRestClips(animator)) { RequestExit(pose); return; }
            pose.animator = animator;pose.actualBody=actualBody; pose.enteredPose = true; pose.lastExitPosition = pose.bed.exit.position;
            MaintainActualVisibility(pose);
            pose.modelLocalPosition = animator.transform.localPosition; pose.modelLocalRotation = animator.transform.localRotation;
            pose.worldRotation = animator.transform.rotation; pose.applyRootMotion = animator.applyRootMotion; pose.culling = animator.cullingMode;
            pose.startHip = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            pose.graph = PlayableGraph.Create("Friend own-bed " + pose.lease.personId); pose.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            pose.mixer = AnimationMixerPlayable.Create(pose.graph, 6);
            var clips = new[] { catalog.restIdle, catalog.restSit, catalog.restLay, catalog.restTuck, catalog.restBrace, catalog.restRecline };
            pose.clips = clips.Select(clip => AnimationClipPlayable.Create(pose.graph, clip)).ToArray();
            for (int i = 0; i < pose.clips.Length; i++) { pose.clips[i].SetSpeed(0); pose.clips[i].SetApplyFootIK(false); pose.graph.Connect(pose.clips[i], 0, pose.mixer, i); }
            AnimationPlayableOutput.Create(pose.graph, "Friend furniture", animator).SetSourcePlayable(pose.mixer);
            pose.graph.Play(); pose.lease.phase = "Sleeping";
        }
        void LateUpdate()
        {
            foreach (var pose in poses.Values)
            {
                var runtime = FriendsLifeRuntime.Instance;
                if (!pose.graph.IsValid() || pose.animator == null || pose.bed == null || !ControlsPose(runtime,pose)) continue;
                MaintainActualVisibility(pose);
                float t = pose.lease.visualElapsed;
                float sit = Smooth((t - CameraLead) / SitDuration), brace = Smooth((t - CameraLead - SitDuration) / 1f);
                float lift = Smooth((t - CameraLead - SitDuration - HandDuration) / TuckDuration);
                float pivot = Smooth((t - CameraLead - SitDuration - HandDuration - TuckDuration) / PivotDuration);
                float lie = Smooth((t - CameraLead - SitDuration - HandDuration - TuckDuration - PivotDuration) / ReclineDuration);
                float recline = Smooth(lie / .65f), extension = Smooth((lie - .65f) / .35f);
                pose.mixer.SetInputWeight(0, 1 - sit); pose.mixer.SetInputWeight(1, sit * (1 - lift) * (1 - brace));
                pose.mixer.SetInputWeight(4, sit * (1 - lift) * brace); pose.mixer.SetInputWeight(3, sit * lift * (1 - recline));
                pose.mixer.SetInputWeight(5, sit * lift * recline * (1 - extension)); pose.mixer.SetInputWeight(2, sit * lift * recline * extension);
                foreach (var clip in pose.clips) clip.SetTime(.02);
                var hip = pose.animator.GetBoneTransform(HumanBodyBones.Hips);
                var desired = Vector3.Lerp(Vector3.Lerp(pose.startHip, pose.bed.seatHip.position, sit), pose.bed.sleepHip.position, pivot);
                pose.actor.Brain.Movement.StopAtPose(new Vector3(desired.x,pose.bed.exit.position.y,desired.z),Quaternion.Euler(0,pose.actor.transform.eulerAngles.y,0));
                pose.animator.transform.localPosition = pose.modelLocalPosition;
                // Exact existing HousingRestSession root projection. Humanoid
                // ResidentialSleep already carries its authored RootQ through
                // the graph; multiplying that same -90 degree root again made
                // the active merged body stand upside down in the bed.
                pose.animator.transform.rotation = Quaternion.Slerp(Quaternion.Slerp(pose.worldRotation, pose.bed.seatHip.rotation, sit), Quaternion.LookRotation(-pose.bed.sleepHip.forward), pivot);
                pose.graph.Evaluate(0); pose.animator.transform.position += desired - hip.position; pose.graph.Evaluate(0);
            }
        }
        static float Smooth(float value) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(value));
        static Animator ActualRestAnimator(ResidentController actor,out SkinnedMeshRenderer body)
        {
            body=null;if(!actor)return null;
            foreach(var system in actor.GetComponentsInChildren<OutfitSystem>(true).OrderByDescending(s=>s.gameObject.activeInHierarchy))
            {
                var merged=system.GetCharacterBody();var animator=system.animator;
                if(!merged||!merged.enabled||!merged.sharedMesh||merged.sharedMesh.vertexCount==0||!animator||!animator.isHuman)continue;
                var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
                if(!hips||merged.bones==null||!merged.bones.Contains(hips))continue;
                body=merged;return animator;
            }
            return actor.VisualAnimator;
        }
        static void MaintainActualVisibility(Pose pose)
        {
            if(!ControlsPose(FriendsLifeRuntime.Instance,pose))return;
            // The original visibility hold is a bool, and a late customization
            // completion can release it. Reassert only while this exact rest
            // owner controls the actual merged body; never activate the hidden
            // source outfits or CombinedSkinnedMesh beneath the Bozo rig.
            pose.actor.Brain.BeginModelVisibilityHold();pose.visibilityHeld=true;
            if(!pose.actualBody||!pose.animator)return;
            for(var t=pose.actualBody.transform;t&&t!=pose.actor.transform;t=t.parent)if(!t.gameObject.activeSelf){if(!pose.visibilityChanges.ContainsKey(t.gameObject))pose.visibilityChanges[t.gameObject]=false;t.gameObject.SetActive(true);}
            for(var t=pose.animator.transform;t&&t!=pose.actor.transform;t=t.parent)if(!t.gameObject.activeSelf){if(!pose.visibilityChanges.ContainsKey(t.gameObject))pose.visibilityChanges[t.gameObject]=false;t.gameObject.SetActive(true);}
        }
        void RequestExit(Pose pose)
        { if (pose.exiting) return; pose.exiting = true; pose.exitPoseTime = Mathf.Min(pose.lease.visualElapsed, EnterDuration); pose.lease.phase = "BedExit"; }
        static void SetOccupied(HousingRestInteractable bed, bool value)
        {
            if (bed.area != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && bed.area.IsSpawned) bed.area.ServerChangeSomeoneUsingRpc(value);
        }
        static bool ControlsPose(FriendsLifeRuntime runtime, Pose pose)
        {
            string id = pose.lease?.personId;
            return runtime && runtime.State != null && runtime.State.worldId == pose.worldId && !string.IsNullOrEmpty(id) &&
                runtime.ActorOf(id) == pose.actor && pose.actor && pose.actor.Brain != null && pose.lease.ownerEpisode == "rest/" + id &&
                runtime.State.leases.Contains(pose.lease) && !runtime.State.leases.Any(l => l.personId == id && l.ownerEpisode != pose.lease.ownerEpisode) &&
                FriendsEconomyService.Instance?.HasActiveJob(id) != true && !pose.actor.GetComponent<FriendResidentDormancy>() &&
                ReferenceEquals(pose.actor.Brain.StateMachine.Current, pose.originalState);
        }
        static void ReleaseReservation(FriendsLifeRuntime runtime, Pose pose)
        {
            bool controls = ControlsPose(runtime,pose);
            if (pose.bed && FriendRestOccupancy.For(pose.bed).friendOccupantPersonId == pose.lease?.personId)
            { FriendRestOccupancy.For(pose.bed).friendOccupantPersonId = ""; SetOccupied(pose.bed, false); }
            if(runtime && runtime.State != null)runtime.State.leases.Remove(pose.lease);
            if (controls && pose.paused)
            {
                foreach(var visibility in pose.visibilityChanges)if(visibility.Key)visibility.Key.SetActive(visibility.Value);pose.visibilityChanges.Clear();
                if(pose.visibilityHeld)pose.actor.Brain.ReleaseModelVisibilityHold();
                pose.visibilityHeld=false;pose.paused=false;
                pose.actor.Brain.Resume(); pose.actor.Brain.RequestScheduleChange();
                FriendOwnedAmbientPathGuardian.ProtectAfterScheduleChange(pose.actor);
            }
        }
        void Finish(FriendsLifeRuntime runtime, string personId, Pose pose)
        {
            bool controls = ControlsPose(runtime,pose);
            if (pose.graph.IsValid()) pose.graph.Destroy();
            if (!controls)
            { ReleaseReservation(runtime,pose); poses.Remove(personId); return; }
            if (pose.animator != null)
            {
                pose.animator.transform.localPosition = pose.modelLocalPosition; pose.animator.transform.localRotation = pose.modelLocalRotation;
                pose.animator.applyRootMotion = pose.applyRootMotion; pose.animator.cullingMode = pose.culling;
            }
            var exit = pose.bed ? pose.bed.exit : null;
            if(pose.enteredPose && (pose.animator == null || exit == null))
            {
                pose.exiting=true;pose.lease.phase="BedExit";
                if(pose.exitBlock == null || !pose.exitBlock.Same(runtime,personId,pose.actor,pose.bed,pose.room,pose.lastExitPosition))
                    pose.exitBlock=RestBlock.Capture(runtime,personId,pose.actor,pose.bed,pose.room,pose.lastExitPosition,"The entered rest pose lost its actual animator or bed-exit binding");
                return;
            }
            if (pose.enteredPose)
            {
                pose.lastExitPosition=exit.position;
                if(pose.exitBlock != null && pose.exitBlock.Same(runtime,personId,pose.actor,pose.bed,pose.room,exit.position))return;
                string failure = null;var agent=pose.actor.GetComponent<NavMeshAgent>();var actualExit=exit.position;
                if(pose.room&&!FriendsPrivateRoomNavigation.Ensure(pose.room,pose.actor,out failure))
                    failure=failure??"The owned private rest navigation binding is unavailable";
                else if(!agent)failure="The owned rest exit has no actual agent";
                else
                {
                    var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
                    if(!NavMesh.SamplePosition(exit.position,out var nav,.25f,filter)||Mathf.Abs(nav.position.y-exit.position.y)>.15f)
                        failure="The owned bed exit has no same-type navigation at its actual endpoint";
                    else
                    {
                        actualExit=nav.position;
                        if(!pose.actor.Brain.Movement.Warp(actualExit,Quaternion.Euler(0,exit.eulerAngles.y,0)))
                            failure="The original movement rejected the owned rest exit";
                        else
                        {
                            pose.actor.Brain.Movement.MatchAgentPositionAtHeight(agent.nextPosition.y);
                            if(!agent.enabled||!agent.isOnNavMesh||Vector3.Distance(pose.actor.transform.position,actualExit)>.15f||Vector3.Distance(pose.actor.transform.position,agent.nextPosition)>.15f)
                                failure="The owned rest exit did not restore the actual body/agent endpoint";
                            else {agent.updatePosition=true;agent.updateRotation=true;}
                        }
                    }
                }
                if(failure!=null)
                {
                    pose.exiting=true;pose.lease.phase="BedExit";
                    pose.exitBlock=RestBlock.Capture(runtime,personId,pose.actor,pose.bed,pose.room,exit.position,failure);return;
                }
                pose.exitBlock=null;
            }
            ReleaseReservation(runtime, pose); poses.Remove(personId); runtime.NotifyChanged();
        }
        void OnDestroy()
        {
            var runtime = FriendsLifeRuntime.Instance;
            foreach (var item in poses.ToArray())
            { if (runtime != null && runtime.Ready) Finish(runtime, item.Key, item.Value); else if (item.Value.graph.IsValid()) item.Value.graph.Destroy(); }
        }
    }
}
