using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using TomodachiLife.Simulation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SushiAndFish.FriendsLife
{
    /// <summary>One host world, one frozen kernel. No presentation getter advances the simulation.</summary>
    [DefaultExecutionOrder(-120)]
    public sealed partial class FriendsLifeRuntime : MonoBehaviour
    {
        public static FriendsLifeRuntime Instance { get; private set; }
        public LifeSimulationRuntime Core { get; private set; }
        public LifeSimulationRuntime Kernel => Core;
        public FriendsLifeSave State { get; private set; }
        public IReadOnlyList<PersonBinding> People => State?.people;
        public bool IsAuthority => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
        public double HostGameMinute => State?.hostGameMinute ?? 0;
        public long ActiveMicros => State?.activeMicros ?? 0;
        public string LastError { get; private set; }
        public string SavePath { get; private set; }
        public bool Ready => Core != null && State != null;
        public Func<string, long> ReadWalletCents;
        public Func<string, long, string, bool> CommitWalletDelta;
        public Func<IReadOnlyList<ActorWalletDelta>, string, bool> CommitWalletBatch;
        public Func<string> CaptureEconomyState;
        public Action CaptureLiveEconomyState;
        public Action<string> RestoreEconomyState;
        public event Action Changed;
        public event Action<PersonBinding> PersonAdded;
        public event Action<double, long> WorldTick;
        public event Action<SourceEpisode> SourceEpisodeRequested;
        public event Action<SourceEpisode> PendingInputRequested;
        public event Action<LifeSimulationRuntime> CoreChanged;
        public Func<string, string, string> SourceCommandOverride;
        private readonly Dictionary<string, ResidentController> actors = new Dictionary<string, ResidentController>();
        private NativeLifeRulesDatabase rules;
        private LifeSimulationConfiguration configuration;
        private CozyWeatherManager weather;
        private PaperPixelGames.Networking.GameStartGate startGate;
        private float nextStartGateLookup;
        public bool IsWorldRunning
        {
            get
            {
                if (!Ready || !IsAuthority || Time.timeScale <= 0 || !Singleton<ResidentManager>.TryGetInstance(out _)) return false;
                var network = NetworkManager.Singleton;
                if (network == null || !network.IsListening) return true;
                if (startGate == null && Time.unscaledTime >= nextStartGateLookup)
                { nextStartGateLookup=Time.unscaledTime+1f; startGate = FindFirstObjectByType<PaperPixelGames.Networking.GameStartGate>(); }
                return startGate != null && startGate.IsGameStarted && startGate.BootstrapCompletedClientCount > 0;
            }
        }
        private double previousHostMinute = double.NaN;
        private float nextDiscovery, nextSave, nextWorldStep, nextInitialization;
        private bool committing, dirty, alignNewWorldClock;
        public bool StandaloneValidationStorage;
        private int loadedSlot = -1;
        private string loadedHost = "";
        private SaveSlotData loadedSlotData;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureRuntime();
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureRuntime();
        private static void EnsureRuntime()
        {
            if (Instance != null || !Singleton<ResidentManager>.TryGetInstance(out _)) return;
            new GameObject("Friends Life Runtime").AddComponent<FriendsLifeRuntime>();
        }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (GetComponent<FriendsLifeNetwork>() == null) gameObject.AddComponent<FriendsLifeNetwork>();
            if (GetComponent<FriendsLifeVisualProjection>() == null) gameObject.AddComponent<FriendsLifeVisualProjection>();
            if (GetComponent<FriendsNpcRestController>() == null) gameObject.AddComponent<FriendsNpcRestController>();
            if (GetComponent<FriendTownMealBillingObserver>() == null) gameObject.AddComponent<FriendTownMealBillingObserver>();
            TryInitialize();
        }
        private void OnDestroy()
        {
            if (Instance != this) return;
            StopFamilyMotions();
            if (Application.isPlaying && Ready && IsAuthority) SaveNow();
            Instance = null;
        }
        private void OnApplicationPause(bool paused)
        {
            previousHostMinute = double.NaN;
            if (paused && Ready && IsAuthority) SaveNow();
        }
        private void OnApplicationQuit() { if (Ready && IsAuthority) SaveNow(); }
        static readonly Unity.Profiling.ProfilerMarker DiscoveryMarker = new Unity.Profiling.ProfilerMarker("FriendsLife.Discovery");
        static readonly Unity.Profiling.ProfilerMarker TickMarker = new Unity.Profiling.ProfilerMarker("FriendsLife.WorldTick");
        static readonly Unity.Profiling.ProfilerMarker FamilyMarker = new Unity.Profiling.ProfilerMarker("FriendsLife.FamilyPresentation");
        static readonly Unity.Profiling.ProfilerMarker AdvanceMarker = new Unity.Profiling.ProfilerMarker("FriendsLife.AdvanceWorld");
        static readonly Unity.Profiling.ProfilerMarker OrchestrationMarker = new Unity.Profiling.ProfilerMarker("FriendsLife.Orchestration");
        static readonly Unity.Profiling.ProfilerMarker MinigameMarker = new Unity.Profiling.ProfilerMarker("FriendsLife.Minigame");
        private void Update()
        {
            if (!IsAuthority) { TickReplicatedDatePresentations(); return; }
            if (!TryInitialize()) return;
            if (!Singleton<ResidentManager>.TryGetInstance(out _)) { previousHostMinute = double.NaN; return; }
            if (Time.unscaledTime >= nextDiscovery)
            {
                nextDiscovery = Time.unscaledTime + 2f;
                using (DiscoveryMarker.Auto()) { DiscoverBoundActors(); EnsureHumanBindings(); }
            }
            if (!IsWorldRunning) { previousHostMinute = double.NaN; return; }
            // A suspended process never pays catch-up Active time. Calendar progress has its own clock.
            long micros = (long)(Math.Min(.25f, Time.unscaledDeltaTime) * 1000000.0);
            State.activeMicros += micros;
            using (TickMarker.Auto()) WorldTick?.Invoke(0, micros);
            using (FamilyMarker.Auto()) AdvanceFamilyWorldPresentations();
            if (Time.unscaledTime >= nextWorldStep)
            {
                nextWorldStep = Time.unscaledTime + .2f;
                double hostMinute;
                if (TryReadHostClock(out hostMinute))
                {
                    if (alignNewWorldClock)
                    {
                        double shift = hostMinute - Core.State.gameMinutes;
                        Core.State.gameMinutes = hostMinute;
                        Core.State.nextPairEventGameMinutes += shift;
                        foreach (var resident in Core.State.residents) resident.activityUntilGameMinutes += shift;
                        alignNewWorldClock = false; dirty = true; State.revision++;
                    }
                    var previousCursor = State.hostCalendarCursor;
                    double previousRaw = State.hostGameMinute;
                    double elapsed = State.hostCalendarCursor.Observe(hostMinute, double.IsNaN(previousHostMinute));
                    previousHostMinute = hostMinute;
                    State.hostGameMinute = hostMinute;
                    if(elapsed==0&&(previousRaw!=hostMinute||previousCursor.initialized!=State.hostCalendarCursor.initialized||previousCursor.consumedMinute!=State.hostCalendarCursor.consumedMinute))
                    {State.revision++;dirty=true;}
                    if (elapsed > 0 && configuration.gameMinutesPerRealSecond > 0)
                    {
                        // Advance is called exactly once for a host calendar delta.
                        using (AdvanceMarker.Auto()) AdvanceFrozenWorld(elapsed / configuration.gameMinutesPerRealSecond);
                        State.revision++;
                        dirty = true;
                        CaptureSourceMailbox();
                        WorldTick?.Invoke(elapsed, 0);
                    }
                }
                using (OrchestrationMarker.Auto()) AdvanceOrchestration();
            }
            using (MinigameMarker.Auto()) TickCurrentMinigame(Time.unscaledDeltaTime);
            ObservePeriodicSave();
            if (dirty && Time.unscaledTime >= nextSave) { nextSave = Time.unscaledTime + 15f; SavePeriodic(); }
        }
        private bool periodicPending;
        // The periodic checkpoint uses the original game's own hourly-save
        // path: the slot is captured this frame and its files are written over
        // the following frames, instead of a synchronous ~110 ms SaveGame.
        // Transactional saves keep calling SaveNow.
        private void SavePeriodic()
        {
            var manager = Singleton<SaveManager>.Instance;
            if (!Ready || !IsAuthority || committing || string.IsNullOrEmpty(SavePath) || manager == null || manager.SaveFlag || !manager.HasValidCurrentSlot ||
                Singleton<GameProgressManager>.Instance == null || manager.CurrentSaveSlotIndex != loadedSlot ||
                (manager.CurrentSaveSlotHostSteamID ?? "local") != loadedHost || !ReferenceEquals(loadedSlotData, manager.CurrentSaveSlotData))
            { SaveNow(); return; }
            dirty = false; periodicPending = true;
            manager.RequestHourlySave(true);
        }
        private void ObservePeriodicSave()
        {
            if (!periodicPending) return;
            var manager = Singleton<SaveManager>.Instance;
            if (manager != null && manager.SaveFlag) return;
            periodicPending = false;
            if (manager == null || manager.LastAutoSaveResult == SaveResult.Failed) { dirty = true; LastError = "世界存档尚未完成，生活账本仍等待保存。"; }
        }
        public bool TryInitialize()
        {
            if (!Ready && Time.unscaledTime < nextInitialization) return false;
            var manager = Singleton<SaveManager>.Instance;
            if (manager == null || !manager.HasValidCurrentSlot) return Ready;
            int slot = manager.CurrentSaveSlotIndex;
            string host = manager.CurrentSaveSlotHostSteamID ?? "local";
            if (Ready && slot == loadedSlot && host == loadedHost && ReferenceEquals(loadedSlotData, manager.CurrentSaveSlotData)) return true;
            if (Ready && IsAuthority) SaveNow();
            try
            {
                foreach (string key in RequiredResourceKeys)
                    if (Resources.Load<TextAsset>(key) == null) throw new InvalidOperationException("Missing frozen resource: " + key);
                rules = NativeLifeRulesDatabase.Load();
                configuration = NativeLifeRulesDatabase.LoadConfiguration();
                var hostSlot = FriendSaveSlotBridge.EnsureCurrent(manager);
                loadedSlot = slot; loadedHost = host; loadedSlotData = hostSlot;
                string safeHost = new string(host.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
                if (safeHost.Length == 0) safeHost = "local";
                SavePath = Path.Combine(Application.persistentDataPath, "FriendsLife", safeHost + "-slot-" + slot + ".json");
                // The ES3 slot is canonical with the host wallet/calendar. A
                // sidecar is read only for the first migration into that slot.
                string embedded = hostSlot?.FriendsLifeJson;
                if (hostSlot != null && hostSlot.FriendsLifeModuleVersion != 1)
                    throw new InvalidOperationException("Unsupported FriendsLife host slot module version: " + hostSlot.FriendsLifeModuleVersion);
                State = !string.IsNullOrEmpty(embedded) ? JsonUtility.FromJson<FriendsLifeSave>(embedded) :
                    StandaloneValidationStorage && File.Exists(SavePath) ? JsonUtility.FromJson<FriendsLifeSave>(File.ReadAllText(SavePath)) : null;
                if (State == null) State = new FriendsLifeSave { worldId = Guid.NewGuid().ToString("N") };
                alignNewWorldClock = string.IsNullOrEmpty(State.coreJson);
                NormalizeAdapterState();
                Core = LifeSimulationRuntime.CreateNew(rules);
                configuration = Core.Configuration;
                if (!string.IsNullOrEmpty(State.coreJson) && !Core.TryImportJson(State.coreJson, out var error)) throw new InvalidOperationException(error);
                BindKernelPorts(Core);
                if(alignNewWorldClock)State.hostGameMinute = Core.State.gameMinutes;
                previousHostMinute = double.NaN;
                actors.Clear();
                BindHostSlotSave(manager, hostSlot);
                RestoreEconomyState?.Invoke(State.economyJson);
                CoreChanged?.Invoke(Core);
                DiscoverBoundActors();
                Changed?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Debug.LogError("[FriendsLife] Initialization failed: " + exception);
                Core = null;
                nextInitialization = Time.unscaledTime + 5f;
                return false;
            }
        }
        private static readonly string[] RequiredResourceKeys =
        {
            "LifeRules/native-life-rules", "LifeRules/reconstruction-config", "LifeRules/localization",
            "NativeEvent/native-event-tables", "Gameplay/daily-source", "LifeCreative/catalog"
        };
        private void NormalizeAdapterState()
        {
            State.people = State.people ?? new List<PersonBinding>();
            State.episodes = State.episodes ?? new List<SourceEpisode>();
            State.leases = State.leases ?? new List<ActivityLease>();
            State.worldTalks = State.worldTalks ?? new List<FriendWorldTalkRequest>();
            State.receipts = State.receipts ?? new List<FriendsKernelReceipt>();
            State.nativeHistory = State.nativeHistory ?? new List<NativeCommittedHistory>();
            foreach (var person in State.people) person.inventory = person.inventory ?? new List<LifeSimulationInventoryStack>();
        }
        private void BindKernelPorts(LifeSimulationRuntime kernel, List<SourceEpisode> preparedIntents = null)
        {
            kernel.NativeEventContextProvider = (focus, target, another) => BuildNativeContext(kernel, focus, target, another);
            kernel.CreativeActivityRequest = (residentId, action) => RouteCreativeActivity(kernel, residentId, action, preparedIntents);
            kernel.ActivityFacilityVisit = (residentId, facility) => RequestFacilityVisit(residentId, facility, preparedIntents);
        }
        private bool TryReadHostClock(out double minute)
        {
            minute = 0;
            if (weather == null && Singleton<GameProgressManager>.TryGetInstance(out var progress)) weather = progress.CozyWeatherManager;
            if (weather == null) return false;
            try { minute = Math.Max(0, weather.CurrentDay - 1) * 1440d + weather.CurrentTime.hours * 60d + weather.CurrentTime.minutes; return true; }
            catch (NullReferenceException) { return false; }
        }
        public PersonBinding[] GetPeople() => State?.people.ToArray() ?? Array.Empty<PersonBinding>();
        public PersonBinding GetPerson(string id) => State?.people.FirstOrDefault(p => p.personId == id || p.nativeResidentId == id);
        public PersonBinding GetPersonByActor(ResidentController actor) => actor == null ? null : State?.people.FirstOrDefault(p => actors.TryGetValue(p.personId, out var value) && value == actor);
        public PersonBinding PersonFromNative(string id) => State?.people.FirstOrDefault(p => p.nativeResidentId == id);
        public ResidentController ActorOf(string personId) => actors.TryGetValue(personId ?? "", out var actor) ? actor : null;
        public bool Arrived(string personId) => GetPerson(personId)?.arrivalGrantUsed == true;
        public LifeSimulationRelationship[] GetRelationships(string personId)
        {
            string native = GetPerson(personId)?.nativeResidentId;
            return native == null || Core == null ? Array.Empty<LifeSimulationRelationship>() : Core.State.relationships.Where(r => r.fromId == native || r.toId == native).ToArray();
        }
        public SourceEpisode[] GetEpisodes(string personId) => State?.episodes.Where(e => !e.committed && (e.focusPersonId == personId || e.targetPersonId == personId || e.anotherPersonId == personId)).ToArray() ?? Array.Empty<SourceEpisode>();
        public PersonBinding CreatePerson(string displayName, string avatarJson = "", PersonControllerKind kind = PersonControllerKind.Npc, string originCommandId = "")
        {
            if (!Ready || !IsAuthority || committing) return null;
            if (!string.IsNullOrEmpty(originCommandId))
            {
                var existing = State.people.FirstOrDefault(p => p.originCommandId == originCommandId);
                if (existing != null) return existing;
            }
            var native = Core.CreateResident(displayName, avatarJson);
            if (native == null) { LastError = Core.LastError; return null; }
            var person = new PersonBinding { worldId = State.worldId, personId = Guid.NewGuid().ToString("N"), nativeResidentId = native.id,
                displayName = native.displayName, controllerKind = kind, addedByPlayer = true, originCommandId = originCommandId };
            State.people.Add(person);
            NotifyChanged();
            PersonAdded?.Invoke(person);
            SaveNow();
            return person;
        }
        public PersonBinding BindResident(ResidentController actor)
        {
            if (!Ready || !IsAuthority || actor == null) return null;
            var existing = GetPersonByActor(actor) ?? State.people.FirstOrDefault(p => p.hostResidentId > 0 && p.hostResidentId == actor.ResidentId);
            if (existing == null)
            {
                var native = CreateWorldResident(actor.ResidentName);
                if (native == null) return null;
                existing = new PersonBinding { worldId = State.worldId, personId = Guid.NewGuid().ToString("N"), nativeResidentId = native.id,
                    displayName = actor.ResidentName, hostResidentId = actor.ResidentId, controllerKind = PersonControllerKind.Npc, existingHostResident = true };
                State.people.Add(existing);
            }
            actors[existing.personId] = actor;
            AttachActorIdentity(actor, existing);
            NotifyChanged();
            return existing;
        }
        public bool UpdatePersonProfile(string id, string displayName, string avatarJson, string catchphrase = null, string hobby = null)
        {
            var person = GetPerson(id);
            if (person == null || !IsAuthority) return false;
            if (!Core.UpdateResidentProfile(person.nativeResidentId, displayName, avatarJson, catchphrase, hobby)) return false;
            person.displayName = Core.FindResident(person.nativeResidentId).displayName;
            NotifyChanged(); SaveNow(); return true;
        }
        public bool SetAppearance(string id, string bozoJson)
        {
            var person = GetPerson(id);
            if (person == null || !IsAuthority || string.IsNullOrEmpty(bozoJson)) return false;
            try
            {
                var candidate = JsonUtility.FromJson<Bozo.ModularCharacters.CharacterData>(bozoJson);
                if (candidate == null || candidate.bodyIDs == null || candidate.faceIDs == null || candidate.outfitDatas == null)
                { LastError = "Bozo角色资料不完整。"; return false; }
            }
            catch (Exception error) { LastError = error.Message; return false; }
            person.appearanceJson = bozoJson;
            person.appearanceRevision++;
            ApplyActorAppearance(person);
            NotifyChanged(); SaveNow(); return true;
        }
        public bool RegisterArrival(string personId, string homeId, bool persist = true)
        {
            var person = GetPerson(personId);
            if (person == null || !IsAuthority || string.IsNullOrEmpty(homeId)) return false;
            if (IsAwaitingArrival(person.nativeResidentId) && ActiveWorldPopulation(Core) >= configuration.maximumResidents) return false;
            if (Core.State.households.All(h => h.id != homeId)) Core.CreateHousehold(homeId, configuration.maximumHouseCapacity);
            if (!Core.AssignHouse(person.nativeResidentId, homeId)) return false;
            person.currentHomeId = homeId;
            if (string.IsNullOrEmpty(person.ownedHomeId)) person.ownedHomeId = homeId;
            if (person.controllerKind == PersonControllerKind.Npc) EnsureActorProjection(person);
            if (persist) { NotifyChanged(); SaveNow(); }
            return true;
        }
        public FriendsKernelReceipt ExecuteKernel(string opId, string payerPersonId, Func<LifeSimulationRuntime, bool> command, string causeEpisode = null, bool recordReceipt = true, IReadOnlyList<ActorWalletDelta> explicitWalletDeltas = null, string editableDraftNativeId = null)
        {
            var existing = State?.receipts.FirstOrDefault(r => r.opId == opId);
            if (existing != null) return existing;
            if (!Ready || !IsAuthority || committing || string.IsNullOrEmpty(opId) || command == null)
                return new FriendsKernelReceipt { opId = opId, success = false, message = "当前不能执行生活操作。" };
            var person = GetPerson(payerPersonId);
            if (person == null) return new FriendsKernelReceipt { opId = opId, success = false, message = "没有找到付款人。" };
            committing = true;
            FriendsKernelReceipt committedReceipt = null;
            try
            {
                long balance = ReadWalletCents?.Invoke(person.personId) ?? 0;
                if (balance < 0) throw new InvalidOperationException("Wallet projection is negative.");
                var scope = LifeSimulationRuntime.CreateNew(rules, configuration);
                if (!scope.TryImportJson(Core.ExportJson(false), out var error)) throw new InvalidOperationException(error);
                var preparedIntents = new List<SourceEpisode>();
                BindKernelPorts(scope, preparedIntents);
                int projected = (int)Math.Min(99999999L, balance);
                scope.State.walletRaw = projected;
                scope.State.inventory = person.inventory.Select(i => new LifeSimulationInventoryStack { category = i.category, itemName = i.itemName, quantity = i.quantity }).ToList();
                var roster = ProjectWorldRoster(scope, editableDraftNativeId);
                bool success;
                try { success = command(scope); }
                finally { RestoreRegisteredRoster(scope, roster); }
                if (!success) return new FriendsKernelReceipt { opId = opId, success = false, payerPersonId = person.personId, message = scope.LastError ?? "原操作未完成。" };
                long delta = (long)scope.State.walletRaw - projected;
                if (explicitWalletDeltas != null)
                {
                    if (explicitWalletDeltas.Any(d => d.deltaCents != 0) && (CommitWalletBatch == null || !CommitWalletBatch(explicitWalletDeltas, opId)))
                        return new FriendsKernelReceipt { opId = opId, success = false, message = "原参与者账单未能原子提交。" };
                    delta = explicitWalletDeltas.Sum(d => d.deltaCents);
                }
                else if (delta != 0 && (CommitWalletDelta == null || !CommitWalletDelta(person.personId, delta, opId)))
                    return new FriendsKernelReceipt { opId = opId, success = false, message = "本人余额或账单版本已变化。" };
                person.inventory = scope.State.inventory;
                scope.State.inventory = Core.State.inventory;
                scope.State.walletRaw = Core.State.walletRaw;
                Core = scope;
                dirty = true;
                BindKernelPorts(Core);
                State.episodes.AddRange(preparedIntents);
                var receipt = new FriendsKernelReceipt { opId = opId, payerPersonId = person.personId, causeEpisode = causeEpisode, domain = "FrozenKernel", success = true,
                    revision = ++State.revision, walletDeltaCents = delta, gameMinute = Core.State.gameMinutes, message = "已提交。",
                    allocations = explicitWalletDeltas?.ToList() ?? new List<ActorWalletDelta>() };
                if (recordReceipt || delta != 0) State.receipts.Add(receipt);
                committedReceipt = receipt;
                foreach (var intent in preparedIntents)
                {
                    intent.inputOwnerPersonId = person.personId;
                    intent.opId = opId;
                    ActivityPresentationRequested?.Invoke(intent.focusPersonId, intent.sourceKind == "FacilityVisit" ? "facility:" + intent.sourceId : intent.sourceId);
                }
                CoreChanged?.Invoke(Core);
                NotifyChanged();
                return receipt;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Debug.LogError("[FriendsLife] Kernel scope failed: " + exception);
                if (committedReceipt != null)
                {
                    committedReceipt.message = "原结果已提交，等待重新建立世界表现。";
                    return committedReceipt;
                }
                return new FriendsKernelReceipt { opId = opId, success = false, message = exception.Message };
            }
            finally { committing = false; }
        }
        public void MarkChanged() => NotifyChanged();
        public void NotifyChanged() { dirty = true; if (State != null) State.revision++; Changed?.Invoke(); }
        public string ExportSnapshot()
        {
            if (!Ready) return "";
            PrepareSnapshotStateForSerialization();
            return JsonUtility.ToJson(State);
        }
        // Keep every current source runner, cursor, native-owner/date and
        // ambient-request preparation. Only final adapter JSON is lazy.
        public void PrepareSnapshotStateForSerialization()
        {
            if (!Ready) return;
            if (IsAuthority) OriginalNpcActivityPersistence.Capture(this);
            if (IsAuthority) SourceGraphs.OriginalGraphBridge.Instance?.CaptureSourceRunnersForSerialization(this);
            State.coreJson = Core.ExportJson(false);
            if (CaptureEconomyState != null) State.economyJson = CaptureEconomyState();
            if (IsAuthority) State.nativeSourceContextJson = SourceGraphs.NativeSourceContextFacade.Capture(this);
            State.savedUtc = DateTime.UtcNow.ToString("O");
        }
        // Same live captures as above for a host with no remote peer. The
        // snapshot JSON is encoded only when it is actually sent or saved.
        public void PrepareLiveStateWithoutSnapshot()
        {
            if (!Ready) return;
            if (IsAuthority) OriginalNpcActivityPersistence.Capture(this);
            if (IsAuthority) SourceGraphs.OriginalGraphBridge.Instance?.CaptureSourceRunnersForSerialization(this);
            CaptureLiveEconomyState?.Invoke();
            if (IsAuthority) SourceGraphs.NativeSourceContextFacade.SynchronizeLive(this);
        }
        public bool ApplySnapshot(string json)
        {
            if (IsAuthority || string.IsNullOrEmpty(json)) return false;
            var snapshot = JsonUtility.FromJson<FriendsLifeSave>(json);
            if (snapshot == null || State != null && snapshot.worldId == State.worldId && snapshot.revision < State.revision) return false;
            if (rules == null) rules = NativeLifeRulesDatabase.Load();
            if (configuration == null) configuration = NativeLifeRulesDatabase.LoadConfiguration();
            var receivedCore = LifeSimulationRuntime.CreateNew(rules);
            if (!receivedCore.TryImportJson(snapshot.coreJson, out var error)) { LastError = error; return false; }
            State = snapshot; NormalizeAdapterState(); Core = receivedCore; configuration = Core.Configuration;
            BindKernelPorts(Core); RestoreEconomyState?.Invoke(State.economyJson); CoreChanged?.Invoke(Core); Changed?.Invoke(); return true;
        }
        public void SaveNow()
        {
            if (!Ready || !IsAuthority || string.IsNullOrEmpty(SavePath)) return;
            // Wallet adapters may request a save while a scope is preparing. The
            // core, its receipt and economy projection must reach disk together.
            if (committing) { dirty = true; return; }
            try
            {
                var manager = Singleton<SaveManager>.Instance;
                if (manager != null && manager.HasValidCurrentSlot && Singleton<GameProgressManager>.Instance != null)
                {
                    if (manager.CurrentSaveSlotIndex != loadedSlot || (manager.CurrentSaveSlotHostSteamID ?? "local") != loadedHost || !ReferenceEquals(loadedSlotData, manager.CurrentSaveSlotData)) return;
                    var saved = manager.SaveGame();
                    if (saved == SaveResult.Success) dirty = false;
                    else { dirty = true; LastError = "世界存档尚未完成，生活账本仍等待保存。"; }
                    return;
                }
                if (!StandaloneValidationStorage) { dirty = true; return; }
                string json = ExportSnapshot();
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                string temp = SavePath + ".pending";
                File.WriteAllText(temp, json);
                if (File.Exists(SavePath)) File.Replace(temp, SavePath, null); else File.Move(temp, SavePath);
                dirty = false;
            }
            catch (Exception exception) { LastError = exception.Message; Debug.LogError("[FriendsLife] Save failed: " + exception.Message); }
        }
    }
}
