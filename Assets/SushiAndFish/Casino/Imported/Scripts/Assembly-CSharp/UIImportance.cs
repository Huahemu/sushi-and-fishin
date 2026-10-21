namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;
using global::UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class UIImportance : MonoBehaviour
{
	[Header("Importance Settings")]
	[Tooltip("Higher value = more important. The most important active button will be auto-selected.")]
	[SerializeField]
	private int importance;

	[Header("Visibility Check")]
	[Tooltip("Optional: Directly assign CanvasGroup to check. If not assigned, will search parent hierarchy.")]
	[SerializeField]
	private CanvasGroup canvasGroup;

	private Selectable selectable;

	public int Importance => importance;

	public Selectable Selectable => selectable;

	private void Awake()
	{
		selectable = GetComponent<Selectable>();
	}

	private void OnEnable()
	{
		if (MonoSingleton<InputModeManager>.Instance != null)
		{
			MonoSingleton<InputModeManager>.Instance.OnUIImportanceEnabled(this);
		}
	}

	private void OnDisable()
	{
		if (MonoSingleton<InputModeManager>.Instance != null)
		{
			MonoSingleton<InputModeManager>.Instance.OnUIImportanceDisabled(this);
		}
	}

	public bool IsVisibleAndEnabled()
	{
		if (selectable == null)
		{
			return false;
		}
		if (!selectable.IsInteractable())
		{
			return false;
		}
		if (!base.gameObject.activeInHierarchy)
		{
			return false;
		}
		if (canvasGroup != null)
		{
			if (!canvasGroup.interactable || canvasGroup.alpha <= 0f)
			{
				return false;
			}
		}
		else
		{
			Transform parent = base.transform;
			while (parent != null)
			{
				CanvasGroup component = parent.GetComponent<CanvasGroup>();
				if (component != null && (!component.interactable || component.alpha <= 0f))
				{
					return false;
				}
				parent = parent.parent;
			}
		}
		return true;
	}
}

}
