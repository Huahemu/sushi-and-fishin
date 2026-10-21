namespace SushiAndFish.Casino.Original
{
using global::FMODUnity;
using global::UnityEngine;

public class MusicManager : MonoBehaviour
{
	public bool enableMusic;

	private StudioEventEmitter studioEventEmitter;

	public static MusicManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		Object.DontDestroyOnLoad(base.gameObject);
		TryGetComponent<StudioEventEmitter>(out studioEventEmitter);
	}

	private void OnEnable()
	{
		if (enableMusic && !studioEventEmitter.IsPlaying())
		{
			studioEventEmitter.Play();
		}
	}

	private void OnValidate()
	{
		if (Application.isPlaying && !(studioEventEmitter == null))
		{
			if (enableMusic && !studioEventEmitter.IsPlaying())
			{
				studioEventEmitter.Play();
			}
			else
			{
				studioEventEmitter.Stop();
			}
		}
	}
}

}
