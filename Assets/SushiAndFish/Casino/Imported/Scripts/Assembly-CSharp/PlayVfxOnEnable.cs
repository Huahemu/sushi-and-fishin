namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class PlayVfxOnEnable : MonoBehaviour
{
	[SerializeField]
	private ParticleSystem targetParticles;

	private void OnEnable()
	{
		targetParticles.Play();
	}

	private void OnDisable()
	{
		targetParticles.Stop();
	}
}

}
