using System;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // Owns only one NEW friend's short physical front-opening traversal. The
    // parent owns the lease, Brain pause, contract, original service and wages.
    // No original obstacle, agent type, collider, mesh or service is modified.
    [DefaultExecutionOrder(200)]
    public sealed class FriendCasinoCabinMotor : MonoBehaviour
    {
        const float MaximumStep = .05f;
        const float EndpointTolerance = .025f;
        const float FootTolerance = .055f;
        public string Owner { get; private set; }
        public ResidentController Actor { get; private set; }
        public Vector3 Gate { get; private set; }
        public Vector3 Duty { get; private set; }
        public string Phase { get; private set; } = "Waiting";
        public string Failure { get; private set; }
        public float DistanceWalked { get; private set; }
        public bool OriginalAgentEnabled => previousEnabled;
        public bool Arrived => Phase == "Arrived" && AtPhysicalEndpoint(Duty);
        public bool Exited => Phase == "Exited" && AtPhysicalEndpoint(Gate) && AgentBound();
        public bool SafeBoundary => Arrived || Exited;
        public bool CanForget => Exited;

        Transform originalNpc;
        NavMeshAgent agent;
        FriendGroundRoutePhysics physics;
        bool exiting, previousEnabled, previousUpdatePosition, previousUpdateRotation;
        int originalAgentType, synchronizationFrame, lastMoveFrame = -1;
        Vector3 synchronizationBody, lastVelocity;
        float lastEffectiveDelta;

        public static bool TryBegin(ResidentController actor, string owner, Transform originalNpc,
            Vector3 gate, Vector3 duty, bool exiting, out FriendCasinoCabinMotor motor, out string failure)
        {
            motor = null; failure = null;
            if (!actor || string.IsNullOrEmpty(owner)) { failure = "actor-or-owner-missing"; return false; }
            var existing = actor.GetComponent<FriendCasinoCabinMotor>();
            if (existing && !string.IsNullOrEmpty(existing.Owner))
            {
                if (!string.Equals(existing.Owner, owner, StringComparison.Ordinal))
                { failure = "physical-motor-owned-by-another-journey"; return false; }
                motor = existing;
                if (exiting && !motor.Exited) motor.RequestExit();
                failure = motor.Failure;
                return true;
            }
            var runtime = FriendsLifeRuntime.Instance;
            if (!runtime || !runtime.Ready || !runtime.IsAuthority || !runtime.IsWorldRunning ||
                !FriendResidentBindings.IsOwned(actor) || actor.Brain == null || !originalNpc)
            { failure = "owned-authority-actor-or-original-source-unavailable"; return false; }
            var actualAgent = actor.GetComponent<NavMeshAgent>();
            if (!actualAgent) { failure = "original-agent-missing"; return false; }
            if (!ValidateSegment(actor, originalNpc, gate, duty, out failure)) return false;
            FriendGroundRoutePhysics checkedPhysics;
            try { checkedPhysics = new FriendGroundRoutePhysics(actor, originalNpc); }
            catch (Exception error) { failure = error.GetBaseException().Message; return false; }
            if (!ValidateTraversal(checkedPhysics, actor.transform.position, exiting ? gate : duty, out failure)) return false;
            motor = existing ? existing : actor.gameObject.AddComponent<FriendCasinoCabinMotor>();
            motor.Actor = actor; motor.Owner = owner; motor.originalNpc = originalNpc;
            motor.Gate = gate; motor.Duty = duty; motor.agent = actualAgent; motor.physics = checkedPhysics;
            motor.previousEnabled = actualAgent.enabled;
            motor.previousUpdatePosition = actualAgent.updatePosition;
            motor.previousUpdateRotation = actualAgent.updateRotation;
            motor.originalAgentType = actualAgent.agentTypeID;
            motor.exiting = exiting; motor.Failure = null;
            // Disable only this owned projection's agent. No MoveTo/TrySnap/Warp
            // can run inside the hole; the parent keeps its Brain paused/leased.
            if (actualAgent.enabled && actualAgent.isOnNavMesh) actor.Brain.Movement.Stop();
            actualAgent.enabled = false;
            motor.Phase = exiting ? "Exiting" : "Entering";
            return true;
        }

        public static bool ValidateSegment(ResidentController actor, Transform originalNpc,
            Vector3 a, Vector3 b, out string reason)
        {
            reason = null;
            if (!actor || !originalNpc || !actor.GetComponent<NavMeshAgent>() || !Finite(a) || !Finite(b))
            { reason = "physical-segment-source-or-coordinate-invalid"; return false; }
            try
            {
                var shape = new FriendGroundRoutePhysics(actor, originalNpc);
                if (!ValidatePoint(shape, a, true, out reason) || !ValidatePoint(shape, b, true, out reason)) return false;
                return SweepSegment(shape, a, b, out reason);
            }
            catch (Exception error) { reason = error.GetBaseException().Message; return false; }
        }

        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) &&
            !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);

        static bool ValidateTraversal(FriendGroundRoutePhysics shape, Vector3 a, Vector3 b, out string reason)
        {
            // A restored or blocked body can be partway through a permitted
            // continuous climb. Preserve that actual pose while validating its
            // reverse; only the final duty/gate endpoint must be grounded.
            if (!Finite(a) || !Finite(b)) { reason = "physical-traversal-nonfinite"; return false; }
            if (!ValidatePoint(shape, a, false, out reason) || !ValidatePoint(shape, b, true, out reason)) return false;
            return SweepSegment(shape, a, b, out reason);
        }

        static bool ValidatePoint(FriendGroundRoutePhysics shape, Vector3 point, bool endpoint, out string reason)
        {
            reason = null;
            if (!shape.Foot(point, point.y + shape.Climb + .005f, out var floor))
            { reason = "physical-floor-missing@" + point.ToString("F4"); return false; }
            float delta = point.y - floor.point.y;
            // A foot may rise by the source climb allowance during a continuous
            // step, but neither endpoint can be a hovering original-NPC pivot.
            if (delta < -.005f || delta > (endpoint ? FootTolerance : shape.Climb + .005f))
            { reason = "physical-foot-height@" + point.ToString("F4") + "/delta=" + delta; return false; }
            if (!shape.Clear(point))
            { reason = "physical-capsule-blocked@" + point.ToString("F4"); return false; }
            return true;
        }

        static bool SweepSegment(FriendGroundRoutePhysics shape, Vector3 a, Vector3 b, out string reason)
        {
            reason = null;
            float length = Vector3.Distance(a, b);
            if (!Finite(a) || !Finite(b) || float.IsNaN(length) || float.IsInfinity(length))
            { reason = "physical-segment-nonfinite"; return false; }
            int count = Mathf.Max(1, Mathf.CeilToInt(length / MaximumStep));
            if (count > 4096) { reason = "physical-segment-outside-short-traversal-scope"; return false; }
            Vector3 previous = a;
            if (!ValidatePoint(shape, previous, false, out reason)) return false;
            for (int i = 1; i <= count; i++)
            {
                Vector3 point = Vector3.Lerp(a, b, (float)i / count);
                if (!ValidatePoint(shape, point, false, out reason)) return false;
                var displacement = point - previous;
                float distance = displacement.magnitude;
                if (distance > .000001f)
                {
                    var hits = Physics.CapsuleCastAll(previous + Vector3.up * (shape.Radius + .04f),
                        previous + Vector3.up * (shape.Height - shape.Radius + .04f), shape.Radius,
                        displacement / distance, distance, ~0, QueryTriggerInteraction.Ignore);
                    var hit = hits.FirstOrDefault(h => h.collider && !h.collider.transform.IsChildOf(shape.Actor.transform));
                    if (hit.collider)
                    { reason = "physical-sweep-blocked/" + hit.collider.name + "@" + point.ToString("F4"); return false; }
                }
                previous = point;
            }
            return true;
        }

        void Update()
        {
            lastVelocity = Vector3.zero; lastEffectiveDelta = 0;
            var runtime = FriendsLifeRuntime.Instance;
            if (string.IsNullOrEmpty(Owner) || !runtime || !runtime.Ready || !runtime.IsAuthority || !runtime.IsWorldRunning) return;
            if (!FriendCasinoGroundNavigation.OriginalGroundAvailable()) return;
            if (!Actor || Actor.Brain == null || !agent || !originalNpc)
            { Block("owned-motor-source-became-unavailable"); return; }
            var identity = Actor.GetComponent<FriendWorldActor>();
            if (!identity || runtime.ActorOf(identity.personId) != Actor ||
                !runtime.State.leases.Any(lease => lease.leaseId == "casino-cabin/" + Owner &&
                    lease.ownerEpisode == Owner && lease.personId == identity.personId))
            { Block("parent-owned-cabin-lease-missing-or-changed"); return; }
            if (Actor.GetComponents<FriendCasinoCabinMotor>().Any(other => other != this &&
                !string.IsNullOrEmpty(other.Owner) && !other.CanForget))
            { Block("another-physical-motor-owns-this-actor"); return; }
            if (agent.agentTypeID != originalAgentType)
            { Block("original-agent-type-changed-externally"); return; }
            if (Phase == "GateSync" || Phase == "GateSyncReadback") { AdvanceGateSynchronization(); return; }
            if (Phase != "Entering" && Phase != "Exiting") return;
            if (agent.enabled) { Block("owned-agent-reenabled-before-physical-exit"); return; }
            float dt = Time.deltaTime, speed = Actor.Brain.EffectiveMoveSpeed;
            if (!(dt > 0) || !(speed > 0) || float.IsNaN(dt) || float.IsInfinity(dt) || float.IsNaN(speed) || float.IsInfinity(speed)) return;
            float budget = Mathf.Min(MaximumStep, speed * dt);
            if (!(budget > 0)) return;
            Vector3 before = Actor.transform.position, target = exiting ? Gate : Duty;
            float arrivalTolerance = exiting ? .001f : EndpointTolerance;
            if (Vector3.Distance(before, target) <= arrivalTolerance && AtPhysicalEndpoint(target))
            { if (exiting) BeginGateSynchronization(); else { Phase = "Arrived"; Failure = null; } return; }
            var wanted = Vector3.MoveTowards(before, target, budget);
            if (!physics.Foot(wanted, Mathf.Max(before.y, wanted.y) + physics.Climb + .005f, out var support))
            { Block("next-step-floor-missing"); return; }
            if (Mathf.Abs(support.point.y - before.y) > physics.Climb + .005f)
            { Block("next-step-exceeds-original-climb"); return; }
            var next = Vector3.MoveTowards(before, support.point, budget);
            if (exiting && Vector3.ProjectOnPlane(before - Gate, Vector3.up).magnitude <= MaximumStep &&
                ValidatePoint(physics, Gate, true, out _))
            {
                // The original NavMesh gate can be a few centimetres above
                // its source support. Reach that verified gate continuously;
                // projecting every final step back to support would deadlock.
                next = Vector3.MoveTowards(before, Gate, budget);
            }
            if (Vector3.Distance(next, before) > budget + .00001f)
            { Block("next-step-budget-exceeded"); return; }
            if (!SweepSegment(physics, before, next, out var reason))
            { Block(reason); return; }
            var horizontal = Vector3.ProjectOnPlane(next - before, Vector3.up);
            var rotation = horizontal.sqrMagnitude > .000001f
                ? Quaternion.RotateTowards(Actor.transform.rotation, Quaternion.LookRotation(horizontal, Vector3.up), 360f * dt)
                : Actor.transform.rotation;
            // Public placement is only the already swept <= budget step. Arrival
            // is checked separately against the actual body and real support.
            Actor.Brain.Movement.StopAtPose(next, rotation);
            Vector3 actual = Actor.transform.position;
            float walked = Vector3.Distance(before, actual);
            if (!Finite(actual) || walked > budget + .0001f || Vector3.Distance(actual, next) > .001f)
            { Block("public-step-placement-readback-mismatch"); return; }
            DistanceWalked += walked; lastMoveFrame = Time.frameCount; lastEffectiveDelta = dt;
            lastVelocity = (actual - before) / dt;
            if (Vector3.Distance(actual, target) <= arrivalTolerance && AtPhysicalEndpoint(target))
            { if (exiting) BeginGateSynchronization(); else { Phase = "Arrived"; Failure = null; } }
        }

        bool AtPhysicalEndpoint(Vector3 target)
        {
            if (!Actor || physics == null || !Finite(Actor.transform.position) || Vector3.Distance(Actor.transform.position, target) > EndpointTolerance) return false;
            return ValidatePoint(physics, Actor.transform.position, true, out _);
        }

        public void RequestExit()
        {
            if (string.IsNullOrEmpty(Owner) || Exited || Phase == "GateSync" || Phase == "GateSyncReadback") return;
            if (exiting && Phase == "Exiting") return;
            if (!Actor || !agent || agent.agentTypeID != originalAgentType)
            { Block("physical-exit-source-or-original-agent-type-unavailable"); return; }
            if (!originalNpc || physics == null)
            { Block("physical-exit-source-missing"); return; }
            if (!ValidateTraversal(physics, Actor.transform.position, Gate, out var reason))
            { Block("physical-exit-validation/" + reason); return; }
            if (agent.enabled && agent.isOnNavMesh) Actor.Brain.Movement.Stop();
            agent.enabled = false; exiting = true; Phase = "Exiting"; Failure = null;
        }

        void BeginGateSynchronization()
        {
            if (!AtPhysicalEndpoint(Gate) || Vector3.Distance(Actor.transform.position, Gate) > .001f)
            { Block("physical-return-gate-not-reached"); return; }
            var pose = Actor.transform.position;
            var filter = new NavMeshQueryFilter { agentTypeID = originalAgentType, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(pose + Vector3.up * .04f, out var nav, .15f, filter) || Vector3.Distance(nav.position, pose) > .001f)
            { Block("physical-return-gate-original-nav-not-continuously-reached"); return; }
            synchronizationBody = pose;
            agent.updatePosition = false; agent.updateRotation = false;
            agent.enabled = true;
            if (!agent.isOnNavMesh) { Block("physical-return-gate-agent-did-not-bind"); return; }
            agent.ResetPath(); agent.isStopped = true; agent.velocity = Vector3.zero;
            // Synchronizes only the simulation position to the body already at
            // the real gate. This is neither Warp nor a body-position assignment.
            agent.nextPosition = pose;
            if (!SynchronizationReadback()) { Block("physical-return-gate-initial-binding-mismatch"); return; }
            synchronizationFrame = Time.frameCount; Phase = "GateSync"; Failure = null;
        }

        bool AgentBound() => Actor && agent && agent.enabled && agent.agentTypeID == originalAgentType && agent.isOnNavMesh &&
            Vector3.Distance(agent.nextPosition, Actor.transform.position) <= .15f;

        bool SynchronizationReadback() => AtPhysicalEndpoint(Gate) && AgentBound() &&
            Vector3.Distance(Actor.transform.position, synchronizationBody) <= MaximumStep &&
            // Gate quantization is reached by continuous physical steps first;
            // restoring updatePosition cannot hide a larger simulation offset.
            Vector3.Distance(agent.nextPosition, Actor.transform.position) <= .001f;

        void AdvanceGateSynchronization()
        {
            if (Time.frameCount <= synchronizationFrame) return;
            if (!SynchronizationReadback()) { Block("physical-return-gate-synchronization-readback-failed"); return; }
            if (Phase == "GateSync")
            {
                agent.updatePosition = previousUpdatePosition; agent.updateRotation = previousUpdateRotation;
                synchronizationFrame = Time.frameCount; Phase = "GateSyncReadback";
                return;
            }
            // A restored traversal may start with the owned agent disabled.
            // Successful real exit deliberately leaves it enabled and bound;
            // previousEnabled is retained for diagnosis, never used to fake exit.
            Phase = "Exited"; Failure = null;
        }

        void Block(string reason)
        {
            Failure = reason; Phase = "Blocked"; lastVelocity = Vector3.zero;
            if (agent) agent.enabled = false;
            // Keep Owner and the parent lease. No resume, fallback teleport or
            // implicit actor release is performed on a blocked step/destruction.
        }

        public void Forget(string owner)
        {
            if (!CanForget || !string.Equals(Owner, owner, StringComparison.Ordinal)) return;
            Owner = null; Destroy(this);
        }

        void LateUpdate()
        {
            if (string.IsNullOrEmpty(Owner) || !Actor || lastMoveFrame != Time.frameCount || !(lastEffectiveDelta > 0)) return;
            var animator = Actor.VisualAnimator;
            if (!animator || !animator.gameObject.activeInHierarchy) return;
            float speed = lastVelocity.magnitude, sourceSpeed = Actor.Brain.EffectiveMoveSpeed;
            bool walking = speed > .05f;
            float forward = walking && sourceSpeed > .01f ? Mathf.Clamp01(speed / sourceSpeed) : 0;
            float turn = walking ? Mathf.Clamp(Actor.transform.InverseTransformDirection(lastVelocity.normalized).x, -1, 1) : 0;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == "IsWalking") animator.SetBool(parameter.nameHash, walking);
                if (parameter.type != AnimatorControllerParameterType.Float) continue;
                if (parameter.name == "Forward" || parameter.name == "Walk") animator.SetFloat(parameter.nameHash, forward);
                else if (parameter.name == "Turn") animator.SetFloat(parameter.nameHash, turn);
            }
        }
    }
}
