namespace SushiAndFish.Casino.Original
{
using global::FMODUnity;
using global::MoreMountains.Tools;
using global::UnityEngine;
using global::UnityEngine.UI;

public class SFXGenericUIButtonComponent : MonoBehaviour
{
	[SerializeField]
	private EventReference onClickEventReference;

	[SerializeField]
	private EventReference onHoverEventReference;

	[SerializeField]
	private bool playClick = true;

	[SerializeField]
	private bool playHover = true;

	private Button _button;

	private MMOnPointer _mmOnPointer;

	private void Awake()
	{
		if (playClick)
		{
			_button = GetComponent<Button>();
		}
		if (playHover)
		{
			_mmOnPointer = GetComponent<MMOnPointer>();
		}
	}

	private void OnEnable()
	{
		if (_button != null)
		{
			_button.onClick.AddListener(PlayClick);
		}
		if (_mmOnPointer != null)
		{
			_mmOnPointer.PointerEnter?.AddListener(PlayHover);
		}
	}

	private void OnDisable()
	{
		if (_button != null)
		{
			_button.onClick.RemoveListener(PlayClick);
		}
		if (_mmOnPointer != null)
		{
			_mmOnPointer.PointerEnter?.RemoveListener(PlayHover);
		}
	}

	public void PlayClick()
	{
		if (!onClickEventReference.IsNull)
		{
			SFXManager.SFXOneShot(onClickEventReference, base.gameObject.transform.position);
		}
	}

	public void PlayHover()
	{
		if (!onHoverEventReference.IsNull)
		{
			SFXManager.SFXOneShot(onHoverEventReference, base.gameObject.transform.position);
		}
	}
}

}
