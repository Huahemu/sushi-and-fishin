namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;
using global::UnityEngine.UI;

public class DisableIfVersionMismatch : MonoBehaviour
{
	[Header("Settings")]
	[Tooltip("Alpha value to set when there is a version mismatch (0-1)")]
	[SerializeField]
	private float disabledAlpha = 0.5f;

	private Button button;

	private CanvasGroup canvasGroup;

	private bool hasVersionMismatch;

	private void Awake()
	{
		button = GetComponent<Button>();
		if (button == null)
		{
			Debug.LogWarning("[DisableIfVersionMismatch] No Button component found on " + base.gameObject.name);
		}
		canvasGroup = GetComponent<CanvasGroup>();
		if (canvasGroup == null)
		{
			Debug.LogWarning("[DisableIfVersionMismatch] No CanvasGroup component found on " + base.gameObject.name);
		}
	}

	private void OnEnable()
	{
		VersionMismatchManager.OnVersionMismatchChanged += OnVersionMismatchChanged;
		if (MonoSingleton<VersionMismatchManager>.Instance != null)
		{
			hasVersionMismatch = MonoSingleton<VersionMismatchManager>.Instance.HasVersionMismatch();
		}
		ApplyDisableState();
	}

	private void OnDisable()
	{
		VersionMismatchManager.OnVersionMismatchChanged -= OnVersionMismatchChanged;
	}

	private void OnVersionMismatchChanged(bool hasMismatch)
	{
		hasVersionMismatch = hasMismatch;
		ApplyDisableState();
	}

	private void ApplyDisableState()
	{
		if (hasVersionMismatch)
		{
			if (canvasGroup != null)
			{
				canvasGroup.alpha = disabledAlpha;
				canvasGroup.blocksRaycasts = false;
				canvasGroup.interactable = false;
			}
			if (button != null)
			{
				button.interactable = false;
			}
		}
		else
		{
			if (canvasGroup != null)
			{
				canvasGroup.alpha = 1f;
				canvasGroup.blocksRaycasts = true;
				canvasGroup.interactable = true;
			}
			if (button != null)
			{
				button.interactable = true;
			}
		}
	}
}

}
