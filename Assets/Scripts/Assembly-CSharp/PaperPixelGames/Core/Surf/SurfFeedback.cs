using System;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Core.Surf
{
	public sealed class SurfFeedback : IDisposable
	{
		private readonly Surfboard _board;

		private SoundManager _manager;

		private OwnedLoopHandle _glide;

		private OwnedLoopHandle _wind;

		private float _waveEnergy;

		private bool _initialized;

		private bool _riding;

		private bool _standing;

		private long _stroke;

		private uint _caughtWave;

		private double _stanceAt = double.PositiveInfinity;

		public SurfFeedback(Surfboard board)
		{
			_board = board;
		}

		public void Tick(double time, float dt)
		{
			long num = SurfPosePlacement.StrokeIndex(time - _board.PoseTime);
			SurfFeedbackProfile feedback = _board.feedback;
			bool isRiding = _board.IsRiding;
			if (_initialized && feedback != null && Singleton<SoundManager>.TryGetInstance(out _manager))
			{
				if (isRiding && !_riding)
				{
					Play(feedback.mount, _board.SurfacePosition);
				}
				if (isRiding && _riding && _standing != _board.Standing)
				{
					_stanceAt = time + 0.42;
				}
				if (isRiding && time >= _stanceAt)
				{
					Play(feedback.stance, _board.SurfacePosition);
					_stanceAt = double.PositiveInfinity;
				}
				if (isRiding && !_board.Standing && _board.Paddling && _board.PaddleBlend > 0.35f && time - _board.PoseTime > 0.65 && num > _stroke)
				{
					float num2 = (((num & 1) != 0L) ? 1 : (-1));
					Play(feedback.paddle, _board.SurfacePosition + _board.transform.right * num2 * 0.35f);
				}
				SurfboardWorld instance = SurfboardWorld.Instance;
				float num3 = ((isRiding && _board.Standing && instance != null) ? instance.ActiveWave.CatchStrength(_board.SurfacePosition, _board.transform.forward, time) : 0f);
				_waveEnergy = Mathf.MoveTowards(_waveEnergy, num3, dt * ((num3 > _waveEnergy) ? 5f : 1.5f));
				if (num3 > 0.3f && instance.ActiveWave.Id != _caughtWave)
				{
					_caughtWave = instance.ActiveWave.Id;
					Play(feedback.catchWave, _board.SurfacePosition);
				}
				UpdateGlide(feedback, isRiding, dt);
			}
			if (!isRiding)
			{
				StopLoop();
				_stanceAt = double.PositiveInfinity;
			}
			_stroke = num;
			_riding = isRiding;
			_standing = _board.Standing;
			_initialized = true;
		}

		private void Play(AudioClipSO sound, Vector3 position)
		{
			if (sound != null && _manager != null)
			{
				_manager.PlaySFX(sound, position);
			}
		}

		private void UpdateGlide(SurfFeedbackProfile profile, bool riding, float dt)
		{
			float num = Mathf.InverseLerp(0.6f, 7f, _board.PresentationSpeed);
			float num2 = ((!riding) ? 0f : (_board.Standing ? Mathf.Clamp01(Mathf.Sqrt(num) + _waveEnergy * 0.25f) : (num * 0.07f)));
			float num3 = 1f + Mathf.Abs(_board.TurnLean) * 0.18f;
			UpdateLoop(ref _glide, profile.glideLoop, num2 * num3, Mathf.Lerp(0.94f, 1.12f, num), dt, "water");
			float target = ((riding && _board.Standing) ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.5f, 7f, _board.PresentationSpeed)) : 0f);
			UpdateLoop(ref _wind, profile.windLoop, target, Mathf.Lerp(0.92f, 1.08f, num), dt, "wind");
		}

		private void UpdateLoop(ref OwnedLoopHandle handle, AudioClipSO sound, float target, float pitch, float dt, string layer)
		{
			if (sound == null)
			{
				if (_manager != null)
				{
					_manager.ReleaseOwnedLoop(handle);
				}
				handle = null;
				return;
			}
			if (target > 0.025f && (handle == null || !handle.IsActive))
			{
				handle = _manager.PlayOwnedLoop(sound, _board.SurfacePosition, null, $"Surfboard-{_board.NetworkObjectId}-{layer}");
				if (handle?.Source != null)
				{
					handle.Source.volume = 0f;
				}
			}
			AudioSource audioSource = handle?.Source;
			if (!(audioSource == null))
			{
				audioSource.transform.position = _board.SurfacePosition;
				audioSource.volume = Mathf.MoveTowards(audioSource.volume, Mathf.Clamp01(sound.Volume * target), dt * 1.8f);
				audioSource.pitch = pitch;
				if (target <= 0.001f && audioSource.volume <= 0.002f)
				{
					_manager.ReleaseOwnedLoop(handle);
					handle = null;
				}
			}
		}

		private void StopLoop()
		{
			if (_manager != null)
			{
				_manager.ReleaseOwnedLoop(_glide);
			}
			if (_manager != null)
			{
				_manager.ReleaseOwnedLoop(_wind);
			}
			_glide = (_wind = null);
			_waveEnergy = 0f;
		}

		public void Dispose()
		{
			StopLoop();
			_stanceAt = double.PositiveInfinity;
		}
	}
}
