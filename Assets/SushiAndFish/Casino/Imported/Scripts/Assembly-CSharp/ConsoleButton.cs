namespace SushiAndFish.Casino.Original
{
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.Events;
using global::UnityEngine.UI;

public class ConsoleButton : MonoBehaviour
{
	[Header("UI Components")]
	[SerializeField]
	private Button button;

	[SerializeField]
	private TextMeshProUGUI buttonText;

	[SerializeField]
	private Image buttonImage;

	private void Awake()
	{
		if (button == null)
		{
			button = GetComponent<Button>();
		}
		if (buttonText == null)
		{
			buttonText = GetComponentInChildren<TextMeshProUGUI>();
		}
		if (buttonImage == null)
		{
			buttonImage = GetComponent<Image>();
		}
	}

	public void SetText(string text)
	{
		if (buttonText != null)
		{
			buttonText.text = text;
		}
	}

	public void SetOnClick(UnityAction action)
	{
		if (button != null)
		{
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(action);
		}
	}

	public void SetInteractable(bool interactable)
	{
		if (button != null)
		{
			button.interactable = interactable;
		}
	}
}

}
