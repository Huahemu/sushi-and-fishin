namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using global::UnityEngine;

public class PlayerExpressionComponent : NetworkBehaviour
{
	[SerializeField]
	private NetworkAnimator networkAnimator;

	public override void OnStartClient()
	{
		if (!base.isLocalPlayer)
		{
			base.enabled = false;
		}
		else
		{
			networkAnimator = GetComponent<NetworkAnimator>();
		}
	}

	public void SetHandAnimationTrigger(string triggerName)
	{
		if (CasinoLab.CasinoLabMode.Active) return;
		networkAnimator.SetTrigger(triggerName);
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
