namespace SushiAndFish.Casino.Original
{
using global::FMOD.Studio;
using global::FMODUnity;
using global::UnityEngine;

public class SFXManager : MonoBehaviour
{
	public static SFXManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		Object.DontDestroyOnLoad(base.gameObject);
	}

	public static void SFXOneShot(EventReference sfxEvent, Vector3 pos = default(Vector3))
	{
		if (!sfxEvent.IsNull)
		{
			RuntimeManager.PlayOneShot(sfxEvent, pos);
		}
	}

	public static void SFXOneShotWithParameters(EventReference sfxEvent, SFXParams[] sFXParams, Vector3 pos = default(Vector3), float pitch = 1f)
	{
		if (sfxEvent.IsNull)
		{
			return;
		}
		EventInstance eventInstance = RuntimeManager.CreateInstance(sfxEvent);
		if (sFXParams != null)
		{
			for (int i = 0; i < sFXParams.Length; i++)
			{
				SFXParams sFXParams2 = sFXParams[i];
				eventInstance.setParameterByName(sFXParams2.name, sFXParams2.value);
			}
		}
		eventInstance.set3DAttributes(pos.To3DAttributes());
		eventInstance.setPitch(pitch);
		eventInstance.start();
		eventInstance.release();
	}

	public static void SFXOneShot3DAttachedWithParameters(EventReference sfxEvent, SFXParams[] sFXParams, GameObject attachObject, bool non_rigidbody_velocity = false)
	{
		if (sfxEvent.IsNull)
		{
			return;
		}
		EventInstance instance = RuntimeManager.CreateInstance(sfxEvent);
		RuntimeManager.AttachInstanceToGameObject(instance, attachObject, non_rigidbody_velocity);
		if (sFXParams != null)
		{
			for (int i = 0; i < sFXParams.Length; i++)
			{
				SFXParams sFXParams2 = sFXParams[i];
				instance.setParameterByName(sFXParams2.name, sFXParams2.value);
			}
		}
		instance.start();
		instance.release();
	}

	public static void SFXOneShot3DAttached(EventReference sfxEvent, GameObject attachObject, bool non_rigidbody_velocity = false)
	{
		if (!sfxEvent.IsNull)
		{
			EventInstance instance = RuntimeManager.CreateInstance(sfxEvent);
			RuntimeManager.AttachInstanceToGameObject(instance, attachObject, non_rigidbody_velocity);
			instance.start();
			instance.release();
		}
	}
}

}
