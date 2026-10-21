namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::Dissonance;
using global::Dissonance.Integrations.MirrorIgnorance;
using global::Gilzoide.UpdateManager;
using global::Mirror;
using global::UnityEngine;

public class PlayerMouth : NetworkBehaviour, ILateUpdatable, IManagedObject
{
	[SerializeField]
	private float gainMultiplier = 10f;

	[SerializeField]
	private Transform mouthTransform;

	public Transform headTransform;

	public float currentAmplitude;

	private DissonanceComms _dissonanceComms;

	private MirrorIgnorancePlayer _dissonancePlayer;

	private VoicePlayerState _voicePlayerState;

	private VoiceBroadcastTrigger _voiceBroadcastTrigger;

	private VoiceProximityBroadcastTrigger _voiceProximityBroadcastTrigger;

	private Vector3 _startScale;

	private void Awake()
	{
		_dissonancePlayer = GetComponent<MirrorIgnorancePlayer>();
	}

	public void OnEnable()
	{
		this.RegisterInManager();
	}

	public void OnDisable()
	{
		this.UnregisterInManager();
	}

	private void Start()
	{
		_startScale = mouthTransform.localScale;
		StartCoroutine(Initialization());
	}

	private IEnumerator Initialization()
	{
		while (!_dissonanceComms)
		{
			_dissonanceComms = DissonanceComms.GetSingleton();
			yield return null;
		}
		while (!_dissonancePlayer)
		{
			_dissonancePlayer = GetComponent<MirrorIgnorancePlayer>();
			yield return null;
		}
		while (string.IsNullOrEmpty(_dissonancePlayer.PlayerId))
		{
			yield return null;
		}
		while (true)
		{
			_voicePlayerState = _dissonanceComms.FindPlayer(_dissonancePlayer.PlayerId);
			if (_voicePlayerState == null)
			{
				yield return null;
				continue;
			}
			break;
		}
	}

	public void ManagedLateUpdate()
	{
		if (_voicePlayerState != null)
		{
			float num = _voicePlayerState.Amplitude;
			if (base.isLocalPlayer && !IsLocallyBroadcasting())
			{
				num = 0f;
			}
			currentAmplitude = num;
			Vector3 localScale = _startScale + new Vector3(num * gainMultiplier, 0f, 0f);
			mouthTransform.localScale = localScale;
		}
	}

	private bool IsLocallyBroadcasting()
	{
		if (_dissonanceComms == null)
		{
			return false;
		}
		if (_voiceBroadcastTrigger == null && _voiceProximityBroadcastTrigger == null)
		{
			RefreshVoiceTriggers();
		}
		if (!(_voiceBroadcastTrigger != null) || !_voiceBroadcastTrigger.IsTransmitting)
		{
			if (_voiceProximityBroadcastTrigger != null)
			{
				return _voiceProximityBroadcastTrigger.IsTransmitting;
			}
			return false;
		}
		return true;
	}

	private void RefreshVoiceTriggers()
	{
		_dissonanceComms.TryGetComponent<VoiceBroadcastTrigger>(out _voiceBroadcastTrigger);
		_dissonanceComms.TryGetComponent<VoiceProximityBroadcastTrigger>(out _voiceProximityBroadcastTrigger);
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
