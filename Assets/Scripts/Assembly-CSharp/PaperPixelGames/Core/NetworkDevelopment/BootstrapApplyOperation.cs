using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using PaperPixelGames.Networking;
using UnityEngine;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public sealed class BootstrapApplyOperation : IDisposable
	{
		internal sealed class PreparedModule
		{
			public IJoinBootstrapModule Module;

			public IPreparedBootstrapModule Application;

			public bool Skipped;
		}

		public const double DefaultFrameBudgetMs = 2.0;

		public const int DefaultStepsPerFrame = 8;

		private readonly List<PreparedModule> modules;

		private readonly Func<bool> isCurrent;

		private readonly Func<double> clock;

		private readonly double frameBudgetMs;

		private readonly int stepsPerFrame;

		private IEnumerator iterator;

		private int moduleIndex;

		private int afterIndex;

		private int moduleSteps;

		private double moduleWorkMs;

		private double moduleMaxStepMs;

		private bool started;

		public BootstrapApplyResult Result { get; private set; }

		public bool Completed { get; private set; }

		public int Frames { get; private set; }

		public int Steps { get; private set; }

		public double MaxFrameWorkMs { get; private set; }

		public double MaxStepMs { get; private set; }

		internal BootstrapApplyOperation(BootstrapApplyResult result, List<PreparedModule> modules, Func<bool> isCurrent, Func<double> clock, double frameBudgetMs, int stepsPerFrame)
		{
			Result = result;
			this.modules = modules;
			this.isCurrent = isCurrent ?? ((Func<bool>)(() => true));
			this.clock = clock ?? new Func<double>(MonotonicSeconds);
			this.frameBudgetMs = ((frameBudgetMs > 0.0) ? frameBudgetMs : 2.0);
			this.stepsPerFrame = Math.Max(1, stepsPerFrame);
			Completed = modules == null;
		}

		public static double MonotonicSeconds()
		{
			return (double)Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
		}

		public void AdvanceFrame()
		{
			if (Completed)
			{
				return;
			}
			double num = clock();
			int num2 = 0;
			try
			{
				if (!isCurrent())
				{
					Fail("Bootstrap operation belongs to an inactive session.");
					return;
				}
				if (!started)
				{
					foreach (PreparedModule module in modules)
					{
						bool num3 = module.Module.ClientCanApply() && module.Application.IsReady;
						module.Skipped = false;
						if (!num3)
						{
							if (module.Module.IsRequired)
							{
								BootstrapApplyResult result = Result;
								result.Retryable = true;
								result.FailReason = $"Required bootstrap targets are not ready. ModuleId={module.Module.ModuleId}";
								Result = result;
								return;
							}
							module.Skipped = true;
						}
					}
					BootstrapApplyResult result2 = Result;
					result2.Retryable = false;
					result2.FailReason = string.Empty;
					Result = result2;
					started = true;
				}
				Frames++;
				while (!Completed)
				{
					if (!isCurrent())
					{
						Fail("Bootstrap operation belongs to an inactive session.");
						break;
					}
					double num4 = clock();
					int num5 = moduleIndex;
					MoveOneStep();
					double num6 = Math.Max(0.0, (clock() - num4) * 1000.0);
					Steps++;
					num2++;
					MaxStepMs = Math.Max(MaxStepMs, num6);
					if (num5 < modules.Count)
					{
						moduleWorkMs += num6;
						moduleMaxStepMs = Math.Max(moduleMaxStepMs, num6);
						if (num5 != moduleIndex)
						{
							PreparedModule preparedModule = modules[num5];
							MultiplayerDiagnostics.Record("snapshot_module_applied", FormattableString.Invariant($"module={preparedModule.Module.ModuleId} skipped={preparedModule.Skipped} steps={moduleSteps} workMs={moduleWorkMs:F2} maxStepMs={moduleMaxStepMs:F2}"), ulong.MaxValue);
							moduleSteps = 0;
							moduleWorkMs = (moduleMaxStepMs = 0.0);
						}
					}
					if (num2 >= stepsPerFrame || (clock() - num) * 1000.0 >= frameBudgetMs)
					{
						break;
					}
				}
			}
			catch (Exception ex)
			{
				ushort num7 = (ushort)((moduleIndex < modules.Count) ? modules[moduleIndex].Module.ModuleId : ((afterIndex > 0) ? modules[afterIndex - 1].Module.ModuleId : 0));
				Fail($"Bootstrap apply exception. ModuleId={num7}: {ex.Message}");
			}
			finally
			{
				MaxFrameWorkMs = Math.Max(MaxFrameWorkMs, Math.Max(0.0, (clock() - num) * 1000.0));
			}
		}

		private void MoveOneStep()
		{
			if (moduleIndex < modules.Count)
			{
				PreparedModule preparedModule = modules[moduleIndex];
				if (preparedModule.Skipped)
				{
					moduleIndex++;
					return;
				}
				try
				{
					if (iterator == null)
					{
						iterator = preparedModule.Application.ApplySteps();
					}
					if (iterator == null)
					{
						throw new InvalidOperationException($"Module {preparedModule.Module.ModuleId} returned no apply steps.");
					}
					if (iterator.MoveNext())
					{
						moduleSteps++;
						return;
					}
				}
				catch (Exception ex) when (!preparedModule.Module.IsRequired)
				{
					preparedModule.Skipped = true;
					UnityEngine.Debug.LogWarning($"Optional bootstrap module {preparedModule.Module.ModuleId} failed: {ex.Message}");
				}
				DisposeIterator();
				moduleIndex++;
			}
			else if (afterIndex < modules.Count)
			{
				PreparedModule preparedModule2 = modules[afterIndex++];
				if (!preparedModule2.Skipped)
				{
					try
					{
						preparedModule2.Module.ClientAfterAllApplied();
					}
					catch (Exception ex2) when (!preparedModule2.Module.IsRequired)
					{
						UnityEngine.Debug.LogWarning($"Optional bootstrap module {preparedModule2.Module.ModuleId} after-apply failed: {ex2.Message}");
					}
				}
			}
			else
			{
				BootstrapApplyResult result = Result;
				result.Success = true;
				Result = result;
				Completed = true;
			}
		}

		private void DisposeIterator()
		{
			IEnumerator enumerator = iterator;
			iterator = null;
			(enumerator as IDisposable)?.Dispose();
		}

		private void Fail(string reason)
		{
			BootstrapApplyResult result = Result;
			result.Success = (result.Retryable = false);
			result.FailReason = reason;
			Result = result;
			Completed = true;
			try
			{
				DisposeIterator();
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}

		public void Dispose()
		{
			if (!Completed)
			{
				Fail("Bootstrap operation cancelled.");
			}
			else
			{
				DisposeIterator();
			}
		}
	}
}
