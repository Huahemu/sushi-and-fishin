namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;
using global::UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class BackButtonEscapeHandler : MonoBehaviour
{
	private Button _button;

	private void Awake()
	{
		_button = GetComponent<Button>();
	}

	private void OnEnable()
	{
		InputEvents.OnEscapeMenuEvent = (Action)Delegate.Combine(InputEvents.OnEscapeMenuEvent, new Action(OnEscape));
	}

	private void OnDisable()
	{
		InputEvents.OnEscapeMenuEvent = (Action)Delegate.Remove(InputEvents.OnEscapeMenuEvent, new Action(OnEscape));
	}

	private void OnEscape()
	{
		if (base.gameObject.activeInHierarchy)
		{
			Debug.Log("OnEscape: " + base.gameObject.name + " - " + _button.onClick.ToString(), base.gameObject);
			_button.onClick.Invoke();
		}
	}
}

}
