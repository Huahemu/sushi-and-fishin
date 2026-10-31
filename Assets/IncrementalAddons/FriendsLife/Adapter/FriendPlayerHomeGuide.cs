using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bozo.ModularCharacters;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.Visuals;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Streaming;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.Game;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Networking;
using SushiAndFish.ResidentialIsland;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SushiAndFish.FriendsLife
{
    // New-world eligibility and per-human enrollment live in the existing NEW
    // economy ledger. Original HasSave is observed, never set or inferred from
    // an empty friend roster. A pre-home autosave cannot lose this enrollment.
    public static class FriendPlayerHomeLedger
    {
        const string domain = "player-home-guide";
        public static string WorldOperation(string world) => "player-home-guide/new-world/" + world;
        public static string HumanOperation(string person) => "player-home-guide/required/" + person;
        static bool Has(FriendsEconomyService economy, string operation)
            => economy.State.receipts.Any(r => r.opId == operation && r.domain == domain);
        public static bool EligibleWorld(FriendsLifeRuntime runtime, FriendsEconomyService economy)
            => runtime && runtime.Ready && economy && Has(economy, WorldOperation(runtime.State.worldId));
        public static bool Required(FriendsLifeRuntime runtime, FriendsEconomyService economy, PersonBinding person)
            => person != null && EligibleWorld(runtime, economy) && Has(economy, HumanOperation(person.personId)) && !HasCommittedHome(economy, person);
        public static bool HasCommittedHome(FriendsEconomyService economy, PersonBinding person)
            {
            if(!economy||person==null||!economy.State.receipts.Any(r=>r.opId=="player-home/"+person.personId&&r.domain=="player-home"))return false;
            if(!string.IsNullOrEmpty(person.ownedHomeId)&&person.currentHomeId==person.ownedHomeId)return true;
            var group=economy.SharedHomeOf(person.personId);
            return group!=null&&group.vacancySchema>=1&&group.homeId==person.currentHomeId&&group.returnHomes.Any(t=>t.personId==person.personId);
        }
        public static void Enroll(FriendsLifeRuntime runtime, FriendsEconomyService economy, bool observedFreshOriginalSlot)
        {
            if (!runtime || !runtime.Ready || !runtime.IsAuthority || !economy) return;
            bool changed = false;
            void Record(string operation, string actor)
            {
                if (Has(economy, operation)) return;
                economy.State.receipts.Add(new EconomyReceipt { opId = operation, domain = domain, actorId = actor,
                    counterparty = runtime.State.worldId, deltaCents = 0, revision = ++economy.State.revision });
                changed = true;
            }
            if (observedFreshOriginalSlot) Record(WorldOperation(runtime.State.worldId), "world");
            if (!EligibleWorld(runtime, economy)) return;
            foreach (var human in runtime.People.Where(p => p.controllerKind == PersonControllerKind.Human && string.IsNullOrEmpty(p.ownedHomeId) && string.IsNullOrEmpty(p.currentHomeId)))
                Record(HumanOperation(human.personId), human.personId);
            if (changed) runtime.NotifyChanged();
        }
    }

    [DefaultExecutionOrder(30150)]
    public sealed class FriendPlayerHomeGuide : MonoBehaviour
    {
        public static FriendPlayerHomeGuide Instance { get; private set; }
        public bool IsChoosingHome { get; private set; }
        public string PersonId { get; private set; }
        public string Phase { get; private set; } = "WaitingForOriginalPlayer";
        public string LastReason { get; private set; }
        public int MapOpenCount { get; private set; }
        public int HomeTeleportCount { get; private set; }
        public string LastTeleportReason { get; private set; }
        public string LastHouseId { get; private set; }
        public bool OriginalFreshSlotObserved { get; private set; }
        public bool AppearanceReady { get; private set; }
        public bool BootstrapReady { get; private set; }
        public bool AppearanceLoadEventsObserved => tpsLoaded && fpsLoaded;

        PlayersManager players;
        PPG_Player player;
        TabletController tablet;
        GameStartGate gate;
        OutfitSystem tps, fps;
        CharacterData expectedAppearance;
        bool tpsLoaded, fpsLoaded, spawnReturnPending, appearanceVerified;
        string sessionKey;
        int returnAfterFrame = -1;
        string pendingReturnReason;
        float nextLookup;
        readonly HashSet<string> freshSlots = new HashSet<string>();
        readonly HashSet<BaseButton> stuckButtons = new HashSet<BaseButton>();
        static readonly FieldInfo stuckButtonField = typeof(PauseScreen).GetField("_imStuckButton", BindingFlags.Instance | BindingFlags.NonPublic);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() => EnsureInstalled();
        public static FriendPlayerHomeGuide EnsureInstalled()
        {
            if (Instance) return Instance;
            var go = new GameObject("Incremental player home guide"); DontDestroyOnLoad(go);
            return go.AddComponent<FriendPlayerHomeGuide>();
        }
        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this; SceneManager.sceneLoaded += SceneLoaded; ObserveOriginalServices();
        }
        void SceneLoaded(Scene scene, LoadSceneMode mode) { nextLookup = 0; ObserveOriginalServices(); }
        void ObserveOriginalServices()
        {
            // BeforeSceneLoad runs before source manager Awake. Its Instance
            // getter performs a scene search and sets the original static;
            // doing that early would make source Awake skip DontDestroyOnLoad.
            // Observe only an instance already registered by source Awake.
            Singleton<PlayersManager>.TryGetInstance(out var current);
            if (current != players)
            {
                if (players) { players.PlayerRegistered -= PlayerRegistered; players.PlayerUnregistered -= PlayerUnregistered; }
                players = current;
                if (players) { players.PlayerRegistered += PlayerRegistered; players.PlayerUnregistered += PlayerUnregistered; }
            }
            foreach (var screen in FindObjectsByType<PauseScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Read the serialized source reference; retain its original
                // handler and add one independent post-recovery listener.
                var button = stuckButtonField?.GetValue(screen) as BaseButton;
                if (button && stuckButtons.Add(button)) button.Clicked += OriginalStuckClicked;
            }
            stuckButtons.RemoveWhere(b => !b);
        }
        void PlayerRegistered(PPG_Player registered)
        {
            if (registered && registered.IsOwner && registered != player) BindPlayer(registered);
        }
        void PlayerUnregistered(PPG_Player unregistered) { if (unregistered == player) ClearPlayer(); }
        void BindPlayer(PPG_Player value)
        {
            ClearPlayer(); player = value; spawnReturnPending = true;
            tablet = player.TabletController;
            BindRigs();
        }
        void BindRigs()
        {
            var visuals = player ? player.CharacterVisuals : null;
            var nextTps = visuals ? visuals.TpsOutfitSystem : null; var nextFps = visuals ? visuals.FpsOutfitSystem : null;
            if (tps != nextTps) { if (tps) tps.OnCharacterLoaded -= TpsLoaded; tps = nextTps; tpsLoaded = appearanceVerified = false; if (tps) tps.OnCharacterLoaded += TpsLoaded; }
            if (fps != nextFps) { if (fps) fps.OnCharacterLoaded -= FpsLoaded; fps = nextFps; fpsLoaded = appearanceVerified = false; if (fps) fps.OnCharacterLoaded += FpsLoaded; }
        }
        void TpsLoaded() { tpsLoaded = true; appearanceVerified = false; }
        void FpsLoaded() { fpsLoaded = true; appearanceVerified = false; }
        void OriginalStuckClicked()
        {
            if (!player || !player.IsOwner) return;
            // The original Clicked invocation, recovery and source spawn
            // teleport finish first. No original callback is replaced.
            returnAfterFrame = Time.frameCount; pendingReturnReason = "OriginalUnstuckCompleted";
        }
        float nextSettledCheck;
        void Update()
        {
            ObserveOriginalSlotFreshness();
            if (Time.unscaledTime >= nextLookup) { nextLookup = Time.unscaledTime + .5f; ObserveOriginalServices(); }
            // Once settled (own home ready, or no guide needed) nothing is pending;
            // re-check four times a second instead of walking the ledgers every frame.
            bool settled = (Phase == "OwnHomeReady" || Phase == "OriginalWorldOrNoGuideEnrollment") && !spawnReturnPending && returnAfterFrame < 0 && !IsChoosingHome && players && player == players.LocalPlayer;
            if (settled && Time.unscaledTime < nextSettledCheck) return;
            nextSettledCheck = Time.unscaledTime + .25f;
            if (!players || !players.LocalPlayer || !players.LocalPlayer.IsSpawned) { if (player) ClearPlayer(); return; }
            if (player != players.LocalPlayer) BindPlayer(players.LocalPlayer);
            if (!player.IsOwner) return;
            BindRigs();
            var runtime = FriendsLifeRuntime.Instance; var economy = FriendsEconomyService.Instance; var housing = FriendHousingIsland.Instance;
            if (!runtime || !runtime.Ready || !economy || runtime.CaptureEconomyState == null || !ReferenceEquals(runtime.CaptureEconomyState.Target, economy))
            { Phase = "WaitingForNewWorldBinding"; return; }
            Singleton<SaveManager>.TryGetInstance(out var save); string slot = SlotKey(save);
            bool observedFresh = slot != null && freshSlots.Contains(slot);
            FriendPlayerHomeLedger.Enroll(runtime, economy, observedFresh);
            if (runtime.IsAuthority && observedFresh && FriendPlayerHomeLedger.EligibleWorld(runtime, economy)) freshSlots.Remove(slot);
            var human = runtime.LocalHuman(); if (human == null) { Phase = "WaitingForOwnHumanBinding"; return; }
            string key = runtime.State.worldId + "/" + player.GetInstanceID() + "/" + human.personId;
            if (sessionKey != key) { sessionKey = key; PersonId = human.personId; spawnReturnPending = true; IsChoosingHome = false; }
            BootstrapReady = OriginalBootstrapReady(); AppearanceReady = OriginalAppearanceReady();
            if (!BootstrapReady || !AppearanceReady) { Phase = "WaitingForOriginalBootstrapAndAppearance"; return; }
            if (!housing || !housing.CanProject || housing.Plots.Count == 0 || (!economy.HasCanonicalHousing && !housing.InitialHousingLoaded))
            { Phase = "WaitingForHousingProjection"; return; }
            if (!tablet) tablet = player.TabletController;
            if (!tablet || !tablet.IsInitialized) { Phase = "WaitingForOriginalTablet"; return; }

            if (FriendPlayerHomeLedger.HasCommittedHome(economy, human))
            {
                bool choosing = IsChoosingHome; IsChoosingHome = false; // Stop map reopening immediately at the commit boundary.
                var home = housing.Plots.FirstOrDefault(p => p.id == human.currentHomeId)?.house;
                if (!home || !FriendHouseMetadata.For(home).friendsOwnedClone) { Phase = "WaitingForOwnNewHouseProjection"; return; }
                bool afterStuck = returnAfterFrame >= 0 && Time.frameCount > returnAfterFrame;
                if (spawnReturnPending || choosing || afterStuck)
                {
                    if (!TryDoorPose(home, player, out var position, out var rotation)) { Phase = "WaitingForRealDoorLanding"; return; }
                    player.TeleportPlayer(position, rotation, true);
                    player.FirstPersonCharacter.SetVelocity(Vector3.zero);
                    HomeTeleportCount++; LastHouseId = home.plot;
                    LastTeleportReason = afterStuck ? pendingReturnReason : choosing ? "OwnHomeCommitted" : "OriginalPlayerRegistered";
                    spawnReturnPending = false; returnAfterFrame = -1; pendingReturnReason = null;
                    if (choosing && tablet.IsTabletActive) tablet.CloseTablet();
                }
                Phase = "OwnHomeReady"; return;
            }
            if (!FriendPlayerHomeLedger.Required(runtime, economy, human))
            { IsChoosingHome = false; Phase = "OriginalWorldOrNoGuideEnrollment"; return; }
            if (!string.IsNullOrEmpty(human.ownedHomeId)) { Phase = "WaitingForExistingHomeCommit"; return; }
            IsChoosingHome = true; Phase = "ChoosingOwnFirstHome";
            if (housing.ArrivalPersonId != human.personId)
            {
                if (!FriendHousingUI.BeginArrival(human.personId)) { LastReason = economy.LastFailure; return; }
                MapOpenCount++;
            }
            if (!FriendTabletAccess.IsMapActive(tablet)) { FriendTabletAccess.OpenMap(tablet); MapOpenCount++; }
            // The map app is a child of this player's own tablet.
            foreach (var map in tablet.GetComponentsInChildren<HousingMap>(true))
                if (map.IsVisible && !map.GetComponent<FriendArrivalMapOverlay>()) map.gameObject.AddComponent<FriendArrivalMapOverlay>();
        }
        void ObserveOriginalSlotFreshness()
        {
            Singleton<SaveManager>.TryGetInstance(out var save);
            if (!save || !save.HasValidCurrentSlot || save.CurrentSaveSlotData?.GameProgress == null || save.CurrentSaveSlotData.GameProgress.HasSave) return;
            string key = SlotKey(save); if (key == null) return;
            freshSlots.Add(key); OriginalFreshSlotObserved = true;
        }
        static string SlotKey(SaveManager save)
            => save && save.HasValidCurrentSlot ? save.GetInstanceID() + "/" + save.CurrentSaveSlotIndex + "/" + (save.CurrentSaveSlotHostSteamID ?? "local") : null;
        bool OriginalBootstrapReady()
        {
            if (!player || !player.IsSpawned || !player.IsLocalGameplayPreparationReady || !player.FirstPersonCharacter) return false;
            if (!gate) gate = FindFirstObjectByType<GameStartGate>();
            if (!gate || !gate.IsGameStarted) return false;
            if (!Singleton<JoinBootstrapCoordinator>.TryGetInstance(out var bootstrap) || !bootstrap.BootstrapCompleted) return false;
            var source = SceneManager.GetSceneByPath("Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity");
            return source.IsValid() && source.isLoaded;
        }
        bool OriginalAppearanceReady()
        {
            if (!RigReady(tps) || !RigReady(fps)) { appearanceVerified = false; return false; }
            if (Singleton<DressingSceneManager>.TryGetInstance(out var dressing) && dressing.IsStartupStreamingBusy) return false;
            if (appearanceVerified) return true;
            if (expectedAppearance == null)
            {
                var data = PlayerCustomizationSession.GetCurrentCharacterData();
                if (data == null) return false;
                expectedAppearance = Bozo_SavePatcher.UpdateSave(data);
            }
            // A vendor Init event alone can refer to the old default outfit.
            // Require the actual selected resources and shapes even after an
            // event, covering late installation without inventing a delay.
            appearanceVerified = MatchesCurrentAppearance(tps, expectedAppearance, false) && MatchesCurrentAppearance(fps, expectedAppearance, true);
            return appearanceVerified;
        }
        static bool RigReady(OutfitSystem rig)
        {
            if (!rig || !rig.initalized || rig.isloading || rig.isMerging || rig.isUpdatingTexture) return false;
            var body = rig.GetCharacterBody(); return body && body.sharedMesh && body.sharedMesh.vertexCount > 0;
        }
        static bool MatchesCurrentAppearance(OutfitSystem rig, CharacterData expected, bool firstPerson)
        {
            if (!rig || expected?.outfitDatas == null) return false;
            try
            {
                var captured = BMAC_SaveSystem.GetCharacterData(rig);
                var expectedOutfits = expected.outfitDatas.Where(o => !string.IsNullOrEmpty(o.outfit) &&
                    (!firstPerson || o.outfit.StartsWith("Body/", StringComparison.Ordinal) || o.outfit.StartsWith("Gloves/", StringComparison.Ordinal))).Select(o => o.outfit).OrderBy(x => x).ToArray();
                var actualOutfits = captured.outfitDatas.Where(o => !string.IsNullOrEmpty(o.outfit)).Select(o => o.outfit).OrderBy(x => x).ToArray();
                if (expectedOutfits.Length == 0 || !expectedOutfits.SequenceEqual(actualOutfits)) return false;
                foreach (var source in expected.outfitDatas.Where(o => expectedOutfits.Contains(o.outfit)))
                {
                    var actual = captured.outfitDatas.First(o => o.outfit == source.outfit);
                    var outfit = rig.GetOutfits().First(o => o && o.GetOutfitData().outfit == source.outfit);
                    if (outfit.customShader)
                    { if (!SameColor(source.color, actual.color) || source.swatch != actual.swatch) return false; }
                    else
                    {
                        if (!SameColors(source.colors, actual.colors)) return false;
                        if ((source.pattern ?? "") != (actual.pattern ?? "")) return false;
                        if (!string.IsNullOrEmpty(source.pattern) && !SameColors(source.patternColors, actual.patternColors)) return false;
                    }
                    if (source.partVisibility != null && source.partVisibility.Length > 0 &&
                        (actual.partVisibility == null || !source.partVisibility.SequenceEqual(actual.partVisibility))) return false;
                }
                if (firstPerson) return true;
                bool Shapes(List<string> ids, List<float> values)
                {
                    if (ids == null || values == null) return true;
                    if (ids.Count != values.Count) return false;
                    for (int i = 0; i < ids.Count; i++) if (Mathf.Abs(rig.GetShapeValue(ids[i]) - values[i]) > .0001f) return false;
                    return true;
                }
                return Shapes(expected.bodyIDs, expected.bodyShapes) && Shapes(expected.faceIDs, expected.faceShapes);
            }
            catch { return false; }
        }
        static bool SameColor(Color a, Color b)
            => Mathf.Abs(a.r - b.r) <= .005f && Mathf.Abs(a.g - b.g) <= .005f && Mathf.Abs(a.b - b.b) <= .005f && Mathf.Abs(a.a - b.a) <= .005f;
        static bool SameColors(List<Color> expected, List<Color> actual)
        {
            if (expected == null || expected.Count == 0) return true;
            if (actual == null || actual.Count < expected.Count) return false;
            for (int i = 0; i < expected.Count; i++) if (!SameColor(expected[i], actual[i])) return false;
            return true;
        }
        public static bool TryDoorPose(HousingHouse house, PPG_Player owner, out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = Quaternion.identity;
            if (!house || !house.entry || !owner || !owner.FirstPersonCharacter) return false;
            var capsule = owner.FirstPersonCharacter.GetComponent<CapsuleCollider>();
            if (!capsule || capsule.direction != 1 || Vector3.Dot(capsule.transform.up, Vector3.up) < .999f) return false;
            Vector3 outward = -Vector3.ProjectOnPlane(house.entry.forward, Vector3.up).normalized;
            if (outward.sqrMagnitude < .5f) outward = -Vector3.ProjectOnPlane(house.transform.forward, Vector3.up).normalized;
            if (outward.sqrMagnitude < .5f) return false;
            rotation = Quaternion.LookRotation(outward, Vector3.up);
            int mask = owner.FirstPersonCharacter.characterMovement.collisionLayers;
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(capsule.transform.lossyScale.x), Mathf.Abs(capsule.transform.lossyScale.z));
            float height = Mathf.Max(radius * 2, capsule.height * Mathf.Abs(capsule.transform.lossyScale.y));
            float feetOffset = capsule.bounds.min.y - owner.transform.position.y;
            Vector3 centerOffset = Quaternion.Inverse(owner.transform.rotation) * (capsule.transform.TransformPoint(capsule.center) - owner.transform.position);
            foreach (float distance in new[] { 1.5f, 1.9f, 2.3f })
            {
                var point = house.entry.position + outward * distance;
                var ground = Physics.RaycastAll(point + Vector3.up * .7f, Vector3.down, 2.5f, mask, QueryTriggerInteraction.Ignore)
                    .Where(h => h.normal.y >= .65f && !h.collider.GetComponentInParent<PPG_Player>()).OrderBy(h => h.distance).FirstOrDefault();
                if (!ground.collider || Mathf.Abs(ground.point.y - house.entry.position.y) > .75f) continue;
                point.y = ground.point.y - feetOffset + .06f;
                var center = point + rotation * centerOffset; float halfCylinder = Mathf.Max(0, height * .5f - radius);
                var overlaps = Physics.OverlapCapsule(center + Vector3.up * halfCylinder, center - Vector3.up * halfCylinder, radius, mask, QueryTriggerInteraction.Ignore);
                if (overlaps.Any(c => c && !c.transform.IsChildOf(owner.transform))) continue;
                position = point; return true;
            }
            return false;
        }
        void ClearPlayer()
        {
            if (tps) tps.OnCharacterLoaded -= TpsLoaded; if (fps) fps.OnCharacterLoaded -= FpsLoaded;
            tps = fps = null; tpsLoaded = fpsLoaded = appearanceVerified = false; expectedAppearance = null;
            player = null; tablet = null; gate = null; PersonId = null; sessionKey = null;
            IsChoosingHome = false; AppearanceReady = BootstrapReady = false; spawnReturnPending = false; returnAfterFrame = -1;
        }
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (players) { players.PlayerRegistered -= PlayerRegistered; players.PlayerUnregistered -= PlayerUnregistered; }
            foreach (var button in stuckButtons) if (button) button.Clicked -= OriginalStuckClicked;
            stuckButtons.Clear(); ClearPlayer(); if (Instance == this) Instance = null;
        }
    }
}
