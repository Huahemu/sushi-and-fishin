using System.Collections.Generic;
using System.Linq;
using TomodachiLife.Simulation;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsLifeRuntime
    {
        sealed class RosterProjection
        {
            public List<LifeSimulationResident> full;
            public HashSet<string> parked;
        }
        public bool IsAwaitingArrival(string nativeResidentId)
        {
            var person = PersonFromNative(nativeResidentId);
            return person != null && person.controllerKind == PersonControllerKind.Npc && person.addedByPlayer && !person.arrivalGrantUsed;
        }
        RosterProjection ProjectWorldRoster(LifeSimulationRuntime kernel, string editableDraftNativeId = null)
        {
            var projection = new RosterProjection { full = kernel.State.residents,
                parked = new HashSet<string>(kernel.State.residents.Where(r => IsAwaitingArrival(r.id) && r.id != editableDraftNativeId).Select(r => r.id)) };
            kernel.State.residents = projection.full.Where(r => !projection.parked.Contains(r.id)).ToList();
            return projection;
        }
        static void RestoreRegisteredRoster(LifeSimulationRuntime kernel, RosterProjection projection)
        {
            var current = kernel.State.residents.ToDictionary(r => r.id);
            var registered = new HashSet<string>(projection.full.Select(r => r.id));
            // Keep registered order and draft records, retain original removals,
            // and append actual new residents produced by an original birth.
            kernel.State.residents = projection.full.Where(r => projection.parked.Contains(r.id) || current.ContainsKey(r.id))
                .Select(r => current.TryGetValue(r.id, out var updated) ? updated : r)
                .Concat(kernel.State.residents.Where(r => !registered.Contains(r.id))).ToList();
        }
        void AdvanceFrozenWorld(double realSeconds)
        {
            var kernel = Core;
            var projection = ProjectWorldRoster(kernel);
            // Preserve any pre-existing port and install our non-persistent
            // per-advance adapter only for this exact kernel invocation.
            var previous = kernel.BeforeResidentActivitySelection;
            System.Func<LifeSimulationResident,double,bool> scoped = (resident,minutes) =>
            { bool prior=previous?.Invoke(resident,minutes)==true; return FriendWorldTalkAuthority.AdvanceHeldActivityClock(resident,minutes)||prior; };
            try { kernel.BeforeResidentActivitySelection = scoped; kernel.Advance(realSeconds); }
            finally
            {
                if (ReferenceEquals(kernel.BeforeResidentActivitySelection,scoped)) kernel.BeforeResidentActivitySelection=previous;
                RestoreRegisteredRoster(kernel, projection);
            }
        }
        LifeSimulationResident CreateWorldResident(string name, string avatarJson = "")
        {
            var projection = ProjectWorldRoster(Core);
            try { return Core.CreateResident(name, avatarJson); }
            finally { RestoreRegisteredRoster(Core, projection); }
        }
        int ActiveWorldPopulation(LifeSimulationRuntime kernel) => kernel.State.residents.Count(r => !IsAwaitingArrival(r.id));
    }
}
