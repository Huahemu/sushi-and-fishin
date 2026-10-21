using PaperPixelGames.Framework.Enums;
using UnityEngine;
using UnityEngine.Audio;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "NewSound", menuName = "Data/Sound")]
	public class AudioClipSO : ScriptableObject
	{
		public SoundType Type;

		public AudioClip Clip;

		[Tooltip("Optional random variants. If empty, Clip is used.")]
		public AudioClip[] Variants;

		[Tooltip("If set, this mixer group will be used directly and AudioRoute will be ignored.")]
		public AudioMixerGroup MixerGroup;

		[Tooltip("High-level routing hint used when MixerGroup is not assigned.")]
		public AudioRoute Route;

		[Range(0f, 1f)]
		public float Volume = 1f;

		public bool UseRandomVolume;

		public Vector2 RandomVolumeRange = new Vector2(0.9f, 1f);

		public bool Cache;

		public bool Loop;

		public bool Is3D;

		[Min(0f)]
		[Tooltip("Minimum time between plays for this sound asset. Zero disables throttling.")]
		public float Cooldown;

		[Min(0f)]
		[Tooltip("Maximum simultaneous instances for this sound asset. Zero means unlimited.")]
		public int MaxInstances;

		public AudioInstanceLimitMode InstanceLimitMode = AudioInstanceLimitMode.StopOldest;

		public bool UseRandomPitch;

		public Vector2 RandomPitchRange = new Vector2(0.9f, 1.1f);

		public bool UseRandomStartTime;

		[Tooltip("Normalized clip time range used as a random playback start offset.")]
		public Vector2 RandomStartTimeRange = new Vector2(0f, 0.25f);

		public bool FadeIn;

		public float FadeInDuration = 1f;

		public bool FadeOut;

		public float FadeOutDuration = 1f;

		[Range(0f, 1f)]
		public float SpatialBlend = 1f;

		[Min(0f)]
		public float MinDistance = 1f;

		public float HearDistance = 10f;

		public AudioRolloffMode RolloffMode;

		[Min(0f)]
		public float DopplerLevel = 1f;
	}
}
