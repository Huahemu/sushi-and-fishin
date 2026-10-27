using UnityEngine;

namespace SushiAndFish.FishingIntegration
{
    // Horizontal travel permission only. Native swimming, oxygen consumption,
    // pressure/equipment requirements and outer-map triggers remain active.
    public static class ArchipelagoTravelArea
    {
        public const float CorridorHalfWidth = 60f;
        public const float MinimumDepthOffset = -300f;
        public const float MaximumHeightOffset = 64f;

        static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);

        public static bool AllowsCurrentWorld(Vector3 point)
        {
            if (!FishingMigration.Enabled) return false;
            var world = FishingMigration.Instance;
            // Also cover initial loading and a slot rebuild, before the new
            // islands have been instantiated. Resources returns the same data
            // used by their normal runtime bootstrap; no saved state is edited.
            var settings = world.Archipelago ? world.Archipelago.Settings : ArchipelagoSettings.Load();
            return Contains(settings, point, world.SeaHeight);
        }

        public static bool Contains(ArchipelagoSettings settings, Vector3 point, float seaHeight)
        {
            if (!settings || !settings.enabled || settings.regions == null || settings.regions.Length < 2 ||
                !Finite(point) || !float.IsFinite(seaHeight) || point.y < seaHeight + MinimumDepthOffset || point.y > seaHeight + MaximumHeightOffset)
                return false;

            var p = new Vector2(point.x, point.z);
            for (int i = 1; i < settings.regions.Length; i++)
            {
                var region = settings.regions[i];
                if (!ValidIsland(region)) continue;
                float radius = region.fishingRadius;
                if ((p - new Vector2(region.center.x, region.center.z)).sqrMagnitude <= radius * radius) return true;
            }
            for (int i = 0; i < settings.regions.Length; i++)
            {
                var a = settings.regions[i];
                if (!ValidDock(a) || i > 0 && !ValidIsland(a)) continue;
                for (int j = i + 1; j < settings.regions.Length; j++)
                {
                    var b = settings.regions[j];
                    if (!ValidIsland(b) || !ValidDock(b)) continue;
                    if (WithinCorridor(p, new Vector2(a.dock.x, a.dock.z), new Vector2(b.dock.x, b.dock.z), CorridorHalfWidth)) return true;
                }
            }
            return false;
        }

        static bool ValidDock(FishingIslandRegion region) => region != null && Finite(region.dock);
        static bool ValidIsland(FishingIslandRegion region) => region != null && region.prefab && Finite(region.center) &&
            float.IsFinite(region.fishingRadius) && region.fishingRadius > 0;

        public static bool WithinCorridor(Vector2 point, Vector2 start, Vector2 end, float halfWidth)
        {
            var line = end - start;
            float t = line.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector2.Dot(point - start, line) / line.sqrMagnitude) : 0;
            return (point - (start + line * t)).sqrMagnitude <= halfWidth * halfWidth;
        }
    }
}
