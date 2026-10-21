using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ECM2.Examples.FirstPerson;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Optimization;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public sealed class RuntimePerformanceSampler : MonoBehaviour
{
	private const float SampleInterval = 0.25f;

	private const float SpikeCooldown = 1f;

	private const int MaxSamples = 480;

	private const int MaxSpikes = 80;

	private const float MidDistanceHotspotMin = 50f;

	private const float MidDistanceHotspotMax = 150f;

	private static readonly Dictionary<Type, UpdateMethodFlags> UpdateMethodCache = new Dictionary<Type, UpdateMethodFlags>();

	private readonly List<PerformanceSample> _samples = new List<PerformanceSample>(480);

	private readonly List<PerformanceSample> _spikes = new List<PerformanceSample>(80);

	private readonly FrameTiming[] _frameTimings = new FrameTiming[4];

	private ProfilerRecorder _mainThreadTime;

	private ProfilerRecorder _renderThreadTime;

	private ProfilerRecorder _gcAllocated;

	private ProfilerRecorder _gcUsed;

	private ProfilerRecorder _systemUsed;

	private ProfilerRecorder _drawCalls;

	private ProfilerRecorder _batches;

	private ProfilerRecorder _setPassCalls;

	private ProfilerRecorder _triangles;

	private ProfilerRecorder _vertices;

	private float _nextSampleTime;

	private float _nextSpikeTime;

	private float _smoothedFrameMs;

	private int _cameraRenderCountSinceSample;

	private bool _panelVisible;

	private string _statusMessage;

	private float _statusUntil;

	private GUIStyle _panelStyle;

	private GUIStyle _labelStyle;

	private GUIStyle _titleStyle;

	public bool HasSample { get; private set; }

	public float SpikeThresholdMs { get; set; } = 25f;

	public PerformanceSample Latest { get; private set; }

	public IReadOnlyList<PerformanceSample> Spikes => _spikes;

	private void OnEnable()
	{
		_mainThreadTime = StartRecorder(ProfilerCategory.Internal, "Main Thread", 32);
		_renderThreadTime = StartRecorder(ProfilerCategory.Internal, "Render Thread", 32);
		_gcAllocated = StartRecorder(ProfilerCategory.Memory, "GC Allocated In Frame");
		_gcUsed = StartRecorder(ProfilerCategory.Memory, "GC Used Memory");
		_systemUsed = StartRecorder(ProfilerCategory.Memory, "System Used Memory");
		_drawCalls = StartRecorder(ProfilerCategory.Render, "Draw Calls Count");
		_batches = StartRecorder(ProfilerCategory.Render, "Batches Count");
		_setPassCalls = StartRecorder(ProfilerCategory.Render, "SetPass Calls Count");
		_triangles = StartRecorder(ProfilerCategory.Render, "Triangles Count");
		_vertices = StartRecorder(ProfilerCategory.Render, "Vertices Count");
		RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
	}

	private void OnDisable()
	{
		Dispose(ref _mainThreadTime);
		Dispose(ref _renderThreadTime);
		Dispose(ref _gcAllocated);
		Dispose(ref _gcUsed);
		Dispose(ref _systemUsed);
		Dispose(ref _drawCalls);
		Dispose(ref _batches);
		Dispose(ref _setPassCalls);
		Dispose(ref _triangles);
		Dispose(ref _vertices);
		RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
	}

	private void Update()
	{
		FrameTimingManager.CaptureFrameTimings();
		float num = Time.unscaledDeltaTime * 1000f;
		_smoothedFrameMs = ((_smoothedFrameMs <= 0f) ? num : Mathf.Lerp(_smoothedFrameMs, num, 0.1f));
		if (Input.GetKeyDown(KeyCode.F6))
		{
			_panelVisible = !_panelVisible;
		}
		if (Input.GetKeyDown(KeyCode.F7))
		{
			CaptureAndSaveReport();
		}
		if (!(Time.realtimeSinceStartup < _nextSampleTime))
		{
			_nextSampleTime = Time.realtimeSinceStartup + 0.25f;
			PerformanceSample performanceSample = (Latest = CaptureSample());
			HasSample = true;
			AddBounded(_samples, performanceSample, 480);
			if (performanceSample.frameMs >= SpikeThresholdMs && Time.realtimeSinceStartup >= _nextSpikeTime)
			{
				performanceSample.reason = "Automatic Spike";
				AddBounded(_spikes, performanceSample, 80);
				_nextSpikeTime = Time.realtimeSinceStartup + 1f;
			}
		}
	}

	public void MarkCurrentFrame(string reason, bool includeDetailedSnapshot = true)
	{
		PerformanceSample performanceSample = CaptureSample();
		performanceSample.reason = reason;
		if (includeDetailedSnapshot)
		{
			performanceSample.detailedSnapshot = BuildDetailedSnapshot(performanceSample.cameraRenderCount);
			performanceSample.hasDetailedSnapshot = true;
		}
		Latest = performanceSample;
		HasSample = true;
		AddBounded(_samples, performanceSample, 480);
		AddBounded(_spikes, performanceSample, 80);
	}

	public void CaptureDetailedSnapshot()
	{
		MarkCurrentFrame("Visibility Scan");
	}

	public string CaptureAndSaveReport()
	{
		MarkCurrentFrame("Manual Build Capture");
		string text = SaveReport();
		_statusMessage = "Rapor kaydedildi:\n" + text;
		_statusUntil = Time.realtimeSinceStartup + 8f;
		Debug.Log("[PerformanceDiagnostic] Report saved: " + text);
		return text;
	}

	public void Clear()
	{
		_samples.Clear();
		_spikes.Clear();
	}

	public void ShowStatus(string message, float durationSeconds = 8f)
	{
		_statusMessage = message;
		_statusUntil = Time.realtimeSinceStartup + Mathf.Max(1f, durationSeconds);
	}

	public string SaveReport()
	{
		string reportDirectory = GetReportDirectory();
		Directory.CreateDirectory(reportDirectory);
		string text = Path.Combine(reportDirectory, $"performance-{DateTime.Now:yyyyMMdd-HHmmss}.json");
		File.WriteAllText(text, CreateReportJson());
		return text;
	}

	public string CreateReportJson()
	{
		List<PerformanceSample> list = new List<PerformanceSample>(_samples);
		List<PerformanceSample> list2 = new List<PerformanceSample>(_spikes);
		PerformanceSample performanceSample = FindLatestDetailedSample(list, list2);
		if (performanceSample == null)
		{
			performanceSample = CaptureSample();
			performanceSample.reason = "Report Export Detail";
			performanceSample.detailedSnapshot = BuildDetailedSnapshot(performanceSample.cameraRenderCount);
			performanceSample.hasDetailedSnapshot = true;
			AddBounded(list, performanceSample, 480);
		}
		return JsonUtility.ToJson(new PerformanceReport
		{
			diagnosticVersion = "2026.07.09.player-movement-v3",
			createdAt = DateTime.Now.ToString("O"),
			unityVersion = Application.unityVersion,
			platform = Application.platform.ToString(),
			isEditor = Application.isEditor,
			isDevelopmentBuild = Debug.isDebugBuild,
			graphicsDevice = SystemInfo.graphicsDeviceName,
			graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
			processor = SystemInfo.processorType,
			systemMemoryMb = SystemInfo.systemMemorySize,
			graphicsMemoryMb = SystemInfo.graphicsMemorySize,
			screenWidth = Screen.width,
			screenHeight = Screen.height,
			qualityLevel = QualitySettings.names[QualitySettings.GetQualityLevel()],
			targetFrameRate = Application.targetFrameRate,
			vSyncCount = QualitySettings.vSyncCount,
			sampleCount = list.Count,
			spikeCount = list2.Count,
			latestDetailedSample = performanceSample,
			latestDetailedSnapshot = performanceSample.detailedSnapshot,
			samples = list,
			spikes = list2
		}, prettyPrint: true);
	}

	private static PerformanceSample FindLatestDetailedSample(List<PerformanceSample> samples, List<PerformanceSample> spikes)
	{
		for (int num = samples.Count - 1; num >= 0; num--)
		{
			if (HasDetailedSnapshot(samples[num]))
			{
				return samples[num];
			}
		}
		for (int num2 = spikes.Count - 1; num2 >= 0; num2--)
		{
			if (HasDetailedSnapshot(spikes[num2]))
			{
				return spikes[num2];
			}
		}
		return null;
	}

	private static bool HasDetailedSnapshot(PerformanceSample sample)
	{
		if (sample != null && sample.hasDetailedSnapshot && sample.detailedSnapshot != null)
		{
			return sample.detailedSnapshot.loadedGameObjectCount > 0;
		}
		return false;
	}

	private static string GetReportDirectory()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
		return Path.Combine(string.IsNullOrWhiteSpace(folderPath) ? Application.persistentDataPath : folderPath, "SushiPerformance");
	}

	private PerformanceSample CaptureSample()
	{
		float mainThreadMs = RecorderAverageMs(_mainThreadTime);
		float renderThreadMs = RecorderAverageMs(_renderThreadTime);
		float gpuMs = 0f;
		float b = 0f;
		if (FrameTimingManager.GetLatestTimings((uint)_frameTimings.Length, _frameTimings) != 0)
		{
			b = (float)_frameTimings[0].cpuFrameTime;
			gpuMs = (float)_frameTimings[0].gpuFrameTime;
		}
		float num = Mathf.Max(_smoothedFrameMs, b);
		PerformancePlayerMovementSnapshot performancePlayerMovementSnapshot = CapturePlayerMovementSnapshot();
		Vector3 vector = ((performancePlayerMovementSnapshot != null && performancePlayerMovementSnapshot.hasLocalPlayer) ? new Vector3(performancePlayerMovementSnapshot.playerX, performancePlayerMovementSnapshot.playerY, performancePlayerMovementSnapshot.playerZ) : ResolvePlayerPosition());
		Classify(num, mainThreadMs, renderThreadMs, gpuMs, out var verdict, out var explanation);
		return new PerformanceSample
		{
			reason = "Sample",
			sunlightDiagnosticMode = RuntimeSunlightPerformanceDiagnostic.CurrentModeName,
			realtime = Time.realtimeSinceStartup,
			fps = ((num > 0.01f) ? (1000f / num) : 0f),
			frameMs = num,
			mainThreadMs = mainThreadMs,
			renderThreadMs = renderThreadMs,
			gpuMs = gpuMs,
			gcAllocatedBytes = RecorderValue(_gcAllocated),
			gcUsedBytes = RecorderValue(_gcUsed),
			systemUsedBytes = RecorderValue(_systemUsed),
			batches = RecorderInt(_batches),
			drawCalls = RecorderInt(_drawCalls),
			setPassCalls = RecorderInt(_setPassCalls),
			triangles = RecorderInt(_triangles),
			vertices = RecorderInt(_vertices),
			cameraRenderCount = ConsumeCameraRenderCount(),
			sceneName = SceneManager.GetActiveScene().name,
			playerX = vector.x,
			playerY = vector.y,
			playerZ = vector.z,
			playerHasLocalPlayer = (performancePlayerMovementSnapshot?.hasLocalPlayer ?? false),
			playerInputX = (performancePlayerMovementSnapshot?.inputX ?? 0f),
			playerInputY = (performancePlayerMovementSnapshot?.inputY ?? 0f),
			playerInputMagnitude = (performancePlayerMovementSnapshot?.inputMagnitude ?? 0f),
			playerSpeed = (performancePlayerMovementSnapshot?.speed ?? 0f),
			playerIsMovingInput = (performancePlayerMovementSnapshot?.isMovingInput ?? false),
			playerIsNetworkSwimming = (performancePlayerMovementSnapshot?.isNetworkSwimming ?? false),
			playerIsCharacterSwimming = (performancePlayerMovementSnapshot?.isCharacterSwimming ?? false),
			playerIsSeaScooter = (performancePlayerMovementSnapshot?.isSeaScooter ?? false),
			playerIsUnderWater = (performancePlayerMovementSnapshot?.isUnderWater ?? false),
			playerIsGrounded = (performancePlayerMovementSnapshot?.isGrounded ?? false),
			playerMovementMode = ((performancePlayerMovementSnapshot != null) ? performancePlayerMovementSnapshot.movementMode : string.Empty),
			playerSeaLayer = ((performancePlayerMovementSnapshot != null) ? performancePlayerMovementSnapshot.seaLayer : string.Empty),
			verdict = verdict,
			explanation = explanation
		};
	}

	private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
	{
		if (camera != null)
		{
			_cameraRenderCountSinceSample++;
		}
	}

	private int ConsumeCameraRenderCount()
	{
		int cameraRenderCountSinceSample = _cameraRenderCountSinceSample;
		_cameraRenderCountSinceSample = 0;
		return cameraRenderCountSinceSample;
	}

	private PerformanceDetailedSnapshot BuildDetailedSnapshot(int cameraRenderCount)
	{
		Camera main = Camera.main;
		PerformanceDetailedSnapshot performanceDetailedSnapshot = new PerformanceDetailedSnapshot
		{
			cameraName = ((main != null) ? main.name : "Main Camera not found"),
			cameraRenderCount = cameraRenderCount,
			sunlightDiagnosticMode = RuntimeSunlightPerformanceDiagnostic.CurrentModeName,
			sunlightDiagnosticTargetSceneCount = RuntimeSunlightPerformanceDiagnostic.TargetSceneCount,
			sunlightDiagnosticTargetRootCount = RuntimeSunlightPerformanceDiagnostic.TargetRootCount,
			sunlightDiagnosticTargetRendererCount = RuntimeSunlightPerformanceDiagnostic.TargetRendererCount,
			sunlightDiagnosticTargetLodGroupCount = RuntimeSunlightPerformanceDiagnostic.TargetLodGroupCount,
			topLayers = new List<PerformanceRankedEntry>(),
			topShaders = new List<PerformanceRankedEntry>(),
			topRenderers = new List<PerformanceRendererEntry>(),
			topLodGroups = new List<PerformanceLodGroupEntry>(),
			distanceBands = CreateDistanceBands(),
			scenes = new List<PerformanceSceneEntry>(),
			cameras = new List<PerformanceCameraEntry>(),
			topComponents = new List<PerformanceRankedEntry>(),
			topMonoBehaviours = new List<PerformanceBehaviourEntry>(),
			topUpdateCandidates = new List<PerformanceBehaviourEntry>(),
			topCloneGroups = new List<PerformanceRankedEntry>(),
			topHierarchyRoots = new List<PerformanceHierarchyRootEntry>(),
			midDistanceMin = 50f,
			midDistanceMax = 150f,
			midDistanceRootHotspots = new List<PerformanceRenderHotspotEntry>(),
			midDistanceShaderHotspots = new List<PerformanceRenderHotspotEntry>()
		};
		performanceDetailedSnapshot.playerMovement = CapturePlayerMovementSnapshot();
		CaptureCameras(performanceDetailedSnapshot);
		CaptureLoadedScenes(performanceDetailedSnapshot);
		CaptureCpuSnapshot(performanceDetailedSnapshot);
		CaptureResidentDiagnostics(performanceDetailedSnapshot);
		CaptureGastroPanDiagnostics(performanceDetailedSnapshot);
		CaptureRestaurantDiagnostics(performanceDetailedSnapshot);
		performanceDetailedSnapshot.codeTimings = RuntimePerformanceCodeTiming.Capture();
		CaptureDressingDiagnostics(performanceDetailedSnapshot, main);
		CaptureUnderwaterChunkDiagnostics(performanceDetailedSnapshot);
		if (main == null)
		{
			return performanceDetailedSnapshot;
		}
		Plane[] planes = GeometryUtility.CalculateFrustumPlanes(main);
		Renderer[] array = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
		LODGroup[] array2 = UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
		Dictionary<string, PerformanceRankedEntry> dictionary = new Dictionary<string, PerformanceRankedEntry>();
		Dictionary<string, PerformanceRankedEntry> dictionary2 = new Dictionary<string, PerformanceRankedEntry>();
		Dictionary<string, PerformanceRenderHotspotEntry> dictionary3 = new Dictionary<string, PerformanceRenderHotspotEntry>(StringComparer.Ordinal);
		Dictionary<string, PerformanceRenderHotspotEntry> dictionary4 = new Dictionary<string, PerformanceRenderHotspotEntry>(StringComparer.Ordinal);
		HashSet<int> hashSet = new HashSet<int>();
		performanceDetailedSnapshot.lodBias = QualitySettings.lodBias;
		performanceDetailedSnapshot.shadowDistance = QualitySettings.shadowDistance;
		performanceDetailedSnapshot.cameraFarClip = main.farClipPlane;
		performanceDetailedSnapshot.cameraLayerCullDistances = CaptureLayerCullDistances(main);
		Renderer[] array3 = array;
		foreach (Renderer renderer in array3)
		{
			if (renderer != null && renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy)
			{
				performanceDetailedSnapshot.enabledRendererCount++;
			}
		}
		LODGroup[] array4 = array2;
		foreach (LODGroup lODGroup in array4)
		{
			if (lODGroup == null || !lODGroup.enabled || !lODGroup.gameObject.activeInHierarchy)
			{
				continue;
			}
			performanceDetailedSnapshot.totalLodGroupCount++;
			LOD[] lODs = lODGroup.GetLODs();
			if (lODs.Length == 0)
			{
				continue;
			}
			if (lODs.Length == 1)
			{
				performanceDetailedSnapshot.singleLevelLodGroupCount++;
			}
			if (lODs[^1].screenRelativeTransitionHeight <= 0.01f)
			{
				performanceDetailedSnapshot.lateCullLodGroupCount++;
			}
			LOD[] array5 = lODs;
			for (int j = 0; j < array5.Length; j++)
			{
				array3 = array5[j].renderers;
				foreach (Renderer renderer2 in array3)
				{
					if (renderer2 != null)
					{
						hashSet.Add(renderer2.GetInstanceID());
					}
				}
			}
			Vector3 vector = lODGroup.transform.TransformPoint(lODGroup.localReferencePoint);
			float num = lODGroup.size * MaxAbsComponent(lODGroup.transform.lossyScale);
			Bounds bounds = new Bounds(vector, Vector3.one * Mathf.Max(0.01f, num));
			if (!GeometryUtility.TestPlanesAABB(planes, bounds))
			{
				continue;
			}
			performanceDetailedSnapshot.frustumLodGroupCount++;
			float num2 = CalculateRelativeScreenHeight(main, vector, num);
			int num3 = ResolveLodIndex(lODs, num2);
			if (num3 < 0)
			{
				performanceDetailedSnapshot.estimatedCulledLodGroupCount++;
				continue;
			}
			if (num3 == 0)
			{
				performanceDetailedSnapshot.estimatedLod0GroupCount++;
			}
			float distance = Vector3.Distance(main.transform.position, vector);
			long num4 = 0L;
			int num5 = 0;
			int num6 = 0;
			array3 = lODs[num3].renderers;
			foreach (Renderer renderer3 in array3)
			{
				if (IsRendererCandidate(renderer3, main, planes))
				{
					num6++;
					long num7 = EstimateTriangles(renderer3);
					num4 += num7;
					num5 += renderer3.sharedMaterials.Length;
					AccumulateRenderer(performanceDetailedSnapshot, renderer3, num7, main, dictionary, dictionary2, dictionary3, dictionary4, selectedByLod: true);
				}
			}
			performanceDetailedSnapshot.estimatedActiveLodRenderers += num6;
			performanceDetailedSnapshot.estimatedActiveLodTriangles += num4;
			performanceDetailedSnapshot.estimatedActiveLodMaterialSlots += num5;
			AddDistanceBand(performanceDetailedSnapshot.distanceBands, distance, num6, num4, num5);
			InsertHeavyLodGroup(performanceDetailedSnapshot.topLodGroups, new PerformanceLodGroupEntry
			{
				name = lODGroup.name,
				path = GetHierarchyPath(lODGroup.transform),
				scene = lODGroup.gameObject.scene.name,
				distance = distance,
				worldSize = num,
				lodCount = lODs.Length,
				estimatedLod = num3,
				relativeScreenHeight = num2,
				selectedTriangles = num4,
				selectedRendererCount = num6,
				selectedMaterialSlots = num5,
				lastTransitionHeight = lODs[^1].screenRelativeTransitionHeight
			});
		}
		array3 = array;
		foreach (Renderer renderer4 in array3)
		{
			if (IsRendererCandidate(renderer4, main, planes) && !hashSet.Contains(renderer4.GetInstanceID()))
			{
				performanceDetailedSnapshot.nonLodFrustumRendererCount++;
				long num8 = EstimateTriangles(renderer4);
				int num9 = renderer4.sharedMaterials.Length;
				performanceDetailedSnapshot.nonLodFrustumTriangles += num8;
				performanceDetailedSnapshot.nonLodFrustumMaterialSlots += num9;
				AccumulateRenderer(performanceDetailedSnapshot, renderer4, num8, main, dictionary, dictionary2, dictionary3, dictionary4, selectedByLod: false);
				AddDistanceBand(performanceDetailedSnapshot.distanceBands, Vector3.Distance(main.transform.position, renderer4.bounds.center), 1, num8, num9);
			}
		}
		performanceDetailedSnapshot.frustumRendererCount = performanceDetailedSnapshot.estimatedActiveLodRenderers + performanceDetailedSnapshot.nonLodFrustumRendererCount;
		performanceDetailedSnapshot.frustumTriangles = performanceDetailedSnapshot.estimatedActiveLodTriangles + performanceDetailedSnapshot.nonLodFrustumTriangles;
		performanceDetailedSnapshot.frustumMaterialSlots = performanceDetailedSnapshot.estimatedActiveLodMaterialSlots + performanceDetailedSnapshot.nonLodFrustumMaterialSlots;
		performanceDetailedSnapshot.topLayers = SortRanks(dictionary, 12);
		performanceDetailedSnapshot.topShaders = SortRanks(dictionary2, 12);
		performanceDetailedSnapshot.midDistanceRootHotspots = SortHotspots(dictionary3, 20);
		performanceDetailedSnapshot.midDistanceShaderHotspots = SortHotspots(dictionary4, 20);
		CaptureCullingComponents(performanceDetailedSnapshot, main);
		return performanceDetailedSnapshot;
	}

	private static void CaptureResidentDiagnostics(PerformanceDetailedSnapshot snapshot)
	{
		PerformanceResidentMergeDiagnostics performanceResidentMergeDiagnostics = new PerformanceResidentMergeDiagnostics
		{
			deferredReasons = new List<PerformanceResidentMergeReasonEntry>(),
			discardedReasons = new List<PerformanceResidentMergeReasonEntry>()
		};
		MonoBehaviour[] array = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		foreach (MonoBehaviour monoBehaviour in array)
		{
			if (monoBehaviour == null)
			{
				continue;
			}
			Type type = monoBehaviour.GetType();
			string text = type.FullName ?? type.Name;
			if (text.EndsWith(".ResidentController", StringComparison.Ordinal) || text == "ResidentController")
			{
				if (ReadMemberBool(monoBehaviour, "IsUsingBakedVisual"))
				{
					performanceResidentMergeDiagnostics.bakedResidentVisualCount++;
				}
				if (ReadMemberBool(monoBehaviour, "IsUsingRuntimeCustomizationFallback"))
				{
					performanceResidentMergeDiagnostics.runtimeCustomizedResidentCount++;
				}
				if (ReadMemberBool(monoBehaviour, "HasActiveResidentAnimator"))
				{
					performanceResidentMergeDiagnostics.activeResidentAnimatorCount++;
				}
				performanceResidentMergeDiagnostics.visibleResidentSkinnedRendererCount += CountVisibleSkinnedMeshRenderersIn(monoBehaviour.transform);
				performanceResidentMergeDiagnostics.activeResidentMagicaClothCount += CountActiveBehavioursByNameIn(monoBehaviour.transform, "MagicaCloth2.MagicaCloth");
				performanceResidentMergeDiagnostics.activeResidentMagicaColliderCount += CountActiveBehavioursByNameIn(monoBehaviour.transform, "MagicaCloth2.MagicaCapsuleCollider");
			}
			if ((text.EndsWith(".ResidentManager", StringComparison.Ordinal) || !(text != "ResidentManager")) && !performanceResidentMergeDiagnostics.managerFound)
			{
				performanceResidentMergeDiagnostics.managerFound = true;
				performanceResidentMergeDiagnostics.residentCount = CountCollectionMember(monoBehaviour, "Residents", "_residents");
				performanceResidentMergeDiagnostics.restaurantQueueCount = CountCollectionMember(monoBehaviour, "_restaurantQueue", "restaurantQueue");
				performanceResidentMergeDiagnostics.restaurantQueuedResidentCount = CountCollectionMember(monoBehaviour, "_restaurantQueuedResidents", "restaurantQueuedResidents");
			}
		}
		performanceResidentMergeDiagnostics.residentControllerCount = CountBehavioursByName("ResidentController");
		performanceResidentMergeDiagnostics.residentBrainCount = CountBehavioursByName("ResidentBrain");
		performanceResidentMergeDiagnostics.skinnedMeshRendererCount = CountComponentsByTypeName<SkinnedMeshRenderer>(null);
		performanceResidentMergeDiagnostics.bozoOutfitCount = CountBehavioursByName("Bozo.ModularCharacters.Outfit");
		performanceResidentMergeDiagnostics.bozoBodyShapeModifierCount = CountBehavioursByName("Bozo.ModularCharacters.BodyShapeModifier");
		performanceResidentMergeDiagnostics.bozoClothSupportCount = CountBehavioursByName("Bozo.ModularCharacters.BoZo_MagicaClothSupport");
		performanceResidentMergeDiagnostics.magicaClothCount = CountBehavioursByName("MagicaCloth2.MagicaCloth");
		performanceResidentMergeDiagnostics.magicaCapsuleColliderCount = CountBehavioursByName("MagicaCloth2.MagicaCapsuleCollider");
		snapshot.residentMergeDiagnostics = performanceResidentMergeDiagnostics;
	}

	private static int CountBehavioursByName(string typeName)
	{
		int num = 0;
		MonoBehaviour[] array = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		foreach (MonoBehaviour monoBehaviour in array)
		{
			if (!(monoBehaviour == null))
			{
				string text = monoBehaviour.GetType().FullName ?? monoBehaviour.GetType().Name;
				if (text == typeName || text.EndsWith("." + typeName, StringComparison.Ordinal))
				{
					num++;
				}
			}
		}
		return num;
	}

	private static int CountActiveBehavioursByNameIn(Transform root, string typeName)
	{
		if (root == null)
		{
			return 0;
		}
		int num = 0;
		MonoBehaviour[] componentsInChildren = root.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
		foreach (MonoBehaviour monoBehaviour in componentsInChildren)
		{
			if (!(monoBehaviour == null) && monoBehaviour.isActiveAndEnabled)
			{
				string text = monoBehaviour.GetType().FullName ?? monoBehaviour.GetType().Name;
				if (text == typeName || text.EndsWith("." + typeName, StringComparison.Ordinal))
				{
					num++;
				}
			}
		}
		return num;
	}

	private static int CountVisibleSkinnedMeshRenderersIn(Transform root)
	{
		if (root == null)
		{
			return 0;
		}
		int num = 0;
		SkinnedMeshRenderer[] componentsInChildren = root.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
		foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren)
		{
			if (skinnedMeshRenderer != null && skinnedMeshRenderer.enabled && skinnedMeshRenderer.gameObject.activeInHierarchy && skinnedMeshRenderer.isVisible)
			{
				num++;
			}
		}
		return num;
	}

	private static int CountComponentsByTypeName<T>(string typeName) where T : Component
	{
		if (string.IsNullOrEmpty(typeName))
		{
			return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
		}
		int num = 0;
		Component[] array = UnityEngine.Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		foreach (Component component in array)
		{
			if (!(component == null))
			{
				string text = component.GetType().FullName ?? component.GetType().Name;
				if (text == typeName || text.EndsWith("." + typeName, StringComparison.Ordinal))
				{
					num++;
				}
			}
		}
		return num;
	}

	private static void CaptureGastroPanDiagnostics(PerformanceDetailedSnapshot snapshot)
	{
		PerformanceGastroPanDiagnostics performanceGastroPanDiagnostics = new PerformanceGastroPanDiagnostics
		{
			ingredientGroups = new List<PerformanceGastroPanIngredientEntry>(),
			topInstances = new List<PerformanceGastroPanInstanceEntry>()
		};
		Dictionary<string, PerformanceGastroPanIngredientEntry> dictionary = new Dictionary<string, PerformanceGastroPanIngredientEntry>(StringComparer.Ordinal);
		MonoBehaviour[] array = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		foreach (MonoBehaviour monoBehaviour in array)
		{
			if (monoBehaviour == null)
			{
				continue;
			}
			string text = monoBehaviour.GetType().FullName ?? monoBehaviour.GetType().Name;
			if (!text.EndsWith(".GastroPan", StringComparison.Ordinal) && text != "GastroPan")
			{
				continue;
			}
			string text2 = ReadMemberString(monoBehaviour, "IngredientId");
			if (string.IsNullOrWhiteSpace(text2))
			{
				text2 = "(not ready)";
			}
			if (!dictionary.TryGetValue(text2, out var value))
			{
				value = new PerformanceGastroPanIngredientEntry
				{
					ingredientId = text2,
					minimumIngredientCount = int.MaxValue
				};
				dictionary.Add(text2, value);
			}
			int num = ReadMemberInt(monoBehaviour, "IngredientCount");
			int num2 = ReadMemberInt(monoBehaviour, "MaxCapacity");
			PerformanceGastroPanInstanceEntry performanceGastroPanInstanceEntry = new PerformanceGastroPanInstanceEntry();
			performanceGastroPanInstanceEntry.name = monoBehaviour.name;
			performanceGastroPanInstanceEntry.path = GetHierarchyPath(monoBehaviour.transform);
			performanceGastroPanInstanceEntry.scene = monoBehaviour.gameObject.scene.name;
			performanceGastroPanInstanceEntry.ingredientId = text2;
			performanceGastroPanInstanceEntry.ingredientCount = num;
			performanceGastroPanInstanceEntry.maxCapacity = num2;
			performanceGastroPanInstanceEntry.activeInHierarchy = monoBehaviour.gameObject.activeInHierarchy;
			performanceGastroPanInstanceEntry.networkSpawned = ReadMemberBool(monoBehaviour, "IsSpawned");
			PerformanceGastroPanInstanceEntry performanceGastroPanInstanceEntry2 = performanceGastroPanInstanceEntry;
			performanceGastroPanDiagnostics.totalPanCount++;
			performanceGastroPanDiagnostics.ingredientUnitsTotal += num;
			performanceGastroPanDiagnostics.capacityTotal += num2;
			if (monoBehaviour.gameObject.activeInHierarchy)
			{
				performanceGastroPanDiagnostics.activePanCount++;
			}
			if (performanceGastroPanInstanceEntry2.networkSpawned)
			{
				performanceGastroPanDiagnostics.networkSpawnedPanCount++;
			}
			if (num == 0)
			{
				performanceGastroPanDiagnostics.emptyPanCount++;
			}
			value.panCount++;
			value.ingredientUnitsTotal += num;
			value.capacityTotal += num2;
			value.minimumIngredientCount = Mathf.Min(value.minimumIngredientCount, num);
			value.maximumIngredientCount = Mathf.Max(value.maximumIngredientCount, num);
			if (num == 0)
			{
				value.emptyPanCount++;
			}
			MonoBehaviour[] componentsInChildren = monoBehaviour.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
			foreach (MonoBehaviour monoBehaviour2 in componentsInChildren)
			{
				if (!(monoBehaviour2 == null))
				{
					string text3 = monoBehaviour2.GetType().FullName ?? monoBehaviour2.GetType().Name;
					if (text3.EndsWith(".PanIngredientGraphics", StringComparison.Ordinal) || !(text3 != "PanIngredientGraphics"))
					{
						AccumulateGastroPanVisual(monoBehaviour2, performanceGastroPanDiagnostics, value, performanceGastroPanInstanceEntry2);
					}
				}
			}
			performanceGastroPanDiagnostics.topInstances.Add(performanceGastroPanInstanceEntry2);
		}
		performanceGastroPanDiagnostics.ingredientGroups.AddRange(dictionary.Values);
		performanceGastroPanDiagnostics.ingredientGroups.Sort((PerformanceGastroPanIngredientEntry a, PerformanceGastroPanIngredientEntry b) =>
		{
			int num3 = b.visibleRendererCount.CompareTo(a.visibleRendererCount);
			return (num3 == 0) ? b.panCount.CompareTo(a.panCount) : num3;
		});
		performanceGastroPanDiagnostics.topInstances.Sort((PerformanceGastroPanInstanceEntry a, PerformanceGastroPanInstanceEntry b) =>
		{
			int num3 = b.visibleRendererCount.CompareTo(a.visibleRendererCount);
			return (num3 == 0) ? b.totalRendererCount.CompareTo(a.totalRendererCount) : num3;
		});
		if (performanceGastroPanDiagnostics.topInstances.Count > 30)
		{
			performanceGastroPanDiagnostics.topInstances.RemoveRange(30, performanceGastroPanDiagnostics.topInstances.Count - 30);
		}
		snapshot.gastroPanDiagnostics = performanceGastroPanDiagnostics;
	}

	private static void AccumulateGastroPanVisual(MonoBehaviour graphics, PerformanceGastroPanDiagnostics diagnostics, PerformanceGastroPanIngredientEntry group, PerformanceGastroPanInstanceEntry instance)
	{
		diagnostics.visualCopyCount++;
		group.visualCopyCount++;
		instance.visualCopyCount++;
		IEnumerable enumerable = ReadMemberValue(graphics, "IngredientObjects") as IEnumerable;
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		if (enumerable != null)
		{
			foreach (object item in enumerable)
			{
				if (item is GameObject gameObject)
				{
					num++;
					if (gameObject.activeSelf)
					{
						num2++;
					}
					if (gameObject.activeInHierarchy)
					{
						num3++;
					}
				}
			}
		}
		int num4 = graphics.GetComponentsInChildren<Transform>(includeInactive: true).Length;
		Renderer[] componentsInChildren = graphics.GetComponentsInChildren<Renderer>(includeInactive: true);
		int num5 = 0;
		Renderer[] array = componentsInChildren;
		foreach (Renderer renderer in array)
		{
			if (renderer != null && renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy)
			{
				num5++;
			}
		}
		diagnostics.totalVisualGameObjectCount += num4;
		diagnostics.totalIngredientSlotCount += num;
		diagnostics.selectedIngredientObjectCount += num2;
		diagnostics.visibleIngredientObjectCount += num3;
		diagnostics.totalRendererCount += componentsInChildren.Length;
		diagnostics.visibleRendererCount += num5;
		group.totalVisualGameObjectCount += num4;
		group.totalIngredientSlotCount += num;
		group.selectedIngredientObjectCount += num2;
		group.visibleIngredientObjectCount += num3;
		group.totalRendererCount += componentsInChildren.Length;
		group.visibleRendererCount += num5;
		instance.totalVisualGameObjectCount += num4;
		instance.totalIngredientSlotCount += num;
		instance.selectedIngredientObjectCount += num2;
		instance.visibleIngredientObjectCount += num3;
		instance.totalRendererCount += componentsInChildren.Length;
		instance.visibleRendererCount += num5;
	}

	private static void CaptureRestaurantDiagnostics(PerformanceDetailedSnapshot snapshot)
	{
		PerformanceRestaurantDiagnostics performanceRestaurantDiagnostics = new PerformanceRestaurantDiagnostics();
		Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
		Dictionary<string, int> counts2 = new Dictionary<string, int>(StringComparer.Ordinal);
		MonoBehaviour[] array = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		foreach (MonoBehaviour monoBehaviour in array)
		{
			if (monoBehaviour == null)
			{
				continue;
			}
			switch (monoBehaviour.GetType().FullName ?? monoBehaviour.GetType().Name)
			{
			case "PaperPixelGames.Core.Restaurant.RestaurantTable":
				performanceRestaurantDiagnostics.restaurantTableCount++;
				if (monoBehaviour.isActiveAndEnabled)
				{
					performanceRestaurantDiagnostics.activeRestaurantTableCount++;
				}
				if (ReadMemberBool(monoBehaviour, "IsServer"))
				{
					performanceRestaurantDiagnostics.serverRestaurantTableCount++;
				}
				if (ReadMemberBool(monoBehaviour, "_takeOrderTimerActive"))
				{
					performanceRestaurantDiagnostics.activeTakeOrderTimerCount++;
				}
				if (ReadMemberBool(monoBehaviour, "_deliverOrderTimerActive"))
				{
					performanceRestaurantDiagnostics.activeDeliverOrderTimerCount++;
				}
				IncrementStringCount(counts, ReadMemberString(monoBehaviour, "CurrentState"));
				break;
			case "PaperPixelGames.Core.Restaurant.ConveyorBeltTable":
				performanceRestaurantDiagnostics.conveyorTableCount++;
				if (monoBehaviour.isActiveAndEnabled)
				{
					performanceRestaurantDiagnostics.activeConveyorTableCount++;
				}
				if (ReadMemberBool(monoBehaviour, "IsServer"))
				{
					performanceRestaurantDiagnostics.serverConveyorTableCount++;
				}
				if (ReadMemberBool(monoBehaviour, "_takingOrderTimerActive"))
				{
					performanceRestaurantDiagnostics.activeConveyorMealTimerCount++;
				}
				if (ReadMemberBool(ReadMemberValue(monoBehaviour, "_paymentScanSession"), "IsActive"))
				{
					performanceRestaurantDiagnostics.activePaymentScanCount++;
				}
				IncrementStringCount(counts2, ReadMemberString(monoBehaviour, "CurrentState"));
				break;
			case "PaperPixelGames.Core.Kitchen.ConveyorBelt":
				performanceRestaurantDiagnostics.conveyorBeltCount++;
				if (monoBehaviour.isActiveAndEnabled)
				{
					performanceRestaurantDiagnostics.activeConveyorBeltCount++;
				}
				if (ReadMemberBool(ReadMemberValue(monoBehaviour, "_isRunning"), "Value"))
				{
					performanceRestaurantDiagnostics.runningConveyorBeltCount++;
				}
				performanceRestaurantDiagnostics.configuredConveyorSlotCount += ReadMemberInt(monoBehaviour, "_resolvedSlotCount");
				performanceRestaurantDiagnostics.networkConveyorSlotCount += CountCollectionMember(monoBehaviour, "_slotOccupants");
				break;
			case "PaperPixelGames.Framework.Indicator.OffscreenIndicatorHandler":
				performanceRestaurantDiagnostics.offscreenIndicatorHandlerCount++;
				performanceRestaurantDiagnostics.registeredOffscreenIndicatorCount += CountCollectionMember(monoBehaviour, "_entries");
				performanceRestaurantDiagnostics.offscreenRegisteredEntryCount += ReadMemberInt(monoBehaviour, "RegisteredEntryCount");
				performanceRestaurantDiagnostics.offscreenUpdatedEntryCount += ReadMemberInt(monoBehaviour, "LastUpdatedEntryCount", "_lastUpdatedEntryCount");
				break;
			case "PaperPixelGames.Core.Quests.QuestIndicatorBridge":
				performanceRestaurantDiagnostics.questIndicatorBridgeCount++;
				performanceRestaurantDiagnostics.trackedQuestIndicatorGroupCount += CountCollectionMember(monoBehaviour, "_activeTargetsByQuestId");
				performanceRestaurantDiagnostics.questHighlightCandidateCount += ReadMemberInt(monoBehaviour, "LastHighlightCandidateCount", "_lastHighlightCandidateCount");
				break;
			case "PaperPixelGames.Framework.WorldUISystem.WorldUIManager":
				performanceRestaurantDiagnostics.worldUiManagerCount++;
				performanceRestaurantDiagnostics.registeredWorldUiAnchorCount += CountCollectionMember(monoBehaviour, "_anchors");
				performanceRestaurantDiagnostics.worldUiRegisteredAnchorCount += ReadMemberInt(monoBehaviour, "RegisteredAnchorCount");
				performanceRestaurantDiagnostics.worldUiUpdatedAnchorCount += ReadMemberInt(monoBehaviour, "LastUpdatedAnchorCount", "_lastUpdatedAnchorCount");
				break;
			}
		}
		performanceRestaurantDiagnostics.restaurantTableStates = BuildStateCounts(counts);
		performanceRestaurantDiagnostics.conveyorTableStates = BuildStateCounts(counts2);
		snapshot.restaurantDiagnostics = performanceRestaurantDiagnostics;
	}

	private static void IncrementStringCount(Dictionary<string, int> counts, string value)
	{
		string key = (string.IsNullOrWhiteSpace(value) ? "Unknown" : value);
		counts.TryGetValue(key, out var value2);
		counts[key] = value2 + 1;
	}

	private static List<PerformanceStateCountEntry> BuildStateCounts(Dictionary<string, int> counts)
	{
		List<PerformanceStateCountEntry> list = new List<PerformanceStateCountEntry>(counts.Count);
		foreach (KeyValuePair<string, int> count in counts)
		{
			list.Add(new PerformanceStateCountEntry
			{
				state = count.Key,
				count = count.Value
			});
		}
		list.Sort((PerformanceStateCountEntry a, PerformanceStateCountEntry b) =>
		{
			int num = b.count.CompareTo(a.count);
			return (num == 0) ? string.CompareOrdinal(a.state, b.state) : num;
		});
		return list;
	}

	private static void CaptureDressingDiagnostics(PerformanceDetailedSnapshot snapshot, Camera camera)
	{
		PerformanceDressingDiagnostics performanceDressingDiagnostics = new PerformanceDressingDiagnostics
		{
			groups = new List<PerformanceDressingGroupEntry>()
		};
		MonoBehaviour[] array = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		MonoBehaviour[] array2 = array;
		foreach (MonoBehaviour monoBehaviour in array2)
		{
			if (!(monoBehaviour == null))
			{
				string text = monoBehaviour.GetType().FullName ?? monoBehaviour.GetType().Name;
				if (text.EndsWith(".DressingSceneManager", StringComparison.Ordinal) || !(text != "DressingSceneManager"))
				{
					performanceDressingDiagnostics.managerFound = true;
					performanceDressingDiagnostics.managerIsLoading = ReadMemberBool(monoBehaviour, "IsLoading");
					performanceDressingDiagnostics.managerIsStartupLoading = ReadMemberBool(monoBehaviour, "IsStartupLoading");
					performanceDressingDiagnostics.loadedSceneCount = CountCollectionMember(monoBehaviour, "_loadedScenes", "LoadedDressingSceneCount");
					performanceDressingDiagnostics.pendingActivationGroupCount = CountCollectionMember(monoBehaviour, "_activationQueue", "PendingActivationGroupCount");
					performanceDressingDiagnostics.activationQueueRunning = ReadMemberValue(monoBehaviour, "_activationCoroutine") != null;
					break;
				}
			}
		}
		array2 = array;
		foreach (MonoBehaviour monoBehaviour2 in array2)
		{
			if (monoBehaviour2 == null)
			{
				continue;
			}
			string text2 = monoBehaviour2.GetType().FullName ?? monoBehaviour2.GetType().Name;
			if (text2.EndsWith(".DressingActivationGroup", StringComparison.Ordinal) || !(text2 != "DressingActivationGroup"))
			{
				PerformanceDressingGroupEntry performanceDressingGroupEntry = CaptureDressingGroupEntry(monoBehaviour2, camera);
				performanceDressingDiagnostics.groups.Add(performanceDressingGroupEntry);
				performanceDressingDiagnostics.totalGroupCount++;
				if (performanceDressingGroupEntry.activeInHierarchy)
				{
					performanceDressingDiagnostics.activeGroupCount++;
				}
				else
				{
					performanceDressingDiagnostics.inactiveGroupCount++;
				}
				performanceDressingDiagnostics.totalGameObjectCount += performanceDressingGroupEntry.gameObjectCount;
				performanceDressingDiagnostics.activeGameObjectCount += performanceDressingGroupEntry.activeGameObjectCount;
				performanceDressingDiagnostics.totalRendererCount += performanceDressingGroupEntry.rendererCount;
				performanceDressingDiagnostics.visibleRendererCount += performanceDressingGroupEntry.visibleRendererCount;
				performanceDressingDiagnostics.totalLodGroupCount += performanceDressingGroupEntry.lodGroupCount;
				performanceDressingDiagnostics.totalColliderCount += performanceDressingGroupEntry.colliderCount;
			}
		}
		performanceDressingDiagnostics.sceneRootCount = CountBehavioursByName("DressingSceneRoot");
		performanceDressingDiagnostics.groups.Sort((PerformanceDressingGroupEntry a, PerformanceDressingGroupEntry b) =>
		{
			int num = b.activeInHierarchy.CompareTo(a.activeInHierarchy);
			return (num == 0) ? b.rendererCount.CompareTo(a.rendererCount) : num;
		});
		snapshot.dressingDiagnostics = performanceDressingDiagnostics;
	}

	private static void CaptureUnderwaterChunkDiagnostics(PerformanceDetailedSnapshot snapshot)
	{
		PerformanceUnderwaterChunkDiagnostics performanceUnderwaterChunkDiagnostics = new PerformanceUnderwaterChunkDiagnostics();
		UnderwaterPropChunkRenderer[] array = UnityEngine.Object.FindObjectsByType<UnderwaterPropChunkRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		foreach (UnderwaterPropChunkRenderer underwaterPropChunkRenderer in array)
		{
			if (!(underwaterPropChunkRenderer == null) && underwaterPropChunkRenderer.gameObject.scene.IsValid())
			{
				performanceUnderwaterChunkDiagnostics.managerCount++;
				if (underwaterPropChunkRenderer.isActiveAndEnabled)
				{
					performanceUnderwaterChunkDiagnostics.activeManagerCount++;
				}
				performanceUnderwaterChunkDiagnostics.chunkCount += underwaterPropChunkRenderer.ChunkCount;
				performanceUnderwaterChunkDiagnostics.sourceCount += underwaterPropChunkRenderer.SourceCount;
				performanceUnderwaterChunkDiagnostics.matrixCount += underwaterPropChunkRenderer.MatrixCount;
				performanceUnderwaterChunkDiagnostics.batchCount += underwaterPropChunkRenderer.BatchCount;
				performanceUnderwaterChunkDiagnostics.visibleChunkCount += underwaterPropChunkRenderer.VisibleChunkCount;
				performanceUnderwaterChunkDiagnostics.lastDrawCallCount += underwaterPropChunkRenderer.LastDrawCallCount;
			}
		}
		snapshot.underwaterChunkDiagnostics = performanceUnderwaterChunkDiagnostics;
	}

	private static PerformanceDressingGroupEntry CaptureDressingGroupEntry(MonoBehaviour group, Camera camera)
	{
		Transform[] componentsInChildren = group.GetComponentsInChildren<Transform>(includeInactive: true);
		Renderer[] componentsInChildren2 = group.GetComponentsInChildren<Renderer>(includeInactive: true);
		LODGroup[] componentsInChildren3 = group.GetComponentsInChildren<LODGroup>(includeInactive: true);
		Collider[] componentsInChildren4 = group.GetComponentsInChildren<Collider>(includeInactive: true);
		int num = 0;
		int num2 = 0;
		Transform[] array = componentsInChildren;
		foreach (Transform transform in array)
		{
			if (!(transform == null))
			{
				if (transform.gameObject.activeSelf)
				{
					num2++;
				}
				if (transform.gameObject.activeInHierarchy)
				{
					num++;
				}
			}
		}
		int num3 = 0;
		int num4 = 0;
		bool flag = false;
		Bounds bounds = new Bounds(group.transform.position, Vector3.zero);
		Renderer[] array2 = componentsInChildren2;
		foreach (Renderer renderer in array2)
		{
			if (!(renderer == null))
			{
				if (renderer.enabled && !renderer.forceRenderingOff)
				{
					num3++;
				}
				if (renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy)
				{
					num4++;
				}
				if (!flag)
				{
					bounds = renderer.bounds;
					flag = true;
				}
				else
				{
					bounds.Encapsulate(renderer.bounds);
				}
			}
		}
		float distanceToCameraBounds = ((camera != null) ? Mathf.Sqrt(bounds.SqrDistance(camera.transform.position)) : 0f);
		PerformanceDressingGroupEntry performanceDressingGroupEntry = new PerformanceDressingGroupEntry();
		performanceDressingGroupEntry.name = group.name;
		performanceDressingGroupEntry.path = GetHierarchyPath(group.transform);
		performanceDressingGroupEntry.scene = group.gameObject.scene.name;
		performanceDressingGroupEntry.activeSelf = group.gameObject.activeSelf;
		performanceDressingGroupEntry.activeInHierarchy = group.gameObject.activeInHierarchy;
		performanceDressingGroupEntry.activateOnPrepare = ReadMemberBool(group, "ActivateOnPrepare");
		performanceDressingGroupEntry.activationWeight = ReadMemberInt(group, "ActivationWeight");
		performanceDressingGroupEntry.gameObjectCount = componentsInChildren.Length;
		performanceDressingGroupEntry.selfEnabledGameObjectCount = num2;
		performanceDressingGroupEntry.activeGameObjectCount = num;
		performanceDressingGroupEntry.rendererCount = componentsInChildren2.Length;
		performanceDressingGroupEntry.enabledRendererCount = num3;
		performanceDressingGroupEntry.visibleRendererCount = num4;
		performanceDressingGroupEntry.lodGroupCount = componentsInChildren3.Length;
		performanceDressingGroupEntry.colliderCount = componentsInChildren4.Length;
		performanceDressingGroupEntry.boundsCenterX = bounds.center.x;
		performanceDressingGroupEntry.boundsCenterY = bounds.center.y;
		performanceDressingGroupEntry.boundsCenterZ = bounds.center.z;
		performanceDressingGroupEntry.boundsSizeX = bounds.size.x;
		performanceDressingGroupEntry.boundsSizeY = bounds.size.y;
		performanceDressingGroupEntry.boundsSizeZ = bounds.size.z;
		performanceDressingGroupEntry.distanceToCameraBounds = distanceToCameraBounds;
		return performanceDressingGroupEntry;
	}

	private static bool IsRendererCandidate(Renderer renderer, Camera camera, Plane[] planes)
	{
		if (renderer != null && renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy && (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0)
		{
			return GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
		}
		return false;
	}

	private static void AccumulateRenderer(PerformanceDetailedSnapshot snapshot, Renderer renderer, long triangles, Camera camera, Dictionary<string, PerformanceRankedEntry> layerStats, Dictionary<string, PerformanceRankedEntry> shaderStats, Dictionary<string, PerformanceRenderHotspotEntry> midDistanceRootStats, Dictionary<string, PerformanceRenderHotspotEntry> midDistanceShaderStats, bool selectedByLod)
	{
		if (renderer.isPartOfStaticBatch)
		{
			snapshot.staticBatchedFrustumRendererCount++;
		}
		if (renderer.shadowCastingMode != ShadowCastingMode.Off)
		{
			snapshot.shadowCasterEnabledCount++;
		}
		float num = Vector3.Distance(camera.transform.position, renderer.bounds.center);
		if (renderer.shadowCastingMode != ShadowCastingMode.Off && num <= QualitySettings.shadowDistance + renderer.bounds.extents.magnitude)
		{
			snapshot.potentialShadowDistanceCasterCount++;
		}
		AddRank(layerStats, LayerMask.LayerToName(renderer.gameObject.layer), triangles);
		Material[] sharedMaterials = renderer.sharedMaterials;
		foreach (Material material in sharedMaterials)
		{
			string text = ((material != null && material.shader != null) ? material.shader.name : "(No shader)");
			AddRank(shaderStats, text, triangles);
		}
		AccumulateMidDistanceHotspots(renderer, triangles, num, selectedByLod, midDistanceRootStats, midDistanceShaderStats);
		InsertHeavyRenderer(snapshot.topRenderers, new PerformanceRendererEntry
		{
			name = renderer.name,
			path = GetHierarchyPath(renderer.transform),
			scene = renderer.gameObject.scene.name,
			layer = LayerMask.LayerToName(renderer.gameObject.layer),
			rendererType = renderer.GetType().Name,
			triangles = triangles,
			materialSlots = renderer.sharedMaterials.Length,
			isPartOfStaticBatch = renderer.isPartOfStaticBatch,
			meshSubMeshCount = (GetMesh(renderer)?.subMeshCount ?? 0),
			renderedSubMeshStart = GetRenderedSubMeshStart(renderer),
			estimatedRenderedSubMeshCount = GetRenderedSubMeshCount(renderer),
			distance = num,
			castsShadows = (renderer.shadowCastingMode != ShadowCastingMode.Off),
			selectedByLod = selectedByLod
		});
	}

	private static void AccumulateMidDistanceHotspots(Renderer renderer, long triangles, float distance, bool selectedByLod, Dictionary<string, PerformanceRenderHotspotEntry> rootStats, Dictionary<string, PerformanceRenderHotspotEntry> shaderStats)
	{
		if (!(renderer == null) && !(distance < 50f) && !(distance > 150f))
		{
			AccumulateHotspot(rootStats, GetMidDistanceOwnerKey(renderer.transform), GetMidDistanceOwnerName(renderer.transform), GetMidDistanceOwnerPath(renderer.transform), renderer.gameObject.scene.name, renderer, triangles, distance, selectedByLod);
			Material[] sharedMaterials = renderer.sharedMaterials;
			foreach (Material material in sharedMaterials)
			{
				string key = ((material != null && material.shader != null) ? material.shader.name : "(No shader)");
				AccumulateHotspot(shaderStats, key, key, string.Empty, renderer.gameObject.scene.name, renderer, triangles, distance, selectedByLod);
			}
		}
	}

	private static void AccumulateHotspot(Dictionary<string, PerformanceRenderHotspotEntry> stats, string key, string name, string path, string scene, Renderer renderer, long triangles, float distance, bool selectedByLod)
	{
		if (!stats.TryGetValue(key, out var value))
		{
			value = new PerformanceRenderHotspotEntry
			{
				name = name,
				path = path,
				scene = scene,
				minDistance = distance,
				maxDistance = distance
			};
			stats.Add(key, value);
		}
		value.rendererCount++;
		value.triangles += triangles;
		value.materialSlots += renderer.sharedMaterials.Length;
		if (selectedByLod)
		{
			value.lodSelectedRendererCount++;
		}
		else
		{
			value.nonLodRendererCount++;
		}
		if (renderer.shadowCastingMode != ShadowCastingMode.Off)
		{
			value.shadowCasterCount++;
		}
		if (renderer.isPartOfStaticBatch)
		{
			value.staticBatchedRendererCount++;
		}
		value.minDistance = Mathf.Min(value.minDistance, distance);
		value.maxDistance = Mathf.Max(value.maxDistance, distance);
	}

	private static string GetMidDistanceOwnerKey(Transform transform)
	{
		Transform midDistanceOwnerTransform = GetMidDistanceOwnerTransform(transform);
		if (!(midDistanceOwnerTransform != null))
		{
			return "(No owner)";
		}
		return midDistanceOwnerTransform.gameObject.scene.name + "/" + GetHierarchyPath(midDistanceOwnerTransform);
	}

	private static string GetMidDistanceOwnerName(Transform transform)
	{
		Transform midDistanceOwnerTransform = GetMidDistanceOwnerTransform(transform);
		if (!(midDistanceOwnerTransform != null))
		{
			return "(No owner)";
		}
		return midDistanceOwnerTransform.name;
	}

	private static string GetMidDistanceOwnerPath(Transform transform)
	{
		Transform midDistanceOwnerTransform = GetMidDistanceOwnerTransform(transform);
		if (!(midDistanceOwnerTransform != null))
		{
			return string.Empty;
		}
		return GetHierarchyPath(midDistanceOwnerTransform);
	}

	private static Transform GetMidDistanceOwnerTransform(Transform transform)
	{
		if (transform == null)
		{
			return null;
		}
		Transform transform2 = transform;
		Transform transform3 = transform;
		while (transform2 != null)
		{
			if (HasComponentNamed(transform2, "DressingActivationGroup"))
			{
				return transform2;
			}
			if (transform2.parent != null && transform2.parent.parent == null)
			{
				transform3 = transform2;
			}
			transform2 = transform2.parent;
		}
		if (!(transform3 != null))
		{
			return transform;
		}
		return transform3;
	}

	private static bool HasComponentNamed(Transform transform, string typeName)
	{
		MonoBehaviour[] components = transform.GetComponents<MonoBehaviour>();
		foreach (MonoBehaviour monoBehaviour in components)
		{
			if (!(monoBehaviour == null))
			{
				string text = monoBehaviour.GetType().FullName ?? monoBehaviour.GetType().Name;
				if (text == typeName || text.EndsWith("." + typeName, StringComparison.Ordinal))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static void CaptureCameras(PerformanceDetailedSnapshot snapshot)
	{
		Camera[] array = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
		foreach (Camera camera in array)
		{
			if (!(camera == null) && camera.enabled && camera.gameObject.activeInHierarchy)
			{
				snapshot.enabledCameraCount++;
				snapshot.cameras.Add(new PerformanceCameraEntry
				{
					name = GetHierarchyPath(camera.transform),
					renderType = camera.cameraType.ToString(),
					depth = camera.depth,
					nearClip = camera.nearClipPlane,
					farClip = camera.farClipPlane,
					target = ((camera.targetTexture != null) ? camera.targetTexture.name : "Screen"),
					cullingMask = MaskToNames(camera.cullingMask)
				});
			}
		}
	}

	private static void CaptureLoadedScenes(PerformanceDetailedSnapshot snapshot)
	{
		snapshot.loadedSceneCount = SceneManager.sceneCount;
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene sceneAt = SceneManager.GetSceneAt(i);
			if (sceneAt.IsValid() && sceneAt.isLoaded)
			{
				PerformanceSceneEntry performanceSceneEntry = new PerformanceSceneEntry
				{
					name = sceneAt.name,
					path = sceneAt.path,
					isActiveScene = (sceneAt == SceneManager.GetActiveScene()),
					rootObjectCount = sceneAt.rootCount
				};
				GameObject[] rootGameObjects = sceneAt.GetRootGameObjects();
				foreach (GameObject gameObject in rootGameObjects)
				{
					performanceSceneEntry.rendererCount += gameObject.GetComponentsInChildren<Renderer>(includeInactive: false).Length;
					performanceSceneEntry.lodGroupCount += gameObject.GetComponentsInChildren<LODGroup>(includeInactive: false).Length;
					performanceSceneEntry.cameraCount += gameObject.GetComponentsInChildren<Camera>(includeInactive: false).Length;
					performanceSceneEntry.lightCount += gameObject.GetComponentsInChildren<Light>(includeInactive: false).Length;
				}
				snapshot.scenes.Add(performanceSceneEntry);
			}
		}
	}

	private static void CaptureCullingComponents(PerformanceDetailedSnapshot snapshot, Camera camera)
	{
		MonoBehaviour[] components = camera.GetComponents<MonoBehaviour>();
		foreach (MonoBehaviour monoBehaviour in components)
		{
			if (!(monoBehaviour == null))
			{
				string text = monoBehaviour.GetType().FullName ?? monoBehaviour.GetType().Name;
				if (text.Contains("StaticCullingCamera", StringComparison.Ordinal) || text.EndsWith(".DC_Camera", StringComparison.Ordinal))
				{
					snapshot.cameraCullingComponents.Add($"{text} (enabled={monoBehaviour.enabled})");
				}
			}
		}
		snapshot.hasStaticCullingCamera = snapshot.cameraCullingComponents.Exists((string name) => name.Contains("StaticCullingCamera", StringComparison.Ordinal));
		snapshot.hasDynamicCullingCamera = snapshot.cameraCullingComponents.Exists((string name) => name.EndsWith(".DC_Camera (enabled=True)", StringComparison.Ordinal) || name.Contains(".DC_Camera ", StringComparison.Ordinal));
	}

	private static List<PerformanceLayerCullDistance> CaptureLayerCullDistances(Camera camera)
	{
		List<PerformanceLayerCullDistance> list = new List<PerformanceLayerCullDistance>();
		float[] layerCullDistances = camera.layerCullDistances;
		for (int i = 0; i < layerCullDistances.Length; i++)
		{
			if (!(layerCullDistances[i] <= 0f))
			{
				string text = LayerMask.LayerToName(i);
				list.Add(new PerformanceLayerCullDistance
				{
					layer = (string.IsNullOrEmpty(text) ? i.ToString() : text),
					distance = layerCullDistances[i]
				});
			}
		}
		return list;
	}

	private static void CaptureCpuSnapshot(PerformanceDetailedSnapshot snapshot)
	{
		Dictionary<string, PerformanceRankedEntry> dictionary = new Dictionary<string, PerformanceRankedEntry>(StringComparer.Ordinal);
		Dictionary<string, PerformanceBehaviourEntry> dictionary2 = new Dictionary<string, PerformanceBehaviourEntry>(StringComparer.Ordinal);
		Dictionary<string, PerformanceBehaviourEntry> dictionary3 = new Dictionary<string, PerformanceBehaviourEntry>(StringComparer.Ordinal);
		Dictionary<string, PerformanceRankedEntry> dictionary4 = new Dictionary<string, PerformanceRankedEntry>(StringComparer.Ordinal);
		Dictionary<string, PerformanceHierarchyRootEntry> stats = new Dictionary<string, PerformanceHierarchyRootEntry>(StringComparer.Ordinal);
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene sceneAt = SceneManager.GetSceneAt(i);
			if (!sceneAt.IsValid() || !sceneAt.isLoaded)
			{
				continue;
			}
			GameObject[] rootGameObjects = sceneAt.GetRootGameObjects();
			foreach (GameObject gameObject in rootGameObjects)
			{
				if (!(gameObject == null))
				{
					PerformanceHierarchyRootEntry orCreateRootEntry = GetOrCreateRootEntry(stats, gameObject);
					AccumulateHierarchySnapshot(gameObject.transform, orCreateRootEntry, snapshot, dictionary, dictionary2, dictionary3, dictionary4);
				}
			}
		}
		snapshot.inactiveGameObjectCount = Mathf.Max(0, snapshot.loadedGameObjectCount - snapshot.activeGameObjectCount);
		snapshot.topComponents = SortRanks(dictionary, 20);
		snapshot.topMonoBehaviours = SortBehaviourEntries(dictionary2, 25);
		snapshot.topUpdateCandidates = SortBehaviourEntries(dictionary3, 25);
		snapshot.topCloneGroups = SortRanks(dictionary4, 20);
		snapshot.topHierarchyRoots = SortRootEntries(stats, 20);
	}

	private static void AccumulateHierarchySnapshot(Transform transform, PerformanceHierarchyRootEntry rootEntry, PerformanceDetailedSnapshot snapshot, Dictionary<string, PerformanceRankedEntry> componentStats, Dictionary<string, PerformanceBehaviourEntry> behaviourStats, Dictionary<string, PerformanceBehaviourEntry> updateStats, Dictionary<string, PerformanceRankedEntry> cloneStats)
	{
		if (transform == null)
		{
			return;
		}
		GameObject gameObject = transform.gameObject;
		snapshot.loadedGameObjectCount++;
		rootEntry.gameObjectCount++;
		if (gameObject.activeInHierarchy)
		{
			snapshot.activeGameObjectCount++;
			rootEntry.activeGameObjectCount++;
		}
		else
		{
			rootEntry.inactiveGameObjectCount++;
		}
		AddRank(cloneStats, StripCloneSuffix(gameObject.name), 0L);
		Component[] components = gameObject.GetComponents<Component>();
		snapshot.componentSlotCount += components.Length;
		rootEntry.componentCount += components.Length;
		Component[] array = components;
		foreach (Component component in array)
		{
			if (component == null)
			{
				snapshot.missingScriptCount++;
				rootEntry.missingScriptCount++;
				AddRank(componentStats, "(Missing Script)", 0L);
				continue;
			}
			Type type = component.GetType();
			string typeName = type.FullName ?? type.Name;
			AddRank(componentStats, typeName, 0L);
			if (!(component is MonoBehaviour monoBehaviour))
			{
				if (!(component is Renderer))
				{
					if (!(component is Collider collider))
					{
						if (!(component is Rigidbody rigidbody))
						{
							if (!(component is Animator))
							{
								if (!(component is Canvas canvas))
								{
									if (!(component is ParticleSystem particleSystem))
									{
										if (component is Light { enabled: not false } light && light.gameObject.activeInHierarchy)
										{
											snapshot.enabledLightCount++;
										}
										continue;
									}
									snapshot.particleSystemCount++;
									rootEntry.particleSystemCount++;
									if (particleSystem.isPlaying)
									{
										snapshot.playingParticleSystemCount++;
									}
								}
								else
								{
									snapshot.canvasCount++;
									rootEntry.canvasCount++;
									if (canvas.renderMode == RenderMode.WorldSpace)
									{
										snapshot.worldSpaceCanvasCount++;
									}
								}
							}
							else
							{
								snapshot.animatorCount++;
								rootEntry.animatorCount++;
							}
						}
						else
						{
							snapshot.rigidbodyCount++;
							rootEntry.rigidbodyCount++;
							if (!rigidbody.IsSleeping())
							{
								snapshot.awakeRigidbodyCount++;
							}
						}
					}
					else
					{
						snapshot.colliderCount++;
						rootEntry.colliderCount++;
						if (collider.isTrigger)
						{
							snapshot.triggerColliderCount++;
						}
					}
				}
				else
				{
					rootEntry.rendererCount++;
				}
			}
			else
			{
				snapshot.monoBehaviourCount++;
				rootEntry.monoBehaviourCount++;
				UpdateMethodFlags updateFlags = GetUpdateFlags(type);
				AccumulateBehaviourEntry(GetOrCreateBehaviourEntry(behaviourStats, typeName, updateFlags), monoBehaviour);
				if (updateFlags.hasUpdate || updateFlags.hasLateUpdate || updateFlags.hasFixedUpdate)
				{
					AccumulateBehaviourEntry(GetOrCreateBehaviourEntry(updateStats, typeName, updateFlags), monoBehaviour);
					snapshot.updateCandidateInstanceCount++;
				}
				if (monoBehaviour.enabled)
				{
					snapshot.enabledMonoBehaviourCount++;
					rootEntry.enabledMonoBehaviourCount++;
				}
				else
				{
					snapshot.disabledMonoBehaviourCount++;
					rootEntry.disabledMonoBehaviourCount++;
				}
			}
		}
		for (int j = 0; j < transform.childCount; j++)
		{
			AccumulateHierarchySnapshot(transform.GetChild(j), rootEntry, snapshot, componentStats, behaviourStats, updateStats, cloneStats);
		}
	}

	private static PerformanceHierarchyRootEntry GetOrCreateRootEntry(Dictionary<string, PerformanceHierarchyRootEntry> stats, GameObject root)
	{
		string text = root.scene.name + "/" + root.name;
		if (stats.TryGetValue(text, out var value))
		{
			return value;
		}
		value = new PerformanceHierarchyRootEntry
		{
			name = root.name,
			path = text,
			scene = root.scene.name
		};
		stats.Add(text, value);
		return value;
	}

	private static PerformanceBehaviourEntry GetOrCreateBehaviourEntry(Dictionary<string, PerformanceBehaviourEntry> stats, string typeName, UpdateMethodFlags flags)
	{
		if (stats.TryGetValue(typeName, out var value))
		{
			return value;
		}
		value = new PerformanceBehaviourEntry
		{
			name = typeName,
			hasUpdate = flags.hasUpdate,
			hasLateUpdate = flags.hasLateUpdate,
			hasFixedUpdate = flags.hasFixedUpdate
		};
		stats.Add(typeName, value);
		return value;
	}

	private static void AccumulateBehaviourEntry(PerformanceBehaviourEntry entry, MonoBehaviour behaviour)
	{
		entry.count++;
		if (behaviour.enabled)
		{
			entry.enabledCount++;
		}
		else
		{
			entry.disabledCount++;
		}
		if (behaviour.gameObject.activeInHierarchy)
		{
			entry.activeInHierarchyCount++;
		}
		else
		{
			entry.inactiveInHierarchyCount++;
		}
	}

	private static string StripCloneSuffix(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return "(Unnamed)";
		}
		if (!name.EndsWith("(Clone)", StringComparison.Ordinal))
		{
			return name;
		}
		return name.Substring(0, name.Length - "(Clone)".Length).TrimEnd();
	}

	private static float CalculateRelativeScreenHeight(Camera camera, Vector3 worldCenter, float worldSize)
	{
		if (camera.orthographic)
		{
			return worldSize / Mathf.Max(0.0001f, camera.orthographicSize * 2f);
		}
		float num = Vector3.Distance(camera.transform.position, worldCenter);
		float f = camera.fieldOfView * 0.5f * (MathF.PI / 180f);
		return worldSize / Mathf.Max(0.0001f, num * 2f * Mathf.Tan(f)) * Mathf.Max(0.01f, QualitySettings.lodBias);
	}

	private static int ResolveLodIndex(LOD[] lods, float relativeHeight)
	{
		for (int i = 0; i < lods.Length; i++)
		{
			if (relativeHeight >= lods[i].screenRelativeTransitionHeight)
			{
				return i;
			}
		}
		return -1;
	}

	private static float MaxAbsComponent(Vector3 value)
	{
		return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
	}

	private static List<PerformanceDistanceBand> CreateDistanceBands()
	{
		return new List<PerformanceDistanceBand>
		{
			new PerformanceDistanceBand
			{
				label = "0-25m",
				minDistance = 0f,
				maxDistance = 25f
			},
			new PerformanceDistanceBand
			{
				label = "25-50m",
				minDistance = 25f,
				maxDistance = 50f
			},
			new PerformanceDistanceBand
			{
				label = "50-100m",
				minDistance = 50f,
				maxDistance = 100f
			},
			new PerformanceDistanceBand
			{
				label = "100-150m",
				minDistance = 100f,
				maxDistance = 150f
			},
			new PerformanceDistanceBand
			{
				label = "150-300m",
				minDistance = 150f,
				maxDistance = 300f
			},
			new PerformanceDistanceBand
			{
				label = "300m+",
				minDistance = 300f,
				maxDistance = float.MaxValue
			}
		};
	}

	private static void AddDistanceBand(List<PerformanceDistanceBand> bands, float distance, int renderers, long triangles, int materialSlots)
	{
		foreach (PerformanceDistanceBand band in bands)
		{
			if (!(distance < band.minDistance) && !(distance >= band.maxDistance))
			{
				band.rendererCount += renderers;
				band.triangles += triangles;
				band.materialSlots += materialSlots;
				break;
			}
		}
	}

	private void OnGUI()
	{
		if (_panelVisible || !(Time.realtimeSinceStartup >= _statusUntil))
		{
			EnsureStyles();
			float width = Mathf.Min(430f, (float)Screen.width - 24f);
			float height = (_panelVisible ? 275f : 72f);
			Rect position = new Rect(12f, 12f, width, height);
			GUI.Box(position, GUIContent.none, _panelStyle);
			GUILayout.BeginArea(new Rect(position.x + 14f, position.y + 10f, position.width - 28f, position.height - 20f));
			if (_panelVisible && Latest != null)
			{
				GUILayout.Label("Performance Diagnostic", _titleStyle);
				GUILayout.Label($"FPS {Latest.fps:0.0} | Frame {Latest.frameMs:0.0} ms | CPU {Latest.mainThreadMs:0.0} ms | GPU {Latest.gpuMs:0.0} ms", _labelStyle);
				GUILayout.Label($"Draw {Latest.drawCalls:N0} | Batches {Latest.batches:N0} | Tris {Latest.triangles:N0} | GC {(float)Latest.gcAllocatedBytes / 1024f:0.0} KB", _labelStyle);
				GUILayout.Label("Tahmin: " + Latest.verdict, _labelStyle);
				GUILayout.Label("Sunlight A/B: " + RuntimeSunlightPerformanceDiagnostic.CurrentModeName, _labelStyle);
				GUILayout.Space(5f);
				GUILayout.Label("F6: panel | F7: JSON | F8: Sunlight A/B modu", _labelStyle);
			}
			if (Time.realtimeSinceStartup < _statusUntil)
			{
				GUILayout.Label(_statusMessage, _labelStyle);
			}
			GUILayout.EndArea();
		}
	}

	private void EnsureStyles()
	{
		if (_panelStyle == null)
		{
			_panelStyle = new GUIStyle(GUI.skin.box);
		}
		if (_labelStyle == null)
		{
			_labelStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 14,
				wordWrap = true,
				normal = 
				{
					textColor = Color.white
				}
			};
		}
		if (_titleStyle == null)
		{
			_titleStyle = new GUIStyle(_labelStyle)
			{
				fontSize = 17,
				fontStyle = FontStyle.Bold
			};
		}
	}

	private static long EstimateTriangles(Renderer renderer)
	{
		Mesh mesh = GetMesh(renderer);
		if (mesh == null)
		{
			return 0L;
		}
		int renderedSubMeshStart = GetRenderedSubMeshStart(renderer);
		int renderedSubMeshCount = GetRenderedSubMeshCount(renderer);
		long num = 0L;
		for (int i = renderedSubMeshStart; i < renderedSubMeshStart + renderedSubMeshCount; i++)
		{
			num += mesh.GetIndexCount(i);
		}
		return num / 3;
	}

	private static Mesh GetMesh(Renderer renderer)
	{
		if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
		{
			return skinnedMeshRenderer.sharedMesh;
		}
		if (!renderer.TryGetComponent<MeshFilter>(out var component))
		{
			return null;
		}
		return component.sharedMesh;
	}

	private static int GetRenderedSubMeshStart(Renderer renderer)
	{
		Mesh mesh = GetMesh(renderer);
		if (mesh == null || !renderer.isPartOfStaticBatch)
		{
			return 0;
		}
		if (!(renderer is MeshRenderer meshRenderer))
		{
			return 0;
		}
		return Mathf.Clamp(meshRenderer.subMeshStartIndex, 0, mesh.subMeshCount);
	}

	private static int GetRenderedSubMeshCount(Renderer renderer)
	{
		Mesh mesh = GetMesh(renderer);
		if (mesh == null)
		{
			return 0;
		}
		int renderedSubMeshStart = GetRenderedSubMeshStart(renderer);
		if (!renderer.isPartOfStaticBatch)
		{
			return mesh.subMeshCount;
		}
		return Mathf.Clamp(renderer.sharedMaterials.Length, 0, mesh.subMeshCount - renderedSubMeshStart);
	}

	private static void AddRank(Dictionary<string, PerformanceRankedEntry> stats, string name, long triangles)
	{
		if (string.IsNullOrEmpty(name))
		{
			name = "(Unnamed Layer)";
		}
		if (!stats.TryGetValue(name, out var value))
		{
			value = new PerformanceRankedEntry
			{
				name = name
			};
			stats.Add(name, value);
		}
		value.count++;
		value.triangles += triangles;
	}

	private static List<PerformanceRankedEntry> SortRanks(Dictionary<string, PerformanceRankedEntry> stats, int limit)
	{
		List<PerformanceRankedEntry> list = new List<PerformanceRankedEntry>(stats.Values);
		list.Sort((PerformanceRankedEntry a, PerformanceRankedEntry b) =>
		{
			int num = b.triangles.CompareTo(a.triangles);
			return (num == 0) ? b.count.CompareTo(a.count) : num;
		});
		if (list.Count > limit)
		{
			list.RemoveRange(limit, list.Count - limit);
		}
		return list;
	}

	private static List<PerformanceBehaviourEntry> SortBehaviourEntries(Dictionary<string, PerformanceBehaviourEntry> stats, int limit)
	{
		List<PerformanceBehaviourEntry> list = new List<PerformanceBehaviourEntry>(stats.Values);
		list.Sort((PerformanceBehaviourEntry a, PerformanceBehaviourEntry b) =>
		{
			int num = b.activeInHierarchyCount.CompareTo(a.activeInHierarchyCount);
			return (num == 0) ? b.count.CompareTo(a.count) : num;
		});
		if (list.Count > limit)
		{
			list.RemoveRange(limit, list.Count - limit);
		}
		return list;
	}

	private static List<PerformanceHierarchyRootEntry> SortRootEntries(Dictionary<string, PerformanceHierarchyRootEntry> stats, int limit)
	{
		List<PerformanceHierarchyRootEntry> list = new List<PerformanceHierarchyRootEntry>(stats.Values);
		list.Sort((PerformanceHierarchyRootEntry a, PerformanceHierarchyRootEntry b) =>
		{
			int num = b.componentCount.CompareTo(a.componentCount);
			return (num == 0) ? b.gameObjectCount.CompareTo(a.gameObjectCount) : num;
		});
		if (list.Count > limit)
		{
			list.RemoveRange(limit, list.Count - limit);
		}
		return list;
	}

	private static UpdateMethodFlags GetUpdateFlags(Type type)
	{
		if (type == null)
		{
			return default;
		}
		if (UpdateMethodCache.TryGetValue(type, out var value))
		{
			return value;
		}
		value = new UpdateMethodFlags
		{
			hasUpdate = DeclaresUnityMessage(type, "Update"),
			hasLateUpdate = DeclaresUnityMessage(type, "LateUpdate"),
			hasFixedUpdate = DeclaresUnityMessage(type, "FixedUpdate")
		};
		UpdateMethodCache.Add(type, value);
		return value;
	}

	private static bool DeclaresUnityMessage(Type type, string methodName)
	{
		Type type2 = type;
		while (type2 != null && type2 != typeof(MonoBehaviour))
		{
			if (type2.GetMethod(methodName, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) != null)
			{
				return true;
			}
			type2 = type2.BaseType;
		}
		return false;
	}

	private static object ReadMemberValue(object target, params string[] names)
	{
		if (target == null || names == null)
		{
			return null;
		}
		Type type = target.GetType();
		foreach (string text in names)
		{
			PropertyInfo property = type.GetProperty(text, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null)
			{
				try
				{
					return property.GetValue(target);
				}
				catch
				{
					return null;
				}
			}
			FieldInfo field = type.GetField(text, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				try
				{
					return field.GetValue(target);
				}
				catch
				{
					return null;
				}
			}
		}
		return null;
	}

	private static bool ReadMemberBool(object target, params string[] names)
	{
		object obj = ReadMemberValue(target, names);
		bool flag = default;
		int num;
		if (obj is bool)
		{
			flag = (bool)obj;
			num = 1;
		}
		else
		{
			num = 0;
		}
		return (byte)((uint)num & (flag ? 1u : 0u)) != 0;
	}

	private static int ReadMemberInt(object target, params string[] names)
	{
		object obj = ReadMemberValue(target, names);
		if (obj == null)
		{
			return 0;
		}
		try
		{
			return Convert.ToInt32(obj);
		}
		catch
		{
			return 0;
		}
	}

	private static string ReadMemberString(object target, params string[] names)
	{
		object obj = ReadMemberValue(target, names);
		if (obj == null)
		{
			return string.Empty;
		}
		return obj.ToString();
	}

	private static int CountCollectionMember(object target, params string[] names)
	{
		object obj = ReadMemberValue(target, names);
		if (obj == null)
		{
			return 0;
		}
		if (obj is ICollection collection)
		{
			return collection.Count;
		}
		if (obj is IEnumerable enumerable)
		{
			int num = 0;
			{
				foreach (object item in enumerable)
				{
					_ = item;
					num++;
				}
				return num;
			}
		}
		return ReadMemberInt(target, names);
	}

	private static void InsertHeavyRenderer(List<PerformanceRendererEntry> entries, PerformanceRendererEntry candidate)
	{
		int num = entries.FindIndex((PerformanceRendererEntry item) => candidate.triangles > item.triangles);
		if (num < 0)
		{
			entries.Add(candidate);
		}
		else
		{
			entries.Insert(num, candidate);
		}
		if (entries.Count > 15)
		{
			entries.RemoveAt(entries.Count - 1);
		}
	}

	private static void InsertHeavyLodGroup(List<PerformanceLodGroupEntry> entries, PerformanceLodGroupEntry candidate)
	{
		int num = entries.FindIndex((PerformanceLodGroupEntry item) => candidate.selectedTriangles > item.selectedTriangles);
		if (num < 0)
		{
			entries.Add(candidate);
		}
		else
		{
			entries.Insert(num, candidate);
		}
		if (entries.Count > 20)
		{
			entries.RemoveAt(entries.Count - 1);
		}
	}

	private static List<PerformanceRenderHotspotEntry> SortHotspots(Dictionary<string, PerformanceRenderHotspotEntry> stats, int maxCount)
	{
		List<PerformanceRenderHotspotEntry> list = new List<PerformanceRenderHotspotEntry>(stats.Values);
		list.Sort((PerformanceRenderHotspotEntry left, PerformanceRenderHotspotEntry right) =>
		{
			int num = right.triangles.CompareTo(left.triangles);
			if (num != 0)
			{
				return num;
			}
			int num2 = right.materialSlots.CompareTo(left.materialSlots);
			return (num2 == 0) ? right.rendererCount.CompareTo(left.rendererCount) : num2;
		});
		if (list.Count > maxCount)
		{
			list.RemoveRange(maxCount, list.Count - maxCount);
		}
		return list;
	}

	private static string GetHierarchyPath(Transform transform)
	{
		string text = transform.name;
		while (transform.parent != null)
		{
			transform = transform.parent;
			text = transform.name + "/" + text;
		}
		return transform.gameObject.scene.name + "/" + text;
	}

	private static string MaskToNames(int mask)
	{
		if (mask == -1)
		{
			return "Everything";
		}
		List<string> list = new List<string>();
		for (int i = 0; i < 32; i++)
		{
			if ((mask & (1 << i)) != 0)
			{
				string text = LayerMask.LayerToName(i);
				list.Add(string.IsNullOrEmpty(text) ? i.ToString() : text);
			}
		}
		return string.Join(", ", list);
	}

	private static void Classify(float frameMs, float mainThreadMs, float renderThreadMs, float gpuMs, out string verdict, out string explanation)
	{
		if (gpuMs > 0.1f && gpuMs >= mainThreadMs * 1.15f && gpuMs >= renderThreadMs * 1.15f)
		{
			verdict = "GPU / Rendering";
			explanation = "GPU time is clearly higher than CPU time.";
		}
		else if (renderThreadMs > 2f && renderThreadMs >= mainThreadMs * 0.9f)
		{
			verdict = "CPU Render Thread";
			explanation = "Rendering command preparation is expensive.";
		}
		else if (mainThreadMs > 2f && mainThreadMs >= gpuMs)
		{
			verdict = "CPU Main Thread";
			explanation = "Scripts, physics, animation, allocation or CPU-side rendering is expensive.";
		}
		else if (frameMs > 20f && gpuMs <= 0.1f)
		{
			verdict = "Unknown (no GPU timing)";
			explanation = "Frame is slow but GPU timing is unavailable.";
		}
		else
		{
			verdict = "Balanced";
			explanation = "No clear bottleneck in this sample.";
		}
	}

	private static Vector3 ResolvePlayerPosition()
	{
		Camera main = Camera.main;
		if (!(main != null))
		{
			return Vector3.zero;
		}
		return main.transform.position;
	}

	private static PerformancePlayerMovementSnapshot CapturePlayerMovementSnapshot()
	{
		PPG_Player pPG_Player = ResolveLocalPlayer();
		if (pPG_Player == null)
		{
			return new PerformancePlayerMovementSnapshot
			{
				hasLocalPlayer = false,
				movementMode = "NoLocalPlayer"
			};
		}
		Vector2 vector = ((pPG_Player.FirstPersonCharacterInput != null) ? pPG_Player.FirstPersonCharacterInput.InputMove : Vector2.zero);
		FirstPersonCharacter firstPersonCharacter = pPG_Player.FirstPersonCharacter;
		Vector3 vector2 = Vector3.zero;
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		if (firstPersonCharacter != null)
		{
			vector2 = firstPersonCharacter.GetVelocity();
			flag = firstPersonCharacter.IsSwimming();
			flag2 = firstPersonCharacter.IsSeaScooter();
			flag3 = firstPersonCharacter.IsGrounded();
		}
		Vector3 position = pPG_Player.transform.position;
		string seaLayer = ((pPG_Player.CurrentSeaLayer != null) ? pPG_Player.CurrentSeaLayer.DisplayName : string.Empty);
		return new PerformancePlayerMovementSnapshot
		{
			hasLocalPlayer = true,
			playerName = pPG_Player.name,
			playerX = position.x,
			playerY = position.y,
			playerZ = position.z,
			inputX = vector.x,
			inputY = vector.y,
			inputMagnitude = vector.magnitude,
			speed = vector2.magnitude,
			velocityX = vector2.x,
			velocityY = vector2.y,
			velocityZ = vector2.z,
			isMovingInput = (vector.sqrMagnitude > 0.0001f),
			isNetworkSwimming = pPG_Player.IsSwimming.Value,
			isCharacterSwimming = flag,
			isSeaScooter = flag2,
			isUnderWater = pPG_Player.IsUnderWater,
			isGrounded = flag3,
			movementMode = (flag2 ? "SeaScooter" : ((flag || pPG_Player.IsSwimming.Value) ? "Swimming" : (flag3 ? "Grounded" : "Airborne"))),
			seaLayer = seaLayer
		};
	}

	private static PPG_Player ResolveLocalPlayer()
	{
		if (!(Singleton<PlayersManager>.Instance != null))
		{
			return null;
		}
		return Singleton<PlayersManager>.Instance.LocalPlayer;
	}

	private static ProfilerRecorder StartRecorder(ProfilerCategory category, string name, int capacity = 1)
	{
		try
		{
			return ProfilerRecorder.StartNew(category, name, capacity);
		}
		catch
		{
			return default;
		}
	}

	private static float RecorderAverageMs(ProfilerRecorder recorder)
	{
		if (!recorder.Valid || recorder.Count == 0)
		{
			return 0f;
		}
		long num = 0L;
		for (int i = 0; i < recorder.Count; i++)
		{
			num += recorder.GetSample(i).Value;
		}
		return (float)num / (float)recorder.Count / 1000000f;
	}

	private static long RecorderValue(ProfilerRecorder recorder)
	{
		if (!recorder.Valid)
		{
			return 0L;
		}
		return recorder.LastValue;
	}

	private static int RecorderInt(ProfilerRecorder recorder)
	{
		if (!recorder.Valid)
		{
			return 0;
		}
		return (int)Math.Min(2147483647L, recorder.LastValue);
	}

	private static void Dispose(ref ProfilerRecorder recorder)
	{
		if (recorder.Valid)
		{
			recorder.Dispose();
		}
		recorder = default;
	}

	private static void AddBounded(List<PerformanceSample> list, PerformanceSample item, int capacity)
	{
		if (list.Count >= capacity)
		{
			list.RemoveAt(0);
		}
		list.Add(item);
	}
}
