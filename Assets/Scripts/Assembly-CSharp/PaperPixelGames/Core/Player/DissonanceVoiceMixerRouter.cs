using Dissonance.Audio.Playback;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.Audio;

namespace PaperPixelGames.Core.Player
{
	[RequireComponent(typeof(VoicePlayback))]
	[RequireComponent(typeof(AudioSource))]
	public class DissonanceVoiceMixerRouter : MonoBehaviour
	{
		[Header("References")]
		[SerializeField]
		private VoicePlayback _playback;

		[SerializeField]
		private AudioSource _source;

		[Header("Mixer Groups")]
		[SerializeField]
		private AudioMixerGroup _groupAA;

		[SerializeField]
		private AudioMixerGroup _groupAU;

		[SerializeField]
		private AudioMixerGroup _groupUA;

		[SerializeField]
		private AudioMixerGroup _groupUU;

		[SerializeField]
		private AudioMixerGroup _groupJellyfish;

		[Header("Update")]
		[SerializeField]
		private float _refreshInterval = 0.25f;

		private string _speakerId;

		private VoiceMixState _currentState;

		private float _nextRefreshTime;

		private PlayersManager _playersManager;

		private bool _speakerHasJellyfish;

		private EventBinding<JellyFishEvent> _jellyFishBinding;

		private void Awake()
		{
			if (_playback == null)
			{
				_playback = GetComponent<VoicePlayback>();
			}
			if (_source == null)
			{
				_source = GetComponent<AudioSource>();
			}
			_jellyFishBinding = new EventBinding<JellyFishEvent>(OnJellyFishEvent);
		}

		private void OnEnable()
		{
			EventBus<JellyFishEvent>.Register(_jellyFishBinding);
			_speakerId = null;
			_currentState = VoiceMixState.None;
			_nextRefreshTime = 0f;
			_speakerHasJellyfish = false;
		}

		private void OnDisable()
		{
			EventBus<JellyFishEvent>.Deregister(_jellyFishBinding);
			_currentState = VoiceMixState.None;
		}

		private void Update()
		{
			if (!Application.isPlaying || Time.unscaledTime < _nextRefreshTime)
			{
				return;
			}
			_nextRefreshTime = Time.unscaledTime + _refreshInterval;
			if (_playback == null || _source == null)
			{
				return;
			}
			if (_playersManager == null)
			{
				_playersManager = Singleton<PlayersManager>.Instance;
				if (_playersManager == null)
				{
					return;
				}
			}
			if (string.IsNullOrEmpty(_speakerId))
			{
				_speakerId = _playback.PlayerName;
				if (string.IsNullOrEmpty(_speakerId))
				{
					return;
				}
			}
			bool localUnderwater = _playersManager.IsLocalUnderWater();
			bool speakerUnderwater = _playersManager.IsPlayerUnderwater(_speakerId);
			VoiceMixState voiceMixState = ResolveState(localUnderwater, speakerUnderwater, _speakerHasJellyfish);
			if (voiceMixState != _currentState)
			{
				_currentState = voiceMixState;
				ApplyMixerGroup(_currentState);
			}
		}

		private static VoiceMixState ResolveState(bool localUnderwater, bool speakerUnderwater, bool speakerHasJellyfish)
		{
			if (speakerHasJellyfish)
			{
				return VoiceMixState.Jellyfish;
			}
			if (!localUnderwater && !speakerUnderwater)
			{
				return VoiceMixState.AboveAbove;
			}
			if (!localUnderwater & speakerUnderwater)
			{
				return VoiceMixState.AboveUnder;
			}
			if (localUnderwater && !speakerUnderwater)
			{
				return VoiceMixState.UnderAbove;
			}
			return VoiceMixState.UnderUnder;
		}

		private void ApplyMixerGroup(VoiceMixState state)
		{
			AudioMixerGroup audioMixerGroup = null;
			switch (state)
			{
			case VoiceMixState.AboveAbove:
				audioMixerGroup = _groupAA;
				break;
			case VoiceMixState.AboveUnder:
				audioMixerGroup = _groupAU;
				break;
			case VoiceMixState.UnderAbove:
				audioMixerGroup = _groupUA;
				break;
			case VoiceMixState.UnderUnder:
				audioMixerGroup = _groupUU;
				break;
			case VoiceMixState.Jellyfish:
				audioMixerGroup = _groupJellyfish;
				break;
			}
			if (audioMixerGroup != null && _source.outputAudioMixerGroup != audioMixerGroup)
			{
				_source.outputAudioMixerGroup = audioMixerGroup;
			}
		}

		public void ForceRefresh()
		{
			_nextRefreshTime = 0f;
		}

		private void OnJellyFishEvent(JellyFishEvent @event)
		{
			if (!(_playback == null))
			{
				if (string.IsNullOrEmpty(_speakerId))
				{
					_speakerId = _playback.PlayerName;
				}
				if (!string.IsNullOrEmpty(_speakerId) && !(@event.Player == null) && !(@event.Player.NfgoPlayer == null) && !(@event.Player.NfgoPlayer.PlayerId != _speakerId))
				{
					_speakerHasJellyfish = @event.Status;
					UtilityMethods.Log($"[DissonanceVoiceMixerRouter] Jellyfish state for speaker {_speakerId}: {@event.Status}");
					ForceRefresh();
				}
			}
		}
	}
}
