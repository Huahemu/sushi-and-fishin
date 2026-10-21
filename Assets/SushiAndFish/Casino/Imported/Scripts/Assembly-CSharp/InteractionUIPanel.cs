namespace SushiAndFish.Casino.Original
{
using global::System;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;
using global::UnityEngine.UI;

public class InteractionUIPanel : MonoBehaviour
{
	[SerializeField]
	private TextMeshProUGUI itemNameText;

	[SerializeField]
	private Image progressBar;

	[SerializeField]
	private KeyButtonManager keyButtonManager;

	[SerializeField]
	private Image throwForceIndicator;

	[SerializeField]
	private GameObject throwForceIndicatorPanel;

	[SerializeField]
	private PlayerInventory playerInventory;

	private void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;
		InputEvents.OnThrowItemEvent = (Action<bool>)Delegate.Combine(InputEvents.OnThrowItemEvent, new Action<bool>(OnThrowItemEvent));
		if ((bool)playerInventory)
		{
			playerInventory.OnThrowChargeChanged += OnThrowChargeChanged;
		}
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
		InputEvents.OnThrowItemEvent = (Action<bool>)Delegate.Remove(InputEvents.OnThrowItemEvent, new Action<bool>(OnThrowItemEvent));
		if ((bool)playerInventory)
		{
			playerInventory.OnThrowChargeChanged -= OnThrowChargeChanged;
		}
	}

	private void Start()
	{
		ResetUI();
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		ResetUI();
	}

	public void SetPlayerInventory(PlayerInventory inventory)
	{
		if ((bool)playerInventory)
		{
			playerInventory.OnThrowChargeChanged -= OnThrowChargeChanged;
		}
		playerInventory = inventory;
		playerInventory.OnThrowChargeChanged += OnThrowChargeChanged;
	}

	public void SetItemNameText(string itemName)
	{
		itemNameText.SetText(itemName);
	}

	public void SetTooltip(string tooltip)
	{
		if (keyButtonManager != null)
		{
			keyButtonManager.ClearKeyButtons();
		}
		foreach (TooltipElement item in TooltipKeyParser.ParseTooltip(tooltip))
		{
			if (item.Type == TooltipElementType.Text)
			{
				keyButtonManager.CreateTextElement(item.Content);
			}
			else if (item.Type == TooltipElementType.Key)
			{
				keyButtonManager.CreateKeyButton(item.Content);
			}
		}
		keyButtonManager.SetupLayout();
	}

	public void UpdateProgressBar(float fillAmount)
	{
		progressBar.fillAmount = fillAmount;
	}

	public void ResetUI()
	{
		progressBar.fillAmount = 0f;
		itemNameText.SetText("");
		if (keyButtonManager != null)
		{
			keyButtonManager.ClearKeyButtons();
		}
		if (throwForceIndicator != null)
		{
			throwForceIndicator.fillAmount = 0f;
		}
		if (throwForceIndicatorPanel != null)
		{
			throwForceIndicatorPanel.SetActive(value: false);
		}
	}

	private void OnThrowItemEvent(bool isPressed)
	{
		if (!isPressed)
		{
			if (throwForceIndicatorPanel != null)
			{
				throwForceIndicatorPanel.SetActive(value: false);
			}
			if (throwForceIndicator != null)
			{
				throwForceIndicator.fillAmount = 0f;
			}
		}
	}

	private void OnThrowChargeChanged(float chargePercentage)
	{
		if (chargePercentage <= 0f)
		{
			if (throwForceIndicatorPanel != null)
			{
				throwForceIndicatorPanel.SetActive(value: false);
			}
			if (throwForceIndicator != null)
			{
				throwForceIndicator.fillAmount = 0f;
			}
			return;
		}
		bool active = chargePercentage > 0f;
		if (throwForceIndicatorPanel != null)
		{
			throwForceIndicatorPanel.SetActive(active);
		}
		if (throwForceIndicator != null)
		{
			throwForceIndicator.fillAmount = Mathf.Lerp(0.33f, 0.67f, chargePercentage);
		}
	}
}

}
