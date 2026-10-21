using UnityEngine;

public static class RuntimePerformanceDiagnostic
{
	public const KeyCode TogglePanelKey = KeyCode.F6;

	public const KeyCode CaptureReportKey = KeyCode.F7;

	private static RuntimePerformanceSampler _sampler;

	public static RuntimePerformanceSampler Sampler => _sampler;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Bootstrap()
	{
	}

	public static RuntimePerformanceSampler EnsureSampler()
	{
		if (_sampler != null)
		{
			return _sampler;
		}
		_sampler = Object.FindAnyObjectByType<RuntimePerformanceSampler>();
		if (_sampler != null)
		{
			return _sampler;
		}
		GameObject gameObject = new GameObject("[Runtime Performance Diagnostic]");
		Object.DontDestroyOnLoad(gameObject);
		_sampler = gameObject.AddComponent<RuntimePerformanceSampler>();
		return _sampler;
	}
}
