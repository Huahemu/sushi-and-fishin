using System;
using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Data.Ai.NPCs;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using SushiAndFish.ResidentialIsland;
using TomodachiLife.Simulation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    public sealed class FriendWorldActor : MonoBehaviour
    {
        public string personId;
        public int appearanceRevision;
    }
    public sealed class FriendCharacterData : DataObject
    {
        public CharacterData data;
        public override CharacterData GetCharacterData() => data;
    }
    /// <summary>Explicit source/host coordinate calibration. No host XZ is passed off as native island coordinates.</summary>
    public sealed class FriendPlaceBinding : MonoBehaviour
    {
        public string placeId, sourceFrame;
        public Transform hostFrame;
        public Matrix4x4 sourceLocalToWorld = Matrix4x4.identity;
        public Vector3 localSize = Vector3.one;
        public bool inside;
        void Awake() => FriendInstances<FriendPlaceBinding>.Add(this);
        void OnDestroy() => FriendInstances<FriendPlaceBinding>.Remove(this);
        public bool Contains(Vector3 position)
        {
            var frame = hostFrame ? hostFrame : transform;
            var p = frame.InverseTransformPoint(position);
            return Math.Abs(p.x) <= localSize.x * .5f && Math.Abs(p.y) <= localSize.y * .5f && Math.Abs(p.z) <= localSize.z * .5f;
        }
        public Vector3 SourcePosition(Vector3 hostPosition)
            => sourceLocalToWorld.MultiplyPoint3x4((hostFrame ? hostFrame : transform).worldToLocalMatrix.MultiplyPoint3x4(hostPosition));
    }
    public sealed partial class FriendsLifeRuntime
    {
        private readonly Dictionary<string, PPG_Player> humanActors = new Dictionary<string, PPG_Player>();
        private SourceFamilyAppearanceRuntime familyAppearance;
        private FriendHousingPrivacy housingPrivacy;
        public Func<NativeMiiEventQuery, bool?> NativePredicateProvider;
        public Func<NativeMiiEvent, NativeMiiEventContext, int?> NativeRelationBoostProvider;
        public Func<uint, NativeMiiEventContext, NativeMiiEventContext> NativeParticipantReselector;
        public Func<double, int?> NativeTimePeriodProvider;
        public event Action<PersonBinding> ActorProjectionFailed;

        private void DiscoverBoundActors()
        {
            var manager = Singleton<ResidentManager>.Instance;
            if (manager == null) return;
            foreach (var person in State.people.Where(p => p.controllerKind == PersonControllerKind.Npc))
            {
                if (actors.TryGetValue(person.personId, out var actor) && actor != null) continue;
                var byIdentity = manager.Residents.FirstOrDefault(r => r != null && r.GetComponent<FriendWorldActor>()?.personId == person.personId);
                var bySavedId = person.hostResidentId > 0 && (!IsAuthority || person.existingHostResident) ? manager.Residents.FirstOrDefault(r => r != null &&
                    (IsAuthority ? r.ResidentId : r.NetworkResidentId) == person.hostResidentId) : null;
                actor = byIdentity ? byIdentity : bySavedId;
                if (actor != null) { actors[person.personId] = actor; AttachActorIdentity(actor, person); }
                else if (person.arrivalGrantUsed || !string.IsNullOrEmpty(person.currentHomeId)) EnsureActorProjection(person);
            }
        }
        private void AttachActorIdentity(ResidentController actor, PersonBinding person)
        {
            var identity = actor.GetComponent<FriendWorldActor>();
            if (identity == null) identity = actor.gameObject.AddComponent<FriendWorldActor>();
            identity.personId = person.personId;
            FriendOwnedAmbientPathGuardian.Attach(actor);
            if (!person.existingHostResident && !actor.GetComponent<FriendResidentOwnedProjection>())
                FriendResidentBindings.MarkOwned(actor, actor.transform.position, actor.transform.rotation);
            if (IsAuthority) person.hostResidentId = actor.ResidentId;
            else if (actor.ResidentData != null && actor.ResidentData.NpcID != person.hostResidentId)
            {
                var data = Instantiate(actor.ResidentData);
                data.Name = person.displayName; data.NpcID = person.hostResidentId; data.BakedVisualPrefab = null;
                actor.ConfigureResident(data, actor.transform.position, actor.transform.rotation, Singleton<ResidentManager>.Instance);
                Singleton<ResidentManager>.Instance?.RegisterRuntimeResident(actor);
            }
            if (identity.appearanceRevision != person.appearanceRevision) ApplyActorAppearance(person);
        }
        // A scene-wide PPG_Player search costs several ms in the merged world.
        // Listening sessions register every spawned player with the original
        // PlayersManager, so read that registry; otherwise search as before.
        static IEnumerable<PPG_Player> ScenePlayers()
        {
            var network = NetworkManager.Singleton;
            if (network != null && network.IsListening && Singleton<PaperPixelGames.Core.Managers.PlayersManager>.TryGetInstance(out var players))
                return players.Players.Values.Where(p => p);
            return FindObjectsByType<PPG_Player>(FindObjectsSortMode.None);
        }
        private void EnsureHumanBindings()
        {
            foreach (var player in ScenePlayers())
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !player.IsSpawned) continue;
                string account = player.SteamID.Value != 0 ? "steam:" + player.SteamID.Value : "client:" + player.OwnerClientId;
                var sessionPerson = humanActors.FirstOrDefault(pair => pair.Value == player);
                var person = string.IsNullOrEmpty(sessionPerson.Key) ? null : GetPerson(sessionPerson.Key);
                if (person == null) person = State.people.FirstOrDefault(p => p.controllerKind == PersonControllerKind.Human && p.platformAccountKey == account);
                if (person != null && humanActors.TryGetValue(person.personId, out var occupied) && occupied != null && occupied != player && occupied.IsSpawned) continue;
                if (person == null)
                {
                    string name = player.PlayerName.Value.ToString();
                    if (string.IsNullOrWhiteSpace(name)) name = player.name;
                    var native = CreateWorldResident(name);
                    if (native == null) continue;
                    person = new PersonBinding { worldId = State.worldId, personId = Guid.NewGuid().ToString("N"), nativeResidentId = native.id,
                        displayName = name, controllerKind = PersonControllerKind.Human, platformAccountKey = account };
                    State.people.Add(person);
                    NotifyChanged();
                }
                else if (person.platformAccountKey.StartsWith("client:", StringComparison.Ordinal) && player.SteamID.Value != 0 &&
                    !State.people.Any(p => p.personId != person.personId && p.platformAccountKey == account)) person.platformAccountKey = account;
                humanActors[person.personId] = player;
            }
        }
        public Transform TransformOf(string personId)
        {
            if (humanActors.TryGetValue(personId ?? "", out var human) && human != null) return human.transform;
            var economy = FriendsEconomyService.Instance;
            var resident = ActorOf(personId);
            // With a resident and no live job, the original result is always the
            // resident. Avoid the global proxy scan only in that exact case.
            bool activeJob = economy != null && economy.HasActiveJob(personId);
            if (resident && !activeJob) return resident.transform;
            var worker = FindObjectsByType<FriendWorkProxy>(FindObjectsSortMode.None).FirstOrDefault(w => w.personId == personId && w.isActiveAndEnabled);
            if (worker != null && activeJob) return worker.transform;
            if (resident) return resident.transform;
            return worker ? worker.transform : null;
        }
        public void RefreshActorBindings() { if (Ready) { DiscoverBoundActors(); if (IsAuthority) EnsureHumanBindings(); } }
        public PersonBinding LocalHuman()
        {
            // The original manager keeps the one owned player as LocalPlayer.
            var registered = Singleton<PaperPixelGames.Core.Managers.PlayersManager>.TryGetInstance(out var players) ? players.LocalPlayer : null;
            var player = registered && registered.IsOwner ? registered : FindObjectsByType<PPG_Player>(FindObjectsSortMode.None).FirstOrDefault(p => p.IsOwner);
            if (player == null) return null;
            string key = player.SteamID.Value != 0 ? "steam:" + player.SteamID.Value : "client:" + player.OwnerClientId;
            return State?.people.FirstOrDefault(p => p.controllerKind == PersonControllerKind.Human && p.platformAccountKey == key);
        }
        public PersonBinding HumanForClient(ulong clientId)
        {
            var bound = humanActors.FirstOrDefault(pair => pair.Value != null && pair.Value.IsSpawned && pair.Value.OwnerClientId == clientId);
            if (!string.IsNullOrEmpty(bound.Key)) return GetPerson(bound.Key);
            PPG_Player player = Singleton<PaperPixelGames.Core.Managers.PlayersManager>.TryGetInstance(out var players) && players.TryGetPlayer(clientId, out var registered) && registered && registered.IsSpawned ? registered : null;
            if (player == null) player = FindObjectsByType<PPG_Player>(FindObjectsSortMode.None).FirstOrDefault(p => p.IsSpawned && p.OwnerClientId == clientId);
            if (player == null) return null;
            string key = player.SteamID.Value != 0 ? "steam:" + player.SteamID.Value : "client:" + player.OwnerClientId;
            var person = State?.people.FirstOrDefault(p => p.controllerKind == PersonControllerKind.Human && p.platformAccountKey == key);
            return person != null && humanActors.TryGetValue(person.personId, out var owner) && owner == player ? person : null;
        }
        public void EnsureActorProjection(PersonBinding person)
        {
            if (!IsAuthority || person == null || person.controllerKind != PersonControllerKind.Npc || ActorOf(person.personId) != null) return;
            var manager = Singleton<ResidentManager>.Instance;
            if (manager == null) return;
            var template = Resources.FindObjectsOfTypeAll<HouseDataSO>().Where(h => h != null && h.Residents != null)
                .SelectMany(h => h.Residents).FirstOrDefault(e => e.ResidentPrefab != null && e.ResidentData != null);
            if (template.ResidentPrefab == null || template.ResidentData == null)
            { LastError = "没有加载可用于朋友的原居民Prefab。"; ActorProjectionFailed?.Invoke(person); return; }
            var used = new HashSet<int>(manager.Residents.Where(r => r != null).Select(r => (int)r.ResidentId));
            foreach (var binding in State.people) if (binding.hostResidentId > 0) used.Add(binding.hostResidentId);
            int id = person.hostResidentId;
            if (id == 0 || manager.Residents.Any(r => r != null && r.ResidentId == id))
            {
                id = Enumerable.Range(1, 254).FirstOrDefault(candidate => !used.Contains(candidate));
                if (id == 0) { LastError = "宿主居民ID已满。"; ActorProjectionFailed?.Invoke(person); return; }
            }
            var plot = FriendsEconomyService.Instance?.Plot(person.currentHomeId);
            var templateAgent=template.ResidentPrefab.GetComponent<NavMeshAgent>();
            if(templateAgent==null){LastError="原居民Prefab缺少导航代理类型。";ActorProjectionFailed?.Invoke(person);return;}
            Vector3 position;Quaternion rotation;
            if(!string.IsNullOrEmpty(person.currentHomeId))
            {
                if(!plot?.house)return;
                if(!HousingNavigation.TryResolveFriendHome(plot.house,templateAgent,out position,out rotation,true))
                {LastError="朋友住宅门前等待安全的可行走落点。";ActorProjectionFailed?.Invoke(person);return;}
            }
            else
            {
                position=template.ResidentPrefab.transform.position;rotation=Quaternion.identity;
                var spawnFilter=new NavMeshQueryFilter{agentTypeID=templateAgent.agentTypeID,areaMask=templateAgent.areaMask};
                if(NavMesh.SamplePosition(position,out var nav,3f,spawnFilter))position=nav.position;
                else{LastError="朋友住宅出口尚未连到可行走区域。";ActorProjectionFailed?.Invoke(person);return;}
            }
            var data = Instantiate(template.ResidentData);
            var schedule = FriendSchedule(person.personId);
            if (schedule) data.Schedule = schedule;
            data.name = "Friend " + person.personId;
            data.Name = person.displayName; data.NpcID = id;
            // CharacterData is the editable authority. A baked visual is never reused for another friend.
            data.BakedVisualPrefab = null;
            var actor = Instantiate(template.ResidentPrefab, position, rotation, manager.transform);
            actor.name = "Friend " + person.displayName;
            FriendResidentBindings.MarkOwned(actor, position, rotation);
            actor.ConfigureResident(data, position, rotation, manager);
            person.hostResidentId = id;
            actors[person.personId] = actor;
            AttachActorIdentity(actor, person);
            manager.RegisterRuntimeResident(actor);
            actor.Initialize();
            ApplyActorAppearance(person);
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer &&
                actor.TryGetComponent<NetworkObject>(out var networkObject) && !networkObject.IsSpawned) networkObject.Spawn(true);
            NotifyChanged();
        }
        static int StableIndex(string key, int count)
        {
            uint hash = 2166136261; foreach (char c in key ?? "") { hash ^= c; hash *= 16777619; }
            return (int)(hash % (uint)count);
        }
        // Every friend used to copy the FIRST template's schedule, so all
        // friends queued for the same bench at the same minute. Use one of the
        // original resident schedules, chosen stably per person. Shopkeeper
        // (Work) schedules stay with the original staff; jobs are a separate
        // M4 assignment. A pick already used by another friend moves to the
        // next schedule, so friends spread over different places.
        ResidentScheduleSO FriendSchedule(string personId)
        {
            var schedules = Resources.FindObjectsOfTypeAll<HouseDataSO>().Where(h => h != null && h.Residents != null)
                .SelectMany(h => h.Residents).Where(e => e.ResidentData != null && e.ResidentData.Schedule != null && e.ResidentData.Schedule.Entries != null)
                .Select(e => e.ResidentData.Schedule).Distinct()
                .Where(s => s.Entries.Length > 0 && s.Entries.All(e => e.Activity != ResidentActivityType.Work && e.Activity != ResidentActivityType.Restaurant))
                .OrderBy(s => s.name, StringComparer.Ordinal)
                // Two original residents can share identical entries under different names.
                .GroupBy(s => string.Join(";", s.Entries.Select(e => e.StartTime + ":" + e.StartMinute + "-" + e.EndTime + ":" + e.EndMinute + "/" + (int)e.Activity + "/" + (int)e.ActivityIndex)))
                .Select(g => g.First()).ToArray();
            if (schedules.Length == 0) return null;
            var taken = new HashSet<string>(actors.Where(pair => pair.Key != personId && pair.Value && pair.Value.ResidentData && pair.Value.ResidentData.Schedule)
                .Select(pair => pair.Value.ResidentData.Schedule.name));
            int start = StableIndex(personId, schedules.Length);
            var source = schedules[start];
            for (int i = 0; i < schedules.Length; i++)
            {
                var candidate = schedules[(start + i) % schedules.Length];
                if (!taken.Contains(candidate.name)) { source = candidate; break; }
            }
            return NightAtHome(source);
        }
        // Tomodachi: Miis come home in the evening and sleep at night. The copy
        // ends the outdoor day at 22:00 (or the original Home start if earlier)
        // and runs the original Home entry until its original morning end.
        const int FriendNightMinute = 22 * 60;
        static ResidentScheduleSO NightAtHome(ResidentScheduleSO source)
        {
            int homeIndex = Array.FindIndex(source.Entries, e => e.Activity == ResidentActivityType.Home);
            if (homeIndex < 0) return source;
            var home = source.Entries[homeIndex];
            int dayStart = DayMinute(Mathf.FloorToInt(home.EndTime), home.EndMinute);
            Func<int, int> rel = minute => ((minute - dayStart) % 1440 + 1440) % 1440;
            int dayLength = Math.Min(rel(DayMinute(home.StartTime, home.StartMinute)), rel(FriendNightMinute));
            if (dayLength == 0) return source;
            var entries = new List<ResidentScheduleSO.Entry>();
            foreach (var entry in source.Entries)
            {
                if (entry.Activity == ResidentActivityType.Home) continue;
                int from = rel(DayMinute(entry.StartTime, entry.StartMinute)), to = rel(DayMinute(Mathf.FloorToInt(entry.EndTime), entry.EndMinute));
                if (to <= from) to = 1440;
                to = Math.Min(to, dayLength);
                if (from < to) entries.Add(Span(entry, (dayStart + from) % 1440, (dayStart + to) % 1440));
            }
            entries.Add(Span(home, (dayStart + dayLength) % 1440, dayStart));
            var copy = ScriptableObject.CreateInstance<ResidentScheduleSO>();
            copy.name = source.name; copy.Entries = entries.ToArray();
            return copy;
        }
        static int DayMinute(int hour, int minute) => Mathf.Clamp(hour, 0, 24) * 60 + Mathf.Clamp(minute, 0, 59);
        static ResidentScheduleSO.Entry Span(ResidentScheduleSO.Entry entry, int from, int to)
        { entry.StartTime = from / 60; entry.StartMinute = from % 60; entry.EndTime = to / 60; entry.EndMinute = to % 60; return entry; }
        private void ApplyActorAppearance(PersonBinding person)
        {
            var actor = ActorOf(person.personId);
            if (actor == null || actor.ResidentData == null || string.IsNullOrEmpty(person.appearanceJson)) return;
            try
            {
                var creative = Core.State.creative?.residents.FirstOrDefault(p => p.residentId == person.nativeResidentId);
                var data = FriendsLifeVisualProjection.ProjectCharacter(this, person, creative);
                if (data == null || data.bodyIDs == null || data.faceIDs == null || data.outfitDatas == null)
                    throw new InvalidOperationException("Bozo CharacterData is incomplete.");
                var appearance = ScriptableObject.CreateInstance<FriendCharacterData>();
                appearance.data = data;
                actor.ResidentData.Name = person.displayName;
                actor.ResidentData.BakedVisualPrefab = null;
                FriendResidentBindings.ApplyAppearance(actor, appearance);
                var identity = actor.GetComponent<FriendWorldActor>();
                if (identity) identity.appearanceRevision = person.appearanceRevision;
            }
            catch (Exception error) { LastError = error.Message; ActorProjectionFailed?.Invoke(person); }
        }
        private void SynchronizeSourceChildren()
        {
            foreach (var native in Core.State.residents.Where(r => State.people.All(p => p.nativeResidentId != r.id)).ToArray())
            {
                var child = Core.State.family?.children.FirstOrDefault(c => c.residentId == native.id);
                if (child == null) continue;
                var parent = PersonFromNative(child.parentAId);
                var person = new PersonBinding { worldId = State.worldId, personId = Guid.NewGuid().ToString("N"), nativeResidentId = native.id,
                    displayName = native.displayName, controllerKind = PersonControllerKind.Npc, currentHomeId = parent?.currentHomeId ?? "",
                    appearanceJson = parent?.appearanceJson ?? "", appearanceRevision = child.appearanceRevision };
                State.people.Add(person);
                if (!string.IsNullOrEmpty(person.currentHomeId)) EnsureActorProjection(person);
                NotifyChanged();
            }
            foreach (var child in Core.State.family?.children.ToArray() ?? Array.Empty<LifeFamilyChild>())
            {
                var person = PersonFromNative(child.residentId);
                if (person == null || person.sourceFamilyAppearanceRevision == child.appearanceRevision && person.sourceFamilyStage == child.stage) continue;
                try
                {
                    if (familyAppearance == null) familyAppearance = new SourceFamilyAppearanceRuntime();
                    var document = familyAppearance.Build(Core, child, out var inherited, out var isNew);
                    var resident = Core.FindResident(child.residentId);
                    if (isNew)
                    {
                        var first = PersonFromNative(child.parentAId); var second = PersonFromNative(child.parentBId);
                        var firstData = string.IsNullOrEmpty(first?.appearanceJson) ? Resources.Load<CharacterObject>("Default")?.GetCharacterData() : JsonUtility.FromJson<CharacterData>(first.appearanceJson);
                        var secondData = string.IsNullOrEmpty(second?.appearanceJson) ? firstData : JsonUtility.FromJson<CharacterData>(second.appearanceJson);
                        if (firstData != null) person.appearanceJson = JsonUtility.ToJson(SourceFamilyAppearanceRuntime.ProjectParentalParts(firstData, secondData, inherited));
                    }
                    Core.UpdateResidentProfile(resident.id, resident.displayName, JsonUtility.ToJson(document));
                    person.sourceFamilyAppearanceRevision = child.appearanceRevision; person.sourceFamilyStage = child.stage; person.appearanceRevision++;
                    NotifyChanged();
                }
                catch (Exception error) { LastError = "原家庭外貌投影未完成：" + error.Message; }
            }
        }
        // A live in-place talk owns no body lease. New work/source actors
        // must wait for it without asking the ambient owner to exit.
        public bool HasActiveInPlaceTalk(string personId)
        {
            var canonical = GetPerson(personId)?.personId;
            return !string.IsNullOrEmpty(canonical) && State?.worldTalks != null && State.worldTalks.Any(r => r != null && !r.npcInitiated && r.personId == canonical && r.inPlace && r.phase == "Active");
        }
        public bool IsWorldAvailable(string personId)
        {
            if (HasActiveInPlaceTalk(personId)) return false;
            var person = GetPerson(personId);
            if (person == null || State.leases.Any(l => l.personId == person.personId)) return false;
            if (FriendsEconomyService.Instance != null && FriendsEconomyService.Instance.HasActiveJob(person.personId)) return false;
            string activity = Core.FindResident(person.nativeResidentId)?.activity;
            if (activity == "睡觉" || activity == "梦中") return false;
            if (person.controllerKind == PersonControllerKind.Human)
                return humanActors.TryGetValue(person.personId, out var human) && human != null && !human.IsUsingEmoteArea;
            var actor = ActorOf(personId);
            return FriendOriginalNpcActivityBoundary.CanAcquire(actor, out _) && !Core.IsResidentInActivity(person.nativeResidentId);
        }
        private bool TryAcquirePairLease(SourceEpisode episode)
        {
            string[] ids = new[] { episode.focusPersonId, episode.targetPersonId, episode.anotherPersonId }.Where(id => !string.IsNullOrEmpty(id)).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (ids.Any(id => HasActiveInPlaceTalk(id) && !State.leases.Any(l => l.personId == id && l.ownerEpisode == episode.episodeId && l.phase != "JobHandover"))) return false;
            if (ids.Any(id => State.leases.Any(l => l.personId == id && l.ownerEpisode != episode.episodeId))) return false;
            // Waiting work handles are upgraded by the shared authority only
            // after the original job has actually exited. They are not bodies.
            foreach (string id in ids)
                if (!State.leases.Any(l => l.personId == id && l.ownerEpisode == episode.episodeId) && !IsWorldAvailable(id))
                {
                    if(GetPerson(id)?.controllerKind==PersonControllerKind.Npc)
                        PaperPixelGames.Core.Ai.NPCs.OriginalNpcActivityPort.RequestSafeExit(ActorOf(id),episode.episodeId);
                    return false;
                }
            if (!SourceGraphs.NativeSourceContextFacade.CanAcquireEpisode(this, episode, out var sourceContextFailure))
            { episode.fault = sourceContextFailure; return false; }
            var acquired = new System.Collections.Generic.List<string>();
            foreach (string id in ids)
            {
                bool owned = FriendsBodyAuthority.IsOwned(this,id,episode.episodeId);
                if (!FriendsBodyAuthority.TryAcquire(this,id,episode.episodeId,"SourceEpisode",out _,out var reason))
                {
                    foreach(string rollback in acquired)FriendsBodyAuthority.Release(this,rollback,episode.episodeId,true);
                    episode.fault=reason;return false;
                }
                if(!owned)acquired.Add(id);
            }
            SourceGraphs.NativeSourceContextFacade.AcquireEpisode(this, episode);
            return true;
        }
        // A loaded Human can still be at the original saved spawn while the
        // additive home guide is waiting for the original bootstrap. Never
        // reserve that transient position as a durable conversation stage.
        private bool HumanParticipantsReadyForStage(SourceEpisode episode)
        {
            foreach (string id in new[] { episode.focusPersonId, episode.targetPersonId, episode.anotherPersonId }.Where(id => !string.IsNullOrEmpty(id)).Distinct())
            {
                if (GetPerson(id)?.controllerKind != PersonControllerKind.Human) continue;
                if (!humanActors.TryGetValue(id, out var human) || !human || !human.IsSpawned) return false;
                // Local preparation is intentionally not a remote-player gate.
                if (!human.IsOwner) continue;
                if (!human.IsLocalGameplayPreparationReady) return false;
                var guide = FriendPlayerHomeGuide.Instance;
                if (guide && guide.Phase != "OwnHomeReady" && guide.Phase != "OriginalWorldOrNoGuideEnrollment") return false;
            }
            return true;
        }
        private bool HumanStageNeedsRefresh(SourceEpisode episode)
        {
            foreach (string id in new[] { episode.focusPersonId, episode.targetPersonId, episode.anotherPersonId }.Where(id => !string.IsNullOrEmpty(id)).Distinct())
            {
                var lease = State.leases.FirstOrDefault(l => l.ownerEpisode == episode.episodeId && l.personId == id);
                // Inspect before acquiring: acquisition can recreate a missing
                // NPC lease, but its default destination is not a real stage.
                if (lease == null) return true;
                if (GetPerson(id)?.controllerKind == PersonControllerKind.Human)
                {
                    var body = TransformOf(id);
                    if (!body || Vector3.Distance(body.position, lease.destination) > 2f) return true;
                }
            }
            return false;
        }
        private bool TryPlanPublicHumanStage(SourceEpisode episode, out Dictionary<string, Vector3> plan, string[] explicitParticipants = null)
        {
            plan = new Dictionary<string, Vector3>();
            var ids = explicitParticipants ?? State.leases.Where(l => l.ownerEpisode == episode.episodeId).Select(l => l.personId).OrderBy(id => id).ToArray();
            var humans = ids.Where(id => GetPerson(id)?.controllerKind == PersonControllerKind.Human).Select(TransformOf).ToArray();
            if (humans.Length == 0 || humans.Any(t => !t)) return false;
            if (!housingPrivacy && FriendHousingIsland.Instance) housingPrivacy = FriendHousingIsland.Instance.GetComponent<FriendHousingPrivacy>();
            var candidates = new Dictionary<string, List<Vector3>>();
            foreach (string id in ids)
            {
                if (GetPerson(id)?.controllerKind == PersonControllerKind.Human)
                { candidates[id] = new List<Vector3> { TransformOf(id).position }; continue; }
                var actor = ActorOf(id); var agent = actor ? actor.GetComponent<NavMeshAgent>() : null;
                if (actor?.Brain == null || !agent || !FriendsPrivateCommonTransit.TryPlanningOrigin(this, id, episode.episodeId, out var planningOrigin, out bool viaPrivatePortal, out _)) return false;
                if (!viaPrivatePortal && (!agent.enabled || !agent.isOnNavMesh || Vector3.Distance(actor.transform.position, agent.nextPosition) > .25f)) return false;
                var points = new List<Vector3>();
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                Vector3 forward = Vector3.ProjectOnPlane(humans[0].forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
                foreach (float radius in new[] { 1.2f, 2f, 2.7f }) foreach (float angle in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f })
                {
                    Vector3 requested = humans[0].position + Quaternion.AngleAxis(angle, Vector3.up) * forward * radius;
                    if (!NavMesh.SamplePosition(requested, out var hit, .25f, filter) || humans.Any(h => Vector3.Distance(h.position, hit.position) > 3f || Vector3.Distance(h.position, hit.position) < .6f)) continue;
                    // Planning is read only: PrepareDoorRoute belongs to the
                    // eventual movement request, never to these candidates.
                    if (!actor.Brain.Movement.TryResolveDestination(hit.position, .05f, out var resolved)) continue;
                    if (!FriendsPrivateCommonTransit.PlannedPath(actor, planningOrigin, resolved, out var path)) continue;
                    if (!viaPrivatePortal && housingPrivacy && housingPrivacy.ExcludesPathFor(actor, path)) continue;
                    if (humans.Any(h => Vector3.Distance(h.position, resolved) > 3f)) continue;
                    if (!points.Any(p => Vector3.Distance(p, resolved) < .1f)) points.Add(resolved);
                }
                if (points.Count == 0) return false;
                candidates[id] = points;
            }
            // Choose the complete group before moving anybody. Pairwise body
            // distance remains the same 3m arrival contract used at commit.
            return ChooseHumanStage(ids, candidates, plan, 0);
        }
        // Read-only planning uses the same authored human social stage. The
        // invitation owns ONLY the NPC; the Human has not accepted any lease.
        public bool TryPlanNpcSocialApproach(string npcId,string humanId,string ownerId,out Vector3 destination,out string reason)
        {
            destination=default;reason=null;
            if(GetPerson(npcId)?.controllerKind!=PersonControllerKind.Npc||GetPerson(humanId)?.controllerKind!=PersonControllerKind.Human||
                !FriendsBodyAuthority.IsOwned(this,npcId,ownerId))
            {reason="An actual NPC-only invitation body claim is required";return false;}
            var request=new SourceEpisode{episodeId=ownerId,focusPersonId=npcId,targetPersonId=humanId};
            if(!HumanParticipantsReadyForStage(request)||!TryPlanPublicHumanStage(request,out var plan,new[]{npcId,humanId})||!plan.TryGetValue(npcId,out destination))
            {reason="The existing public Human social stage has no complete actual approach";return false;}
            return true;
        }
        static bool ChooseHumanStage(string[] ids, Dictionary<string, List<Vector3>> candidates, Dictionary<string, Vector3> plan, int index)
        {
            if (index == ids.Length) return true;
            string id = ids[index];
            foreach (var point in candidates[id])
            {
                if (plan.Values.Any(other => Vector3.Distance(other, point) > 3f || Vector3.Distance(other, point) < .6f)) continue;
                plan[id] = point;
                if (ChooseHumanStage(ids, candidates, plan, index + 1)) return true;
                plan.Remove(id);
            }
            return false;
        }
        private void MoveEpisodeParticipantsTogether(SourceEpisode episode)
        {
            episode.worldArrivalVerified = false;
            var focus = TransformOf(episode.focusPersonId);
            var target = TransformOf(episode.targetPersonId);
            if (!focus || !target) { episode.status = "Suspended"; ReleaseEpisodeLeases(episode.episodeId); return; }
            var human = new[] { episode.focusPersonId, episode.targetPersonId, episode.anotherPersonId }
                .Where(id => GetPerson(id)?.controllerKind == PersonControllerKind.Human).Select(TransformOf).FirstOrDefault(t => t != null);
            var actualParticipants = new[] { episode.focusPersonId, episode.targetPersonId, episode.anotherPersonId }.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray();
            bool privateOrigin = actualParticipants.Any(id => FriendsPrivateCommonTransit.IsInOwnPrivateRoom(this, id));
            // A physical portal is not the original Tree/NoDir Demo stage.
            // Keep that missing semantic binding explicit instead of choosing
            // a borrowed R common room for an O native event.
            if (privateOrigin && episode.sourceKind == "MiiEvent")
            { episode.status = "BindingFault"; episode.fault = "Original Demo semantic public stage is unbound; private/common transit cannot stand in for Tree/NoDir placement."; ReleaseEpisodeLeases(episode.episodeId); return; }
            if (human)
            {
                if (!TryPlanPublicHumanStage(episode, out var plan))
                { episode.status = "Suspended"; episode.fault = "No permitted public conversation stage within the actual participants' range."; ReleaseEpisodeLeases(episode.episodeId); return; }
                foreach (var lease in State.leases.Where(l => l.ownerEpisode == episode.episodeId).OrderBy(l => l.personId))
                {
                    lease.destination = plan[lease.personId]; lease.phase = "Travel";
                    if (!ActorOf(lease.personId)) continue; // Human bodies stay under original player input.
                    if (!TryMoveWorldActor(lease.personId, lease.destination, out var resolved, .05f))
                    { episode.status = "Suspended"; episode.fault = "Original host navigation or home permission rejected the planned public stage."; ReleaseEpisodeLeases(episode.episodeId); return; }
                    lease.destination = resolved;
                }
                episode.fault = ""; return;
            }
            Vector3 center;
            if (privateOrigin && episode.familyChoice?.action == "Date")
            {
                var originalManager = Singleton<ResidentManager>.Instance;
                if (!originalManager || !originalManager.TryGetNextPatrolPoint(ResidentActivityType.ParkPatrol, ActorOf(episode.focusPersonId), -1, out _, out var originalPark) || !originalPark)
                { episode.status = "Suspended"; episode.fault = "The original borrowed R ParkPatrol point is unavailable."; ReleaseEpisodeLeases(episode.episodeId); return; }
                center = originalPark.position;
            }
            else if (privateOrigin)
            {
                var economy = FriendsEconomyService.Instance;
                var common = actualParticipants.Select(id => economy?.Plot(GetPerson(id)?.currentHomeId)?.house)
                    .FirstOrDefault(home => home && home.entry && home.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone == true && actualParticipants.All(id => economy.CanEnter(home,id)));
                if (!common)
                { episode.status = "Suspended"; episode.fault = "No actual authorized common entry is available for the borrowed R family stage."; ReleaseEpisodeLeases(episode.episodeId); return; }
                var outward = -Vector3.ProjectOnPlane(common.entry.forward, Vector3.up); if (outward.sqrMagnitude < .0001f) outward = common.transform.forward; outward.Normalize();
                center = common.entry.position + outward * 1.5f;
            }
            else center = human ? human.position : (focus.position + target.position) * .5f;
            if(!human&&!privateOrigin&&episode.familyChoice?.action=="Date")
            {
                var manager=Singleton<ResidentManager>.Instance;
                if(manager&&manager.TryGetNextPatrolPoint(ResidentActivityType.ParkPatrol,ActorOf(episode.focusPersonId),-1,out _,out var park)&&park)center=park.position;
            }
            var stageAgent=ActorOf(episode.focusPersonId)?.GetComponent<NavMeshAgent>()??ActorOf(episode.targetPersonId)?.GetComponent<NavMeshAgent>();
            if(stageAgent==null){episode.status="Suspended";episode.fault="Participants have no original navigation type.";ReleaseEpisodeLeases(episode.episodeId);return;}
            var stageFilter=new NavMeshQueryFilter{agentTypeID=stageAgent.agentTypeID,areaMask=stageAgent.areaMask};
            if (!NavMesh.SamplePosition(center, out var middle, 4, stageFilter))
            { episode.status = "Suspended"; episode.fault = "Participants have no reachable shared stage."; ReleaseEpisodeLeases(episode.episodeId); return; }
            int index = 0;
            foreach (var lease in State.leases.Where(l => l.ownerEpisode == episode.episodeId).OrderBy(l => l.personId))
            {
                var actor = ActorOf(lease.personId);
                lease.destination = middle.position + new Vector3(index++ == 0 ? -.65f : .65f, 0, 0);
                lease.phase = "Travel";
                var agent = actor ? actor.GetComponent<NavMeshAgent>() : null;
                if (actor != null && (agent == null || !agent.enabled || !agent.isOnNavMesh))
                { episode.status = "Suspended"; episode.fault = "Participant has no continuous navigation surface at its real position."; ReleaseEpisodeLeases(episode.episodeId); return; }
                if (actor != null)
                {
                    if (!TryMoveWorldActor(lease.personId, lease.destination, out var resolved))
                    { episode.status = "Suspended"; episode.fault = "Original host navigation or home permission rejected this stage."; ReleaseEpisodeLeases(episode.episodeId); return; }
                    // Store the resolved intended endpoint, never a possibly
                    // stale Agent.destination immediately after SetPath.
                    lease.destination = resolved;
                }
            }
            // Historical failure receipts remain external; clear this live
            // reason only after every original movement call was accepted.
            episode.fault = "";
        }
        public string LastWorldMoveDiagnostic { get; private set; }
        void ObserveWorldMove(string personId, Vector3 destination, Vector3 resolved, string stage)
        { LastWorldMoveDiagnostic = "frame=" + Time.frameCount + "; person=" + personId + "; stage=" + stage + "; requested=" + destination.ToString("F3") + "; resolved=" + resolved.ToString("F3"); }
        public bool TryMoveWorldActor(string personId, Vector3 destination, out Vector3 resolved, float sampleRadius = 2f)
        {
            resolved = destination;
            if (FriendsPrivateCommonTransit.TryHandle(this, personId, destination, sampleRadius, out bool transitAccepted, out var transitTarget, out string transitFailure))
            { resolved = transitTarget; ObserveWorldMove(personId, destination, resolved, transitAccepted ? "PrivateCommonTransitAccepted" : "PrivateCommonTransitRejected/" + transitFailure); return transitAccepted; }
            var actor = ActorOf(personId); var agent = actor ? actor.GetComponent<NavMeshAgent>() : null;
            if (actor?.Brain == null || agent == null || !agent.enabled || !agent.isOnNavMesh) { ObserveWorldMove(personId,destination,resolved,"ActorOrNavigationNotReady"); return false; }
            HousingNavigation.PrepareDoorRoute(actor, destination);
            // StopAtPose can deliberately separate an actor from an old agent
            // location. Such a path is not evidence of a walk by this body.
            if (Vector3.Distance(actor.transform.position, agent.nextPosition) > .25f) { ObserveWorldMove(personId,destination,resolved,"BodyAgentDiscontinuity"); return false; }
            if (!actor.Brain.Movement.TryResolveDestination(destination, sampleRadius, out resolved)) { ObserveWorldMove(personId,destination,resolved,"OriginalDestinationRejected"); return false; }
            var path = new NavMeshPath();
            if (!agent.CalculatePath(resolved, path) || path.status != NavMeshPathStatus.PathComplete) { ObserveWorldMove(personId,destination,resolved,"PathNotComplete"); return false; }
            if (path.corners.Length > 0 && Vector3.Distance(path.corners[0], actor.transform.position) > .25f) { ObserveWorldMove(personId,destination,resolved,"PathStartDiscontinuity"); return false; }
            if(!housingPrivacy&&FriendHousingIsland.Instance)housingPrivacy=FriendHousingIsland.Instance.GetComponent<FriendHousingPrivacy>();
            if (housingPrivacy != null && housingPrivacy.ExcludesPathFor(actor, path)) { ObserveWorldMove(personId,destination,resolved,"PrivateHomePathRejected"); return false; }
            bool accepted=actor.Brain.Movement.MoveTo(resolved, actor.Brain.EffectiveMoveSpeed, .05f, true);
            if (accepted && Ready && IsAuthority && IsWorldRunning && FriendResidentBindings.IsOwned(actor) &&
                GetPerson(personId)?.controllerKind == PersonControllerKind.Npc &&
                !Core.IsResidentAway(GetPerson(personId).nativeResidentId) &&
                FriendsEconomyService.Instance?.HasActiveJob(personId) != true && !actor.GetComponent<FriendResidentDormancy>() &&
                !State.episodes.Any(e => !e.committed && e.sourceKind == "MiiEvent" && e.status == "SourceRunning" &&
                    new[] { e.focusPersonId, e.targetPersonId, e.anotherPersonId }.Contains(personId)))
                actor.Brain.SetModelVisible(true);
            ObserveWorldMove(personId,destination,resolved,accepted?"OriginalMoveAccepted":"OriginalMoveToRejected"); return accepted;
        }
        private bool EpisodeParticipantsArrived(SourceEpisode episode)
        {
            var leases = State.leases.Where(l => l.ownerEpisode == episode.episodeId).ToArray();
            var participants = new[] { episode.focusPersonId, episode.targetPersonId, episode.anotherPersonId }.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray();
            if (leases.Length != participants.Length || leases.Any(l => !participants.Contains(l.personId))) return false;
            foreach (var lease in leases)
            {
                var actor = ActorOf(lease.personId);
                if (actor == null)
                {
                    var human = TransformOf(lease.personId);
                    if (GetPerson(lease.personId)?.controllerKind != PersonControllerKind.Human || human == null || Vector3.Distance(human.position, lease.destination) > 2f) return false;
                    continue;
                }
                var agent = actor.GetComponent<NavMeshAgent>();
                float tolerance = Mathf.Max(.15f, (agent ? agent.stoppingDistance : 0) + .05f);
                if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.pathPending ||
                    lease.phase == "Travel" && agent.pathStatus != NavMeshPathStatus.PathComplete ||
                    Vector3.Distance(actor.Brain.Movement.Destination, lease.destination) > tolerance ||
                    Vector3.Distance(actor.transform.position, lease.destination) > tolerance || !actor.Brain.Movement.HasArrived()) return false;
            }
            var bodies = participants.Select(TransformOf).ToArray();
            if (bodies.Any(t => t == null) || bodies.Any(a => bodies.Any(b => Vector3.Distance(a.position, b.position) > 3f))) return false;
            episode.worldArrivalVerified = true;
            episode.focusCommitPosition = TransformOf(episode.focusPersonId).position;
            episode.targetCommitPosition = TransformOf(episode.targetPersonId).position;
            episode.participantDistanceAtCommit = Vector3.Distance(episode.focusCommitPosition, episode.targetCommitPosition);
            foreach (var lease in leases)
            {
                lease.phase = "AtStage";
                var actor = ActorOf(lease.personId);
                if (actor == null) continue;
                var other = leases.Select(l => TransformOf(l.personId)).FirstOrDefault(t => t != null && t != actor.transform);
                if (other != null)
                {
                    Vector3 direction = other.position - actor.transform.position; direction.y = 0;
                    if (direction.sqrMagnitude > .001f) actor.Brain.Movement.StopAtRotation(Quaternion.LookRotation(direction));
                }
            }
            return true;
        }
        public void ReleaseEpisodeLeases(string episodeId)
        {
            StopDatePresentation(episodeId);
            foreach (var lease in State.leases.Where(l => l.ownerEpisode == episodeId).ToArray())
            {
                var actor = ActorOf(lease.personId);
                if (actor != null && actor.Brain != null) { actor.Brain.Resume(); actor.Brain.RequestScheduleChange(); }
                State.leases.Remove(lease);
                FriendOwnedAmbientPathGuardian.ProtectAfterScheduleChange(actor);
            }
            if (Ready && IsAuthority) SourceGraphs.NativeSourceContextFacade.SynchronizeCompleted(this);
        }
        private void AdvanceWorldLeases()
        {
            SyncHomeBindings(false);
            foreach (var lease in State.leases.Where(l => l.untilGameMinute > 0 && Core.State.gameMinutes >= l.untilGameMinute).ToArray()) ReleaseEpisodeLeases(lease.ownerEpisode);
            foreach (var lease in State.leases.ToArray())
            {
                var actor = ActorOf(lease.personId);
                if (actor == null || actor.Brain == null) continue;
                bool sourceLease = State.episodes.Any(e => e.episodeId == lease.ownerEpisode && (e.sourceKind == "MiiEvent" || e.sourceKind == "Family"));
                if (sourceLease && !actor.Runtime.IsSnapshotApplying) actor.Brain.Pause();
                if (lease.phase == "Travel" && Vector3.Distance(actor.transform.position, lease.destination) > .2f &&
                    (actor.Brain.Movement.RemainingDistance <= 0 || Vector3.Distance(actor.Brain.Movement.Destination, lease.destination) > .2f))
                {
                    var agent = actor.GetComponent<NavMeshAgent>();
                    if (agent == null || !agent.enabled || !agent.isOnNavMesh) continue;
                    TryMoveWorldActor(lease.personId, lease.destination, out _, .05f);
                }
            }
            if (Ready && IsAuthority) SourceGraphs.NativeSourceContextFacade.SynchronizeCompleted(this);
        }
        public bool SyncHomeBindings(bool persist = false)
        {
            if (!Ready || !IsAuthority) return false;
            bool changed = false;
            foreach (var person in State.people)
            {
                var actor=ActorOf(person.personId);
                var home=FriendsEconomyService.Instance?.Plot(person.currentHomeId)?.house;
                var agent=actor?actor.GetComponent<NavMeshAgent>():null;
                if(actor&&HousingNavigation.TryResolveFriendHome(home,agent,out var homePosition,out var homeRotation))
                    FriendResidentBindings.SetHome(actor,homePosition,homeRotation);
                var native = Core.FindResident(person.nativeResidentId);
                if (native == null || native.homeId == person.currentHomeId) continue;
                if (string.IsNullOrEmpty(person.currentHomeId)) { Core.UnassignHouse(person.nativeResidentId); changed = true; continue; }
                if (Core.State.households.All(h => h.id != person.currentHomeId)) Core.CreateHousehold(person.currentHomeId, configuration.maximumHouseCapacity);
                if (!Core.AssignHouse(person.nativeResidentId, person.currentHomeId)) { LastError = Core.LastError; return false; }
                changed = true;
            }
            if (changed) { dirty = true; if (persist) { NotifyChanged(); SaveNow(); } }
            return true;
        }
        private FriendPlaceBinding PlaceOf(string nativeId)
        {
            var person = PersonFromNative(nativeId);
            var transform = TransformOf(person?.personId);
            return transform == null ? null : FriendInstances<FriendPlaceBinding>.Active().FirstOrDefault(p => p.Contains(transform.position));
        }
        public NativeMiiEventContext BuildNativeContext(LifeSimulationRuntime kernel, string focus, string target, string another)
        {
            Func<string, NativeMiiEventActorSnapshot> snapshot = id =>
            {
                var native = kernel.FindResident(id); var person = PersonFromNative(id);
                if (native == null || person == null) return null;
                var actor = ActorOf(person.personId); var body = TransformOf(person.personId); var place = PlaceOf(id);
                var lease = State.leases.FirstOrDefault(l => l.personId == person.personId);
                int group = lease == null ? -1 : State.episodes.FindIndex(e => e.episodeId == lease.ownerEpisode) + 1;
                var privateRoom = SourceGraphs.NativeSourcePrivateRoomContext.Observe(this, id);
                var islandKind = privateRoom == null && place == null ? SourceGraphs.NativeSourceIslandMapContext.ObserveOnIsland(this, id) : (bool?)null;
                Vector3? source = privateRoom == null && body != null && place != null ? (Vector3?)place.SourcePosition(body.position) : null;
                return new NativeMiiEventActorSnapshot { id = id, homeId = person.currentHomeId, feelingHash = native.feelingHash,
                    cooldownReady = SourceGraphs.NativeSourceContextFacade.CooldownReady(this, id),
                    sleeping = native.activity == "睡觉", walking = actor?.Brain?.Movement.RemainingDistance > 0,
                    hasFight = kernel.State.relationships.Any(r => r.fromId == id && r.fighting), hasOneSideLove = kernel.State.relationships.Any(r => r.fromId == id && r.oneSideLove),
                    hasTrouble = native.activeTroubleHash != 0, isGroup = group > 0, groupId = group,
                    groupSize = lease == null ? 1 : State.leases.Count(l => l.ownerEpisode == lease.ownerEpisode),
                    housemateCount = string.IsNullOrEmpty(person.currentHomeId) || FriendsEconomyService.Instance?.Plot(person.currentHomeId)?.house == null ? 0 : State.people.Count(p => p.currentHomeId == person.currentHomeId),
                    hasOtherInSamePlace = place == null ? (bool?)null : State.people.Any(p => p.personId != person.personId && TransformOf(p.personId) != null && place.Contains(TransformOf(p.personId).position)),
                    worldX = source?.x, worldZ = source?.z, onIsland = privateRoom != null ? (bool?)false : place == null ? islandKind : !place.inside,
                    nativeGoodsIds = person.inventory.Any(i => i.category == "Goods" && i.quantity > 0) ? null : Array.Empty<uint>() };
            };
            return new NativeMiiEventContext { Focus = snapshot(focus), Targets = string.IsNullOrEmpty(target) ? Array.Empty<NativeMiiEventActorSnapshot>() : new[] { snapshot(target) },
                Anothers = string.IsNullOrEmpty(another) ? Array.Empty<NativeMiiEventActorSnapshot>() : new[] { snapshot(another) }, population = ActiveWorldPopulation(kernel),
                timePeriod = NativeTimePeriodProvider?.Invoke(kernel.State.gameMinutes) ?? OriginalTimePeriod(kernel.State.gameMinutes), firstHistoryAvailable = true,
                firstHistoryAllows = eventId => !State.nativeHistory.Any(h => h.eventId == eventId && h.focusNativeId == focus),
                statistics = _ => null, resolve = query => ResolveNativePredicate(kernel, query), relation = (a, b) => NativeRelation(kernel, a, b),
                relationBoostRate = NativeRelationBoostProvider, reselectBranchParticipants = NativeParticipantReselector };
        }
        // Source Parameter/ClockSystem: ordered EarlyMorning 05:00-08:00,
        // Morning 08:00-11:30, Afternoon 11:30-17:00, Evening 17:00-19:00,
        // Night 19:00-00:00, Midnight 00:00-05:00.
        public static int OriginalTimePeriod(double minutes)
        {
            double within = (minutes % 1440 + 1440) % 1440;
            if (within < 300) return 5;
            if (within < 480) return 0;
            if (within < 690) return 1;
            if (within < 1020) return 2;
            if (within < 1140) return 3;
            return 4;
        }
        private NativeMiiEventRelationSnapshot NativeRelation(LifeSimulationRuntime kernel, string from, string to)
        {
            var forward = kernel.State.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            var reverse = kernel.State.relationships.FirstOrDefault(r => r.fromId == to && r.toId == from);
            int? type = NativeRelationType(kernel, from, to, forward?.kind);
            return new NativeMiiEventRelationSnapshot { nativeType = type, meter = forward?.meter, reverseMeter = reverse?.meter,
                oneSideLove = forward?.oneSideLove ?? false, reverseOneSideLove = reverse?.oneSideLove ?? false, fighting = forward?.fighting ?? false };
        }
        private int? NativeRelationType(LifeSimulationRuntime kernel, string from, string to, string kind)
        {
            switch (kind)
            {
                case null: case "Other": return 0;
                case "Known": case "Know": return 1;
                case "Friend": case "BestFriend": return 2;
                case "ExFriend": return 3;
                case "Lover": return 4;
                case "ExLover": return 5;
                case "Spouse": case "Couple": return 6;
                case "ExSpouse": case "Divorce": return 7;
                case "Parent": return 8; case "Child": return 9;
                case "OlderSibling": return 10; case "YoungerSibling": return 11;
                case "GrandParent": return 12; case "GrandChild": return 13; case "Relative": return 14;
                case "Family":
                    var a = kernel.State.family?.profiles.FirstOrDefault(p => p.residentId == from);
                    var b = kernel.State.family?.profiles.FirstOrDefault(p => p.residentId == to);
                    if (a == null || b == null) return null;
                    if (b.parentAId == from || b.parentBId == from) return 8;
                    if (a.parentAId == to || a.parentBId == to) return 9;
                    bool sharedParent = new[] { a.parentAId, a.parentBId }.Any(p => !string.IsNullOrEmpty(p) && (p == b.parentAId || p == b.parentBId));
                    if (sharedParent && a.ageYears != b.ageYears) return a.ageYears > b.ageYears ? 10 : 11;
                    if (kernel.State.family.profiles.Any(p => (p.residentId == b.parentAId || p.residentId == b.parentBId) && (p.parentAId == from || p.parentBId == from))) return 12;
                    if (kernel.State.family.profiles.Any(p => (p.residentId == a.parentAId || p.residentId == a.parentBId) && (p.parentAId == to || p.parentBId == to))) return 13;
                    return sharedParent ? (int?)null : 14;
                default: return null;
            }
        }
        private bool? ResolveNativePredicate(LifeSimulationRuntime kernel, NativeMiiEventQuery query)
        {
            var privateResult = SourceGraphs.NativeSourcePrivateRoomContext.Resolve(this, kernel, query);
            if (privateResult.HasValue) return privateResult;
            var a = PersonFromNative(query.actorId); var b = PersonFromNative(query.otherId);
            switch (query.kind)
            {
                case "SameHouse": case "Roommate":
                    if (a != null && b != null) return !string.IsNullOrEmpty(a.currentHomeId) && a.currentHomeId == b.currentHomeId; break;
                case "SamePlace":
                    var placeA = PlaceOf(query.actorId); var placeB = PlaceOf(query.otherId);
                    if (placeA != null && placeB != null) return placeA.placeId == placeB.placeId; break;
            }
            // The original sourceFunction remains attached to unresolved inputs.
            return NativePredicateProvider?.Invoke(query);
        }
    }
}
