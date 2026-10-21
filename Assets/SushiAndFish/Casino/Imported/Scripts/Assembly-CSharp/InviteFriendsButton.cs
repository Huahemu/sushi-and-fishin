namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class InviteFriendsButton : MonoBehaviour
{
	public void OnInviteFriendsClick()
	{
		if (MonoSingleton<LobbyManager>.Instance == null)
		{
			Debug.LogError("[InviteFriendsButton] LobbyManager.Instance is null!");
		}
		else
		{
			MonoSingleton<LobbyManager>.Instance.InviteFriend();
		}
	}
}

}
