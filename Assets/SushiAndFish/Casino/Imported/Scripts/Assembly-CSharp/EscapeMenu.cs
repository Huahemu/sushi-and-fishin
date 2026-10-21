namespace SushiAndFish.Casino.Original
{
using global::System;
using global::Mirror;
using global::UnityEngine;

public class EscapeMenu : MonoBehaviour
{
	private CanvasGroup _canvasGroup;

	private bool _isOpen;

	[SerializeField]
	private EscapeMenuVoiceList voiceList;

	private void Awake()
	{
		_canvasGroup = GetComponentInChildren<CanvasGroup>();
		if (voiceList == null)
		{
			voiceList = GetComponentInChildren<EscapeMenuVoiceList>();
		}
		_canvasGroup.alpha = 0f;
		_canvasGroup.interactable = false;
		_canvasGroup.blocksRaycasts = false;
	}

	public void OnStuckClicked()
	{
		if (InputEvents.ActiveLayer == InputLayer.Default)
		{
			PlayerController playerController = NetworkClient.localPlayer?.GetComponent<PlayerController>();
			if (playerController != null)
			{
				playerController.LocalTeleport((GameObject.Find("StuckTeleport")?.transform)?.position ?? Vector3.zero);
				playerController.LocalRotate(Vector2.zero);
				ToggleEscapeMenu();
			}
		}
	}

	private void OnEnable()
	{
		InputEvents.OnEscapeMenuEvent = (Action)Delegate.Combine(InputEvents.OnEscapeMenuEvent, new Action(ToggleEscapeMenu));
	}

	private void OnDisable()
	{
		InputEvents.OnEscapeMenuEvent = (Action)Delegate.Remove(InputEvents.OnEscapeMenuEvent, new Action(ToggleEscapeMenu));
	}

	private void ToggleEscapeMenu()
	{
		if (_isOpen)
		{
			EscapeMenuSettingsPanel escapeMenuSettingsPanel = UnityEngine.Object.FindFirstObjectByType<EscapeMenuSettingsPanel>();
			if (escapeMenuSettingsPanel != null && escapeMenuSettingsPanel.gameObject.activeSelf)
			{
				return;
			}
			BugReportPanel bugReportPanel = UnityEngine.Object.FindFirstObjectByType<BugReportPanel>();
			if (bugReportPanel != null && bugReportPanel.gameObject.activeSelf)
			{
				return;
			}
		}
		_isOpen = !_isOpen;
		PlayerController component = NetworkClient.localPlayer.GetComponent<PlayerController>();
		component.IsLocked = _isOpen;
		component.head.isLocked = _isOpen;
		_canvasGroup.alpha = (_isOpen ? 1 : 0);
		_canvasGroup.interactable = _isOpen;
		_canvasGroup.blocksRaycasts = _isOpen;
		if (_isOpen)
		{
			UICursorSimple.Instance?.ShowCursor();
			voiceList?.RefreshList();
		}
		else
		{
			UICursorSimple.Instance?.HideCursor();
		}
	}
}

}
