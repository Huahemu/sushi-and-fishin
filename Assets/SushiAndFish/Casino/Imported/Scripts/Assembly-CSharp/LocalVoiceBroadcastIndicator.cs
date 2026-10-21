namespace SushiAndFish.Casino.Original
{
using global::Dissonance;
using global::UnityEngine;

public class LocalVoiceBroadcastIndicator : MonoBehaviour
{
	[SerializeField]
	private CanvasGroup canvasGroup;

	[SerializeField]
	private float fadeSpeed = 12f;

	private MonoBehaviour[] _triggerBehaviours;

	private void Awake()
	{
		if (canvasGroup == null)
		{
			canvasGroup = GetComponent<CanvasGroup>();
		}
	}

	private void Start()
	{
		RefreshTriggers();
	}

	private void OnEnable()
	{
		if (canvasGroup != null)
		{
			canvasGroup.alpha = 0f;
		}
	}

	private void Update()
	{
		if (canvasGroup == null)
		{
			return;
		}
		if (_triggerBehaviours == null || _triggerBehaviours.Length == 0)
		{
			RefreshTriggers();
		}
		bool flag = false;
		for (int i = 0; i < _triggerBehaviours.Length; i++)
		{
			if (_triggerBehaviours[i] is IVoiceBroadcastTrigger { IsTransmitting: not false })
			{
				flag = true;
				break;
			}
		}
		float b = (flag ? 1f : 0f);
		canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, b, fadeSpeed * Time.unscaledDeltaTime);
	}

	private void RefreshTriggers()
	{
		_triggerBehaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
	}
}

}
