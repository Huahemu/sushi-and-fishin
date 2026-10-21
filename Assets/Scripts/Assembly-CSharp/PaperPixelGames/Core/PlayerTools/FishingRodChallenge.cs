using System;
using PaperPixelGames.Core.Data.Tools;
using UnityEngine;

namespace PaperPixelGames.Core.PlayerTools
{
	public sealed class FishingRodChallenge
	{
		private readonly FishingRodFishSettings _fish;

		private readonly RodDifficultyMatchup _matchup;

		private readonly Func<float> _random;

		private float _remaining;

		private float _counterTime;

		private float _interruption;

		public FishingRodFishDirection Direction { get; private set; }

		public float Progress { get; private set; }

		public float Tension { get; private set; }

		public float CounterProgress => Mathf.Clamp01(_counterTime / RequiredCounterTime);

		public float PreparationRemaining { get; private set; }

		public bool Succeeded
		{
			get
			{
				if (Progress >= 1f)
				{
					return Tension < 1f;
				}
				return false;
			}
		}

		public bool Failed => Tension >= 1f;

		public bool FailedWhileReeling { get; private set; }

		public int CountersCompleted { get; private set; }

		private float RequiredCounterTime => Mathf.Max(0.05f, _fish.CounterRequiredDuration * _matchup.CounterDurationMultiplier);

		public FishingRodChallenge(FishingRodFishSettings fish, RodDifficultyMatchup matchup, Func<float> random)
		{
			_fish = fish ?? throw new ArgumentNullException("fish");
			_matchup = matchup ?? throw new ArgumentNullException("matchup");
			_random = random ?? throw new ArgumentNullException("random");
			NextDirection(counterSucceeded: false);
			PreparationRemaining = 0.55f;
		}

		public static bool IsNewerSession(uint candidate, uint previous)
		{
			if (candidate != 0)
			{
				return (int)(candidate - previous) > 0;
			}
			return false;
		}

		public static Vector2 ResolveCounter(Vector2 input, float threshold)
		{
			if (!float.IsFinite(input.x) || !float.IsFinite(input.y))
			{
				return Vector2.zero;
			}
			float num = Mathf.Abs(input.x);
			float num2 = Mathf.Max(0f, input.y);
			threshold = Mathf.Clamp(threshold, 0.05f, 1f);
			if (num >= threshold && num >= num2 * 1.2f)
			{
				return new Vector2(Mathf.Sign(input.x), 0f);
			}
			if (num2 >= threshold && num2 >= num * 1.2f)
			{
				return Vector2.up;
			}
			return Vector2.zero;
		}

		public static bool IsCorrect(FishingRodFishDirection direction, Vector2 counter)
		{
			return direction switch
			{
				FishingRodFishDirection.Left => counter.x > 0f, 
				FishingRodFishDirection.Right => counter.x < 0f, 
				FishingRodFishDirection.Forward => counter.y > 0f, 
				_ => false, 
			};
		}

		public static float Charge(float elapsed, float duration, bool holdAtMaximum)
		{
			duration = Mathf.Max(0.05f, duration);
			if (holdAtMaximum)
			{
				return Mathf.Clamp01(elapsed / duration);
			}
			float num = Mathf.Repeat(Mathf.Max(0f, elapsed), duration * 2f + 0.2f);
			if (num <= duration)
			{
				return num / duration;
			}
			if (num <= duration + 0.2f)
			{
				return 1f;
			}
			return 1f - (num - duration - 0.2f) / duration;
		}

		public void Tick(float deltaTime, Vector2 counter, bool reeling)
		{
			if (float.IsFinite(deltaTime) && !(deltaTime <= 0f))
			{
				float num = Mathf.Min(deltaTime, 0.5f);
				while (num > 0f && !Succeeded && !Failed)
				{
					float num2 = Mathf.Min(num, 1f / 60f);
					num -= num2;
					Step(num2, counter, reeling);
				}
			}
		}

		private void Step(float dt, Vector2 counter, bool reeling)
		{
			if (PreparationRemaining > 0f)
			{
				PreparationRemaining = Mathf.Max(0f, PreparationRemaining - dt);
				return;
			}
			_remaining -= dt;
			FishingRodFishDirection direction = Direction;
			if (direction == FishingRodFishDirection.Centered || direction == FishingRodFishDirection.Tired)
			{
				if (reeling)
				{
					Progress += _fish.CatchProgressPerSecond * 0.01f * _matchup.CatchProgressMultiplier * ((Direction == FishingRodFishDirection.Tired) ? 1.35f : 1f) * dt;
				}
				Recover(dt, reeling ? 1f : 0.45f);
			}
			else if (!reeling && IsCorrect(Direction, counter))
			{
				_interruption = 0f;
				_counterTime += dt;
				Recover(dt, 1f);
				if (_counterTime >= RequiredCounterTime)
				{
					CountersCompleted++;
					NextDirection(counterSucceeded: true);
					return;
				}
			}
			else
			{
				_interruption += dt;
				if (_interruption > 0.15f)
				{
					_counterTime = Mathf.Max(0f, _counterTime - dt * 2f);
				}
				Tension += _fish.TensionGainPerSecond * 0.01f * _matchup.TensionGainMultiplier * (reeling ? _fish.WrongReelTensionMultiplier : 1f) * dt;
				if (Tension >= 1f)
				{
					FailedWhileReeling = reeling;
				}
			}
			Tension = Mathf.Clamp01(Tension);
			Progress = Mathf.Clamp01(Progress);
			if (_remaining <= 0f && !Succeeded && !Failed)
			{
				NextDirection(counterSucceeded: false);
			}
		}

		private void Recover(float dt, float strength)
		{
			Tension = Mathf.Max(0f, Tension - _fish.TensionRecoveryPerSecond * 0.01f * _matchup.TensionRecoveryMultiplier * strength * dt);
		}

		private void NextDirection(bool counterSucceeded)
		{
			_counterTime = (_interruption = 0f);
			PreparationRemaining = Mathf.Max(0.25f, _matchup.CounterForgiveness);
			if (counterSucceeded)
			{
				bool flag = _random() < _fish.TiredChance;
				Direction = (flag ? FishingRodFishDirection.Tired : FishingRodFishDirection.Centered);
				_remaining = Mathf.Max(0.1f, flag ? _fish.TiredDuration : _fish.CenteredDuration);
				PreparationRemaining = 0f;
			}
			else
			{
				float num = Mathf.Max(0f, _fish.LeftWeight);
				float num2 = Mathf.Max(0f, _fish.RightWeight);
				float num3 = num + num2 + Mathf.Max(0f, _fish.ForwardWeight);
				float num4 = Mathf.Clamp01(_random()) * num3;
				Direction = ((!(num3 <= 0f)) ? ((num4 < num) ? FishingRodFishDirection.Left : ((num4 < num + num2) ? FishingRodFishDirection.Right : FishingRodFishDirection.Forward)) : FishingRodFishDirection.Centered);
				_remaining = Mathf.Max(0.1f, (num3 <= 0f) ? _fish.CenteredDuration : _fish.MovementDuration);
			}
		}
	}
}
