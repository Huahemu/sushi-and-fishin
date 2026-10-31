using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.ResidentialIsland;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // Additional homes have their own permission guard. No original exclusion
    // list, resolver, waypoint, schedule or original guard component is patched.
    [DefaultExecutionOrder(31000)]
    public sealed class FriendHousingPrivacy : MonoBehaviour
    {
        readonly List<HousingHouse> homes = new List<HousingHouse>();
        float next;
        bool Authority => FriendsLifeRuntime.Instance && FriendsLifeRuntime.Instance.IsAuthority;
        // Every scene resident, original or added friend, registers with the
        // original ResidentManager; reading it avoids a ~4 ms scene search.
        static ResidentController[] Residents()
            => PaperPixelGames.Framework.Patterns.Singleton<ResidentManager>.TryGetInstance(out var manager)
                ? manager.Residents.Where(r => r && r.gameObject.activeInHierarchy).ToArray()
                : FindObjectsByType<ResidentController>(FindObjectsSortMode.None);
        IEnumerable<HousingHouse> AllHomes => homes.Where(h => h && h.definition != null).Concat(
            (FriendHousingIsland.Instance ? FriendHousingIsland.Instance.Plots : new List<HousingPlot>()).Where(p => p.house && p.house.definition != null).Select(p => p.house)).Distinct();
        public void Register(HousingHouse house)
        { if (house && FriendHouseMetadata.For(house).friendsOwnedClone && !homes.Contains(house)) homes.Add(house); }
        public bool Excludes(Vector3 position, float margin = .55f) => AllHomes.Any(h => HousingPrivacyVolume.Of(h).Contains(position, margin));
        bool Allowed(HousingHouse house, ResidentController actor)
        {
            var economy = FriendsEconomyService.Instance;
            return economy && actor && FriendResidentBindings.HasBinding(actor) && economy.CanEnter(house, economy.PersonId(actor));
        }
        public bool ExcludesFor(ResidentController actor, Vector3 position, float margin = .55f)
            => AllHomes.Any(h => !Allowed(h, actor) && HousingPrivacyVolume.Of(h).Contains(position, margin));
        // The original Work state plans a public walk around these same
        // private volumes; ownership and physical ejection remain unchanged.
        internal HousingPrivacyVolume[] ExcludedVolumesForWork(ResidentController actor)
            => AllHomes.Where(h => !Allowed(h, actor)).Select(HousingPrivacyVolume.Of).ToArray();
        public bool ExcludesPathFor(ResidentController actor, NavMeshPath path, float margin = .55f)
        {
            if (!actor || path == null || path.status != NavMeshPathStatus.PathComplete) return true;
            foreach (var home in AllHomes)
            {
                if (Allowed(home, actor)) continue;
                var size = home.definition.footprint;
                var bounds = new Bounds(new Vector3(0, 2.5f, 0), new Vector3(size.x + margin * 2, 11, size.y + margin * 2));
                var previous = home.transform.InverseTransformPoint(actor.transform.position);
                if (bounds.Contains(previous)) return true;
                foreach (var corner in path.corners)
                {
                    var point = home.transform.InverseTransformPoint(corner); var delta = point - previous; float length = delta.magnitude;
                    if (bounds.Contains(point) || length > .0001f && bounds.IntersectRay(new Ray(previous, delta / length), out var hit) && hit <= length + .0001f) return true;
                    previous = point;
                }
            }
            return false;
        }
        public bool TrySafePosition(Vector3 origin, out Vector3 position)
            => HousingNpcLanding.Nearest(origin, AllHomes.Select(HousingPrivacyVolume.Of).ToList(), null, null, out position);
        public bool RelocateForPlacement(HousingPlot plot, HousingType type, float yaw)
        {
            if (!Authority) return false;
            var proposed = new HousingPrivacyVolume { center = plot.Center, rotation = Quaternion.Euler(0, yaw, 0), size = type.footprint };
            var volumes = AllHomes.Select(HousingPrivacyVolume.Of).ToList(); volumes.Add(proposed);
            var moves = new List<(ResidentController actor, Vector3 point)>(); var reserved = new List<Vector3>();
            foreach (var actor in Residents().OrderBy(a => a.ResidentId))
            {
                if (!proposed.Contains(actor.transform.position, .6f)) continue;
                if (!HousingNpcLanding.Nearest(actor.transform.position, volumes, actor, reserved, out var safe)) return false;
                moves.Add((actor, safe)); reserved.Add(safe);
            }
            foreach (var move in moves)
            {
                if (move.actor.Brain == null) { move.actor.transform.position = move.point; continue; }
                move.actor.Brain.Movement.Stop();
                if (!move.actor.Brain.Movement.Warp(move.point, move.actor.transform.rotation)) return false;
                if (move.actor.Runtime != null) { move.actor.Runtime.TargetDestination = move.point; move.actor.Runtime.HasValidTarget = false; }
            }
            return true;
        }
        public void ProtectNow()
        {
            if (!Authority) return;
            homes.RemoveAll(h => !h);
            if (homes.Count == 0) return;
            // Volumes once per pass; the cheap containment test runs before the
            // costly permission lookup, which only matters for actors inside.
            var guarded = homes.Select(h => (house: h, volume: HousingPrivacyVolume.Of(h))).ToList();
            foreach (var actor in Residents())
            {
                // Baseline houses retain the original guard. This guard only
                // ejects an unauthorized actor from a newly created home.
                var position = actor.transform.position;
                if (!guarded.Any(g => g.volume.Contains(position, .15f) && !Allowed(g.house, actor))) continue;
                if (!HousingNpcLanding.Nearest(actor.transform.position, AllHomes.Select(HousingPrivacyVolume.Of).ToList(), actor, null, out var safe)) continue;
                if (actor.Brain != null)
                {
                    actor.Brain.Movement.Stop(); actor.Brain.Movement.Warp(safe, actor.transform.rotation);
                    if (actor.Runtime != null) { actor.Runtime.TargetDestination = safe; actor.Runtime.HasValidTarget = false; actor.Runtime.HasArrived = false; }
                }
                else actor.transform.position = safe;
            }
        }
        void LateUpdate()
        { if (Time.unscaledTime < next) return; next = Time.unscaledTime + .5f; ProtectNow(); }
    }
}
