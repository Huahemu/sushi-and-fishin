using System;

[Serializable]
public sealed class PerformanceUnderwaterChunkDiagnostics
{
	public int managerCount;

	public int activeManagerCount;

	public int chunkCount;

	public int sourceCount;

	public int matrixCount;

	public int batchCount;

	public int visibleChunkCount;

	public int lastDrawCallCount;
}
