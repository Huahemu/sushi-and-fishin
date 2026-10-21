using System;

[Serializable]
public sealed class PerformanceSample
{
	public string reason;

	public string sunlightDiagnosticMode;

	public bool hasDetailedSnapshot;

	public float realtime;

	public float fps;

	public float frameMs;

	public float mainThreadMs;

	public float renderThreadMs;

	public float gpuMs;

	public long gcAllocatedBytes;

	public long gcUsedBytes;

	public long systemUsedBytes;

	public int batches;

	public int drawCalls;

	public int setPassCalls;

	public int triangles;

	public int vertices;

	public int cameraRenderCount;

	public string sceneName;

	public float playerX;

	public float playerY;

	public float playerZ;

	public bool playerHasLocalPlayer;

	public float playerInputX;

	public float playerInputY;

	public float playerInputMagnitude;

	public float playerSpeed;

	public bool playerIsMovingInput;

	public bool playerIsNetworkSwimming;

	public bool playerIsCharacterSwimming;

	public bool playerIsSeaScooter;

	public bool playerIsUnderWater;

	public bool playerIsGrounded;

	public string playerMovementMode;

	public string playerSeaLayer;

	public string verdict;

	public string explanation;

	public PerformanceDetailedSnapshot detailedSnapshot;
}
