namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using global::UnityEngine;

public class RemoveIfLocalPlayer : NetworkBehaviour
{
	public override void OnStartClient()
	{
		if (base.isLocalPlayer)
		{
			Object.Destroy(base.gameObject);
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
