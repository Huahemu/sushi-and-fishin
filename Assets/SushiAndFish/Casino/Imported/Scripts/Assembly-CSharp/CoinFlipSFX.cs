namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class CoinFlipSFX : MonoBehaviour
{
	[SerializeField]
	private Rigidbody coin;

	[SerializeField]
	private SFXLoopComponent loopComponent;

	[SerializeField]
	private SFXLocalLoopComponent localLoopComponent;

	private bool useLocalLoopComponent;

	private bool noLoopComponents;

	private void Start()
	{
		if (loopComponent == null && localLoopComponent != null)
		{
			useLocalLoopComponent = true;
		}
		else
		{
			noLoopComponents = true;
		}
	}

	private void Update()
	{
		if (coin.IsSleeping() || noLoopComponents)
		{
			return;
		}
		if (!useLocalLoopComponent)
		{
			if (loopComponent.loopInstance.isValid())
			{
				float value = coin.angularVelocity.magnitude * 2f;
				loopComponent.loopInstance.setParameterByName("AngularVelocity", value);
			}
		}
		else if (localLoopComponent.loopInstance.isValid())
		{
			float value2 = coin.angularVelocity.magnitude * 2f;
			localLoopComponent.loopInstance.setParameterByName("AngularVelocity", value2);
		}
	}
}

}
