using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RuntimeSunlightPerformanceDiagnostic : MonoBehaviour
{
	private enum DiagnosticMode
	{
		Normal = 0,
		RenderersDisabled = 1,
		RootsDisabled = 2
	}

	private sealed class RootState
	{
		public readonly GameObject root;

		public readonly bool wasActiveSelf;

		public RootState(GameObject root, bool wasActiveSelf)
		{
			this.root = root;
			this.wasActiveSelf = wasActiveSelf;
		}
	}

	private sealed class RendererState
	{
		public readonly Renderer renderer;

		public readonly bool wasForceRenderingOff;

		public RendererState(Renderer renderer, bool wasForceRenderingOff)
		{
			this.renderer = renderer;
			this.wasForceRenderingOff = wasForceRenderingOff;
		}
	}

	public const KeyCode ToggleKey = KeyCode.F8;

	private static readonly HashSet<string> TargetSceneNames = new HashSet<string> { "200_Dressing_Sunlight_Near_01", "201_Dressing_Sunlight_Near_02", "210_Dressing_Sunlight_Far_01" };

	private readonly List<RootState> _roots = new List<RootState>();

	private readonly List<RendererState> _renderers = new List<RendererState>();

	private DiagnosticMode _mode;

	private int _targetSceneCount;

	private int _targetLodGroupCount;

	public static RuntimeSunlightPerformanceDiagnostic Instance { get; private set; }

	public static string CurrentModeName
	{
		get
		{
			if (!(Instance != null))
			{
				return "Unavailable";
			}
			return Instance._mode.ToString();
		}
	}

	public static int TargetSceneCount
	{
		get
		{
			if (!(Instance != null))
			{
				return 0;
			}
			return Instance._targetSceneCount;
		}
	}

	public static int TargetRootCount
	{
		get
		{
			if (!(Instance != null))
			{
				return 0;
			}
			return Instance._roots.Count;
		}
	}

	public static int TargetRendererCount
	{
		get
		{
			if (!(Instance != null))
			{
				return 0;
			}
			return Instance._renderers.Count;
		}
	}

	public static int TargetLodGroupCount
	{
		get
		{
			if (!(Instance != null))
			{
				return 0;
			}
			return Instance._targetLodGroupCount;
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Bootstrap()
	{
	}

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Object.Destroy(base.gameObject);
		}
		else
		{
			Instance = this;
		}
	}

	private void Update()
	{
	}

	private void OnDestroy()
	{
		if (!(Instance != this))
		{
			RestoreNormalState();
			Instance = null;
		}
	}

	private void AdvanceMode()
	{
		switch (_mode)
		{
		case DiagnosticMode.Normal:
			EnterRenderersDisabledMode();
			return;
		case DiagnosticMode.RenderersDisabled:
			EnterRootsDisabledMode();
			return;
		}
		RestoreNormalState();
		_mode = DiagnosticMode.Normal;
		Announce("SUNLIGHT TEST: NORMAL. Sunlight kokleri ve renderer durumlari geri yuklendi. 8-10 saniye bekleyip F7 ile JSON alabilirsin.");
	}

	private void EnterRenderersDisabledMode()
	{
		RestoreNormalState();
		CaptureTargets();
		if (_roots.Count == 0)
		{
			Announce("SUNLIGHT TEST: Hedef sahne Root objeleri bulunamadi. Mod degistirilmedi.");
			return;
		}
		foreach (RendererState renderer in _renderers)
		{
			if (renderer.renderer != null)
			{
				renderer.renderer.forceRenderingOff = true;
			}
		}
		_mode = DiagnosticMode.RenderersDisabled;
		Announce($"SUNLIGHT TEST: RENDERERS DISABLED. {_targetSceneCount} sahne, {_roots.Count} Root, " + $"{_renderers.Count} renderer kapali; {_targetLodGroupCount} LODGroup ve hiyerarsi aktif. " + "8-10 saniye bekle, sonra F7 ile JSON al.");
	}

	private void EnterRootsDisabledMode()
	{
		foreach (RootState root in _roots)
		{
			if (root.root != null)
			{
				root.root.SetActive(value: false);
			}
		}
		_mode = DiagnosticMode.RootsDisabled;
		Announce($"SUNLIGHT TEST: ROOTS DISABLED. {_roots.Count} Root tamamen kapali. " + "Renderer-disabled JSON ile arasindaki fark hiyerarsi/LODGroup CPU maliyetini gosterir. 8-10 saniye bekle, sonra F7 ile JSON al.");
	}

	private void CaptureTargets()
	{
		_roots.Clear();
		_renderers.Clear();
		_targetSceneCount = 0;
		_targetLodGroupCount = 0;
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene sceneAt = SceneManager.GetSceneAt(i);
			if (!sceneAt.isLoaded || !TargetSceneNames.Contains(sceneAt.name))
			{
				continue;
			}
			_targetSceneCount++;
			GameObject[] rootGameObjects = sceneAt.GetRootGameObjects();
			foreach (GameObject gameObject in rootGameObjects)
			{
				if (gameObject == null || gameObject.name != "Root")
				{
					continue;
				}
				_roots.Add(new RootState(gameObject, gameObject.activeSelf));
				Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
				foreach (Renderer renderer in componentsInChildren)
				{
					if (renderer != null)
					{
						_renderers.Add(new RendererState(renderer, renderer.forceRenderingOff));
					}
				}
				_targetLodGroupCount += gameObject.GetComponentsInChildren<LODGroup>(includeInactive: true).Length;
			}
		}
	}

	private void RestoreNormalState()
	{
		foreach (RootState root in _roots)
		{
			if (root.root != null)
			{
				root.root.SetActive(root.wasActiveSelf);
			}
		}
		foreach (RendererState renderer in _renderers)
		{
			if (renderer.renderer != null)
			{
				renderer.renderer.forceRenderingOff = renderer.wasForceRenderingOff;
			}
		}
	}

	private static void Announce(string message)
	{
		Debug.LogWarning("[PerformanceDiagnostic] " + message);
		RuntimePerformanceDiagnostic.EnsureSampler().ShowStatus(message, 10f);
	}
}
