namespace SushiAndFish.Casino.Original
{
using global::MoreMountains.Feedbacks;
using global::UnityEngine;

public class PlinkoPillar : MonoBehaviour
{
	[SerializeField]
	private Plinko plinko;

	[SerializeField]
	private MMF_Player onHitFb;

	private void OnCollisionEnter(Collision other)
	{
		if (other.gameObject.layer == SushiAndFish.Casino.CasinoLayers.NameToLayer("PlinkoPuck"))
		{
			plinko.ServerPlayPillarFeedbacks(this);
		}
	}

	public void PlayFeedbacks()
	{
		onHitFb.PlayFeedbacks();
	}
}

}
