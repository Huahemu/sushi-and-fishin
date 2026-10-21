namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::Dissonance;
using global::Dissonance.Integrations.FMOD_Playback;
using global::FMODUnity;
using global::UnityEngine;

public class VoipManipulation : MonoBehaviour
{
	private FMODVoicePlayback _playbackComponent;

	private string _playerName;

	private VoipManipulationManager _voipManipulationManager;

	private DissonanceComms _dissonanceComms;

	[SerializeField]
	private VoipManipulationManager.VoipFX voipFX;

	[SerializeField]
	private int voipBusId;

	private void Awake()
	{
		_playbackComponent = GetComponent<FMODVoicePlayback>();
		_voipManipulationManager = GetComponentInParent<VoipManipulationManager>();
		_dissonanceComms = GetComponentInParent<DissonanceComms>();
	}

	private void Start()
	{
		UpdateVoipFX();
		UpdateVoipBus();
		UpdateMouthFX();
	}

	private void OnEnable()
	{
		_playerName = _playbackComponent.PlayerName;
		_voipManipulationManager.OnDesiredVoipFXChanged += UpdateVoipFX;
		_voipManipulationManager.OnDesiredVoipBusChanged += UpdateVoipBus;
		_voipManipulationManager.OnMouthFXChanged += UpdateMouthFX;
		UpdateVoipBus();
		UpdateVoipFX();
	}

	private void OnDisable()
	{
		_playerName = null;
		_voipManipulationManager.OnDesiredVoipBusChanged -= UpdateVoipBus;
		_voipManipulationManager.OnDesiredVoipFXChanged -= UpdateVoipFX;
		_voipManipulationManager.OnMouthFXChanged -= UpdateMouthFX;
	}

	private void UpdateVoipFX()
	{
		StartCoroutine(VoipFXRoutine());
	}

	private IEnumerator VoipFXRoutine()
	{
		yield return new WaitForEndOfFrame();
		voipFX = _voipManipulationManager.GetDesiredVoipFX(_playerName);
		SetVoipFXParam(voipFX);
	}

	private void UpdateMouthFX()
	{
		StartCoroutine(MouthFXRoutine());
	}

	private IEnumerator MouthFXRoutine()
	{
		yield return new WaitForEndOfFrame();
		int desiredMouthFX = _voipManipulationManager.GetDesiredMouthFX(_playerName);
		SetMouthFXParam(desiredMouthFX);
	}

	private void SetMouthFXParam(int i)
	{
		string text = "NO MOUTH " + voipBusId;
		RuntimeManager.StudioSystem.setParameterByName(text, i);
	}

	private void UpdateVoipBus()
	{
		StartCoroutine(VoipBusRoutine());
	}

	private IEnumerator VoipBusRoutine()
	{
		yield return new WaitForEndOfFrame();
		voipBusId = _voipManipulationManager.GetDesiredVoipBus(_playerName);
		_playbackComponent.OutputBusID = GetVoipBusID(voipBusId);
	}

	private void SetVoipFXParam(VoipManipulationManager.VoipFX fx)
	{
		string label = voipFX.ToString();
		string text = "FX " + voipBusId;
		RuntimeManager.StudioSystem.setParameterByNameWithLabel(text, label);
	}

	private string GetVoipBusID(int i)
	{
		return i switch
		{
			0 => "{b4f26317-56fe-4bcb-824d-48025ee6f104}", 
			1 => "{2860e6e7-17e7-42e9-9d86-a4b89a6309e8}", 
			2 => "{09be8ccb-eae0-40d8-949b-004ae6b6ca17}", 
			3 => "{4fc37a58-191d-4c79-865e-c8de1f1b27f4}", 
			4 => "{ab3422bf-2919-45db-810a-1d6650ff4d62}", 
			5 => "{60c025f9-638e-4159-83ec-77e90cfe04f1}", 
			6 => "{14fc2e88-3b32-4090-8c59-d4011a585e1c}", 
			7 => "{b9298076-c3d3-4f65-841f-b5b0ec31d38b}", 
			8 => "{ebc1d0aa-ca1e-4b6e-a43e-34c454dc1505}", 
			9 => "{bc251c8f-77ac-470f-a58a-c9e27f81d2b9}", 
			_ => "{0e0eb864-3277-43d3-8255-bfa8f493c8fc}", 
		};
	}

	public float GetNormalizedDistanceParameter()
	{
		RuntimeManager.StudioSystem.getListenerAttributes(0, out var attributes);
		return Mathf.Clamp01((new Vector3(attributes.position.x, attributes.position.y, attributes.position.z) - base.transform.position).magnitude / _playbackComponent.MaxDistance);
	}
}

}
