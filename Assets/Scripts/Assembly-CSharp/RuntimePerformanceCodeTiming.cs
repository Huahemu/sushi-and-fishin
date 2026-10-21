using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public static class RuntimePerformanceCodeTiming
{
	public readonly struct Scope : IDisposable
	{
		private readonly RuntimePerformanceCodePoint _point;

		private readonly long _startedAt;

		private readonly bool _active;

		internal Scope(RuntimePerformanceCodePoint point, long startedAt, bool active)
		{
			_point = point;
			_startedAt = startedAt;
			_active = active;
		}

		public void Dispose()
		{
			if (_active)
			{
				Record(_point, _startedAt);
			}
		}
	}

	private static readonly long[] TotalTicks = new long[37];

	private static readonly long[] MaxTicks = new long[37];

	private static readonly long[] CallCounts = new long[37];

	private static readonly int[] ActiveFrameCounts = new int[37];

	private static readonly int[] LastActiveFrames = new int[37];

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void Reset()
	{
		Array.Clear(TotalTicks, 0, TotalTicks.Length);
		Array.Clear(MaxTicks, 0, MaxTicks.Length);
		Array.Clear(CallCounts, 0, CallCounts.Length);
		Array.Clear(ActiveFrameCounts, 0, ActiveFrameCounts.Length);
		for (int i = 0; i < LastActiveFrames.Length; i++)
		{
			LastActiveFrames[i] = -1;
		}
	}

	public static Scope Measure(RuntimePerformanceCodePoint point)
	{
		return default;
	}

	public static List<PerformanceCodeTimingEntry> Capture()
	{
		List<PerformanceCodeTimingEntry> list = new List<PerformanceCodeTimingEntry>(37);
		double num = (double)Stopwatch.Frequency / 1000.0;
		double num2 = Math.Max(0.001, Time.realtimeSinceStartupAsDouble);
		for (int i = 0; i < 37; i++)
		{
			double num3 = (double)TotalTicks[i] / num;
			double num4 = (double)MaxTicks[i] / num;
			PerformanceCodeTimingEntry performanceCodeTimingEntry = new PerformanceCodeTimingEntry();
			RuntimePerformanceCodePoint runtimePerformanceCodePoint = (RuntimePerformanceCodePoint)i;
			performanceCodeTimingEntry.name = runtimePerformanceCodePoint.ToString();
			performanceCodeTimingEntry.callCount = CallCounts[i];
			performanceCodeTimingEntry.activeFrameCount = ActiveFrameCounts[i];
			performanceCodeTimingEntry.totalMs = (float)num3;
			performanceCodeTimingEntry.averageCallMs = ((CallCounts[i] > 0) ? ((float)(num3 / (double)CallCounts[i])) : 0f);
			performanceCodeTimingEntry.averageActiveFrameMs = ((ActiveFrameCounts[i] > 0) ? ((float)(num3 / (double)ActiveFrameCounts[i])) : 0f);
			performanceCodeTimingEntry.maxCallMs = (float)num4;
			performanceCodeTimingEntry.averageMsPerSecond = (float)(num3 / num2);
			list.Add(performanceCodeTimingEntry);
		}
		return list;
	}

	private static void Record(RuntimePerformanceCodePoint point, long startedAt)
	{
		if (point >= RuntimePerformanceCodePoint.ConveyorBeltFixedUpdate && point < RuntimePerformanceCodePoint.Count)
		{
			long num = Math.Max(0L, Stopwatch.GetTimestamp() - startedAt);
			TotalTicks[(int)point] += num;
			CallCounts[(int)point]++;
			if (num > MaxTicks[(int)point])
			{
				MaxTicks[(int)point] = num;
			}
			int frameCount = Time.frameCount;
			if (LastActiveFrames[(int)point] != frameCount)
			{
				LastActiveFrames[(int)point] = frameCount;
				ActiveFrameCounts[(int)point]++;
			}
		}
	}
}
