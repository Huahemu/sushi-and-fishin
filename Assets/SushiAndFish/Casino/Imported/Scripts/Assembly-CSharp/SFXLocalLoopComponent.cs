namespace SushiAndFish.Casino.Original
{
using global::FMOD.Studio;
using global::FMODUnity;
using global::UnityEngine;

public class SFXLocalLoopComponent : MonoBehaviour
{
	[SerializeField]
	private EventReference eventReference;

	[SerializeField]
	private bool allowFadeout = true;

	public EventInstance loopInstance;

	public void LoopSFX(bool play)
	{
		if (eventReference.IsNull)
		{
			return;
		}
		if (play)
		{
			if (loopInstance.isValid())
			{
				loopInstance.getPlaybackState(out var state);
				if (state == PLAYBACK_STATE.PLAYING)
				{
					return;
				}
			}
			loopInstance = RuntimeManager.CreateInstance(eventReference);
			loopInstance.set3DAttributes(base.transform.position.To3DAttributes());
			RuntimeManager.AttachInstanceToGameObject(loopInstance, base.gameObject, nonRigidbodyVelocity: true);
			loopInstance.start();
		}
		else if (loopInstance.isValid())
		{
			loopInstance.stop((!allowFadeout) ? FMOD.Studio.STOP_MODE.IMMEDIATE : FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			loopInstance.release();
		}
	}

	private void OnDisable()
	{
		LoopSFX(play: false);
	}

	public void ModulatePitch(float pitch)
	{
		loopInstance.setPitch(pitch);
	}
}

}
