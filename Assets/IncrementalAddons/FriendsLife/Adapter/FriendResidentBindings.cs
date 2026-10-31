using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bozo.ModularCharacters;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Data.Ai.NPCs;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // This marker belongs only to a newly instantiated friend projection. An
    // existing original resident is never implicitly converted into an owned
    // appearance rig or given a new home-state override.
    public sealed class FriendResidentOwnedProjection : MonoBehaviour { }

    public sealed class FriendResidentBindingState : MonoBehaviour
    {
        public ResidentController actor;
        public Vector3 homePosition;
        public Quaternion homeRotation;
        public bool hasHome;
        public int homeRevision;
        public DataObject desiredAppearance;
        public FriendCharacterData nativeAppearance;
        // Presentation copy with missing hair/clothes filled; never saved.
        public FriendCharacterData filledAppearance;
        public string filledSlots = "";
        internal CancellationTokenSource appearanceCancellation;
        internal Task appearanceTask;

        // The original Brain resolves this live home itself (ResolveHome) and is
        // the only writer of the Home/ReturnHome states. A changed home only
        // asks it to re-evaluate; the old second return-home state is gone.
        void LateUpdate()
        { if (actor && actor.Brain != null) OriginalNpcActivityPersistence.LiveHomeBindingChanged(this); }

        void OnDestroy() { appearanceCancellation?.Cancel(); appearanceCancellation?.Dispose(); if (nativeAppearance) Destroy(nativeAppearance); if (filledAppearance) Destroy(filledAppearance); }
    }

    public static class FriendResidentBindings
    {
        public static bool HasBinding(ResidentController actor) => actor && actor.GetComponent<FriendWorldActor>();
        public static bool IsOwned(ResidentController actor) => HasBinding(actor) && actor.GetComponent<FriendResidentOwnedProjection>();

        public static void MarkOwned(ResidentController actor, Vector3 home, Quaternion rotation)
        {
            if (!actor) throw new ArgumentNullException(nameof(actor));
            if (!actor.GetComponent<FriendResidentOwnedProjection>()) actor.gameObject.AddComponent<FriendResidentOwnedProjection>();
            var state = actor.GetComponent<FriendResidentBindingState>();
            if (!state) state = actor.gameObject.AddComponent<FriendResidentBindingState>();
            state.actor = actor; state.homePosition = home; state.homeRotation = rotation; state.hasHome = true;
            FriendOwnedAmbientPathGuardian.Attach(actor);
        }

        public static void SetHome(ResidentController actor, Vector3 position, Quaternion rotation)
        {
            if (!IsOwned(actor)) return;
            var state = actor.GetComponent<FriendResidentBindingState>();
            if (!state) { MarkOwned(actor, position, rotation); return; }
            if (state.hasHome && Vector3.Distance(state.homePosition, position) < .01f && Quaternion.Angle(state.homeRotation, rotation) < .1f) return;
            state.homePosition = position; state.homeRotation = rotation; state.hasHome = true; state.homeRevision++;
        }

        public static void ApplyAppearance(ResidentController actor, DataObject appearance)
        {
            if (!IsOwned(actor) || actor.ResidentData == null || !appearance) return;
            var state = actor.GetComponent<FriendResidentBindingState>();
            state.desiredAppearance = appearance;
            appearance = WithOriginalOutfit(actor, state, appearance);
            // The original startup/wake path must see only native resource
            // paths. The full generated authority remains in this new binding.
            var full = appearance.GetCharacterData();
            if (FriendAppearancePort.HasGenerated(full))
            {
                var donor = FriendAppearancePort.ToDonorData(full);
                if (!state.nativeAppearance) state.nativeAppearance = ScriptableObject.CreateInstance<FriendCharacterData>();
                state.nativeAppearance.name = "Friend native startup donor";
                state.nativeAppearance.data = donor;
                actor.ResidentData.CharacterData = state.nativeAppearance;
            }
            else actor.ResidentData.CharacterData = appearance;
            actor.ResidentData.BakedVisualPrefab = null;
            state.appearanceCancellation?.Cancel(); state.appearanceCancellation?.Dispose();
            state.appearanceCancellation = new CancellationTokenSource();
            state.appearanceTask = ApplyAppearanceAsync(actor, appearance, state.appearanceCancellation.Token);
        }

        // A friend made in the creator often has only head, body, eyes and the
        // underwear base (bald, undershirt, boxers, barefoot). Original
        // residents always wear a full outfit from their CharacterObject. Fill
        // ONLY the missing hair / top / bottom / shoes slots from one original
        // resident outfit, chosen stably per person. Every slot, colour, body
        // and face shape the friend already has is kept. The saved
        // appearanceJson and desiredAppearance stay unchanged.
        static List<CharacterData> outfitPresets;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOutfitPresets() { outfitPresets = null; }
        static DataObject WithOriginalOutfit(ResidentController actor, FriendResidentBindingState state, DataObject appearance)
        {
            var data = appearance ? appearance.GetCharacterData() : null;
            string personId = actor.GetComponent<FriendWorldActor>()?.personId;
            var filled = PresentationData(data, personId, out string slots);
            state.filledSlots = slots;
            return ReferenceEquals(data, filled) ? appearance : Wrap(state, filled, appearance);
        }
        // Shared by the owned world actor and committed NPC portrait generation.
        // Never write this copy back to the person's appearanceJson or a draft.
        public static CharacterData PresentationData(CharacterData data, string personId, out string filledSlots)
        {
            filledSlots = "";
            // Legacy photo packages own their observed geometry and clothing.
            if (data?.outfitDatas == null || string.IsNullOrEmpty(personId) || NativePhotoParameterFit.HasGenerated(data)) return data;
            var has = new HashSet<string>(data.outfitDatas.Select(o => OutfitType(o.outfit)), StringComparer.OrdinalIgnoreCase);
            bool hair = !has.Contains("HairFront") && !has.Contains("HairBack") && !has.Contains("Hat");
            bool top = !has.Contains("Top") && !has.Contains("Overall"), bottom = !has.Contains("Bottom") && !has.Contains("Overall"), feet = !has.Contains("Feet");
            if (!(hair || top || bottom || feet)) return data;
            // Same body style first (BoZo BodyType 100 = masculine; a low
            // BodyType with a chest shape = feminine), so hair and clothes fit.
            bool feminine = Feminine(data);
            var presets = OutfitPresets().Where(p => Feminine(p) == feminine).ToList();
            if (presets.Count == 0) presets = OutfitPresets();
            if (presets.Count == 0) return data;
            uint hash = 2166136261; foreach (char c in personId) { hash ^= c; hash *= 16777619; }
            var preset = presets[(int)(hash % (uint)presets.Count)];
            var filled = JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(data));
            var donor = JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(preset));
            var added = new List<string>();
            foreach (var item in donor.outfitDatas)
            {
                string type = OutfitType(item.outfit);
                if (!(hair && (type == "HairFront" || type == "HairBack") || top && type == "Top" || bottom && type == "Bottom" || feet && type == "Feet")) continue;
                filled.outfitDatas.Add(item); added.Add(type);
            }
            filledSlots = added.Count == 0 ? "" : preset.characterName + ": " + string.Join(",", added);
            return added.Count == 0 ? data : filled;
        }
        static FriendCharacterData Wrap(FriendResidentBindingState state, CharacterData data, DataObject source)
        {
            if (!state.filledAppearance) state.filledAppearance = ScriptableObject.CreateInstance<FriendCharacterData>();
            state.filledAppearance.name = source ? source.name : "Friend appearance";
            state.filledAppearance.data = data;
            return state.filledAppearance;
        }
        // Cached once: the same original resident CharacterObjects the
        // BaseResident houses use (HouseDataSO), excluding shopkeepers on a
        // Work schedule and presets without a complete hair/top/bottom/shoes set.
        static List<CharacterData> OutfitPresets()
        {
            if (outfitPresets != null && outfitPresets.Count > 0) return outfitPresets;
            outfitPresets = Resources.FindObjectsOfTypeAll<HouseDataSO>().Where(h => h != null && h.Residents != null).SelectMany(h => h.Residents)
                .Where(r => r.ResidentData != null && r.ResidentData.CharacterData != null &&
                    (r.ResidentData.Schedule?.Entries == null || r.ResidentData.Schedule.Entries.All(e => e.Activity != ResidentActivityType.Work)))
                .Select(r => r.ResidentData.CharacterData).OfType<CharacterObject>().Distinct().OrderBy(c => c.name, StringComparer.Ordinal)
                .Select(c => { var d = c.GetCharacterData(); if (d != null && string.IsNullOrEmpty(d.characterName)) d = WithName(d, c.name); return d; })
                .Where(d => d?.outfitDatas != null && d.outfitDatas.Any(o => OutfitType(o.outfit) == "HairFront" || OutfitType(o.outfit) == "HairBack") &&
                    new[] { "Top", "Bottom", "Feet" }.All(t => d.outfitDatas.Any(o => OutfitType(o.outfit) == t)) &&
                    d.outfitDatas.Where(o => new[] { "HairFront", "HairBack", "Top", "Bottom", "Feet" }.Contains(OutfitType(o.outfit)))
                        .All(o => !string.IsNullOrEmpty(o.outfit) && Resources.Load<Outfit>(o.outfit)))
                .ToList();
            return outfitPresets;
        }
        static CharacterData WithName(CharacterData data, string name)
        { var copy = JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(data)); copy.characterName = name; return copy; }
        static bool Feminine(CharacterData data)
        {
            float Shape(string id) { int i = data.bodyIDs?.IndexOf(id) ?? -1; return i >= 0 && data.bodyShapes != null && i < data.bodyShapes.Count ? data.bodyShapes[i] : 0; }
            return Shape("BodyType") < 30 && Shape("Chest") > 20;
        }
        static string OutfitType(string resource)
        {
            string type = string.IsNullOrEmpty(resource) ? "" : resource.Split('/')[0];
            switch (type.ToLowerInvariant())
            {
                case "hairfront": return "HairFront"; case "hairback": return "HairBack";
                case "hat": return "Hat"; case "top": return "Top"; case "bottom": return "Bottom";
                case "overall": return "Overall"; case "feet": return "Feet"; default: return type;
            }
        }
        public static string FilledSlots(ResidentController actor)
        { var state = actor ? actor.GetComponent<FriendResidentBindingState>() : null; return state ? state.filledSlots : ""; }

        public static bool IsApplyingAppearance(ResidentController actor)
        {
            var state = actor ? actor.GetComponent<FriendResidentBindingState>() : null;
            return state && state.appearanceTask != null && !state.appearanceTask.IsCompleted;
        }
        public static DataObject DesiredAppearance(ResidentController actor)
        {
            var state = actor ? actor.GetComponent<FriendResidentBindingState>() : null;
            return state && state.desiredAppearance ? state.desiredAppearance : actor?.ResidentData?.CharacterData;
        }

        static async Task ApplyAppearanceAsync(ResidentController actor, DataObject appearance, CancellationToken token)
        {
            try
            {
                // Allow the unmodified resident startup customization to finish
                // before the additive generated-outfit load takes ownership.
                await Task.Yield(); await Task.Yield();
                float until = Time.realtimeSinceStartup + 40f;
                OutfitSystem outfit;
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    if (!actor) return;
                    outfit = actor.GetComponentInChildren<OutfitSystem>(true);
                    if (outfit && outfit.initalized && !actor.IsCustomizationApplying && !outfit.isloading && !outfit.isMerging && !outfit.isUpdatingTexture) break;
                    if (Time.realtimeSinceStartup > until) throw new TimeoutException("Friend outfit startup did not settle");
                    await Task.Yield();
                }
                if (FriendAppearancePort.HasGenerated(appearance.GetCharacterData()))
                {
                    FriendAppearancePort.MarkOwnedClone(outfit);
                    await FriendAppearancePort.Load(outfit, appearance.GetCharacterData(), token);
                }
                else
                {
                    // This path belongs to an already-owned NEW resident projection.
                    FriendAppearancePort.MarkOwnedClone(outfit);
                    await ResidentCustomization.ApplyToOutfitSystem(outfit, actor.ResidentData, actor, true);
                    // Separate task: appearanceTask must not stay "applying"
                    // while a friend is hidden at home for a long time.
                    _ = EnsureBodyCoverTagsAsync(actor, outfit, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { Debug.LogError("[FriendsLife] Owned resident appearance failed: " + error.Message, actor); }
        }

        // This add-on reapply runs without the native model-visibility hold and
        // can race the native customization pass, so the vendor body's
        // OutfitHideByTag (hides back/shoulder/chest sections under tagged
        // tops) may be left unsubscribed/unapplied. The merged body then keeps
        // the UnderUpper undershirt sections that poke through the T-shirt.
        // Once the rig is visible and settled, re-bind the vendor extension
        // (public OnDestroy/Initalize = idempotent unsubscribe+subscribe),
        // reapply the tags and re-merge only if a section actually changed.
        static async Task EnsureBodyCoverTagsAsync(ResidentController actor, OutfitSystem system, CancellationToken token)
        {
            try
            {
                int settled = 0;
                while (settled < 3)
                {
                    token.ThrowIfCancellationRequested();
                    if (!actor || !system) return;
                    bool ready = system.gameObject.activeInHierarchy && system.initalized && !system.isloading && !system.isMerging &&
                        !system.isUpdatingTexture && !actor.IsCustomizationApplying;
                    settled = ready ? settled + 1 : 0;
                    await Task.Yield();
                }
                if (!actor || !system) return;
                var body = system.GetOutfit("Body");
                if (!body || body.skinnedRenderers == null) return;
                var hides = body.GetComponents<OutfitHideByTag>();
                if (hides.Length == 0) return;
                var parts = body.skinnedRenderers;
                var before = new bool[parts.Length];
                for (int i = 0; i < parts.Length; i++) before[i] = parts[i] && parts[i].gameObject.activeSelf;
                foreach (var hide in hides) { hide.OnDestroy(); hide.Initalize(system, body); }
                system.AddTags();
                bool changed = false;
                for (int i = 0; i < parts.Length && !changed; i++) changed = (parts[i] && parts[i].gameObject.activeSelf) != before[i];
                if (changed) await system.MergeCharacter(token);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { Debug.LogWarning("[FriendsLife] Friend body cover-tag repair skipped: " + error.Message, actor); }
        }

        public static bool HasArrivedAt(ResidentController actor, Vector3 position, float tolerance = .2f)
        {
            if (!actor || actor.Brain == null) return false;
            var agent = actor.GetComponent<NavMeshAgent>();
            return agent && agent.enabled && agent.isOnNavMesh && !agent.pathPending &&
                (!agent.hasPath || agent.pathStatus == NavMeshPathStatus.PathComplete) &&
                Vector3.Distance(actor.transform.position, position) <= Mathf.Max(.05f, tolerance) &&
                agent.velocity.sqrMagnitude <= .0025f && actor.Brain.Movement.HasArrived();
        }
    }
}
