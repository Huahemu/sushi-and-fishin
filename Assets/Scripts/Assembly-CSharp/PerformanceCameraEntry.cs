using System;

[Serializable]
public sealed class PerformanceCameraEntry
{
	public string name;

	public string renderType;

	public float depth;

	public float nearClip;

	public float farClip;

	public string target;

	public string cullingMask;
}
