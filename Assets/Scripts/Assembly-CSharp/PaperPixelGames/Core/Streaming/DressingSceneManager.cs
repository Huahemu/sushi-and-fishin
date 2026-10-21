using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using PaperPixelGames.Core.DressingSceneSystem;
using PaperPixelGames.Core.Lighthouse;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VLB;

namespace PaperPixelGames.Core.Streaming
{
	public class DressingSceneManager : Singleton<DressingSceneManager>
	{
		private readonly struct LoadingFlavorText
		{
			public readonly string Key;

			public readonly string Fallback;

			public LoadingFlavorText(string key, string fallback)
			{
				Key = key;
				Fallback = fallback;
			}
		}

		[Serializable]
		private sealed class LoadedDressingSceneData
		{
			public string SceneName;

			public Scene Scene;

			public DressingSceneRoot SceneRoot;

			public bool IsFullyActivated;

			public int PendingActivationCount;

			public int PendingActivationWeight;

			public int ActivatedGroupCount;

			public int ActivatedWeight;

			public LoadedDressingSceneData(string sceneName, Scene scene, DressingSceneRoot sceneRoot)
			{
				SceneName = sceneName;
				Scene = scene;
				SceneRoot = sceneRoot;
				IsFullyActivated = false;
			}
		}

		private readonly struct ActivationRequest
		{
			public readonly string SceneName;

			public readonly DressingActivationGroup Group;

			public ActivationRequest(string sceneName, DressingActivationGroup group)
			{
				SceneName = sceneName;
				Group = group;
			}
		}

		private const string LighthouseDressingSceneName = "100_Dressing_City_01";

		[SerializeField]
		private string _persistentActiveSceneName = "GameState";

		[Header("Startup Load")]
		[SerializeField]
		private bool _loadStartupScenesOnStart = true;

		[SerializeField]
		private List<string> _startupSceneNames = new List<string>();

		[SerializeField]
		private int _framesBetweenSceneLoads = 1;

		[Header("Load Settings")]
		[SerializeField]
		private float _loadPollInterval;

		[Header("Activation Settings")]
		[SerializeField]
		private bool _useFrameBudget = true;

		[SerializeField]
		private float _activationFrameBudgetMs = 1f;

		[SerializeField]
		private int _activationFrameBudgetWeight = 8;

		[SerializeField]
		private float _slowActivationLogThresholdMs = 4f;

		[SerializeField]
		private int _fallbackGroupsPerFrame = 1;

		[SerializeField]
		private bool _logDebug;

		[Header("Reflection Cubemap Blend")]
		[SerializeField]
		private ReflectionProbe _probe;

		[SerializeField]
		private Cubemap _dayReflectionCubemap;

		[SerializeField]
		private Cubemap _nightReflectionCubemap;

		[SerializeField]
		[Range(0f, 24f)]
		private float _nightBlendRampUpStartHour = 19f;

		[SerializeField]
		[Range(0f, 24f)]
		private float _nightBlendRampUpEndHour = 22f;

		[SerializeField]
		[Range(0f, 24f)]
		private float _nightBlendRampDownStartHour = 4f;

		[SerializeField]
		[Range(0f, 24f)]
		private float _nightBlendRampDownEndHour = 7f;

		[Space]
		[SerializeField]
		private OcclusionPortal _cityPortal;

		[SerializeField]
		private float _cityPortalDepth = 1f;

		[SerializeField]
		private float _restaurantPortalDistance = 1f;

		[Header("DEBUG EDITOR")]
		[SerializeField]
		private bool _closeAll;

		[SerializeField]
		private bool _onlyCity;

		[SerializeField]
		private bool _onlyOcean;

		private readonly Dictionary<string, LoadedDressingSceneData> _loadedScenes = new Dictionary<string, LoadedDressingSceneData>();

		private readonly Queue<ActivationRequest> _activationQueue = new Queue<ActivationRequest>();

		private readonly HashSet<string> _loadingSceneNames = new HashSet<string>();

		private static readonly LoadingFlavorText[] LoadingFlavorTexts = new LoadingFlavorText[12]
		{
			new LoadingFlavorText("loading.flavor.waking_npcs", "Waking up the NPCs..."),
			new LoadingFlavorText("loading.flavor.refreshing_sand", "Refreshing the beach sand..."),
			new LoadingFlavorText("loading.flavor.shrimp_personal_space", "Teaching shrimps personal space..."),
			new LoadingFlavorText("loading.flavor.polishing_pebbles", "Polishing the tiny pebbles..."),
			new LoadingFlavorText("loading.flavor.palm_trees", "Convincing palm trees to stand still..."),
			new LoadingFlavorText("loading.flavor.refilling_ocean", "Refilling the ocean, please wait..."),
			new LoadingFlavorText("loading.flavor.dusting_city", "Dusting off the city corners..."),
			new LoadingFlavorText("loading.flavor.restoring_shadows", "Putting shadows back where they belong..."),
			new LoadingFlavorText("loading.flavor.seagulls_signed_in", "Checking if the seagulls signed in..."),
			new LoadingFlavorText("loading.flavor.decoration_wires", "Untangling invisible decoration wires..."),
			new LoadingFlavorText("loading.flavor.rock_pep_talk", "Giving rocks their daily pep talk..."),
			new LoadingFlavorText("loading.flavor.repainting_sunlight", "Repainting the sunlight by hand...")
		};

		private Coroutine _activationCoroutine;

		private Coroutine _startupLoadCoroutine;

		private bool _isLoading;

		private bool _isStartupLoading;

		private bool _isStartupStreamingSuspended;

		private RenderTexture _blendedReflectionCubemap;

		private ReflectionProbeMode _originalProbeMode;

		private Texture _originalProbeCustomTexture;

		private bool _reflectionBlendInitializationAttempted;

		private bool _reflectionBlendInitialized;

		private bool _reflectionBlendFailureLogged;

		private DressingRendererDistanceCuller _rendererDistanceCuller;

		private float _lastReflectionBlendWeight = float.NaN;

		private Scene _persistentActiveScene;

		private int _loadingFlavorTextIndex = -1;

		private bool _startupInitialized;

		private bool _startupCompleted;

		public override SingletonScope Scope => SingletonScope.Scene;

		public bool IsLoading => _isLoading;

		public bool IsStartupLoading => _isStartupLoading;

		public bool IsStreamingBusy
		{
			get
			{
				if (!_isLoading && _activationCoroutine == null)
				{
					return _activationQueue.Count > 0;
				}
				return true;
			}
		}

		public bool IsStartupStreamingBusy
		{
			get
			{
				if (!_isStartupLoading)
				{
					if (_isStartupStreamingSuspended)
					{
						return IsStreamingBusy;
					}
					return false;
				}
				return true;
			}
		}

		public void OpenCityPortal()
		{
			_cityPortal.open = true;
		}

		public void CloseCityPortal()
		{
			_cityPortal.open = false;
		}

		public void OnDepthUpdate(float depth)
		{
			if (depth < _cityPortalDepth)
			{
				OpenCityPortal();
			}
			else
			{
				CloseCityPortal();
			}
		}

		public void Initialize()
		{
			CachePersistentActiveScene();
			EnsureRendererDistanceCuller();
			if (_startupInitialized)
			{
				DebugLog("Initialize skipped. Startup already initialized.");
				return;
			}
			_startupInitialized = true;
			UpdateReflectionCubemapFromCurrentTime(force: true);
			if (_loadStartupScenesOnStart)
			{
				if (_startupSceneNames == null || _startupSceneNames.Count == 0)
				{
					DebugLog("Startup load is enabled but scene list is empty.");
					return;
				}
				StartStartupLoading();
				OpenCityPortal();
			}
		}

		private void EnsureRendererDistanceCuller()
		{
			if (_rendererDistanceCuller == null)
			{
				_rendererDistanceCuller = GetComponent<DressingRendererDistanceCuller>();
			}
			if (_rendererDistanceCuller == null)
			{
				_rendererDistanceCuller = base.gameObject.AddComponent<DressingRendererDistanceCuller>();
			}
			_rendererDistanceCuller.Initialize(this);
		}

		private bool IsSceneKnown(string sceneName)
		{
			if (string.IsNullOrWhiteSpace(sceneName))
			{
				return false;
			}
			if (_loadedScenes.ContainsKey(sceneName))
			{
				return true;
			}
			if (_loadingSceneNames.Contains(sceneName))
			{
				return true;
			}
			Scene sceneByName = SceneManager.GetSceneByName(sceneName);
			if (sceneByName.IsValid() && sceneByName.isLoaded)
			{
				return TryAdoptLoadedScene(sceneName, sceneByName);
			}
			return false;
		}

		private bool TryAdoptLoadedScene(string sceneName, Scene loadedScene)
		{
			if (_loadedScenes.ContainsKey(sceneName))
			{
				return true;
			}
			DressingSceneRoot dressingSceneRoot = FindSceneRoot(loadedScene);
			if (dressingSceneRoot == null)
			{
				DebugLog("Loaded scene could not be adopted because DressingSceneRoot was not found: " + sceneName);
				return true;
			}
			dressingSceneRoot.PrepareForStreaming();
			LoadedDressingSceneData loadedDressingSceneData = new LoadedDressingSceneData(sceneName, loadedScene, dressingSceneRoot);
			_loadedScenes.Add(sceneName, loadedDressingSceneData);
			EnqueueActivation(loadedDressingSceneData);
			RestorePersistentActiveScene();
			DebugLog("Adopted already loaded scene: " + sceneName);
			return true;
		}

		private void CachePersistentActiveScene()
		{
			if (string.IsNullOrWhiteSpace(_persistentActiveSceneName))
			{
				DebugLog("Persistent active scene name is null or empty.");
				return;
			}
			Scene sceneByName = SceneManager.GetSceneByName(_persistentActiveSceneName);
			if (!sceneByName.IsValid() || !sceneByName.isLoaded)
			{
				DebugLog("Persistent active scene not found or not loaded: " + _persistentActiveSceneName);
				return;
			}
			_persistentActiveScene = sceneByName;
			DebugLog("Cached persistent active scene by name: " + _persistentActiveScene.name);
		}

		private void RestorePersistentActiveScene()
		{
			if (string.IsNullOrWhiteSpace(_persistentActiveSceneName))
			{
				return;
			}
			Scene sceneByName = SceneManager.GetSceneByName(_persistentActiveSceneName);
			if (!sceneByName.IsValid() || !sceneByName.isLoaded)
			{
				DebugLog("Persistent active scene is not valid or not loaded: " + _persistentActiveSceneName);
				return;
			}
			_persistentActiveScene = sceneByName;
			if (!(SceneManager.GetActiveScene() == sceneByName))
			{
				if (!SceneManager.SetActiveScene(sceneByName))
				{
					DebugLog("Failed to restore active scene: " + sceneByName.name);
				}
				else
				{
					DebugLog("Active scene restored to: " + sceneByName.name);
				}
			}
		}

		public void StartStartupLoading()
		{
			if (_startupCompleted)
			{
				DebugLog("Startup loading skipped. Already completed.");
			}
			else if (!_isStartupLoading)
			{
				if (_startupLoadCoroutine != null)
				{
					DebugLog("Startup loading is already running.");
				}
				else
				{
					_startupLoadCoroutine = StartCoroutine(LoadStartupScenesRoutine());
				}
			}
		}

		public void LoadSceneAsync(string sceneName)
		{
			if (string.IsNullOrWhiteSpace(sceneName))
			{
				DebugLog("LoadSceneAsync failed. Scene name is null or empty.");
			}
			else if (IsSceneKnown(sceneName))
			{
				DebugLog("LoadSceneAsync skipped. Scene already known: " + sceneName);
			}
			else
			{
				StartCoroutine(LoadSceneRoutine(sceneName));
			}
		}

		public void LoadScenesAsync(IEnumerable<string> sceneNames)
		{
			StartCoroutine(LoadScenesRoutine(sceneNames));
		}

		public void UnloadSceneAsync(string sceneName)
		{
			if (!_loadedScenes.TryGetValue(sceneName, out var value))
			{
				DebugLog("Unload skipped. Scene not loaded: " + sceneName);
			}
			else
			{
				StartCoroutine(UnloadSceneRoutine(value));
			}
		}

		public void UnloadAllScenesAsync()
		{
			StartCoroutine(UnloadAllScenesRoutine());
		}

		public void ActivateSceneImmediate(string sceneName)
		{
			if (!_loadedScenes.TryGetValue(sceneName, out var value))
			{
				DebugLog("ActivateSceneImmediate failed. Scene not loaded: " + sceneName);
				return;
			}
			value.SceneRoot.ActivateAllImmediate();
			value.IsFullyActivated = true;
		}

		private IEnumerator LoadStartupScenesRoutine()
		{
			_isStartupLoading = true;
			BeginStartupStreamingSuspend();
			try
			{
				DebugLog("Startup dressing scene loading started.");
				Stopwatch startupStopwatch = Stopwatch.StartNew();
				Stopwatch sceneStopwatch = Stopwatch.StartNew();
				for (int i = 0; i < _startupSceneNames.Count; i++)
				{
					sceneStopwatch.Restart();
					string sceneName = _startupSceneNames[i];
					if (string.IsNullOrWhiteSpace(sceneName))
					{
						continue;
					}
					if (IsSceneKnown(sceneName))
					{
						DebugLog("Startup skip. Scene already known: " + sceneName);
						continue;
					}
					yield return LoadSceneRoutine(sceneName);
					DebugLog($"Startup scene completed | scene:{sceneName} totalMs:{sceneStopwatch.Elapsed.TotalMilliseconds:0.00}");
					int waitFrames = Mathf.Max(0, _framesBetweenSceneLoads);
					for (int frame = 0; frame < waitFrames; frame++)
					{
						yield return null;
					}
				}
				_startupCompleted = true;
				yield return WaitForActivationQueueRoutine();
				DebugLog($"Startup activation drain completed | elapsedMs:{startupStopwatch.Elapsed.TotalMilliseconds:0.00}");
				EndStartupStreamingSuspend();
				UpdateReflectionCubemapFromCurrentTime(force: true);
				LoadingScreen.ForceHide();
				DebugLog($"Startup dressing scene loading completed | totalMs:{startupStopwatch.Elapsed.TotalMilliseconds:0.00}");
			}
			finally
			{
				DressingSceneManager dressingSceneManager = this;
				if (dressingSceneManager._isStartupStreamingSuspended)
				{
					dressingSceneManager.EndStartupStreamingSuspend();
				}
				LoadingScreen.ForceHide();
				dressingSceneManager._isStartupLoading = false;
				dressingSceneManager._startupLoadCoroutine = null;
			}
		}

		public void UpdateReflectionCubemapBlend(float currentHour, bool force = false)
		{
			if (!EnsureReflectionCubemapBlendInitialized())
			{
				return;
			}
			float num = CalculateNightBlendWeight(currentHour);
			if (force || float.IsNaN(_lastReflectionBlendWeight) || !(Mathf.Abs(_lastReflectionBlendWeight - num) < 0.0001f))
			{
				if (!ReflectionProbe.BlendCubemap(_dayReflectionCubemap, _nightReflectionCubemap, num, _blendedReflectionCubemap))
				{
					LogReflectionBlendFailure("Cubemap blend failed. The current probe texture was kept unchanged.");
					return;
				}
				_blendedReflectionCubemap.IncrementUpdateCount();
				_probe.mode = ReflectionProbeMode.Custom;
				_probe.customBakedTexture = _blendedReflectionCubemap;
				_lastReflectionBlendWeight = num;
				_reflectionBlendFailureLogged = false;
			}
		}

		private bool EnsureReflectionCubemapBlendInitialized()
		{
			if (_reflectionBlendInitializationAttempted)
			{
				return _reflectionBlendInitialized;
			}
			_reflectionBlendInitializationAttempted = true;
			if (_probe == null || _dayReflectionCubemap == null || _nightReflectionCubemap == null)
			{
				LogReflectionBlendFailure("Initialization failed because the probe, day cubemap, or night cubemap reference is missing.");
				return false;
			}
			if (_dayReflectionCubemap.width != _nightReflectionCubemap.width || _dayReflectionCubemap.height != _nightReflectionCubemap.height)
			{
				LogReflectionBlendFailure($"Initialization failed because source resolutions differ | day:{_dayReflectionCubemap.width}x{_dayReflectionCubemap.height} " + $"night:{_nightReflectionCubemap.width}x{_nightReflectionCubemap.height}");
				return false;
			}
			_originalProbeMode = _probe.mode;
			_originalProbeCustomTexture = _probe.customBakedTexture;
			_blendedReflectionCubemap = new RenderTexture(_dayReflectionCubemap.width, _dayReflectionCubemap.height, 0, RenderTextureFormat.ARGBHalf)
			{
				name = "Runtime Reflection Cubemap Blend",
				dimension = TextureDimension.Cube,
				useMipMap = true,
				autoGenerateMips = false,
				filterMode = FilterMode.Trilinear,
				wrapMode = TextureWrapMode.Clamp,
				hideFlags = HideFlags.DontSave
			};
			if (!_blendedReflectionCubemap.Create())
			{
				UnityEngine.Object.Destroy(_blendedReflectionCubemap);
				_blendedReflectionCubemap = null;
				LogReflectionBlendFailure("Initialization failed because the cubemap RenderTexture could not be created.");
				return false;
			}
			_reflectionBlendInitialized = true;
			DebugLog($"Reflection cubemap blend initialized | resolution:{_dayReflectionCubemap.width}");
			return true;
		}

		private void UpdateReflectionCubemapFromCurrentTime(bool force)
		{
			float currentHour = 12f;
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			if (instance != null && instance.CozyWeatherManager != null)
			{
				currentHour = instance.CozyWeatherManager.CurrentHourFloat;
			}
			UpdateReflectionCubemapBlend(currentHour, force);
		}

		private float CalculateNightBlendWeight(float currentHour)
		{
			currentHour = Mathf.Repeat(currentHour, 24f);
			if (IsInCyclicHourRange(currentHour, _nightBlendRampUpStartHour, _nightBlendRampUpEndHour))
			{
				float cyclicHourRangeT = GetCyclicHourRangeT(currentHour, _nightBlendRampUpStartHour, _nightBlendRampUpEndHour);
				return Mathf.SmoothStep(0f, 1f, cyclicHourRangeT);
			}
			if (IsInCyclicHourRange(currentHour, _nightBlendRampUpEndHour, _nightBlendRampDownStartHour))
			{
				return 1f;
			}
			if (IsInCyclicHourRange(currentHour, _nightBlendRampDownStartHour, _nightBlendRampDownEndHour))
			{
				float cyclicHourRangeT2 = GetCyclicHourRangeT(currentHour, _nightBlendRampDownStartHour, _nightBlendRampDownEndHour);
				return 1f - Mathf.SmoothStep(0f, 1f, cyclicHourRangeT2);
			}
			return 0f;
		}

		private static bool IsInCyclicHourRange(float currentHour, float startHour, float endHour)
		{
			currentHour = Mathf.Repeat(currentHour, 24f);
			startHour = Mathf.Repeat(startHour, 24f);
			endHour = Mathf.Repeat(endHour, 24f);
			if (startHour < endHour)
			{
				if (currentHour >= startHour)
				{
					return currentHour < endHour;
				}
				return false;
			}
			if (!(currentHour >= startHour))
			{
				return currentHour < endHour;
			}
			return true;
		}

		private static float GetCyclicHourRangeT(float currentHour, float startHour, float endHour)
		{
			float num = Mathf.Repeat(endHour - startHour, 24f);
			if (Mathf.Approximately(num, 0f))
			{
				return 1f;
			}
			return Mathf.Clamp01(Mathf.Repeat(currentHour - startHour, 24f) / num);
		}

		private void LogReflectionBlendFailure(string message)
		{
			if (!_reflectionBlendFailureLogged)
			{
				_reflectionBlendFailureLogged = true;
				UtilityMethods.Log("[DressingSceneManager] Reflection cubemap blend | " + message, LogType.Error);
			}
		}

		private void OnDestroy()
		{
			if (_probe != null && _blendedReflectionCubemap != null && _probe.customBakedTexture == _blendedReflectionCubemap)
			{
				_probe.customBakedTexture = _originalProbeCustomTexture;
				_probe.mode = _originalProbeMode;
			}
			if (_blendedReflectionCubemap != null)
			{
				_blendedReflectionCubemap.Release();
				UnityEngine.Object.Destroy(_blendedReflectionCubemap);
				_blendedReflectionCubemap = null;
			}
			_reflectionBlendInitialized = false;
		}

		private void BeginStartupStreamingSuspend()
		{
			if (!_isStartupStreamingSuspended)
			{
				_isStartupStreamingSuspended = true;
				if (Singleton<GameProgressManager>.Instance != null && Singleton<GameProgressManager>.Instance.CozyWeatherManager != null)
				{
					Singleton<GameProgressManager>.Instance.CozyWeatherManager.BeginStreamingSuspend();
				}
			}
		}

		private void EndStartupStreamingSuspend()
		{
			if (_isStartupStreamingSuspended)
			{
				_isStartupStreamingSuspended = false;
				if (Singleton<GameProgressManager>.Instance != null && Singleton<GameProgressManager>.Instance.CozyWeatherManager != null)
				{
					Singleton<GameProgressManager>.Instance.CozyWeatherManager.EndStreamingSuspend();
				}
			}
		}

		private IEnumerator WaitForActivationQueueRoutine()
		{
			while (_activationCoroutine != null || _activationQueue.Count > 0)
			{
				yield return null;
			}
		}

		private IEnumerator LoadScenesRoutine(IEnumerable<string> sceneNames)
		{
			if (sceneNames == null)
			{
				yield break;
			}
			foreach (string sceneName in sceneNames)
			{
				yield return LoadSceneRoutine(sceneName);
			}
		}

		private IEnumerator LoadSceneRoutine(string sceneName)
		{
			if (string.IsNullOrWhiteSpace(sceneName) || !ShouldLoadDressingScene(sceneName) || IsSceneKnown(sceneName) || !_loadingSceneNames.Add(sceneName))
			{
				yield break;
			}
			try
			{
				_isLoading = true;
				UpdateLoadingFlavorText();
				DebugLog("Loading scene: " + sceneName);
				Stopwatch loadStopwatch = Stopwatch.StartNew();
				AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
				if (loadOperation == null)
				{
					yield break;
				}
				while (!loadOperation.isDone)
				{
					yield return null;
				}
				DebugLog($"LoadSceneAsync completed | scene:{sceneName} elapsedMs:{loadStopwatch.Elapsed.TotalMilliseconds:0.00}");
				Stopwatch stopwatch = Stopwatch.StartNew();
				Scene sceneByName = SceneManager.GetSceneByName(sceneName);
				if (!sceneByName.IsValid() || !sceneByName.isLoaded)
				{
					yield break;
				}
				DressingSceneRoot dressingSceneRoot = FindSceneRoot(sceneByName);
				if (!(dressingSceneRoot == null))
				{
					DebugLog($"Scene root resolved | scene:{sceneName} elapsedMs:{stopwatch.Elapsed.TotalMilliseconds:0.00}");
					AttachLighthouseBeamFader(sceneName, sceneByName);
					stopwatch.Restart();
					dressingSceneRoot.PrepareForStreaming();
					DebugLog($"PrepareForStreaming completed | scene:{sceneName} groupCount:{dressingSceneRoot.Groups.Count} elapsedMs:{stopwatch.Elapsed.TotalMilliseconds:0.00}");
					if (!_loadedScenes.ContainsKey(sceneName))
					{
						LoadedDressingSceneData loadedDressingSceneData = new LoadedDressingSceneData(sceneName, sceneByName, dressingSceneRoot);
						_loadedScenes.Add(sceneName, loadedDressingSceneData);
						stopwatch.Restart();
						EnqueueActivation(loadedDressingSceneData);
						DebugLog($"Activation groups queued | scene:{sceneName} queued:{loadedDressingSceneData.PendingActivationCount} totalWeight:{loadedDressingSceneData.PendingActivationWeight} elapsedMs:{stopwatch.Elapsed.TotalMilliseconds:0.00}");
						stopwatch.Restart();
						RestorePersistentActiveScene();
						DebugLog($"Persistent active scene restored after load | scene:{sceneName} elapsedMs:{stopwatch.Elapsed.TotalMilliseconds:0.00}");
					}
					DebugLog("Scene loaded successfully: " + sceneName);
				}
			}
			finally
			{
				DressingSceneManager dressingSceneManager = this;
				dressingSceneManager._loadingSceneNames.Remove(sceneName);
				dressingSceneManager._isLoading = false;
			}
		}

		private static void AttachLighthouseBeamFader(string sceneName, Scene loadedScene)
		{
			if (sceneName != "100_Dressing_City_01")
			{
				return;
			}
			VolumetricLightBeamSD volumetricLightBeamSD = null;
			int num = 0;
			GameObject[] rootGameObjects = loadedScene.GetRootGameObjects();
			for (int i = 0; i < rootGameObjects.Length; i++)
			{
				VolumetricLightBeamSD[] componentsInChildren = rootGameObjects[i].GetComponentsInChildren<VolumetricLightBeamSD>(includeInactive: true);
				for (int j = 0; j < componentsInChildren.Length; j++)
				{
					volumetricLightBeamSD = componentsInChildren[j];
					num++;
				}
			}
			LighthouseBeamLightFader component;
			if (num != 1 || volumetricLightBeamSD == null)
			{
				UtilityMethods.Log("[DressingSceneManager] Lighthouse beam setup expected exactly one " + $"VolumetricLightBeamSD. Scene={sceneName} Count={num}", LogType.Warning);
			}
			else if (!volumetricLightBeamSD.TryGetComponent<LighthouseBeamLightFader>(out component))
			{
				volumetricLightBeamSD.gameObject.AddComponent<LighthouseBeamLightFader>();
			}
		}

		private void UpdateLoadingFlavorText()
		{
			if (LoadingFlavorTexts.Length != 0)
			{
				_loadingFlavorTextIndex = (_loadingFlavorTextIndex + 1) % LoadingFlavorTexts.Length;
				LoadingFlavorText loadingFlavorText = LoadingFlavorTexts[_loadingFlavorTextIndex];
				LoadingScreen.UpdateLocalizedText("loading.title.preparing_world", "Preparing the World", loadingFlavorText.Key, loadingFlavorText.Fallback);
			}
		}

		private bool ShouldLoadDressingScene(string sceneName)
		{
			return true;
		}

		private IEnumerator UnloadSceneRoutine(LoadedDressingSceneData sceneData)
		{
			if (sceneData == null)
			{
				yield break;
			}
			string sceneName = sceneData.SceneName;
			DebugLog("Unloading scene: " + sceneName);
			RemoveQueuedActivation(sceneName);
			AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(sceneData.Scene);
			if (unloadOperation == null)
			{
				DebugLog("UnloadSceneAsync returned null for scene: " + sceneName);
				yield break;
			}
			while (!unloadOperation.isDone)
			{
				yield return null;
			}
			_loadedScenes.Remove(sceneName);
			_rendererDistanceCuller?.RequestTopologyRefresh();
			RestorePersistentActiveScene();
			DebugLog("Scene unloaded successfully: " + sceneName);
		}

		private IEnumerator UnloadAllScenesRoutine()
		{
			List<LoadedDressingSceneData> scenesToUnload = new List<LoadedDressingSceneData>(_loadedScenes.Values);
			for (int i = 0; i < scenesToUnload.Count; i++)
			{
				yield return UnloadSceneRoutine(scenesToUnload[i]);
			}
		}

		private DressingSceneRoot FindSceneRoot(Scene scene)
		{
			GameObject[] rootGameObjects = scene.GetRootGameObjects();
			for (int i = 0; i < rootGameObjects.Length; i++)
			{
				DressingSceneRoot componentInChildren = rootGameObjects[i].GetComponentInChildren<DressingSceneRoot>(includeInactive: true);
				if (componentInChildren != null)
				{
					return componentInChildren;
				}
			}
			return null;
		}

		private void EnqueueActivation(LoadedDressingSceneData sceneData)
		{
			IReadOnlyList<DressingActivationGroup> groups = sceneData.SceneRoot.Groups;
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < groups.Count; i++)
			{
				DressingActivationGroup dressingActivationGroup = groups[i];
				if (!(dressingActivationGroup == null) && !dressingActivationGroup.ActivateOnPrepare)
				{
					_activationQueue.Enqueue(new ActivationRequest(sceneData.SceneName, dressingActivationGroup));
					num++;
					num2 += dressingActivationGroup.ActivationWeight;
				}
			}
			sceneData.PendingActivationCount = num;
			sceneData.PendingActivationWeight = num2;
			if (_activationCoroutine == null)
			{
				_activationCoroutine = StartCoroutine(ProcessActivationQueueRoutine());
			}
		}

		private void RemoveQueuedActivation(string sceneName)
		{
			if (_activationQueue.Count == 0)
			{
				return;
			}
			Queue<ActivationRequest> queue = new Queue<ActivationRequest>();
			while (_activationQueue.Count > 0)
			{
				ActivationRequest item = _activationQueue.Dequeue();
				if (!string.Equals(item.SceneName, sceneName, StringComparison.Ordinal))
				{
					queue.Enqueue(item);
				}
			}
			while (queue.Count > 0)
			{
				_activationQueue.Enqueue(queue.Dequeue());
			}
		}

		private IEnumerator ProcessActivationQueueRoutine()
		{
			while (_activationQueue.Count > 0)
			{
				if (_useFrameBudget)
				{
					float realtimeSinceStartup = Time.realtimeSinceStartup;
					int num = 0;
					int num2 = Mathf.Max(1, _activationFrameBudgetWeight);
					bool flag = false;
					while (_activationQueue.Count > 0)
					{
						ActivationRequest request = _activationQueue.Peek();
						if (request.Group == null)
						{
							_activationQueue.Dequeue();
							continue;
						}
						int activationWeight = request.Group.ActivationWeight;
						if (flag && num + activationWeight > num2)
						{
							break;
						}
						_activationQueue.Dequeue();
						ActivateGroupRequest(request, out var elapsedMs);
						num += activationWeight;
						flag = true;
						if (elapsedMs >= _slowActivationLogThresholdMs)
						{
							DebugLog($"Slow activation group | scene:{request.SceneName} group:{request.Group.name} weight:{activationWeight} elapsedMs:{elapsedMs:0.00}");
						}
						MarkGroupActivated(request);
						if (activationWeight > num2)
						{
							DebugLog($"Activation group exceeds frame weight budget | scene:{request.SceneName} group:{request.Group.name} weight:{activationWeight} budget:{num2}");
							break;
						}
						if ((Time.realtimeSinceStartup - realtimeSinceStartup) * 1000f >= _activationFrameBudgetMs)
						{
							break;
						}
					}
				}
				else
				{
					int num3 = 0;
					while (_activationQueue.Count > 0 && num3 < Mathf.Max(1, _fallbackGroupsPerFrame))
					{
						ActivationRequest request2 = _activationQueue.Dequeue();
						if (!(request2.Group == null))
						{
							ActivateGroupRequest(request2, out var elapsedMs2);
							if (elapsedMs2 >= _slowActivationLogThresholdMs)
							{
								DebugLog($"Slow activation group | scene:{request2.SceneName} group:{request2.Group.name} weight:{request2.Group.ActivationWeight} elapsedMs:{elapsedMs2:0.00}");
							}
							MarkGroupActivated(request2);
							num3++;
						}
					}
				}
				RefreshFullyActivatedFlags();
				yield return null;
			}
			RefreshFullyActivatedFlags();
			_activationCoroutine = null;
			_rendererDistanceCuller?.RequestTopologyRefresh();
		}

		private void ActivateGroupRequest(ActivationRequest request, out float elapsedMs)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			request.Group.SetActivated(isActive: true);
			elapsedMs = (float)stopwatch.Elapsed.TotalMilliseconds;
		}

		private void MarkGroupActivated(ActivationRequest request)
		{
			if (_loadedScenes.TryGetValue(request.SceneName, out var value))
			{
				value.ActivatedGroupCount++;
				value.ActivatedWeight += request.Group.ActivationWeight;
				DebugLog("Activation group completed | scene:" + request.SceneName + " group:" + request.Group.name + " " + $"weight:{request.Group.ActivationWeight} progress:{value.ActivatedGroupCount}/{value.PendingActivationCount} " + $"weightProgress:{value.ActivatedWeight}/{value.PendingActivationWeight}");
			}
		}

		private void RefreshFullyActivatedFlags()
		{
			foreach (KeyValuePair<string, LoadedDressingSceneData> loadedScene in _loadedScenes)
			{
				LoadedDressingSceneData value = loadedScene.Value;
				IReadOnlyList<DressingActivationGroup> groups = value.SceneRoot.Groups;
				bool isFullyActivated = true;
				for (int i = 0; i < groups.Count; i++)
				{
					DressingActivationGroup dressingActivationGroup = groups[i];
					if (!(dressingActivationGroup == null) && !dressingActivationGroup.IsActivated)
					{
						isFullyActivated = false;
						break;
					}
				}
				value.IsFullyActivated = isFullyActivated;
			}
		}

		private void DebugLog(string message)
		{
			if (_logDebug)
			{
				UtilityMethods.Log("<b><color=#6FA8DC>[DressingSceneManager]</color></b> " + message);
			}
		}
	}
}
