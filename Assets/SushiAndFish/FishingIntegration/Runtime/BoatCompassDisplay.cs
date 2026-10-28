using System;
using UnityEngine;

namespace SushiAndFish.FishingIntegration
{
    // The recovered boat retains its authored world-space radar anchor.
    // Local XY is the screen face, with the viewer on the negative Z side.
    // This display has no camera, RenderTexture, raycaster or physics component.
    public sealed class BoatCompassDisplay : IDisposable
    {
        public const string AnchorPath = "Boat/VisualPhysicsBoat/VisualBoat/Radar/MapCanvas";
        public const float PixelScale = .00075f;
        OriginalBoatRig boat;
        public Canvas Screen { get; private set; }
        public SailingCompass Compass { get; private set; }

        public void Render(OriginalBoatRig vessel, MigrationConfig config, bool visible, FishingIslandRegion[] regions)
        {
            if (boat != vessel || (vessel && !Screen))
            {
                Dispose();
                boat = vessel;
                if (vessel) Attach(vessel, config);
            }
            if (!Screen) return;
            Screen.enabled = visible;
            if (visible) Compass.Render(vessel.transform.position, vessel.transform.eulerAngles.y, regions);
        }

        void Attach(OriginalBoatRig vessel, MigrationConfig config)
        {
            var anchor = vessel.transform.Find(AnchorPath);
            if (!anchor) return;
            var root = new GameObject("Original radar compass screen", typeof(RectTransform), typeof(Canvas));
            root.layer = vessel.gameObject.layer;
            root.transform.SetParent(anchor, false);
            var rect = (RectTransform)root.transform;
            rect.localPosition = new Vector3(0, 0, -.004f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * PixelScale;
            rect.sizeDelta = new Vector2(352, 264);
            Screen = root.GetComponent<Canvas>();
            Screen.renderMode = RenderMode.WorldSpace;
            Screen.enabled = false;
            var dial = new GameObject("Compass directions and island dots", typeof(RectTransform));
            dial.transform.SetParent(root.transform, false);
            Compass = dial.AddComponent<SailingCompass>();
            Compass.Build(config);
            Compass.Show(true);
        }

        public void Dispose()
        {
            if (Screen)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(Screen.gameObject);
                else UnityEngine.Object.DestroyImmediate(Screen.gameObject);
            }
            Screen = null;
            Compass = null;
            boat = null;
        }
    }
}
