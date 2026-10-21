namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;
using global::AOT;
using global::FMOD;
using global::FMOD.Studio;
using global::FMODUnity;
using global::UnityEngine;

public class VoiceAnnouncementPlayer : MonoBehaviour
{
	private class AudioDataGenerator
	{
		private readonly VoiceAnnouncementPlayer _parent;

		private float[] _tempBuffer;

		private readonly object _lock = new object();

		public AudioDataGenerator(VoiceAnnouncementPlayer parent)
		{
			_parent = parent;
		}

		public RESULT GetAudio(IntPtr outbuffer, uint length, uint outchannels)
		{
			lock (_lock)
			{
				int num = (int)(length / outchannels);
				if (_tempBuffer == null || num > _tempBuffer.Length)
				{
					_tempBuffer = new float[num * 2];
				}
				_parent.GetAudioData(_tempBuffer, num);
				if (outbuffer != IntPtr.Zero)
				{
					float[] array = new float[length];
					int num2 = 0;
					for (uint num3 = 0u; num3 < length; num3 += outchannels)
					{
						float num4 = _tempBuffer[num2++];
						for (int i = 0; i < outchannels; i++)
						{
							array[num3 + i] = num4;
						}
					}
					Marshal.Copy(array, 0, outbuffer, (int)length);
				}
				return RESULT.OK;
			}
		}

		public RESULT ShouldProcess()
		{
			lock (_parent.audioBufferQueue)
			{
				return (!_parent.isPlaying || _parent.audioBufferQueue.Count <= 0) ? RESULT.ERR_DSP_SILENCE : RESULT.OK;
			}
		}
	}

	private Queue<float[]> audioBufferQueue = new Queue<float[]>();

	private bool isPlaying;

	private const int INPUT_SAMPLE_RATE = 16000;

	private DSP _dsp;

	private Channel _channel;

	private GCHandle _handle;

	private AudioDataGenerator _generator;

	private int _fmodSampleRate;

	[Header("Voice Effect Settings")]
	[SerializeField]
	private bool applyRadioEffect = true;

	private Bus _voiceBus;

	[Header("SFX")]
	[SerializeField]
	private string sfxMicOnEvent = "event:/Items/Microphone/MicOn";

	private float _resamplePosition;

	private float _resampleRatio;

	private bool _needsResampling;

	private List<float> _resampleBuffer = new List<float>();

	public void OnVoiceStart()
	{
		UnityEngine.Debug.Log("Voice announcement starting");
		if (!base.gameObject.activeInHierarchy)
		{
			base.gameObject.SetActive(value: true);
		}
		OnVoiceStop();
		_resamplePosition = 0f;
		_resampleBuffer.Clear();
		lock (audioBufferQueue)
		{
			audioBufferQueue.Clear();
		}
		isPlaying = true;
		RuntimeManager.CoreSystem.getSoftwareFormat(out _fmodSampleRate, out var _, out var _);
		_needsResampling = _fmodSampleRate != 16000;
		if (!_needsResampling)
		{
			_resampleRatio = 1f;
			UnityEngine.Debug.Log($"✅ Sample rates match ({_fmodSampleRate} Hz), no resampling needed");
		}
		else
		{
			_resampleRatio = 16000f / (float)_fmodSampleRate;
			UnityEngine.Debug.Log($"⚠\ufe0f Sample rate mismatch - FMOD: {_fmodSampleRate} Hz, Input: {16000} Hz, Resample ratio: {_resampleRatio:F6}");
		}
		_resamplePosition = 0f;
		_generator = new AudioDataGenerator(this);
		_handle = GCHandle.Alloc(_generator);
		DSP_DESCRIPTION description = new DSP_DESCRIPTION
		{
			numinputbuffers = 0,
			numoutputbuffers = 1,
			read = ReadDSP,
			shouldiprocess = ShouldProcessDSP,
			userdata = (IntPtr)_handle
		};
		RESULT rESULT = RuntimeManager.CoreSystem.createDSP(ref description, out _dsp);
		if (rESULT != RESULT.OK)
		{
			UnityEngine.Debug.LogError($"Failed to create FMOD DSP: {rESULT}");
			return;
		}
		GUID id = GUID.Parse("{56eae11b-1ae9-48d6-9574-3178c27509a6}");
		if (RuntimeManager.StudioSystem.getBusByID(id, out _voiceBus) == RESULT.OK)
		{
			_voiceBus.lockChannelGroup();
			RuntimeManager.StudioSystem.flushCommands();
			if (_voiceBus.getChannelGroup(out var group) == RESULT.OK && RuntimeManager.CoreSystem.playDSP(_dsp, group, paused: false, out _channel) == RESULT.OK)
			{
				UnityEngine.Debug.Log("✅ Voice playback started on bus");
			}
		}
		if (!_channel.hasHandle())
		{
			rESULT = RuntimeManager.CoreSystem.playDSP(_dsp, default, paused: false, out _channel);
			if (rESULT != RESULT.OK)
			{
				UnityEngine.Debug.LogError($"Failed to play FMOD DSP: {rESULT}");
				return;
			}
			UnityEngine.Debug.LogWarning("⚠\ufe0f Could not set bus, playing on default channel");
		}
		_channel.setPriority(0);
		RuntimeManager.StudioSystem.setParameterByName("AnnouncerFX", applyRadioEffect ? 1 : 0);
		PlayMicOnSFX();
		UnityEngine.Debug.Log("✅ Voice playback started via FMOD DSP");
	}

	public void OnVoiceAudio(string base64AudioData)
	{
		if (!isPlaying)
		{
			UnityEngine.Debug.LogWarning("Received audio data but not playing. Call OnVoiceStart first.");
			return;
		}
		if (string.IsNullOrEmpty(base64AudioData))
		{
			UnityEngine.Debug.LogWarning("Received empty audio data");
			return;
		}
		try
		{
			byte[] array = Convert.FromBase64String(base64AudioData);
			if (array.Length < 2)
			{
				UnityEngine.Debug.LogWarning($"Audio data too short: {array.Length} bytes");
				return;
			}
			int num = array.Length / 2;
			float[] array2 = new float[num];
			for (int i = 0; i < num; i++)
			{
				short num2 = (short)(array[i * 2] | (array[i * 2 + 1] << 8));
				array2[i] = (float)num2 / 32768f;
			}
			lock (audioBufferQueue)
			{
				audioBufferQueue.Enqueue(array2);
			}
			if (UnityEngine.Random.Range(0, 20) == 0)
			{
				UnityEngine.Debug.Log($"Audio chunk received: {num} samples, Queue size: {audioBufferQueue.Count}");
			}
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogError("Error processing audio data: " + ex.Message + "\n" + ex.StackTrace);
		}
	}

	public void OnVoiceStop()
	{
		isPlaying = false;
		if (_channel.hasHandle())
		{
			_channel.stop();
			_channel.clearHandle();
		}
		if (_dsp.hasHandle())
		{
			_dsp.release();
			_dsp.clearHandle();
		}
		if (_voiceBus.isValid())
		{
			_voiceBus.unlockChannelGroup();
			_voiceBus.clearHandle();
		}
		if (_handle.IsAllocated)
		{
			_handle.Free();
		}
		_generator = null;
		_resampleBuffer.Clear();
		_resamplePosition = 0f;
		lock (audioBufferQueue)
		{
			audioBufferQueue.Clear();
		}
	}

	internal bool GetAudioData(float[] buffer, int samplesRequested)
	{
		if (!isPlaying)
		{
			for (int i = 0; i < samplesRequested; i++)
			{
				buffer[i] = 0f;
			}
			return true;
		}
		if (_needsResampling)
		{
			ResampleAudio(buffer, samplesRequested);
		}
		else
		{
			CopySamplesDirectly(buffer, samplesRequested);
		}
		lock (audioBufferQueue)
		{
			return audioBufferQueue.Count == 0 && !isPlaying;
		}
	}

	private void CopySamplesDirectly(float[] outputBuffer, int outputSamples)
	{
		int num = 0;
		lock (audioBufferQueue)
		{
			while (num < outputSamples && audioBufferQueue.Count > 0)
			{
				float[] array = audioBufferQueue.Dequeue();
				int num2 = Mathf.Min(array.Length, outputSamples - num);
				Array.Copy(array, 0, outputBuffer, num, num2);
				num += num2;
				if (num2 < array.Length)
				{
					float[] array2 = new float[array.Length - num2];
					Array.Copy(array, num2, array2, 0, array2.Length);
					audioBufferQueue.Enqueue(array2);
				}
			}
		}
		for (int i = num; i < outputSamples; i++)
		{
			outputBuffer[i] = 0f;
		}
	}

	private void ResampleAudio(float[] outputBuffer, int outputSamples)
	{
		int num = Mathf.CeilToInt((float)outputSamples * _resampleRatio) + 2;
		lock (audioBufferQueue)
		{
			while (_resampleBuffer.Count < num && audioBufferQueue.Count > 0)
			{
				float[] collection = audioBufferQueue.Dequeue();
				_resampleBuffer.AddRange(collection);
			}
		}
		if (_resampleBuffer.Count < 2)
		{
			for (int i = 0; i < outputSamples; i++)
			{
				outputBuffer[i] = 0f;
			}
			return;
		}
		for (int j = 0; j < outputSamples; j++)
		{
			float resamplePosition = _resamplePosition;
			int num2 = Mathf.FloorToInt(resamplePosition);
			float t = resamplePosition - (float)num2;
			if (num2 + 1 < _resampleBuffer.Count)
			{
				outputBuffer[j] = Mathf.Lerp(_resampleBuffer[num2], _resampleBuffer[num2 + 1], t);
			}
			else if (num2 < _resampleBuffer.Count)
			{
				outputBuffer[j] = _resampleBuffer[num2];
			}
			else
			{
				outputBuffer[j] = 0f;
			}
			_resamplePosition += _resampleRatio;
		}
		int num3 = Mathf.FloorToInt(_resamplePosition);
		if (num3 > 0)
		{
			if (num3 <= _resampleBuffer.Count)
			{
				_resampleBuffer.RemoveRange(0, num3);
				_resamplePosition -= num3;
			}
			else
			{
				_resamplePosition -= _resampleBuffer.Count;
				_resampleBuffer.Clear();
			}
		}
	}

	private void OnDisable()
	{
		OnVoiceStop();
	}

	private void PlayMicOnSFX()
	{
		if (!string.IsNullOrWhiteSpace(sfxMicOnEvent))
		{
			RuntimeManager.PlayOneShot(sfxMicOnEvent);
		}
	}

	[MonoPInvokeCallback(typeof(DSP_READ_CALLBACK))]
	private static RESULT ReadDSP(ref DSP_STATE dsp_state, IntPtr inbuffer, IntPtr outbuffer, uint length, int inchannels, ref int outchannels)
	{
		if (dsp_state.functions.getuserdata(ref dsp_state, out var userdata) != RESULT.OK || userdata == IntPtr.Zero)
		{
			ClearBuffer(outbuffer, length);
			return RESULT.OK;
		}
		AudioDataGenerator audioDataGenerator = (AudioDataGenerator)GCHandle.FromIntPtr(userdata).Target;
		if (audioDataGenerator == null)
		{
			ClearBuffer(outbuffer, length);
			return RESULT.OK;
		}
		return audioDataGenerator.GetAudio(outbuffer, length, (uint)outchannels);
	}

	[MonoPInvokeCallback(typeof(DSP_SHOULDIPROCESS_CALLBACK))]
	private static RESULT ShouldProcessDSP(ref DSP_STATE dsp_state, bool inputsidle, uint length, CHANNELMASK inmask, int inchannels, SPEAKERMODE speakermode)
	{
		if (dsp_state.functions.getuserdata(ref dsp_state, out var userdata) != RESULT.OK || userdata == IntPtr.Zero)
		{
			return RESULT.ERR_DSP_SILENCE;
		}
		return ((AudioDataGenerator)GCHandle.FromIntPtr(userdata).Target)?.ShouldProcess() ?? RESULT.ERR_DSP_SILENCE;
	}

	private static void ClearBuffer(IntPtr buffer, uint length)
	{
		Marshal.Copy(new float[length], 0, buffer, (int)length);
	}
}

}
