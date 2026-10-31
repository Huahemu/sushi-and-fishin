using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.ResidentialIsland;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SushiAndFish.FriendsLife
{
    [Serializable]
    public sealed class HomeWorldActionSnapshot
    {
        public string actionKey, ownerEpisode, personId, sourceAction, anchorId, phase, failure,holdingClip,sourceFoodId,chopstickPrefab,foodVisualTemplate;
        public bool arrived, poseReady, safeBoundary, supportVerified;
        public float hipError, supportDistance,holdingElapsed;
        public Vector3 exitPosition;
    }

    // This claim belongs to one source-node entry. It is also visible to the
    // housing transaction preflight when no network seat has spawned yet.
    public sealed class FriendsHomeFurnitureClaim : MonoBehaviour
    {
        public string actionKey, ownerEpisode, personId;
        public bool Active => !string.IsNullOrEmpty(actionKey);
        void Awake() => FriendInstances<FriendsHomeFurnitureClaim>.Add(this);
        void OnDestroy() => FriendInstances<FriendsHomeFurnitureClaim>.Remove(this);
    }

    // Only a host projection: the original graph owns the decision, duration,
    // result and continuation. No needs, relationship or wallet is written here.
    [DefaultExecutionOrder(2000)]
    public sealed class FriendsHomeWorldActions : MonoBehaviour
    {
        public static FriendsHomeWorldActions Instance { get; private set; }
        sealed class Action
        {
            public HomeWorldActionSnapshot state;
            public ResidentController actor;
            public HousingHouse house;
            public HousingPrivateRoom room;
            public HousingRestInteractable rest;
            public HousingSeatAnchor chair;
            public Transform approach, seatHip, sleepHip, exit;
            public EmoteArea area;
            public readonly string areaReservationKey = "home-action/" + Guid.NewGuid().ToString("N");
            public FriendsHomeFurnitureClaim claim;
            public ActivityLease lease;
            public bool ownLease, paused, visibilityHeld, needsPortal, entered, graphEvaluated;
            public string leasePhase;
            public Vector3 leaseDestination, destination, localPosition, startHip,chairHip;
            public Quaternion localRotation, startRotation,chairRotation;
            public bool leaseSafe, applyRootMotion;
            public float leaseElapsed, elapsed, evaluatedTime = -1, exitElapsed, exitPoseTime, entryDuration;
            public Animator animator;
            public AnimatorCullingMode culling;
            public PlayableGraph graph;
            public AnimationMixerPlayable mixer;
            public AnimationClipPlayable[] clips;
            public AnimationClip holdingClip;
            public AnimationClipPlayable holdingPlayable;
            public GameObject chopstickProp,foodProp;
        }
        readonly Dictionary<string, Action> actions = new Dictionary<string, Action>(StringComparer.Ordinal);
        bool shuttingDown;
        const float CameraLead = .65f, SitDuration = 1.8f, HandDuration = 1.4f, TuckDuration = 1f, PivotDuration = 1.1f, ReclineDuration = 1.7f;
        static bool Terminal(Action action) => action.state.phase == "Completed" || action.state.phase == "Failed";
        public static bool Controls(string personId) => Instance && Instance.actions.Values.Any(a => a.state.personId == personId && !Terminal(a));
        void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            if(!GetComponent<FriendsHomeStandingNavigation>())gameObject.AddComponent<FriendsHomeStandingNavigation>();
        }
        public bool TryBegin(string personId, string actionKey, string sourceAction, string ownerEpisode, string anchorId = null)
        {
            var runtime = FriendsLifeRuntime.Instance;
            if (!runtime || !runtime.Ready || !runtime.IsAuthority || string.IsNullOrEmpty(actionKey) || string.IsNullOrEmpty(ownerEpisode) ||
                runtime.GetPerson(personId)?.controllerKind != PersonControllerKind.Npc ||
                (sourceAction != "Bed" && sourceAction != "Chair" && sourceAction != "Toilet")) return false;
            if (actions.TryGetValue(actionKey, out var existing))
                return existing.state.personId == personId && existing.state.ownerEpisode == ownerEpisode && existing.state.sourceAction == sourceAction &&
                    (string.IsNullOrEmpty(anchorId) || existing.state.anchorId == anchorId);
            if(FriendsHomeStandingNavigation.Instance&&FriendsHomeStandingNavigation.Instance.Controls(personId))return false;
            if (actions.Values.Any(a => a.state.personId == personId && !Terminal(a))) return false;
            actions.Add(actionKey, new Action { state = new HomeWorldActionSnapshot { personId = personId, actionKey = actionKey, sourceAction = sourceAction,
                ownerEpisode = ownerEpisode, anchorId = anchorId, phase = "Queued", safeBoundary = true, hipError = float.PositiveInfinity } });
            return true;
        }
        public HomeWorldActionSnapshot Observe(string actionKey)
        {
            if (string.IsNullOrEmpty(actionKey) || !actions.TryGetValue(actionKey, out var action)) return null;
            var s = action.state;
            return new HomeWorldActionSnapshot { actionKey = s.actionKey, ownerEpisode = s.ownerEpisode, personId = s.personId, sourceAction = s.sourceAction,
                anchorId = s.anchorId, phase = s.phase, failure = s.failure, arrived = s.arrived, poseReady = s.poseReady, safeBoundary = s.safeBoundary,
                hipError = s.hipError, supportVerified = s.supportVerified, supportDistance = s.supportDistance,exitPosition=s.exitPosition,
                holdingClip=s.holdingClip,holdingElapsed=s.holdingElapsed,sourceFoodId=s.sourceFoodId,chopstickPrefab=s.chopstickPrefab,foodVisualTemplate=s.foodVisualTemplate };
        }
        public HomeWorldActionSnapshot[] ObserveAll()=>actions.Keys.OrderBy(key=>key,StringComparer.Ordinal).Select(Observe).ToArray();
        public bool RequestOwnerExit(string ownerEpisode)
        {
            if(string.IsNullOrEmpty(ownerEpisode))return false;
            bool accepted=true;
            foreach(var action in actions.Values.Where(a=>a.state.ownerEpisode==ownerEpisode&&!Terminal(a)).ToArray())
                accepted&=RequestExit(action.state.actionKey,ownerEpisode);
            if(FriendsHomeStandingNavigation.Instance)accepted&=FriendsHomeStandingNavigation.Instance.RequestOwnerExit(ownerEpisode);
            return accepted;
        }
        public bool OwnerAtSafeBoundary(string ownerEpisode)
        {
            if(string.IsNullOrEmpty(ownerEpisode))return false;
            return actions.Values.Where(a=>a.state.ownerEpisode==ownerEpisode).All(a=>Terminal(a)&&a.state.safeBoundary)&&
                (!FriendsHomeStandingNavigation.Instance||FriendsHomeStandingNavigation.Instance.OwnerAtSafeBoundary(ownerEpisode));
        }
        static AnimationClip SourceFoodClip(Animator animator)
        {
            const string name="Customer_Sit_Chair_Eat_Sushi";
            return animator&&animator.runtimeAnimatorController?
                animator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c&&c.name==name):null;
        }
        public bool CanBeginSourceFoodUse(string actionKey,string ownerEpisode)
        {
            return actions.TryGetValue(actionKey??"",out var action)&&action.state.ownerEpisode==ownerEpisode&&action.state.sourceAction=="Chair"&&
                action.state.phase=="Holding"&&action.state.poseReady&&action.state.supportVerified&&action.graph.IsValid()&&SourceFoodClip(action.animator)&&
                FriendResidentBindings.IsOwned(action.actor)&&Chopstick(action)!=null&&FoodPropAnchor(action)&&action.animator.GetBoneTransform(HumanBodyBones.LeftHand)&&
                Resources.Load<GameObject>("FriendsLife/HostTemplates/FoodSushiMaki");
        }
        public bool TryBeginSourceFoodUse(string actionKey,string ownerEpisode)
            =>TryBeginSourceFoodUse(actionKey,ownerEpisode,null);
        public bool TryBeginSourceFoodUse(string actionKey,string ownerEpisode,string sourceFoodId)
        {
            if(!CanBeginSourceFoodUse(actionKey,ownerEpisode))return false;
            var action=actions[actionKey];if(action.holdingClip)return true;
            var clip=SourceFoodClip(action.animator);
            action.holdingPlayable=AnimationClipPlayable.Create(action.graph,clip);action.holdingPlayable.SetSpeed(0);action.holdingPlayable.SetApplyFootIK(false);
            action.mixer.SetInputCount(3);if(!action.graph.Connect(action.holdingPlayable,0,action.mixer,2))return false;
            action.holdingClip=clip;action.state.holdingClip=clip.name;action.state.holdingElapsed=0;
            AddFoodProps(action,sourceFoodId);
            FriendsLifeRuntime.Instance?.NotifyChanged();return true;
        }
        static object ActorField(ResidentController actor,string name)=>typeof(ResidentController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(actor);
        static GameObject Chopstick(Action action)
        {
            var prefabs=ActorField(action.actor,"_chopstickPropPrefabs") as List<GameObject>;if(prefabs==null||prefabs.Count==0)return null;
            int table=0;var plot=action.house.plot;if(!string.IsNullOrEmpty(plot)&&plot.Length>1)int.TryParse(plot.Substring(1),out table);
            int seat=Array.IndexOf(action.house.chairs??Array.Empty<HousingSeatAnchor>(),action.chair);
            int start=(int)((uint)((table*397)^Math.Max(0,seat))%(uint)prefabs.Count);
            for(int i=0;i<prefabs.Count;i++)if(prefabs[(start+i)%prefabs.Count])return prefabs[(start+i)%prefabs.Count];return null;
        }
        static Transform FoodPropAnchor(Action action)
        {
            var anchor=ActorField(action.actor,"_restaurantPropAnchor") as Transform;
            return anchor&&anchor.IsChildOf(action.animator.transform)?anchor:action.animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
        void AddFoodProps(Action action,string sourceFoodId)
        {
            var anchor=FoodPropAnchor(action);
            var prefab=Chopstick(action);if(!anchor||!prefab)return;
            action.chopstickProp=Instantiate(prefab,anchor);action.chopstickProp.name="SourceFoodChopsticks";
            if(ActorField(action.actor,"_chopstickPropLocalPosition") is Vector3 position)action.chopstickProp.transform.localPosition=position;
            if(ActorField(action.actor,"_chopstickPropLocalRotation") is Vector3 rotation)action.chopstickProp.transform.localRotation=Quaternion.Euler(rotation);
            if(ActorField(action.actor,"_chopstickPropLocalScale") is Vector3 scale)action.chopstickProp.transform.localScale=scale;
            // This tiny shared template is an unchanged original host Maki
            // prefab containing only its original mesh/material renderer.
            var food=Resources.Load<GameObject>("FriendsLife/HostTemplates/FoodSushiMaki");
            var left=action.animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if(food&&left){action.foodProp=Instantiate(food,left);action.foodProp.name="SourceFoodVisual";action.foodProp.transform.localPosition=Vector3.zero;action.foodProp.transform.localRotation=Quaternion.identity;}
            action.state.sourceFoodId=sourceFoodId;action.state.chopstickPrefab=prefab.name;action.state.foodVisualTemplate="FriendsLife/HostTemplates/FoodSushiMaki";
        }
        void ClearFoodProps(Action action)
        {if(action.chopstickProp)Destroy(action.chopstickProp);if(action.foodProp)Destroy(action.foodProp);action.chopstickProp=null;action.foodProp=null;}
        public bool RequestExit(string actionKey, string ownerEpisode)
        {
            if (!actions.TryGetValue(actionKey ?? "", out var action) || action.state.ownerEpisode != ownerEpisode) return false;
            if (Terminal(action)) return true;
            if (action.state.phase == "Queued" || !action.entered) { Finish(action, null); return true; }
            if (action.state.phase == "Exiting") return true;
            action.state.phase = "Exiting"; action.state.safeBoundary = false; action.state.poseReady = false;
            ClearFoodProps(action);
            action.exitPoseTime = Mathf.Min(action.elapsed, action.entryDuration); action.exitElapsed = 0;
            return true;
        }
        // Caller removes a terminal observation only after the source cursor
        // has persisted its own continuation. A stale callback cannot free it.
        public bool Forget(string actionKey, string ownerEpisode)
        {
            if (!actions.TryGetValue(actionKey ?? "", out var action) || action.state.ownerEpisode != ownerEpisode || !Terminal(action)) return false;
            return actions.Remove(actionKey);
        }
        void Update()
        {
            var runtime = FriendsLifeRuntime.Instance;
            if (!runtime || !runtime.Ready || !runtime.IsAuthority || !runtime.IsWorldRunning) return;
            foreach (var action in actions.Values.Where(a => !Terminal(a)).ToArray())
            {
                try { Advance(runtime, action); }
                catch (Exception error) { Finish(action, error.GetBaseException().Message); }
            }
        }
        void Advance(FriendsLifeRuntime runtime, Action action)
        {
            var s = action.state;
            if (s.phase == "Queued")
            {
                // A queued new action must not ask the original owner to exit
                // or claim a body while an in-place conversation is active.
                if (runtime.HasActiveInPlaceTalk(s.personId) && !runtime.State.leases.Any(l => l.personId == s.personId && l.ownerEpisode == s.ownerEpisode && l.phase != "JobHandover")) return;
                var old = runtime.GetComponent<FriendsNpcRestController>();
                if (old && old.IsResting(s.personId)) { old.RequestSourceExit(s.personId); return; }
                if (runtime.State.leases.Any(l => l.personId == s.personId && l.ownerEpisode != s.ownerEpisode)) return;
                if (FriendsEconomyService.Instance?.HasActiveJob(s.personId) == true) return;
                var actor = runtime.ActorOf(s.personId);
                if (!actor || actor.Brain == null || actor.Runtime == null || actor.Runtime.IsInteracting || actor.Runtime.IsInRestaurant || actor.Runtime.IsWorking) return;
                if (!Prepare(runtime, action, actor, out var failure)) { Finish(action, failure); return; }
                action.actor = actor;
                action.lease = runtime.State.leases.FirstOrDefault(l => l.personId == s.personId && l.ownerEpisode == s.ownerEpisode);
                action.ownLease = action.lease == null||action.lease.leaseId=="home-action/"+s.actionKey;
                if (action.lease==null)
                {
                    action.lease = new ActivityLease { leaseId = "home-action/" + s.actionKey, personId = s.personId, ownerEpisode = s.ownerEpisode, facilityId = s.anchorId };
                    runtime.State.leases.Add(action.lease);
                }
                action.leasePhase = action.lease.phase; action.leaseDestination = action.lease.destination;
                action.leaseSafe = action.lease.safeBoundary; action.leaseElapsed = action.lease.visualElapsed;
                if (!actor.Runtime.IsSnapshotApplying) { actor.Brain.Pause(); action.paused = true; }
                actor.Brain.BeginModelVisibilityHold(); action.visibilityHeld = true;
                action.claim.actionKey = s.actionKey; action.claim.ownerEpisode = s.ownerEpisode; action.claim.personId = s.personId;
                if (action.rest) FriendRestOccupancy.For(action.rest).friendOccupantPersonId = s.personId;
                if (!SetOccupied(action, true))
                { Finish(action, "The actual furniture rejected the source-owned reservation"); return; }
                if (!runtime.TryMoveWorldActor(s.personId, action.approach.position, out action.destination, .25f))
                { Finish(action, runtime.LastWorldMoveDiagnostic ?? "Original navigation rejected the actual furniture approach"); return; }
                action.lease.destination = action.destination; action.lease.phase = "HomeActionTravel"; action.lease.safeBoundary = true;
                s.phase = "Travel"; runtime.NotifyChanged(); return;
            }
            if (!action.actor || action.actor.Brain == null || runtime.ActorOf(s.personId)!=action.actor || !action.house || !action.claim || action.claim.actionKey != s.actionKey ||
                action.lease==null||!runtime.State.leases.Contains(action.lease)||action.lease.ownerEpisode!=s.ownerEpisode)
            { Finish(action, "The source-owned furniture or actor projection disappeared"); return; }
            if(action.entered&&(!action.animator||action.actor.VisualAnimator!=action.animator))
            {Finish(action,"The owned appearance changed; retain the source result and rebuild only its presentation");return;}
            if (s.phase == "Travel")
            {
                var agent = action.actor.GetComponent<NavMeshAgent>();
                float tolerance = Mathf.Max(.15f, (agent ? agent.stoppingDistance : 0) + .05f);
                if (!agent || !agent.enabled || !agent.isOnNavMesh || agent.pathPending || agent.pathStatus != NavMeshPathStatus.PathComplete ||
                    !action.actor.Brain.Movement.HasArrived() || Vector3.Distance(action.actor.transform.position, action.destination) > tolerance ||
                    Vector3.Distance(action.actor.Brain.Movement.Destination, action.destination) > tolerance) return;
                if (action.needsPortal)
                {
                    if (!action.room || !FriendsHomeInteriors.Instance || !FriendsHomeInteriors.Instance.EnterPrivateRoom(s.personId))
                    { Finish(action, "The actual private-room portal rejected entry"); return; }
                }
                action.actor.Brain.Movement.StopAtPose(action.exit.position, Upright(action.exit.rotation));
                StartPose(action); s.arrived = true; s.phase = "Entering"; s.safeBoundary = false;
                action.lease.phase = "HomeActionEntering"; action.lease.safeBoundary = false; runtime.NotifyChanged();
            }
            else if (s.phase == "Entering" || s.phase == "Holding")
            {
                action.elapsed += Time.unscaledDeltaTime;
                if(s.phase=="Holding"&&action.holdingClip)s.holdingElapsed+=Time.unscaledDeltaTime;
                action.lease.visualElapsed = action.elapsed;
                if(action.graphEvaluated&&action.evaluatedTime>=action.entryDuration&&!s.supportVerified)
                {Finish(action,s.failure??"The actual furniture support rejected this pose");return;}
                // A timer cannot attest an actual furniture pose. LateUpdate
                // must have evaluated the graph and measured the real pelvis.
                if (action.graphEvaluated && action.evaluatedTime >= action.entryDuration && s.hipError <= .015f && s.supportVerified)
                {
                    bool changed=s.phase!="Holding";s.phase = "Holding"; s.poseReady = true; s.safeBoundary = true; action.lease.phase = "HomeActionHolding"; action.lease.safeBoundary = true;
                    if(changed)runtime.NotifyChanged();
                }
            }
            else if (s.phase == "Exiting")
            {
                action.exitElapsed += Time.unscaledDeltaTime;
                float duration = (s.sourceAction == "Bed" ? 6.5f : 2f) * Mathf.Clamp(action.exitPoseTime / action.entryDuration, .2f, 1);
                action.elapsed = action.exitPoseTime * (1 - Mathf.Clamp01(action.exitElapsed / duration));
                if (action.exitElapsed >= duration && action.graphEvaluated && action.evaluatedTime <= .001f) Finish(action, null);
            }
        }
        bool Prepare(FriendsLifeRuntime runtime, Action action, ResidentController actor, out string failure)
        {
            failure = null; var s = action.state; var economy = FriendsEconomyService.Instance; var person = runtime.GetPerson(s.personId);
            var privateRoom = FriendsHomeInteriors.Instance?.RoomFor(s.personId);
            var common = economy?.Plot(person.currentHomeId)?.house;
            if(s.sourceAction=="Bed"&&!privateRoom&&economy!=null&&economy.MembersOf(person.currentHomeId).Count()>1)
            {failure="The member's own private bed is not yet projected";return false;}
            var candidates = new List<(HousingHouse house, HousingPrivateRoom room)>();
            if (s.sourceAction == "Bed" && privateRoom) candidates.Add((privateRoom.house, privateRoom));
            if (common && (s.sourceAction != "Bed" || !privateRoom)) candidates.Add((common, null));
            if (s.sourceAction != "Bed" && privateRoom) candidates.Add((privateRoom.house, privateRoom));
            foreach (var candidate in candidates)
            {
                if (!candidate.house || candidate.house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone != true || !economy.CanEnter(candidate.house, s.personId)) continue;
                if (s.sourceAction == "Chair")
                {
                    FriendsChairApproachProjection.Apply(candidate.house, actor);
                    foreach (var chair in (candidate.house.chairs ?? Array.Empty<HousingSeatAnchor>()).Where(c => c && c.pose && c.exit))
                    {
                        string id = AnchorIdentity(candidate.house, chair.transform);
                        if (!string.IsNullOrEmpty(s.anchorId) && s.anchorId != id) continue;
                        var area = candidate.house.spawnedSeats.Where(go => go).Select(go => go.GetComponent<EmoteArea>()).FirstOrDefault(a => a && Vector3.Distance(a.transform.position, chair.pose.position) < .1f);
                        if (area && area.IsOccupied) continue;
                        var claim = Claim(chair.gameObject); if (claim.Active && claim.actionKey != s.actionKey) continue;
                        action.house = candidate.house; action.room = candidate.room; action.chair = chair; action.exit = chair.exit; action.seatHip = chair.pose;
                        action.area = area; action.claim = claim; s.anchorId = id; break;
                    }
                }
                else
                {
                    var kind = s.sourceAction == "Bed" ? HousingRestKind.Bed : HousingRestKind.Toilet;
                    foreach (var rest in candidate.house.GetComponentsInChildren<HousingRestInteractable>(true).Where(r => r.kind == kind && r.exit && r.seatHip && (kind != HousingRestKind.Bed || r.sleepHip)))
                    {
                        string id = AnchorIdentity(candidate.house, rest.transform);
                        if (!string.IsNullOrEmpty(s.anchorId) && s.anchorId != id) continue;
                        if (rest.area && rest.area.IsOccupied || !string.IsNullOrEmpty(FriendRestOccupancy.For(rest).friendOccupantPersonId)) continue;
                        var claim = Claim(rest.gameObject); if (claim.Active && claim.actionKey != s.actionKey) continue;
                        action.house = candidate.house; action.room = candidate.room; action.rest = rest; action.exit = rest.exit; action.seatHip = rest.seatHip;
                        action.sleepHip = rest.sleepHip; action.area = rest.area; action.claim = claim; s.anchorId = id; break;
                    }
                }
                if (action.house) break;
            }
            if (!action.house) { failure = "No permitted, unoccupied actual " + s.sourceAction + " anchor matches this source action"; return false; }
            var animator = actor.VisualAnimator; var catalog = HousingIsland.Instance?.Catalog;
            if (!animator || !animator.isHuman || !animator.avatar || !animator.GetBoneTransform(HumanBodyBones.Hips) || !catalog || !catalog.restSit || !catalog.restIdle ||
                s.sourceAction == "Bed" && (!catalog.restLay || !catalog.restTuck || !catalog.restBrace || !catalog.restRecline))
            { failure = "The actual Humanoid furniture clips or pelvis are unavailable"; return false; }
            if(s.sourceAction=="Bed"&&!FriendsHostRestRootRotation.Ready){failure="The original rest RootQ binding is unavailable";return false;}
            action.approach = action.exit;
            if (action.room && Vector3.Distance(actor.transform.position, action.room.transform.position) > 20)
            {
                var portal = FriendInstances<HousingRoomPortal>.Active().FirstOrDefault(p => p.allowedPersonId == s.personId && p.homeId == person.currentHomeId && !p.leavingPrivateRoom);
                if (!portal) { failure = "The actual private-room entry portal is unavailable"; return false; }
                action.approach = portal.transform; action.needsPortal = true;
            }
            action.entryDuration = CameraLead + SitDuration + (s.sourceAction == "Bed" ? HandDuration + TuckDuration + PivotDuration + ReclineDuration : 0);
            return true;
        }
        static FriendsHomeFurnitureClaim Claim(GameObject go)
        { var claim = go.GetComponent<FriendsHomeFurnitureClaim>(); return claim ? claim : go.AddComponent<FriendsHomeFurnitureClaim>(); }
        public static string AnchorIdentity(HousingHouse house, Transform anchor)
        {
            string path = anchor.name; for (var parent = anchor.parent; parent && parent != house.transform; parent = parent.parent) path = parent.name + "/" + path;
            var metadata = house.GetComponent<FriendHouseMetadata>();
            // Private room clones are projections rebuilt after loading. Their
            // temporary clone GUID is not the source anchor's durable identity.
            var common=!string.IsNullOrEmpty(metadata?.privatePersonId)?FriendsEconomyService.Instance?.Plot(house.plot)?.house:null;
            string generation=common?FriendHousingIsland.HomeIdentity(common):FriendHousingIsland.HomeIdentity(house);
            return "home-anchor/" + house.plot + "/" + generation + "/" + (metadata?.privatePersonId ?? "") + "/" + path;
        }
        static Quaternion Upright(Quaternion rotation)
        { var forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up); return forward.sqrMagnitude > .0001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity; }
        void StartPose(Action action)
        {
            var catalog = HousingIsland.Instance.Catalog; var animator = action.actor.VisualAnimator;
            action.animator = animator; action.localPosition = animator.transform.localPosition; action.localRotation = animator.transform.localRotation;
            action.startRotation = animator.transform.rotation; action.startHip = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            action.applyRootMotion = animator.applyRootMotion; action.culling = animator.cullingMode;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            action.graph = PlayableGraph.Create("Source home action " + action.state.actionKey); action.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var assets = action.state.sourceAction == "Bed" ? new[] { catalog.restIdle, catalog.restSit, catalog.restLay, catalog.restTuck, catalog.restBrace, catalog.restRecline } : new[] { catalog.restIdle, catalog.restSit };
            action.mixer = AnimationMixerPlayable.Create(action.graph, assets.Length); action.clips = new AnimationClipPlayable[assets.Length];
            for (int i = 0; i < assets.Length; i++) { action.clips[i] = AnimationClipPlayable.Create(action.graph, assets[i]); action.clips[i].SetSpeed(0); action.clips[i].SetApplyFootIK(false); action.graph.Connect(action.clips[i], 0, action.mixer, i); }
            AnimationPlayableOutput.Create(action.graph, "Actual home furniture", animator).SetSourcePlayable(action.mixer); action.graph.Play(); action.entered = true;
            if (action.chair)
            {
                // Native chair authoring stores a ROOT seated pose, not a hip.
                // Measure the normal retargeted seated pelvis at that root once;
                // do not align a pelvis to the floor-level root anchor.
                action.actor.Brain.Movement.StopAtPose(action.chair.pose.position, Upright(action.chair.pose.rotation));
                animator.transform.localPosition = action.localPosition; animator.transform.localRotation = action.localRotation;
                action.mixer.SetInputWeight(0, 0); action.mixer.SetInputWeight(1, 1); action.clips[1].SetTime(.02); action.graph.Evaluate(0);
                action.chairHip = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                action.chairRotation = animator.transform.rotation;
                // Measuring the chair's authored root pose must not replace the
                // standing start pelvis. Keep the original stand-to-seat blend;
                // otherwise idle legs are translated below the floor at t=0.
                action.actor.Brain.Movement.StopAtPose(action.exit.position,Upright(action.exit.rotation));
                animator.transform.localPosition=action.localPosition;animator.transform.localRotation=action.localRotation;
            }
        }
        void LateUpdate()
        {
            foreach (var action in actions.Values.Where(a => !Terminal(a) && a.graph.IsValid() && a.animator && a.house).ToArray())
            {
                var animator = action.animator; float t = action.elapsed;
                float sit = Smooth((t - CameraLead) / SitDuration);
                float brace = action.state.sourceAction == "Bed" ? Smooth((t - CameraLead - SitDuration) / 1f) : 0;
                float lift = action.state.sourceAction == "Bed" ? Smooth((t - CameraLead - SitDuration - HandDuration) / TuckDuration) : 0;
                float pivot = action.state.sourceAction == "Bed" ? Smooth((t - CameraLead - SitDuration - HandDuration - TuckDuration) / PivotDuration) : 0;
                float lie = action.state.sourceAction == "Bed" ? Smooth((t - CameraLead - SitDuration - HandDuration - TuckDuration - PivotDuration) / ReclineDuration) : 0;
                float recline = Smooth(lie / .65f), extension = Smooth((lie - .65f) / .35f);
                action.mixer.SetInputWeight(0, 1 - sit); action.mixer.SetInputWeight(1, sit * (1 - lift) * (1 - brace));
                if (action.state.sourceAction == "Bed")
                { action.mixer.SetInputWeight(4, sit * (1 - lift) * brace); action.mixer.SetInputWeight(3, sit * lift * (1 - recline)); action.mixer.SetInputWeight(5, sit * lift * recline * (1 - extension)); action.mixer.SetInputWeight(2, sit * lift * recline * extension); }
                foreach (var clip in action.clips) clip.SetTime(.02);
                if(action.holdingClip&&action.holdingPlayable.IsValid())
                {
                    bool usingFood=action.state.phase=="Holding";
                    action.mixer.SetInputWeight(2,usingFood?1:0);
                    if(usingFood)
                    {
                        action.mixer.SetInputWeight(0,0);action.mixer.SetInputWeight(1,0);
                        double clipTime=action.holdingClip.isLooping?action.state.holdingElapsed%Math.Max(.001f,action.holdingClip.length):Math.Min(action.state.holdingElapsed,action.holdingClip.length);
                        action.holdingPlayable.SetTime(clipTime);
                    }
                }
                var hip = animator.GetBoneTransform(HumanBodyBones.Hips);
                Vector3 desired = Vector3.Lerp(action.startHip,action.chair?action.chairHip:action.seatHip.position,sit);
                if (action.state.sourceAction == "Bed") desired = Vector3.Lerp(desired, action.sleepHip.position, pivot);
                // The original player furniture session places its body below
                // the actual pelvis X/Z as well. Keep the NPC's real collider
                // and context position with its visible pose, rather than
                // leaving the body at a remote approach while moving only art.
                var bodyRotation=action.chair?Quaternion.Slerp(Upright(action.exit.rotation),Upright(action.chair.pose.rotation),sit):Upright(action.exit.rotation);
                action.actor.Brain.Movement.StopAtPose(new Vector3(desired.x,action.exit.position.y,desired.z),bodyRotation);
                animator.transform.localPosition = action.localPosition;
                var seatedRotation = action.chair ? action.chairRotation : action.seatHip.rotation;
                animator.transform.rotation = Quaternion.Slerp(action.startRotation, seatedRotation, sit);
                if (action.state.sourceAction == "Bed") animator.transform.rotation = Quaternion.Slerp(animator.transform.rotation, Quaternion.LookRotation(-action.sleepHip.forward), pivot);
                if(action.state.sourceAction=="Bed")animator.transform.rotation*=FriendsHostRestRootRotation.Blend(action.mixer,action.clips);
                action.graph.Evaluate(0); animator.transform.position += desired - hip.position; action.graph.Evaluate(0);
                action.state.hipError = Vector3.Distance(hip.position, desired); action.graphEvaluated = true; action.evaluatedTime = t;
                if(t>=action.entryDuration)
                {
                    action.state.supportDistance=SupportDistance(action.house,hip.position);
                    action.state.supportVerified=action.state.supportDistance<=.35f;
                    if(!action.state.supportVerified)action.state.failure="The actual pelvis has no furniture support within 0.35 metres";
                    else action.state.failure=null;
                }
                if (action.rest && action.rest.toilet) action.rest.toilet.SetOpen(Smooth(t / CameraLead));
            }
        }
        static float Smooth(float value) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(value));
        static float SupportDistance(HousingHouse house,Vector3 pelvis)
        {
            // Only the current house's physical surfaces attest the seat/bed.
            // A floor well below a pelvis cannot turn a floating pose into success.
            return Physics.RaycastAll(pelvis+Vector3.up*.05f,Vector3.down,.4f,~0,QueryTriggerInteraction.Ignore)
                .Where(hit=>hit.collider&&hit.collider.transform.IsChildOf(house.transform)&&hit.normal.y>=.5f)
                .Select(hit=>Mathf.Abs(pelvis.y-hit.point.y)).DefaultIfEmpty(float.PositiveInfinity).Min();
        }
        static bool SetOccupied(Action action, bool value)
        {
            if (!action.area) return true;
            // Player requests authenticate the RPC sender, not this source NPC.
            // The per-action resident token also makes stale releases harmless.
            return value
                ? action.area.TryReserveForResident(action.areaReservationKey)
                : action.area.ReleaseResidentReservation(action.areaReservationKey);
        }
        static bool TryExitPoint(Action action,Transform exit,out Vector3 point)
        {
            point=default;var agent=action.actor.GetComponent<NavMeshAgent>();
            if(!agent)return false;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            var requested=action.room?exit.position:action.destination;
            if(action.room&&!FriendsPrivateRoomNavigation.Ensure(action.room,action.actor,out _))return false;
            if(!NavMesh.SamplePosition(requested,out var nav,.25f,filter)||Mathf.Abs(nav.position.y-requested.y)>.15f)return false;
            var capsule=action.actor.GetComponent<CapsuleCollider>();if(!capsule||capsule.direction!=1)return false;
            var scale=capsule.transform.lossyScale;float radius=capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
            float height=Mathf.Max(radius*2,capsule.height*Mathf.Abs(scale.y));
            var center=nav.position+action.actor.transform.rotation*Vector3.Scale(capsule.center,scale);
            var lower=center+Vector3.down*(height*.5f-radius)+Vector3.up*.025f;
            var upper=center+Vector3.up*(height*.5f-radius)+Vector3.up*.025f;
            foreach(var c in Physics.OverlapCapsule(lower,upper,radius,~0,QueryTriggerInteraction.Ignore))
            {
                if(!c||c.transform.IsChildOf(action.actor.transform))continue;
                var at=capsule.transform.position+(nav.position-action.actor.transform.position)+Vector3.up*.025f;
                if(Physics.ComputePenetration(capsule,at,capsule.transform.rotation,c,c.transform.position,c.transform.rotation,out var normal,out var depth)&&
                    (Vector3.Dot(normal,Vector3.up)<.5f||depth>capsule.contactOffset))return false;
            }
            point=nav.position;return true;
        }
        void Finish(Action action, string failure)
        {
            if (Terminal(action)) return;
            var runtime = FriendsLifeRuntime.Instance; var s = action.state;
            ClearFoodProps(action);
            if (action.graph.IsValid()) action.graph.Destroy();
            if (action.rest && action.rest.toilet) action.rest.toilet.SetOpen(0);
            if (action.animator)
            { action.animator.transform.localPosition = action.localPosition; action.animator.transform.localRotation = action.localRotation; action.animator.applyRootMotion = action.applyRootMotion; action.animator.cullingMode = action.culling; }
            bool controlsActor=runtime&&runtime.State!=null&&runtime.ActorOf(s.personId)==action.actor&&action.lease!=null&&runtime.State.leases.Contains(action.lease)&&action.lease.ownerEpisode==s.ownerEpisode&&
                !runtime.State.leases.Any(l=>l.personId==s.personId&&l.ownerEpisode!=s.ownerEpisode);
            if(!action.entered&&controlsActor&&action.actor&&action.actor.Brain!=null&&action.claim&&action.claim.actionKey==s.actionKey)action.actor.Brain.Movement.Stop();
            if (action.entered && controlsActor && action.actor && action.actor.Brain != null && action.exit)
            {
                var exit = action.exit;
                if (exit)
                {
                    bool exited=TryExitPoint(action,exit,out var actualExit);
                    if(exited)
                    {
                        action.actor.Brain.Movement.StopAtPose(actualExit,Upright(exit.rotation));
                        exited=action.actor.Brain.Movement.Warp(actualExit,Upright(exit.rotation));
                        var agent=action.actor.GetComponent<NavMeshAgent>();
                        if(exited&&agent&&agent.enabled&&agent.isOnNavMesh)
                        {
                            action.actor.Brain.Movement.MatchAgentPositionAtHeight(agent.nextPosition.y);
                            exited=Vector3.Distance(action.actor.transform.position,actualExit)<=.15f&&Vector3.Distance(action.actor.transform.position,agent.nextPosition)<=.15f;
                        }
                        if(exited)s.exitPosition=action.actor.transform.position;
                    }
                    if(!exited)
                    {
                        failure="The original movement rejected the actual furniture exit";
                        if(!shuttingDown)
                        {
                            // Retain the same claim/lease and retry the existing
                            // exit. A rejected Warp is not a safe handover.
                            s.phase="Exiting";s.failure=failure;s.poseReady=false;s.safeBoundary=false;
                            action.graphEvaluated=true;action.evaluatedTime=0;action.exitPoseTime=0;action.exitElapsed=2;
                            return;
                        }
                    }
                }
            }
            if (action.claim && action.claim.actionKey == s.actionKey && action.claim.ownerEpisode == s.ownerEpisode)
            {
                action.claim.actionKey = action.claim.ownerEpisode = action.claim.personId = null;
                if (action.rest && FriendRestOccupancy.For(action.rest).friendOccupantPersonId == s.personId) FriendRestOccupancy.For(action.rest).friendOccupantPersonId = "";
            }
            SetOccupied(action, false);
            if (runtime && runtime.State != null && action.lease != null && action.lease.ownerEpisode == s.ownerEpisode)
            {
                if (action.ownLease) runtime.State.leases.Remove(action.lease);
                else if (runtime.State.leases.Contains(action.lease))
                { action.lease.phase = action.leasePhase; action.lease.destination = action.leaseDestination; action.lease.safeBoundary = action.leaseSafe; action.lease.visualElapsed = action.leaseElapsed; }
            }
            if (action.actor && action.actor.Brain != null)
            {
                if (action.visibilityHeld) action.actor.Brain.ReleaseModelVisibilityHold();
                if (action.paused && runtime && runtime.ActorOf(s.personId)==action.actor&&!runtime.State.leases.Any(l => l.personId == s.personId)) { action.actor.Brain.Resume(); action.actor.Brain.RequestScheduleChange(); }
            }
            s.failure = failure; s.phase = failure == null ? "Completed" : "Failed"; s.poseReady = false; s.safeBoundary = true;
            if(action.paused)FriendOwnedAmbientPathGuardian.ProtectAfterScheduleChange(action.actor);
            if (runtime && runtime.Ready) runtime.NotifyChanged();
        }
        void OnDestroy()
        { shuttingDown=true;foreach (var action in actions.Values.Where(a => !Terminal(a)).ToArray()) Finish(action, "Home action projection was unloaded"); if (Instance == this) Instance = null; }
    }
}
