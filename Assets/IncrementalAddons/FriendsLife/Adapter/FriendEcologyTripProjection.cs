using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class EcologyTripColliderState
    { public Collider collider; public bool enabled; }

    // Pure world projection of an already committed original R travel session.
    // No trip/clock/input/reward, position, or original schedule is fabricated.
    public sealed class EcologyTripAbsence : MonoBehaviour
    {
        public string worldId, personId, tripId, leaseId;
        public bool sourceAuthority, previousVisible;
        public List<EcologyTripColliderState> colliders = new List<EcologyTripColliderState>();
        ResidentController actor;
        public void Bind(FriendsLifeRuntime runtime, ResidentController body, string person, string trip)
        {
            actor = body; worldId = runtime.State.worldId; personId = person; tripId = trip;
            sourceAuthority = runtime.IsAuthority; leaseId = "source-trip/" + trip + "/" + person;
            previousVisible = body.VisualAnimator && body.VisualAnimator.gameObject.activeSelf;
            foreach (var c in body.GetComponentsInChildren<Collider>(true))
            { colliders.Add(new EcologyTripColliderState { collider = c, enabled = c.enabled }); c.enabled = false; }
            if (sourceAuthority)
            {
                body.Brain.Pause();
                runtime.State.leases.Add(new ActivityLease { leaseId = leaseId, personId = person, ownerEpisode = "source-trip/" + trip,
                    facilityId = "OriginalRTravel", phase = "Away", safeBoundary = false });
                runtime.NotifyChanged();
            }
            body.Brain.SetModelVisible(false);
        }
        void LateUpdate()
        {
            var runtime = FriendsLifeRuntime.Instance;
            if (!runtime || !runtime.Ready || runtime.State.worldId != worldId) return;
            if (!actor) actor = GetComponent<ResidentController>();
            if (!actor || actor.Brain == null || runtime.ActorOf(personId) != actor) return;
            bool away = runtime.Core.State.activities?.travels.Any(t => t.id == tripId && !t.returned) == true;
            if (away)
            {
                actor.Brain.SetModelVisible(false);
                foreach (var c in colliders) if (c.collider) c.collider.enabled = false;
                return;
            }
            Restore(runtime);
        }
        void Restore(FriendsLifeRuntime runtime)
        {
            // An unrelated source owner must finish first. This component never
            // resumes a job proxy, food/chair owner, or a new world incarnation.
            if (runtime.State.leases.Any(l => l.personId == personId && l.leaseId != leaseId) ||
                FriendsEconomyService.Instance?.HasActiveJob(personId) == true || GetComponent<FriendResidentDormancy>()) return;
            foreach (var c in colliders) if (c.collider) c.collider.enabled = c.enabled;
            actor.Brain.SetModelVisible(previousVisible);
            if (sourceAuthority && runtime.IsAuthority)
            {
                runtime.State.leases.RemoveAll(l => l.leaseId == leaseId);
                actor.Brain.Resume(); actor.Brain.RequestScheduleChange(); runtime.NotifyChanged();
            }
            Destroy(this);
        }
    }

    [DefaultExecutionOrder(-115)]
    public sealed class FriendEcologyTripProjection : MonoBehaviour
    {
        static FriendEcologyTripProjection instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() { instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install()
        { if (!instance) new GameObject("Original R trip world projection").AddComponent<FriendEcologyTripProjection>(); }
        void Awake() { if (instance && instance != this) { Destroy(gameObject); return; } instance = this; DontDestroyOnLoad(gameObject); }
        void Update()
        {
            var runtime = FriendsLifeRuntime.Instance;
            if (!runtime || !runtime.Ready || runtime.IsAuthority && !runtime.IsWorldRunning) return;
            foreach (var trip in runtime.Core.State.activities?.travels.Where(t => !t.returned).ToArray() ?? Array.Empty<TomodachiLife.Simulation.LifeTravelSession>())
                foreach (string native in new[] { trip.residentId, trip.partnerId }.Where(id => !string.IsNullOrEmpty(id)).Distinct())
                {
                    var person = runtime.PersonFromNative(native);
                    if (person?.controllerKind != PersonControllerKind.Npc) continue; // Human body/controller is never taken over.
                    var actor = runtime.ActorOf(person.personId);
                    if (!actor || actor.Brain == null || !FriendResidentBindings.IsOwned(actor) || actor.GetComponent<EcologyTripAbsence>() ||
                        actor.GetComponent<FriendResidentDormancy>() || actor.IsCustomizationApplying || FriendResidentBindings.IsApplyingAppearance(actor) ||
                        FriendsEconomyService.Instance?.HasActiveJob(person.personId) == true ||
                        runtime.State.leases.Any(l => l.personId == person.personId && l.ownerEpisode != "source-trip/" + trip.id)) continue;
                    // Restore the same authoritative lease after normal reload;
                    // projection rebuild is not another StartTravel or payment.
                    if (runtime.IsAuthority) runtime.State.leases.RemoveAll(l => l.leaseId == "source-trip/" + trip.id + "/" + person.personId);
                    actor.gameObject.AddComponent<EcologyTripAbsence>().Bind(runtime, actor, person.personId, trip.id);
                }
        }
    }
}
