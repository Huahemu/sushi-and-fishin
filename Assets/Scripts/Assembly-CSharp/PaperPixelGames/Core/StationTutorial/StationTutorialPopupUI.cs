using DG.Tweening;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Tutorials;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace PaperPixelGames.Core.StationTutorial
{
	public class StationTutorialPopupUI : MonoBehaviour
	{
		[SerializeField]
		private StationTutorialData _tutorialData;

		[Header("Root")]
		[SerializeField]
		private GameObject _root;

		[SerializeField]
		private CanvasGroup _canvasGroup;

		[Header("Content")]
		[SerializeField]
		private TextMeshProUGUI _titleText;

		[SerializeField]
		private TextMeshProUGUI _bodyText;

		[SerializeField]
		private RawImage _videoImage;

		[SerializeField]
		private VideoPlayer _videoPlayer;

		[Header("Buttons")]
		[SerializeField]
		private Button _closeButton;

		[SerializeField]
		private TextMeshProUGUI _closeButtonText;

		[Header("Animation")]
		[SerializeField]
		private float _openDuration = 0.18f;

		[SerializeField]
		private float _closeDuration = 0.12f;

		private CursorLockMode _previousCursorLockState;

		private bool _previousCursorVisible;

		private bool _restoreCursorOnClose;

		private Tween _tween;

		private PPG_Player _player;

		private bool _resumePlayerOnClose;

		public bool IsOpen
		{
			get
			{
				if (_root != null)
				{
					return _root.activeInHierarchy;
				}
				return false;
			}
		}

		private void Awake()
		{
			if (_closeButton != null)
			{
				_closeButton.onClick.AddListener(Close);
			}
			HideImmediate(resumePlayer: false);
		}

		private void OnDestroy()
		{
			if (_closeButton != null)
			{
				_closeButton.onClick.RemoveListener(Close);
			}
			ResumePlayerIfNeeded();
			RestoreCursorIfNeeded();
			_tween?.Kill();
		}

		public void Open()
		{
			OpenInternal(_tutorialData, null, pausePlayer: false);
		}

		public void Open(StationTutorialData data)
		{
			OpenInternal(data, null, pausePlayer: false);
		}

		public void OpenAfterRepair(StationTutorialData data, PPG_Player player)
		{
			OpenInternal(data, player, pausePlayer: true);
		}

		private void OpenInternal(StationTutorialData data, PPG_Player player, bool pausePlayer)
		{
			if (data == null)
			{
				UtilityMethods.Log("StationTutorialPopupUI tried to open with null tutorial data.");
				return;
			}
			base.gameObject.SetActive(value: true);
			ResumePlayerIfNeeded();
			_tutorialData = data;
			_player = player;
			_resumePlayerOnClose = pausePlayer && _player != null;
			if (_resumePlayerOnClose)
			{
				_player.PauseCharacter();
			}
			if (pausePlayer)
			{
				_previousCursorLockState = Cursor.lockState;
				_previousCursorVisible = Cursor.visible;
				_restoreCursorOnClose = true;
				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = true;
			}
			if (_titleText != null)
			{
				_titleText.text = LocalizationDatabase.GetString("UiTable", _tutorialData.TitleLocalizationKey, _tutorialData.Title);
			}
			if (_bodyText != null)
			{
				_bodyText.text = LocalizationDatabase.GetString("UiTable", _tutorialData.BodyLocalizationKey, _tutorialData.Body);
			}
			if (_closeButtonText != null)
			{
				_closeButtonText.text = LocalizationDatabase.GetString("UiTable", "ui.common.okay", "Okay");
			}
			SetupMedia();
			if (_root != null)
			{
				_root.SetActive(value: true);
			}
			_tween?.Kill();
			if (_canvasGroup != null)
			{
				_canvasGroup.alpha = 0f;
				_canvasGroup.interactable = true;
				_canvasGroup.blocksRaycasts = true;
			}
			Sequence sequence = DOTween.Sequence();
			if (_canvasGroup != null)
			{
				sequence.Join(_canvasGroup.DOFade(1f, _openDuration).SetEase(Ease.OutQuad));
			}
			_tween = sequence;
		}

		public void Close()
		{
			_tween?.Kill();
			if (_canvasGroup == null)
			{
				HideImmediate(resumePlayer: true);
				return;
			}
			_canvasGroup.interactable = false;
			_canvasGroup.blocksRaycasts = false;
			_tween = DOTween.Sequence().Join(_canvasGroup.DOFade(0f, _closeDuration).SetEase(Ease.OutQuad)).OnComplete(() =>
			{
				HideImmediate(resumePlayer: true);
			});
		}

		private void SetupMedia()
		{
			bool flag = _tutorialData != null && _tutorialData.VideoClip != null && _videoPlayer != null && _videoImage != null;
			if (_videoImage != null)
			{
				_videoImage.gameObject.SetActive(flag);
			}
			if (flag)
			{
				_videoPlayer.Stop();
				_videoPlayer.clip = _tutorialData.VideoClip;
				_videoPlayer.isLooping = true;
				_videoPlayer.Play();
			}
			else if (_videoPlayer != null)
			{
				_videoPlayer.Stop();
				_videoPlayer.clip = null;
			}
		}

		private void HideImmediate(bool resumePlayer)
		{
			_tween?.Kill();
			if (_videoPlayer != null)
			{
				_videoPlayer.Stop();
				_videoPlayer.clip = null;
			}
			if (_canvasGroup != null)
			{
				_canvasGroup.alpha = 0f;
				_canvasGroup.interactable = false;
				_canvasGroup.blocksRaycasts = false;
			}
			if (_root != null)
			{
				_root.SetActive(value: false);
			}
			if (resumePlayer)
			{
				ResumePlayerIfNeeded();
				RestoreCursorIfNeeded();
			}
		}

		private void RestoreCursorIfNeeded()
		{
			if (_restoreCursorOnClose)
			{
				Cursor.lockState = _previousCursorLockState;
				Cursor.visible = _previousCursorVisible;
				_restoreCursorOnClose = false;
			}
		}

		private void ResumePlayerIfNeeded()
		{
			if (_resumePlayerOnClose)
			{
				if (_player != null)
				{
					_player.ResumeCharacter();
				}
				_player = null;
				_resumePlayerOnClose = false;
			}
		}
	}
}
