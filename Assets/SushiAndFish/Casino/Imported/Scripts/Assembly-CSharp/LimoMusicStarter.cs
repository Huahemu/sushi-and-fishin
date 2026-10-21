namespace SushiAndFish.Casino.Original
{
using global::FMODUnity;
using global::UnityEngine;

public class LimoMusicStarter : MonoBehaviour
{
	[SerializeField]
	private StudioEventEmitter carEmitter;

	private float _startDelay = 2f;

	private void Start()
	{
		Invoke("TryStartingCarMusic", _startDelay);
	}

	private void TryStartingCarMusic()
	{
		if (!(carEmitter == null) && !carEmitter.IsPlaying())
		{
			carEmitter.Play();
		}
	}
}

}
