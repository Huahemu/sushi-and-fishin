using System;
using System.Collections.Generic;

[Serializable]
public sealed class PerformanceReport
{
	public string diagnosticVersion;

	public string createdAt;

	public string unityVersion;

	public string platform;

	public bool isEditor;

	public bool isDevelopmentBuild;

	public string graphicsDevice;

	public string graphicsApi;

	public string processor;

	public int systemMemoryMb;

	public int graphicsMemoryMb;

	public int screenWidth;

	public int screenHeight;

	public string qualityLevel;

	public int targetFrameRate;

	public int vSyncCount;

	public int sampleCount;

	public int spikeCount;

	public PerformanceSample latestDetailedSample;

	public PerformanceDetailedSnapshot latestDetailedSnapshot;

	public List<PerformanceSample> samples;

	public List<PerformanceSample> spikes;
}
