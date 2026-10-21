using System;
using Dissonance;
using Dissonance.Audio.Capture;
using NAudio.Wave;
using PaperPixelGames.Framework.Dissonance;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Restaurant
{
	public sealed class FishTrophyAudioRecorder : MonoBehaviour, IMicrophoneSubscriber
	{
		private const int TargetSampleRate = 16000;

		private const int MaximumInputSampleRate = 48000;

		private const float MinimumRecordingSeconds = 0.2f;

		private const float MinimumRms = 0.001f;

		private readonly object _sampleLock = new object();

		private float[] _samples;

		private DissonanceComms _comms;

		private int _sampleCount;

		private int _inputSampleRate;

		private int _maximumSampleCount;

		private float _startedAt;

		private bool _subscribed;

		public bool IsRecording { get; private set; }

		public float ElapsedSeconds
		{
			get
			{
				if (!IsRecording)
				{
					return 0f;
				}
				return Time.unscaledTime - _startedAt;
			}
		}

		public bool TryStart(float maximumDurationSeconds)
		{
			if (IsRecording)
			{
				return false;
			}
			DissonanceManager instance = Singleton<DissonanceManager>.Instance;
			_comms = ((instance != null) ? instance.Comms : null);
			if (_comms == null)
			{
				UtilityMethods.Log("[FishTrophy][Recorder] DissonanceComms is unavailable.", LogType.Warning);
				return false;
			}
			int num = Mathf.CeilToInt(Mathf.Max(1f, maximumDurationSeconds) * 48000f);
			if (_samples == null || _samples.Length < num)
			{
				_samples = new float[num];
			}
			lock (_sampleLock)
			{
				_sampleCount = 0;
				_inputSampleRate = 0;
				_maximumSampleCount = num;
				IsRecording = true;
				_startedAt = Time.unscaledTime;
			}
			_comms.SubscribeToRecordedAudio(this);
			_subscribed = true;
			return true;
		}

		public bool TryFinish(out byte[] pcm16Bytes, out int sampleRate)
		{
			pcm16Bytes = Array.Empty<byte>();
			sampleRate = 16000;
			if (!IsRecording)
			{
				return false;
			}
			Unsubscribe();
			int sampleCount;
			int inputSampleRate;
			float[] array;
			lock (_sampleLock)
			{
				IsRecording = false;
				sampleCount = _sampleCount;
				inputSampleRate = _inputSampleRate;
				array = new float[sampleCount];
				if (sampleCount > 0)
				{
					Array.Copy(_samples, array, sampleCount);
				}
			}
			if (inputSampleRate <= 0 || sampleCount < Mathf.CeilToInt((float)inputSampleRate * 0.2f))
			{
				UtilityMethods.Log("[FishTrophy][Recorder] Recording was too short or no microphone samples arrived.", LogType.Warning);
				return false;
			}
			int num = Mathf.Max(1, Mathf.FloorToInt((float)sampleCount * (16000f / (float)inputSampleRate)));
			pcm16Bytes = new byte[num * 2];
			double num2 = 0.0;
			float num3 = (float)inputSampleRate / 16000f;
			for (int i = 0; i < num; i++)
			{
				float num4 = (float)i * num3;
				int num5 = Mathf.Min(Mathf.FloorToInt(num4), sampleCount - 1);
				int num6 = Mathf.Min(num5 + 1, sampleCount - 1);
				float value = Mathf.Lerp(array[num5], array[num6], num4 - (float)num5);
				value = Mathf.Clamp(value, -1f, 1f);
				num2 += (double)(value * value);
				short num7 = (short)Mathf.RoundToInt(value * 32767f);
				int num8 = i * 2;
				pcm16Bytes[num8] = (byte)(num7 & 0xFF);
				pcm16Bytes[num8 + 1] = (byte)((num7 >> 8) & 0xFF);
			}
			if (Math.Sqrt(num2 / (double)num) < 0.0010000000474974513)
			{
				pcm16Bytes = Array.Empty<byte>();
				UtilityMethods.Log("[FishTrophy][Recorder] Silent recording was rejected.", LogType.Warning);
				return false;
			}
			return true;
		}

		public void Cancel()
		{
			Unsubscribe();
			lock (_sampleLock)
			{
				IsRecording = false;
				_sampleCount = 0;
				_inputSampleRate = 0;
			}
		}

		public void ReceiveMicrophoneData(ArraySegment<float> buffer, WaveFormat format)
		{
			if (buffer.Array == null || format == null)
			{
				return;
			}
			lock (_sampleLock)
			{
				if (IsRecording)
				{
					if (_inputSampleRate == 0)
					{
						_inputSampleRate = format.SampleRate;
					}
					if (format.SampleRate != _inputSampleRate)
					{
						_sampleCount = 0;
						_inputSampleRate = format.SampleRate;
					}
					int b = _maximumSampleCount - _sampleCount;
					int num = Mathf.Min(buffer.Count, b);
					if (num > 0)
					{
						Array.Copy(buffer.Array, buffer.Offset, _samples, _sampleCount, num);
						_sampleCount += num;
					}
				}
			}
		}

		void IMicrophoneSubscriber.Reset()
		{
			lock (_sampleLock)
			{
				if (IsRecording)
				{
					_sampleCount = 0;
					_inputSampleRate = 0;
				}
			}
		}

		private void OnDisable()
		{
			Cancel();
		}

		private void Unsubscribe()
		{
			if (_subscribed)
			{
				if (_comms != null)
				{
					_comms.UnsubscribeFromRecordedAudio(this);
				}
				_subscribed = false;
				_comms = null;
			}
		}
	}
}
