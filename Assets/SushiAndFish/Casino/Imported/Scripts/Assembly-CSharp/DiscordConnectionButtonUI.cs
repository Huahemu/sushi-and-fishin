namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.Extensions;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class DiscordConnectionButtonUI : MonoBehaviour
{
	[SerializeField]
	private UIColorPalette colorPalette;

	[Header("Optional UI")]
	[SerializeField]
	private Button button;

	[SerializeField]
	private TextMeshProUGUI statusText;

	[SerializeField]
	private Graphic statusGraphic;

	[SerializeField]
	private string connectedText = "Discord Connected";

	[SerializeField]
	private string disconnectedText = "Connect Discord";

	private void OnEnable()
	{
		DiscordRichPresenceManager instance = MonoSingleton<DiscordRichPresenceManager>.Instance;
		if (instance != null)
		{
			instance.ConnectionStateChanged += OnConnectionStateChanged;
		}
		RefreshConnectionState();
	}

	private void OnDisable()
	{
		DiscordRichPresenceManager instance = MonoSingleton<DiscordRichPresenceManager>.Instance;
		if (instance != null)
		{
			instance.ConnectionStateChanged -= OnConnectionStateChanged;
		}
	}

	public void OnApplicationFocus(bool hasFocus)
	{
		if (hasFocus)
		{
			RefreshConnectionState();
		}
	}

	public void StartDiscordIntegration()
	{
		DiscordRichPresenceManager instance = MonoSingleton<DiscordRichPresenceManager>.Instance;
		if (!(instance == null))
		{
			if (instance.IsConnected)
			{
				instance.DisconnectDiscord();
			}
			else
			{
				DiscordRichPresenceManager.SetUserAcceptedDiscordToaster();
				instance.ConnectDiscord();
			}
			RefreshConnectionState();
		}
	}

	public void RefreshConnectionState()
	{
		DiscordRichPresenceManager instance = MonoSingleton<DiscordRichPresenceManager>.Instance;
		bool isConnected = instance != null && instance.IsConnected;
		ApplyVisualState(isConnected);
	}

	public void SetConnectedVisual()
	{
		ApplyVisualState(isConnected: true);
	}

	public void SetDisconnectedVisual()
	{
		ApplyVisualState(isConnected: false);
	}

	private void ApplyVisualState(bool isConnected)
	{
		if ((bool)statusText)
		{
			statusText.text = (isConnected ? connectedText : disconnectedText);
		}
		if ((bool)statusGraphic)
		{
			statusGraphic.color = (isConnected ? colorPalette.profitGreen : colorPalette.white);
		}
		button.interactable = true;
		button.GetComponent<Image>().color = (isConnected ? colorPalette.profitGreen : colorPalette.white);
	}

	private void OnConnectionStateChanged(bool isConnected)
	{
		ApplyVisualState(isConnected);
	}
}

}
