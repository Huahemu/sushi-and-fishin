using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SushiAndFish.FriendsLife
{
    // A local interaction proxy belongs ONLY to a newly created friend clone.
    // It follows the real authored capsule and uses the original Interactor's
    // raycast, layer mask, distance, gaze selection and E event dispatch. There
    // is no global keyboard listener and no original resident method override.
    public sealed class FriendWorldInteractable : GenericInteractable, IBasicInteractable
    {
        public ResidentController Actor { get; private set; }
        public string PersonId { get; private set; }
        public int InputCount { get; private set; }
        CapsuleCollider source, capsule;
        FriendWorldInteractLogic logic;
        public new bool CanGaze => Available();
        public new bool CanInteractable => Available();

        public void Bind(ResidentController actor, string person, CapsuleCollider actualCapsule)
        {
            Actor = actor; PersonId = person; source = actualCapsule;
            capsule = GetComponent<CapsuleCollider>(); if (!capsule) capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true; capsule.direction = source.direction; capsule.height = source.height;
            capsule.center = source.center; capsule.radius = source.radius + .015f;
            gameObject.layer = source.gameObject.layer; logic = new FriendWorldInteractLogic(this); Follow();
        }
        bool Available() => Available(null, false);
        // Reuse only the live body read by this same synchronous Follow call.
        bool Available(Transform observedBody, bool bodyAlreadyObserved)
        {
            var runtime = FriendsLifeRuntime.Instance;
            if (!runtime || !runtime.Ready || !Actor || !source || !Actor.gameObject.activeInHierarchy || !FriendResidentBindings.IsOwned(Actor)) return false;
            if (!(bodyAlreadyObserved ? observedBody : FriendWorldTalkAuthority.VisibleBody(runtime, PersonId)) || Actor.IsCustomizationApplying || FriendResidentBindings.IsApplyingAppearance(Actor)) return false;
            var state = Actor.Runtime;
            if (state == null) return false; // Busy activities retain their source owner; E queues one talk request.
            if (!Singleton<PlayersManager>.TryGetInstance(out var players) || !players.LocalPlayer || !players.LocalPlayer.IsOwner) return false;
            var player = players.LocalPlayer; var interactor = player.Interactor;
            // Holding an item is allowed: E -> 送礼 gives the item in hand.
            if (!interactor || !interactor.InteractorStatus || interactor.IsUsing || interactor.IsUseRequestPending || interactor.OnPlacement || interactor.OnThrow) return false;
            if (player.IsToolActive || player.HasSurfboard || player.IsTabletActive || player.DivingEquipment && player.DivingEquipment.IsKnocked) return false;
            if (Singleton<GameUIManager>.TryGetInstance(out var ui) && (ui.IsShopUIActive || ui.DialoguePanelController && ui.DialoguePanelController.IsVisible || ui.PauseScreen && ui.PauseScreen.IsVisible)) return false;
            return runtime.GetPerson(PersonId)?.controllerKind == PersonControllerKind.Npc && runtime.ActorOf(PersonId) == Actor;
        }
        void Follow()
        {
            if (!source) return;
            var body = FriendWorldTalkAuthority.VisibleBody(FriendsLifeRuntime.Instance, PersonId);
            if (!body) { if (capsule) capsule.enabled = false; return; }
            transform.SetPositionAndRotation(body.position, body.rotation);
            transform.localScale = body.lossyScale;
            if (capsule) capsule.enabled = Available(body, true);
        }
        void LateUpdate() { if (!Actor || !source) { Destroy(gameObject); return; } Follow(); }
        public new void GazeEnter() { logic?.GazeEnter(this); }
        public new void GazeExit() { logic?.GazeExit(this); }
        public new void TriggerInput(Interactor interactor, InputKind kind, InputActionPhase phase) { logic?.TriggerInput(this, interactor, kind, phase); }
        public new void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind kind, InputActionPhase phase) { logic?.TriggerCrossInput(this, interactor, held, kind, phase); }
        internal void Open(Interactor interactor)
        {
            if (!Available() || !interactor || !interactor.Player || !interactor.Player.IsOwner) return;
            if (Vector3.Distance(interactor.Player.transform.position, transform.position) > interactor.InteractRange + capsule.bounds.extents.magnitude) return;
            InputCount++; FriendDialoguePresenter.ShowWorldInteraction(PersonId);
        }
    }
    [Serializable]
    public sealed class FriendWorldInteractLogic : IGenericInteractLogic
    {
        readonly FriendWorldInteractable target;
        public FriendWorldInteractLogic(FriendWorldInteractable owner) { target = owner; }
        public bool CanGaze => target && target.CanGaze;
        public bool CanInteractable => target && target.CanInteractable;
        public void GazeEnter(GenericInteractable owner) { if (CanGaze && Singleton<HintsManager>.TryGetInstance(out var hints)) hints.AddHint("Interact", "key_interact"); }
        public void GazeExit(GenericInteractable owner) { if (Singleton<HintsManager>.TryGetInstance(out var hints)) hints.RemoveHint("Interact"); }
        public void TriggerInput(GenericInteractable owner, Interactor interactor, InputKind kind, InputActionPhase phase)
        { if (kind == InputKind.Interact && phase == InputActionPhase.Started && CanInteractable) target.Open(interactor); }
        // With an item in hand E opens the friend menu; other keys keep the
        // item's own action (place, throw) as if no friend were gazed.
        public void TriggerCrossInput(GenericInteractable owner, Interactor interactor, BaseHoldable held, InputKind kind, InputActionPhase phase)
        {
            if (kind == InputKind.Interact) { if (phase == InputActionPhase.Started && CanInteractable) target.Open(interactor); }
            else if (interactor) interactor.CurrentHeldTrigger(kind, phase);
        }
    }
    public sealed class FriendWorldInteractInstaller : MonoBehaviour
    {
        static FriendWorldInteractInstaller instance;
        readonly Dictionary<int, FriendWorldInteractable> proxies = new Dictionary<int, FriendWorldInteractable>();
        float next;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() { instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance) return;
            var go = new GameObject("Incremental friend world interaction"); DontDestroyOnLoad(go); instance = go.AddComponent<FriendWorldInteractInstaller>();
        }
        void Update()
        {
            if (Time.unscaledTime < next) return; next = Time.unscaledTime + .5f;
            var runtime = FriendsLifeRuntime.Instance; if (!runtime || !runtime.Ready) return;
            foreach (var person in runtime.People.Where(p => p.controllerKind == PersonControllerKind.Npc && !p.existingHostResident))
            {
                var actor = runtime.ActorOf(person.personId); if (!actor || !FriendResidentBindings.IsOwned(actor)) continue;
                int key = actor.GetInstanceID(); if (proxies.TryGetValue(key, out var existing) && existing) continue;
                var actual = actor.GetComponent<CapsuleCollider>(); if (!actual) continue;
                // Keep the proxy separate from the actor's NetworkObject tree.
                // The original raycast would otherwise prefer its parent
                // BaseInteractable over an IBasicInteractable on the same hit.
                var go = new GameObject("Friend interaction " + person.personId);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, actor.gameObject.scene);
                var proxy = go.AddComponent<FriendWorldInteractable>(); proxy.Bind(actor, person.personId, actual); proxies[key] = proxy;
            }
            foreach (int key in proxies.Where(p => !p.Value).Select(p => p.Key).ToArray()) proxies.Remove(key);
        }
        void OnDestroy() { foreach (var proxy in proxies.Values) if (proxy) Destroy(proxy.gameObject); proxies.Clear(); if (instance == this) instance = null; }
    }
}
