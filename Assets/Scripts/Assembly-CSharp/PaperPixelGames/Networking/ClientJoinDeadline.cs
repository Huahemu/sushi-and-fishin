using System;

namespace PaperPixelGames.Networking
{
	public sealed class ClientJoinDeadline
	{
		public const double WarningSeconds = 60.0;

		public const double SceneNoProgressSeconds = 360.0;

		public const int MinimumSceneTimeoutSeconds = 600;

		public const double SceneReadyGraceSeconds = 30.0;

		private double _approvalSeconds;

		private double _sceneSeconds;

		private float _sceneProgress;

		private bool _warned;

		public long Attempt { get; private set; }

		public ClientJoinStage Stage { get; private set; }

		public double StageStartedAt { get; private set; }

		public double LastProgressAt { get; private set; }

		public double StageLimitSeconds { get; private set; }

		public bool IsWaiting
		{
			get
			{
				if (Stage != ClientJoinStage.Idle)
				{
					return Stage != ClientJoinStage.Connected;
				}
				return false;
			}
		}

		public long Begin(double now, double transportSeconds, double approvalSeconds, double sceneSeconds)
		{
			Attempt++;
			_approvalSeconds = PositiveSeconds(approvalSeconds, 120.0);
			_sceneSeconds = Math.Max(600.0, PositiveSeconds(sceneSeconds, 600.0));
			SetStage(ClientJoinStage.TransportConnecting, now, PositiveSeconds(transportSeconds, 120.0));
			return Attempt;
		}

		public bool TransportConnected(long attempt, double now)
		{
			if (attempt != Attempt || Stage != ClientJoinStage.TransportConnecting)
			{
				return false;
			}
			SetStage(ClientJoinStage.AwaitingApproval, now, _approvalSeconds);
			return true;
		}

		public bool BeginSceneSynchronization(long attempt, double now)
		{
			if (attempt != Attempt || !IsWaiting || Stage == ClientJoinStage.SceneSynchronization)
			{
				return false;
			}
			SetStage(ClientJoinStage.SceneSynchronization, now, _sceneSeconds);
			return true;
		}

		public void BeginSceneLoad(long attempt, double now)
		{
			if (attempt == Attempt && Stage == ClientJoinStage.SceneSynchronization)
			{
				_sceneProgress = 0f;
				LastProgressAt = now;
			}
		}

		public void ObserveSceneProgress(long attempt, double now, float progress)
		{
			if (attempt == Attempt && Stage == ClientJoinStage.SceneSynchronization && !float.IsNaN(progress) && !float.IsInfinity(progress) && !(progress <= _sceneProgress) && !(progress > 1f))
			{
				_sceneProgress = progress;
				LastProgressAt = now;
			}
		}

		public void Complete(long attempt)
		{
			if (attempt == Attempt && IsWaiting)
			{
				Stage = ClientJoinStage.Connected;
			}
		}

		public void Cancel()
		{
			Stage = ClientJoinStage.Idle;
		}

		public JoinWaitStatus Evaluate(long attempt, double now)
		{
			if (attempt != Attempt || !IsWaiting)
			{
				return JoinWaitStatus.Inactive;
			}
			double num = now - StageStartedAt;
			if (num >= StageLimitSeconds)
			{
				return JoinWaitStatus.TimedOut;
			}
			if (Stage == ClientJoinStage.SceneSynchronization && now - LastProgressAt >= 360.0)
			{
				return JoinWaitStatus.NoProgress;
			}
			if (!_warned && num >= 60.0)
			{
				_warned = true;
				return JoinWaitStatus.Warning;
			}
			return JoinWaitStatus.Waiting;
		}

		private void SetStage(ClientJoinStage stage, double now, double limit)
		{
			Stage = stage;
			double stageStartedAt = (LastProgressAt = now);
			StageStartedAt = stageStartedAt;
			StageLimitSeconds = limit;
			_sceneProgress = 0f;
			_warned = false;
		}

		private static double PositiveSeconds(double value, double fallback)
		{
			if (!double.IsNaN(value) && !double.IsInfinity(value))
			{
				return Math.Max(1.0, value);
			}
			return fallback;
		}
	}
}
