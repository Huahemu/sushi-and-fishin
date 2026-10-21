namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::TMPro;
using global::UnityEngine;

public class KeyButtonManager : MonoBehaviour
{
	[Header("Key Button Settings")]
	[SerializeField]
	private GameObject keyButtonPrefab;

	[SerializeField]
	private GameObject textElementPrefab;

	[SerializeField]
	private Transform tooltipParent;

	private List<GameObject> activeKeyButtons = new List<GameObject>();

	private List<GameObject> activeTextElements = new List<GameObject>();

	private void Awake()
	{
		if (tooltipParent == null)
		{
			tooltipParent = base.transform;
		}
	}

	public GameObject CreateKeyButton(string keyText)
	{
		if (keyButtonPrefab == null)
		{
			Debug.LogError("KeyButtonManager: Key button prefab is not assigned!");
			return null;
		}
		GameObject gameObject = Object.Instantiate(keyButtonPrefab, tooltipParent);
		gameObject.name = "KeyButton_" + keyText;
		TextMeshProUGUI componentInChildren = gameObject.GetComponentInChildren<TextMeshProUGUI>();
		if (componentInChildren != null)
		{
			componentInChildren.text = keyText;
		}
		else
		{
			Debug.LogError("KeyButtonManager: No TextMeshProUGUI component found in key button prefab for key '" + keyText + "'");
		}
		gameObject.transform.SetAsLastSibling();
		activeKeyButtons.Add(gameObject);
		return gameObject;
	}

	public GameObject CreateTextElement(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		if (textElementPrefab == null)
		{
			Debug.LogError("KeyButtonManager: Text element prefab is not assigned!");
			return null;
		}
		GameObject gameObject = Object.Instantiate(textElementPrefab, tooltipParent);
		gameObject.name = "TextElement_" + text;
		TextMeshProUGUI component = gameObject.GetComponent<TextMeshProUGUI>();
		if (component != null)
		{
			component.text = text;
		}
		else
		{
			Debug.LogError("KeyButtonManager: No TextMeshProUGUI component found in text element prefab for text '" + text + "'");
		}
		gameObject.transform.SetAsLastSibling();
		activeTextElements.Add(gameObject);
		return gameObject;
	}

	public void ClearKeyButtons()
	{
		foreach (GameObject activeKeyButton in activeKeyButtons)
		{
			if (activeKeyButton != null)
			{
				Object.Destroy(activeKeyButton);
			}
		}
		activeKeyButtons.Clear();
		foreach (GameObject activeTextElement in activeTextElements)
		{
			if (activeTextElement != null)
			{
				Object.Destroy(activeTextElement);
			}
		}
		activeTextElements.Clear();
	}

	public void SetupLayout()
	{
	}

	public float GetTotalWidth()
	{
		float num = 0f;
		foreach (GameObject activeKeyButton in activeKeyButtons)
		{
			if (activeKeyButton != null)
			{
				RectTransform component = activeKeyButton.GetComponent<RectTransform>();
				if (component != null)
				{
					num += component.sizeDelta.x;
				}
			}
		}
		foreach (GameObject activeTextElement in activeTextElements)
		{
			if (activeTextElement != null)
			{
				TextMeshProUGUI componentInChildren = activeTextElement.GetComponentInChildren<TextMeshProUGUI>();
				if (componentInChildren != null)
				{
					num += componentInChildren.preferredWidth;
				}
			}
		}
		return num;
	}

	private void OnDestroy()
	{
		ClearKeyButtons();
	}
}

}
