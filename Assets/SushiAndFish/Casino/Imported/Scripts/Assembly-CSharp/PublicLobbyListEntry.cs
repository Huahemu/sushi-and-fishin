namespace SushiAndFish.Casino.Original
{
using global::SushiAndFish.Casino.Steamworks;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class PublicLobbyListEntry : MonoBehaviour
{
	[SerializeField]
	private TextMeshProUGUI label;

	[SerializeField]
	private Button button;

	private CSteamID _lobbyId;

	private PublicLobbyListUI _owner;

	public void Initialize(PublicLobbyListUI owner, CSteamID lobbyId, string labelText)
	{
		_owner = owner;
		_lobbyId = lobbyId;
		if (label != null)
		{
			label.text = labelText;
		}
		if (button != null)
		{
			button.onClick.AddListener(OnClick);
		}
	}

	public void OnClick()
	{
		Debug.Log($"[PublicLobbyListEntry] OnClick: lobby {_lobbyId.m_SteamID}");
		_owner?.JoinLobby(_lobbyId);
	}
}

}
