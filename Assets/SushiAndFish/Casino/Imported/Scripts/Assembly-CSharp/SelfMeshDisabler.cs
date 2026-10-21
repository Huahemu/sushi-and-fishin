namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using global::UnityEngine;

public class SelfMeshDisabler : MonoBehaviour
{
	private NetworkIdentity _networkIdentity;

	private void Start()
	{
		_networkIdentity = base.transform.root.GetComponent<NetworkIdentity>();
		if (IsLocalPlayer())
		{
			base.gameObject.layer = SushiAndFish.Casino.CasinoLayers.NameToLayer("SelfMeshPlayer");
		}
	}

	private bool IsLocalPlayer()
	{
		if (_networkIdentity != null)
		{
			return NetworkClient.localPlayer == _networkIdentity;
		}
		return false;
	}

	public void ToggleMesh(bool enabled)
	{
		if (IsLocalPlayer())
		{
			base.gameObject.layer = (enabled ? SushiAndFish.Casino.CasinoLayers.NameToLayer("Player") : SushiAndFish.Casino.CasinoLayers.NameToLayer("SelfMeshPlayer"));
		}
	}
}

}
