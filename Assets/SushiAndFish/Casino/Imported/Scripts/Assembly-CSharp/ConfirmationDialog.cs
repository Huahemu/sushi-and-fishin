namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class ConfirmationDialog : MonoBehaviour
{
	[Header("UI References")]
	[SerializeField]
	private GameObject dialogPanel;

	[SerializeField]
	private TextMeshProUGUI questionText;

	[SerializeField]
	private Button confirmButton;

	[SerializeField]
	private Button cancelButton;

	[SerializeField]
	private TextMeshProUGUI confirmButtonText;

	[SerializeField]
	private TextMeshProUGUI cancelButtonText;

	[Header("Animation Settings")]
	[SerializeField]
	private float appearSpeed = 0.3f;

	[SerializeField]
	private float disappearSpeed = 0.3f;

	private CanvasGroup canvasGroup;

	private Action onConfirm;

	private Action onCancel;

	private void Awake()
	{
		if (dialogPanel != null)
		{
			canvasGroup = dialogPanel.GetComponent<CanvasGroup>();
			if (canvasGroup == null)
			{
				canvasGroup = dialogPanel.AddComponent<CanvasGroup>();
			}
		}
		if (dialogPanel != null)
		{
			dialogPanel.SetActive(value: false);
		}
	}

	private void Start()
	{
		if (confirmButton != null)
		{
			confirmButton.onClick.RemoveAllListeners();
			confirmButton.onClick.AddListener(OnConfirmClicked);
		}
		if (cancelButton != null)
		{
			cancelButton.onClick.RemoveAllListeners();
			cancelButton.onClick.AddListener(OnCancelClicked);
		}
	}

	public void Show(string question, Action onConfirm, Action onCancel = null, string confirmLabel = "Yes", string cancelLabel = "No")
	{
		this.onConfirm = onConfirm;
		this.onCancel = onCancel;
		if (questionText != null)
		{
			questionText.text = question;
		}
		if (confirmButtonText != null)
		{
			confirmButtonText.text = confirmLabel;
		}
		if (cancelButtonText != null)
		{
			cancelButtonText.text = cancelLabel;
		}
		if (dialogPanel != null)
		{
			dialogPanel.SetActive(value: true);
			StartCoroutine(AppearCoroutine());
		}
	}

	public void Hide()
	{
		if (dialogPanel != null && dialogPanel.activeSelf)
		{
			StartCoroutine(DisappearCoroutine());
		}
	}

	private void OnConfirmClicked()
	{
		onConfirm?.Invoke();
		Hide();
	}

	private void OnCancelClicked()
	{
		onCancel?.Invoke();
		Hide();
	}

	private IEnumerator AppearCoroutine()
	{
		if (!(canvasGroup == null))
		{
			canvasGroup.alpha = 0f;
			float elapsed = 0f;
			while (elapsed < appearSpeed)
			{
				elapsed += Time.deltaTime;
				canvasGroup.alpha = Mathf.Clamp01(elapsed / appearSpeed);
				yield return null;
			}
			canvasGroup.alpha = 1f;
		}
	}

	private IEnumerator DisappearCoroutine()
	{
		if (!(canvasGroup == null))
		{
			float elapsed = 0f;
			float startAlpha = canvasGroup.alpha;
			while (elapsed < disappearSpeed)
			{
				elapsed += Time.deltaTime;
				canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / disappearSpeed);
				yield return null;
			}
			canvasGroup.alpha = 0f;
			dialogPanel.SetActive(value: false);
		}
	}
}

}
