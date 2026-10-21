using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PaperPixelGames.Core.Tablet
{
    [DisallowMultipleComponent]
    public sealed class LiveSceneTabletMap : MonoBehaviour
    {
        [SerializeField] private int _rendererIndex;
        private Camera _camera;
        private RenderTexture _texture;
        private UnityEngine.UI.RawImage _image;
        private bool _visible;
        private bool _fogBefore;
        private bool _fogChanged;
        private readonly List<PaperPixelGames.Core.DressingSceneSystem.DressingRendererDistanceCuller> _pausedCullers = new List<PaperPixelGames.Core.DressingSceneSystem.DressingRendererDistanceCuller>();
        private readonly List<Renderer> _hiddenPlayerRenderers = new List<Renderer>();
        public Rect WorldBounds { get; private set; }
        public Camera MapCamera => _camera;
        public RenderTexture Texture => _texture;
        public int RenderedFrames { get; private set; }
        public bool IsUnderwater { get; private set; }

        public void Configure(UnityEngine.UI.Image background)
        {
            if (_camera != null) return;
            background.enabled = false;
            var view = new GameObject("LiveSceneView", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            view.transform.SetParent(background.transform, false);
            view.transform.SetAsFirstSibling();
            var rect = (RectTransform)view.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _image = view.GetComponent<UnityEngine.UI.RawImage>();
            _image.raycastTarget = true;
            var places=GetComponent<SushiAndFish.CityExpansion.CityMapPlaces>();
            if(!places)places=gameObject.AddComponent<SushiAndFish.CityExpansion.CityMapPlaces>();
            places.Configure(rect);
            _texture = new RenderTexture(2048, 1152, 24, RenderTextureFormat.ARGB32) { name = "CurrentSceneTabletMap", antiAliasing = 1 };
            _texture.Create(); _image.texture = _texture;
            var root = new GameObject("LiveTabletMapCamera") { hideFlags = HideFlags.DontSave };
            _camera = root.AddComponent<Camera>();
            _camera.enabled = false; _camera.orthographic = true;
            _camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            _camera.targetTexture = _texture; _camera.aspect = 16f / 9f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(.15f, .39f, .47f);
            _camera.nearClipPlane = .05f; _camera.farClipPlane = 1000;
            _camera.cullingMask = LayerMask.GetMask("Default", "Ignore Raycast", "Interactable", "Placeable", "Fish", "Resident", "Surface", "SeasoningSurface", "BossCaseWall", "BossCaseGround");
            var data = _camera.GetUniversalAdditionalCameraData();
            data.SetRenderer(_rendererIndex); data.renderPostProcessing = false;
            data.renderShadows = false; data.requiresColorOption = CameraOverrideOption.On;
            data.requiresDepthOption = CameraOverrideOption.On;
            data.volumeLayerMask = 0;
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
        }

        public Rect SetLayer(MapLayer layer)
        {
            IsUnderwater = layer == MapLayer.Underwater;
            var bounds = new Bounds(); bool any = false;
            void Include(Vector3 point) { if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; } else bounds.Encapsulate(point); }
            if (!IsUnderwater)
            {
                foreach (var terrain in Terrain.activeTerrains)
                {
                    var data = terrain.terrainData; var n = data.heightmapResolution;
                    var h = data.GetHeights(0, 0, n, n); var pos = terrain.transform.position;
                    for (int z = 0; z < n; z += 2) for (int x = 0; x < n; x += 2)
                        if (pos.y + h[z, x] * data.size.y > -4)
                            Include(new Vector3(pos.x + x * data.size.x / (n - 1), 0, pos.z + z * data.size.z / (n - 1)));
                }
            }
            else
            {
                foreach (var root in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (root.name == "Underwater Models")
                        foreach (var renderer in root.GetComponentsInChildren<Renderer>()) { Include(renderer.bounds.min); Include(renderer.bounds.max); }
            }
            if (!any) bounds = new Bounds(Vector3.zero, new Vector3(220, 0, 150));
            float width = Mathf.Max(bounds.size.x + 32, (bounds.size.z + 32) * _camera.aspect);
            float height = width / _camera.aspect;
            WorldBounds = new Rect(bounds.center.x - width / 2, bounds.center.z - height / 2, width, height);
            _camera.orthographicSize = height / 2;
            _camera.transform.position = new Vector3(bounds.center.x, IsUnderwater ? -4.1f : 80f, bounds.center.z);
            _camera.farClipPlane = IsUnderwater ? 1000f : 84f;
            GetComponent<SushiAndFish.CityExpansion.CityMapPlaces>()?.Refresh(this);
            return WorldBounds;
        }

        private void SetOverviewCulling(bool overview)
        {
            if (overview)
            {
                foreach (var culler in UnityEngine.Object.FindObjectsByType<PaperPixelGames.Core.DressingSceneSystem.DressingRendererDistanceCuller>(FindObjectsSortMode.None))
                    if (culler.enabled) { _pausedCullers.Add(culler); culler.enabled = false; }
            }
            else { foreach (var culler in _pausedCullers) if (culler) culler.enabled = true; _pausedCullers.Clear(); }
        }
        public void SetVisible(bool visible) { _visible = visible; SetOverviewCulling(visible); if (_camera) _camera.enabled = visible && isActiveAndEnabled; }
        private void OnEnable() { if (_camera) _camera.enabled = _visible; if (_visible) SetOverviewCulling(true); }
        private void OnDisable() { if (_camera) _camera.enabled = false; SetOverviewCulling(false); }
        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camera) return;
            var manager = PaperPixelGames.Framework.Patterns.Singleton<PaperPixelGames.Core.Managers.PlayersManager>.Instance;
            if (manager != null && manager.LocalPlayer != null)
                foreach (var renderer in manager.LocalPlayer.GetComponentsInChildren<Renderer>())
                    if (!renderer.forceRenderingOff) { _hiddenPlayerRenderers.Add(renderer); renderer.forceRenderingOff = true; }
            _fogBefore = RenderSettings.fog; _fogChanged = true; RenderSettings.fog = false;
        }
        private void RestoreCameraVisibility()
        {
            foreach (var renderer in _hiddenPlayerRenderers) if (renderer) renderer.forceRenderingOff = false;
            _hiddenPlayerRenderers.Clear();
        }
        private void AfterCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camera) return;
            RestoreCameraVisibility();
            if (_fogChanged) { RenderSettings.fog = _fogBefore; _fogChanged = false; }
            RenderedFrames++;
        }
        private void OnDestroy()
        {
            SetOverviewCulling(false);
            RestoreCameraVisibility();
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            RenderPipelineManager.endCameraRendering -= AfterCamera;
            if (_camera) Destroy(_camera.gameObject);
            if (_texture) { _texture.Release(); Destroy(_texture); }
        }
    }
}
