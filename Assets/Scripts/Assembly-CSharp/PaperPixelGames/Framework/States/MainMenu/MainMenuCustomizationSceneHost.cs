using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Bozo.ModularCharacters;
using Cysharp.Threading.Tasks;
using PaperPixelGames.Core.Player.Visuals;
using PaperPixelGames.Framework.Utility;
using StylizedWater3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.States.MainMenu
{
	[DefaultExecutionOrder(-1000)]
	[DisallowMultipleComponent]
	public sealed class MainMenuCustomizationSceneHost : MonoBehaviour
	{
		private const string LegacyResetMarkerKey = "BoZoCustomizationReset_20260628";

		private const string LegacyColdLoadKey = "CustomizationColdLoadDone";

		private const string LegacyCacheFileName = "customization_last.bin";

		private const int OutfitLoadWaitFrameLimit = 600;

		private static readonly FieldInfo CreatorCameraField = typeof(CameraController).GetField("cam", BindingFlags.Instance | BindingFlags.NonPublic);

		private static readonly FieldInfo CreatorZoomActionField = typeof(CameraController).GetField("zoomAction", BindingFlags.Instance | BindingFlags.NonPublic);

		private readonly List<GameObject> _creatorRoots = new List<GameObject>();

		private readonly List<GameObject> _menuPuppetRoots = new List<GameObject>();

		private readonly List<RaycastResult> _pointerRaycastResults = new List<RaycastResult>();

		private readonly List<InputAction> _creatorZoomActions = new List<InputAction>();

		private Scene _mainMenuScene;

		private Scene _creatorScene;

		private Scene _previousActiveScene;

		private GameObject _preloadOverlay;

		private RectTransform _preloadIndicator;

		private OutfitSystem _creatorOutfitSystem;

		private CharacterCreator _characterCreator;

		private CharacterCreator _boundCharacterCreator;

		private ColorPickerControl _creatorColorPicker;

		private GameObject _creatorMainPage;

		private UniTaskCompletionSource<bool> _preloadCompletion;

		private bool _isOpen;

		private bool _openInProgress;

		private bool _closeInProgress;

		private bool _creatorZoomInputBlocked;

		private int _lastBackHandledFrame = -1;

		private Func<GameObject> _localMenuPuppetResolver;

		private GameObject _mainMenuManagerObject;

		private GameObject _mainMenuCamera;

		private GameObject _boatMemberChars;

		private AlignToWater _boatAlignment;

		private bool _mainMenuManagerWasActive;

		private bool _mainMenuCameraWasActive;

		private bool _boatMemberCharsWasActive;

		private bool _boatAlignmentWasEnabled;

		private bool _mainMenuPresentationStateCached;

		public bool IsLoaded
		{
			get
			{
				if (_creatorScene.IsValid())
				{
					return _creatorScene.isLoaded;
				}
				return false;
			}
		}

		public bool IsOpen => _isOpen;

		public event Action<CharacterData> LocalCustomizationChanged;

		public void Initialize()
		{
			_mainMenuScene = base.gameObject.scene;
			RunLegacyCustomizationCleanupOnce();
		}

		public void SetMenuPuppetRoots(IReadOnlyList<GameObject> puppetRoots)
		{
			_menuPuppetRoots.Clear();
			if (puppetRoots == null)
			{
				return;
			}
			for (int i = 0; i < puppetRoots.Count; i++)
			{
				if (puppetRoots[i] != null && !_menuPuppetRoots.Contains(puppetRoots[i]))
				{
					_menuPuppetRoots.Add(puppetRoots[i]);
				}
			}
		}

		public void SetLocalMenuPuppetResolver(Func<GameObject> resolver)
		{
			_localMenuPuppetResolver = resolver;
		}

		public void ConfigureMainMenuPresentation(GameObject mainMenuManagerObject, GameObject mainMenuCamera, GameObject boatMemberChars, AlignToWater boatAlignment)
		{
			_mainMenuManagerObject = mainMenuManagerObject;
			_mainMenuCamera = mainMenuCamera;
			_boatMemberChars = boatMemberChars;
			_boatAlignment = boatAlignment;
		}

		private void OnApplicationQuit()
		{
			if (!_isOpen || _creatorOutfitSystem == null || _creatorOutfitSystem.isloading || _creatorOutfitSystem.isMerging || _creatorOutfitSystem.isUpdatingTexture)
			{
				return;
			}
			try
			{
				if (!_creatorOutfitSystem.initalized)
				{
					_creatorOutfitSystem.Init();
				}
				PlayerCustomizationSession.SetCurrentCharacterData(BMAC_SaveSystem.GetCharacterData(_creatorOutfitSystem), persist: true);
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[MainMenuCustomizationSceneHost] Failed to save customization while quitting: " + ex.Message, LogType.Warning);
			}
		}

		private void Update()
		{
			UpdatePreloadIndicator();
			if (_isOpen)
			{
				UpdateCreatorZoomInputBlock();
				UpdateBackButtonVisibility();
				if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
				{
					HandleBack();
				}
			}
		}

		public UniTask PreloadAsync()
		{
			if (IsLoaded)
			{
				return UniTask.CompletedTask;
			}
			if (_preloadCompletion != null)
			{
				return _preloadCompletion.Task;
			}
			_preloadCompletion = new UniTaskCompletionSource<bool>();
			PreloadInternalAsync().Forget();
			return _preloadCompletion.Task;
		}

		public async UniTask OpenAsync()
		{
			if (_isOpen || _openInProgress)
			{
				return;
			}
			_openInProgress = true;
			try
			{
				await PreloadAsync();
				if (IsLoaded)
				{
					_previousActiveScene = SceneManager.GetActiveScene();
					PrepareMainMenuPresentationForCustomization();
					SetCreatorRootsActive(active: true);
					BindCreatorCameraControllers();
					SetCreatorZoomInputBlocked(blocked: false, force: true);
					SceneManager.SetActiveScene(_creatorScene);
					CacheCreatorNavigation();
					CursorHelper.CursorVisible = true;
					_isOpen = true;
					UpdateBackButtonVisibility();
				}
			}
			finally
			{
				_openInProgress = false;
			}
		}

		public void Close()
		{
			if (_isOpen && !_closeInProgress)
			{
				CloseAsync().Forget();
			}
		}

		public bool HandleBack()
		{
			if (!_isOpen)
			{
				return false;
			}
			if (_closeInProgress || _lastBackHandledFrame == Time.frameCount)
			{
				return true;
			}
			_lastBackHandledFrame = Time.frameCount;
			if (TryReturnToCreatorMainPage())
			{
				return true;
			}
			Close();
			return true;
		}

		private bool TryReturnToCreatorMainPage()
		{
			CacheCreatorNavigation();
			if (_characterCreator == null || _creatorMainPage == null || _creatorMainPage.activeSelf)
			{
				return false;
			}
			_characterCreator.OpenPage(_creatorMainPage);
			_characterCreator.SwitchCatagory(string.Empty);
			_creatorColorPicker?.RemoveObject();
			UpdateBackButtonVisibility();
			return true;
		}

		private void UpdateBackButtonVisibility()
		{
			CacheCreatorNavigation();
			bool backButtonVisible = _isOpen && !_closeInProgress && (_creatorMainPage == null || _creatorMainPage.activeSelf);
			_characterCreator?.SetBackButtonVisible(backButtonVisible);
		}

		public UniTask<bool> ConfirmAndCloseAsync()
		{
			if (!_isOpen || _closeInProgress) return UniTask.FromResult(false);
			return CloseAsync(requireSave: true);
		}

		private async UniTask<bool> CloseAsync(bool requireSave = false)
		{
			_closeInProgress = true;
			try
			{
				CharacterData creatorData = await CaptureCreatorCharacterDataAsync();
				if (creatorData != null && PlayerCustomizationSession.SetCurrentCharacterData(creatorData, persist: true))
				{
					LocalCustomizationChanged?.Invoke(creatorData);
				}
				else
				{
					if (requireSave) return false;
					creatorData = null;
				}
				_characterCreator?.SetBackButtonVisible(visible: false);
				SetCreatorRootsActive(active: false);
				if (_previousActiveScene.IsValid() && _previousActiveScene.isLoaded)
				{
					SceneManager.SetActiveScene(_previousActiveScene);
				}
				else if (_mainMenuScene.IsValid() && _mainMenuScene.isLoaded)
				{
					SceneManager.SetActiveScene(_mainMenuScene);
				}
				RestoreMainMenuPresentation();
				_isOpen = false;
				await UniTask.Yield();
				await SyncCreatorDataToMenuPuppetsAsync(creatorData);
				return creatorData != null;
			}
			finally
			{
				_closeInProgress = false;
			}
		}

		private void OnDestroy()
		{
			UnbindCharacterCreatorEvents();
		}

		private async UniTaskVoid PreloadInternalAsync()
		{
			_ = 3;
			try
			{
				SetPreloadOverlayVisible(visible: true);
				await UniTask.Yield();
				await (SceneManager.LoadSceneAsync("StylizedCharacterCreator", LoadSceneMode.Additive) ?? throw new InvalidOperationException("Scene load failed to start: StylizedCharacterCreator")).ToUniTask();
				_creatorScene = SceneManager.GetSceneByName("StylizedCharacterCreator");
				CacheCreatorRoots();
				CacheCreatorOutfitSystem();
				CacheCreatorNavigation();
				BindCreatorCameraControllers();
				await WaitForOutfitSystemLoadAsync(_creatorOutfitSystem);
				await RestoreSavedCustomizationToCreatorAsync();
				SetCreatorRootsActive(active: false);
				_preloadCompletion?.TrySetResult(result: true);
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[MainMenuCustomizationSceneHost] Preload failed: " + ex.Message, LogType.Error);
				_preloadCompletion?.TrySetException(ex);
			}
			finally
			{
				SetPreloadOverlayVisible(visible: false);
			}
		}

		private void CacheCreatorRoots()
		{
			CacheCreatorRoots(_creatorScene);
		}

		private void CacheCreatorRoots(Scene scene)
		{
			_creatorRoots.Clear();
			if (!scene.IsValid() || !scene.isLoaded)
			{
				return;
			}
			GameObject[] rootGameObjects = scene.GetRootGameObjects();
			for (int i = 0; i < rootGameObjects.Length; i++)
			{
				if (rootGameObjects[i] != null)
				{
					_creatorRoots.Add(rootGameObjects[i]);
				}
			}
		}

		private void CacheCreatorOutfitSystem()
		{
			_creatorOutfitSystem = null;
			for (int i = 0; i < _creatorRoots.Count; i++)
			{
				GameObject gameObject = _creatorRoots[i];
				if (gameObject == null)
				{
					continue;
				}
				OutfitSystem[] componentsInChildren = gameObject.GetComponentsInChildren<OutfitSystem>(includeInactive: true);
				foreach (OutfitSystem outfitSystem in componentsInChildren)
				{
					if (!(outfitSystem == null) && !outfitSystem.isStatic)
					{
						_creatorOutfitSystem = outfitSystem;
						return;
					}
				}
			}
		}

		private void CacheCreatorNavigation()
		{
			if (_characterCreator != null && _creatorColorPicker != null && _creatorMainPage != null)
			{
				BindCharacterCreatorEvents();
				return;
			}
			_characterCreator = null;
			_creatorColorPicker = null;
			_creatorMainPage = null;
			for (int i = 0; i < _creatorRoots.Count; i++)
			{
				GameObject gameObject = _creatorRoots[i];
				if (gameObject == null)
				{
					continue;
				}
				if ((object)_characterCreator == null)
				{
					_characterCreator = gameObject.GetComponentInChildren<CharacterCreator>(includeInactive: true);
				}
				if ((object)_creatorColorPicker == null)
				{
					_creatorColorPicker = gameObject.GetComponentInChildren<ColorPickerControl>(includeInactive: true);
				}
				Transform[] componentsInChildren = gameObject.GetComponentsInChildren<Transform>(includeInactive: true);
				foreach (Transform transform in componentsInChildren)
				{
					if (transform != null && string.Equals(transform.name, "Page_Main", StringComparison.Ordinal))
					{
						_creatorMainPage = transform.gameObject;
						break;
					}
				}
				if (_characterCreator != null && _creatorColorPicker != null && _creatorMainPage != null)
				{
					BindCharacterCreatorEvents();
					return;
				}
			}
			BindCharacterCreatorEvents();
		}

		private void BindCharacterCreatorEvents()
		{
			if (!(_boundCharacterCreator == _characterCreator))
			{
				UnbindCharacterCreatorEvents();
				_boundCharacterCreator = _characterCreator;
				if (_boundCharacterCreator != null)
				{
					_boundCharacterCreator.BackRequested += OnCharacterCreatorBackRequested;
				}
			}
		}

		private void UnbindCharacterCreatorEvents()
		{
			if (!(_boundCharacterCreator == null))
			{
				_boundCharacterCreator.BackRequested -= OnCharacterCreatorBackRequested;
				_boundCharacterCreator = null;
			}
		}

		private void OnCharacterCreatorBackRequested()
		{
			HandleBack();
		}

		private void BindCreatorCameraControllers()
		{
			_creatorZoomActions.Clear();
			if (CreatorCameraField == null)
			{
				UtilityMethods.Log("[MainMenuCustomizationSceneHost] BoZo CameraController camera field could not be found.", LogType.Warning);
				return;
			}
			for (int i = 0; i < _creatorRoots.Count; i++)
			{
				GameObject gameObject = _creatorRoots[i];
				if (gameObject == null)
				{
					continue;
				}
				CameraController[] componentsInChildren = gameObject.GetComponentsInChildren<CameraController>(includeInactive: true);
				foreach (CameraController cameraController in componentsInChildren)
				{
					Camera camera = FindCreatorCamera(cameraController);
					if (camera == null)
					{
						UtilityMethods.Log("[MainMenuCustomizationSceneHost] Creator camera was not found for '" + cameraController.name + "'.", LogType.Warning);
						continue;
					}
					CreatorCameraField.SetValue(cameraController, camera);
					if (CreatorZoomActionField?.GetValue(cameraController) is InputActionReference { action: not null } inputActionReference && !_creatorZoomActions.Contains(inputActionReference.action))
					{
						_creatorZoomActions.Add(inputActionReference.action);
					}
				}
			}
		}

		private static Camera FindCreatorCamera(CameraController controller)
		{
			if (controller == null)
			{
				return null;
			}
			Camera[] componentsInChildren = controller.GetComponentsInChildren<Camera>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i] != null && componentsInChildren[i].CompareTag("MainCamera"))
				{
					return componentsInChildren[i];
				}
			}
			if (componentsInChildren.Length == 0)
			{
				return null;
			}
			return componentsInChildren[0];
		}

		private void UpdateCreatorZoomInputBlock()
		{
			SetCreatorZoomInputBlocked(IsPointerOverCreatorScrollRect());
		}

		private bool IsPointerOverCreatorScrollRect()
		{
			EventSystem current = EventSystem.current;
			if (current == null)
			{
				return false;
			}
			PointerEventData eventData = new PointerEventData(current)
			{
				position = GetPointerPosition()
			};
			_pointerRaycastResults.Clear();
			current.RaycastAll(eventData, _pointerRaycastResults);
			for (int i = 0; i < _pointerRaycastResults.Count; i++)
			{
				GameObject gameObject = _pointerRaycastResults[i].gameObject;
				ScrollRect scrollRect = ((gameObject != null) ? gameObject.GetComponentInParent<ScrollRect>() : null);
				if (scrollRect != null && scrollRect.gameObject.scene == _creatorScene)
				{
					return true;
				}
			}
			return false;
		}

		private static Vector2 GetPointerPosition()
		{
			if (Mouse.current == null)
			{
				return Vector2.zero;
			}
			return Mouse.current.position.ReadValue();
		}

		private void SetCreatorZoomInputBlocked(bool blocked, bool force = false)
		{
			if (!force && _creatorZoomInputBlocked == blocked)
			{
				return;
			}
			_creatorZoomInputBlocked = blocked;
			for (int i = 0; i < _creatorZoomActions.Count; i++)
			{
				InputAction inputAction = _creatorZoomActions[i];
				if (inputAction != null)
				{
					if (blocked)
					{
						inputAction.Disable();
					}
					else
					{
						inputAction.Enable();
					}
				}
			}
		}

		private void SetCreatorRootsActive(bool active)
		{
			// Additive creator UI shares the menu's input system. Its legacy
			// EventSystem must not compete with the already active one.
			var externalEventSystem = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
			bool sharedInput = Array.Exists(externalEventSystem, e => e.isActiveAndEnabled && e.gameObject.scene != _creatorScene);
			for (int i = 0; i < _creatorRoots.Count; i++)
			{
				if (_creatorRoots[i] != null)
				{
					_creatorRoots[i].SetActive(active);
					if (sharedInput)
						foreach (var creatorInput in _creatorRoots[i].GetComponentsInChildren<EventSystem>(true))
							creatorInput.gameObject.SetActive(false);
				}
			}
		}

		private void PrepareMainMenuPresentationForCustomization()
		{
			_mainMenuManagerWasActive = _mainMenuManagerObject != null && _mainMenuManagerObject.activeSelf;
			_mainMenuCameraWasActive = _mainMenuCamera != null && _mainMenuCamera.activeSelf;
			_boatMemberCharsWasActive = _boatMemberChars != null && _boatMemberChars.activeSelf;
			_boatAlignmentWasEnabled = _boatAlignment != null && _boatAlignment.enabled;
			_mainMenuPresentationStateCached = true;
			if (_mainMenuCamera != null)
			{
				_mainMenuCamera.SetActive(value: false);
			}
			if (_boatMemberChars != null)
			{
				_boatMemberChars.SetActive(value: false);
			}
			if (_boatAlignment != null)
			{
				_boatAlignment.enabled = false;
				_boatAlignment.transform.rotation = Quaternion.Euler(0f, _boatAlignment.rotation, 0f);
			}
			if (_mainMenuManagerObject != null)
			{
				_mainMenuManagerObject.SetActive(value: false);
			}
		}

		private void RestoreMainMenuPresentation()
		{
			if (_mainMenuPresentationStateCached)
			{
				if (_mainMenuCamera != null)
				{
					_mainMenuCamera.SetActive(_mainMenuCameraWasActive);
				}
				if (_boatMemberChars != null)
				{
					_boatMemberChars.SetActive(_boatMemberCharsWasActive);
				}
				if (_boatAlignment != null)
				{
					_boatAlignment.enabled = _boatAlignmentWasEnabled;
				}
				if (_mainMenuManagerObject != null)
				{
					_mainMenuManagerObject.SetActive(_mainMenuManagerWasActive);
				}
				_mainMenuPresentationStateCached = false;
			}
		}

		private async UniTask<CharacterData> CaptureCreatorCharacterDataAsync()
		{
			if (_creatorOutfitSystem == null)
			{
				CacheCreatorOutfitSystem();
			}
			if (_creatorOutfitSystem == null)
			{
				return null;
			}
			await WaitForOutfitSystemLoadAsync(_creatorOutfitSystem);
			if (_creatorOutfitSystem.isloading || _creatorOutfitSystem.isMerging || _creatorOutfitSystem.isUpdatingTexture)
			{
				UtilityMethods.Log("[MainMenuCustomizationSceneHost] Skipped creator capture because customization is still loading.", LogType.Warning);
				return null;
			}
			try
			{
				if (!_creatorOutfitSystem.initalized)
				{
					_creatorOutfitSystem.Init();
				}
				return BMAC_SaveSystem.GetCharacterData(_creatorOutfitSystem);
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[MainMenuCustomizationSceneHost] Failed to capture creator character data: " + ex.Message, LogType.Warning);
				return null;
			}
		}

		private async UniTask SyncCreatorDataToMenuPuppetsAsync(CharacterData creatorData)
		{
			if (creatorData == null || _menuPuppetRoots.Count == 0)
			{
				return;
			}
			GameObject gameObject = ResolveLocalMenuPuppet();
			if (!(gameObject == null) && gameObject.activeInHierarchy)
			{
				OutfitSystem componentInChildren = gameObject.GetComponentInChildren<OutfitSystem>(includeInactive: true);
				if (!(componentInChildren == null) && !componentInChildren.isStatic)
				{
					await PlayerCustomizationSession.ApplyCharacterDataToOutfitSystemAsync(componentInChildren, creatorData, "local menu puppet '" + gameObject.name + "'");
				}
			}
		}

		private GameObject ResolveLocalMenuPuppet()
		{
			if (_localMenuPuppetResolver != null)
			{
				try
				{
					GameObject gameObject = _localMenuPuppetResolver();
					if (gameObject != null)
					{
						return gameObject;
					}
				}
				catch (Exception ex)
				{
					UtilityMethods.Log("[MainMenuCustomizationSceneHost] Local puppet resolution failed: " + ex.Message, LogType.Warning);
				}
			}
			for (int i = 0; i < _menuPuppetRoots.Count; i++)
			{
				if (_menuPuppetRoots[i] != null && _menuPuppetRoots[i].activeInHierarchy)
				{
					return _menuPuppetRoots[i];
				}
			}
			return null;
		}

		public async UniTask RestoreSavedCustomizationToMenuPuppetsAsync()
		{
			CharacterData currentCharacterData = PlayerCustomizationSession.GetCurrentCharacterData();
			if (currentCharacterData != null)
			{
				await SyncCreatorDataToMenuPuppetsAsync(currentCharacterData);
			}
		}

		private async UniTask RestoreSavedCustomizationToCreatorAsync()
		{
			CharacterData currentCharacterData = PlayerCustomizationSession.GetCurrentCharacterData();
			if (currentCharacterData != null && !(_creatorOutfitSystem == null))
			{
				await PlayerCustomizationSession.ApplyCharacterDataToOutfitSystemAsync(_creatorOutfitSystem, currentCharacterData, "character creator");
			}
		}

		private static async UniTask WaitForOutfitSystemLoadAsync(OutfitSystem outfitSystem)
		{
			if (!(outfitSystem == null))
			{
				int frame = 0;
				while ((outfitSystem.isloading || outfitSystem.isMerging || outfitSystem.isUpdatingTexture) && frame < 600)
				{
					frame++;
					await UniTask.Yield();
				}
			}
		}

		private void EnsurePreloadOverlay()
		{
			if (!(_preloadOverlay != null))
			{
				_preloadOverlay = new GameObject("CustomizationPreloadOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
				_preloadOverlay.transform.SetParent(base.transform, worldPositionStays: false);
				Canvas component = _preloadOverlay.GetComponent<Canvas>();
				component.renderMode = RenderMode.ScreenSpaceOverlay;
				component.sortingOrder = 32100;
				CanvasScaler component2 = _preloadOverlay.GetComponent<CanvasScaler>();
				component2.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
				component2.referenceResolution = new Vector2(1920f, 1080f);
				component2.matchWidthOrHeight = 0.5f;
				RectTransform component3 = _preloadOverlay.GetComponent<RectTransform>();
				component3.anchorMin = Vector2.zero;
				component3.anchorMax = Vector2.one;
				component3.offsetMin = Vector2.zero;
				component3.offsetMax = Vector2.zero;
				Image component4 = _preloadOverlay.GetComponent<Image>();
				component4.color = new Color(0.025f, 0.03f, 0.035f, 0.98f);
				component4.raycastTarget = true;
				GameObject obj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
				obj.transform.SetParent(_preloadOverlay.transform, worldPositionStays: false);
				RectTransform component5 = obj.GetComponent<RectTransform>();
				component5.anchorMin = new Vector2(0.5f, 0.5f);
				component5.anchorMax = new Vector2(0.5f, 0.5f);
				component5.pivot = new Vector2(0.5f, 0.5f);
				component5.anchoredPosition = new Vector2(0f, 34f);
				component5.sizeDelta = new Vector2(640f, 48f);
				TextMeshProUGUI component6 = obj.GetComponent<TextMeshProUGUI>();
				component6.text = "PREPARING CHARACTER";
				component6.fontSize = 28f;
				component6.alignment = TextAlignmentOptions.Center;
				component6.color = new Color(0.94f, 0.95f, 0.96f, 1f);
				component6.raycastTarget = false;
				GameObject gameObject = new GameObject("Track", typeof(RectTransform), typeof(Image));
				gameObject.transform.SetParent(_preloadOverlay.transform, worldPositionStays: false);
				RectTransform component7 = gameObject.GetComponent<RectTransform>();
				component7.anchorMin = new Vector2(0.5f, 0.5f);
				component7.anchorMax = new Vector2(0.5f, 0.5f);
				component7.pivot = new Vector2(0.5f, 0.5f);
				component7.anchoredPosition = new Vector2(0f, -20f);
				component7.sizeDelta = new Vector2(360f, 4f);
				Image component8 = gameObject.GetComponent<Image>();
				component8.color = new Color(1f, 1f, 1f, 0.16f);
				component8.raycastTarget = false;
				GameObject gameObject2 = new GameObject("Indicator", typeof(RectTransform), typeof(Image));
				gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
				_preloadIndicator = gameObject2.GetComponent<RectTransform>();
				_preloadIndicator.anchorMin = new Vector2(0.5f, 0.5f);
				_preloadIndicator.anchorMax = new Vector2(0.5f, 0.5f);
				_preloadIndicator.pivot = new Vector2(0.5f, 0.5f);
				_preloadIndicator.sizeDelta = new Vector2(96f, 4f);
				Image component9 = gameObject2.GetComponent<Image>();
				component9.color = new Color(0.2f, 0.78f, 0.72f, 1f);
				component9.raycastTarget = false;
				_preloadOverlay.SetActive(value: false);
			}
		}

		private void SetPreloadOverlayVisible(bool visible)
		{
			EnsurePreloadOverlay();
			if (_preloadOverlay != null)
			{
				_preloadOverlay.SetActive(visible);
			}
			if (visible && _preloadIndicator != null)
			{
				_preloadIndicator.anchoredPosition = new Vector2(-132f, 0f);
			}
		}

		private void UpdatePreloadIndicator()
		{
			if (!(_preloadOverlay == null) && _preloadOverlay.activeSelf && !(_preloadIndicator == null))
			{
				float t = Mathf.PingPong(Time.unscaledTime * 0.85f, 1f);
				float x = Mathf.Lerp(-132f, 132f, t);
				_preloadIndicator.anchoredPosition = new Vector2(x, 0f);
			}
		}

		private static void RunLegacyCustomizationCleanupOnce()
		{
			if (PlayerPrefs.GetInt("BoZoCustomizationReset_20260628", 0) == 1)
			{
				return;
			}
			try
			{
				string path = Path.Combine(Application.persistentDataPath, "customization_last.bin");
				if (File.Exists(path))
				{
					File.Delete(path);
				}
				PlayerPrefs.DeleteKey("CustomizationColdLoadDone");
				PlayerPrefs.SetInt("BoZoCustomizationReset_20260628", 1);
				PlayerPrefs.Save();
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[MainMenuCustomizationSceneHost] Legacy customization cleanup failed: " + ex.Message, LogType.Warning);
			}
		}
	}
}
