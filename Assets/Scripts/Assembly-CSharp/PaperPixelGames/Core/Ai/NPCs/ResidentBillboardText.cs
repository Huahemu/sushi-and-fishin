using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentBillboardText : MonoBehaviour
	{
		[Header("Billboard")]
		[SerializeField]
		private bool _autoFindMainCamera = true;

		[SerializeField]
		private bool _lockRoll = true;

		[Header("UI")]
		[SerializeField]
		private Canvas _canvas;

		[SerializeField]
		private TextMeshProUGUI _text;

		[SerializeField]
		private GameObject _root;

		[Header("Smart Align")]
		[SerializeField]
		private bool _smartAlign = true;

		[Tooltip("If preferred width is below this, center it. Otherwise left align.")]
		[SerializeField]
		private float _centerThreshold = 220f;

		[Tooltip("When left aligned, shift the text box slightly to the left (local canvas units).")]
		[SerializeField]
		private float _leftModeOffsetX = -35f;

		[Tooltip("When centered, keep it at this X offset (local canvas units).")]
		[SerializeField]
		private float _centerModeOffsetX;

		[Header("Text Constraints")]
		[SerializeField]
		private bool _enableWrapping = true;

		[Tooltip("Maximum visible line count. Set to 0 to allow all lines.")]
		[SerializeField]
		[Min(0f)]
		private int _maxLines;

		[Tooltip("Auto shrink font between min/max. Ellipsis applies only when a line limit is set.")]
		[SerializeField]
		private bool _enableAutoSize = true;

		[SerializeField]
		[Min(1f)]
		private float _fontSizeMax = 32f;

		[SerializeField]
		[Min(1f)]
		private float _fontSizeMin = 18f;

		[Header("Typewriter")]
		[SerializeField]
		[Min(0.005f)]
		private float _charDelay = 0.01f;

		private Transform _cameraTf;

		private Coroutine _typingRoutine;

		private string _fullText;

		private bool _isTyping;

		private Action _onTypingComplete;

		private RectTransform _textRect;

		public bool IsTyping => _isTyping;

		private void Awake()
		{
			CacheCameraIfNeeded();
			SetVisible(visible: false);
			if (_text != null)
			{
				_textRect = _text.rectTransform;
				ApplyTextConstraints();
				_text.text = string.Empty;
				_text.maxVisibleCharacters = int.MaxValue;
			}
		}

		private void LateUpdate()
		{
			CacheCameraIfNeeded();
			if (!(_cameraTf == null))
			{
				base.transform.rotation = Quaternion.LookRotation(_cameraTf.forward, _cameraTf.up);
				if (_lockRoll)
				{
					Vector3 eulerAngles = base.transform.rotation.eulerAngles;
					base.transform.rotation = Quaternion.Euler(eulerAngles.x, eulerAngles.y, 0f);
				}
			}
		}

		private void CacheCameraIfNeeded()
		{
			if (!(_cameraTf != null) && _autoFindMainCamera)
			{
				Camera main = Camera.main;
				if (main != null)
				{
					_cameraTf = main.transform;
				}
			}
		}

		public void PlayTypewriter(string message, Action onComplete = null)
		{
			if (_text == null)
			{
				onComplete?.Invoke();
				return;
			}
			if (message == null)
			{
				message = string.Empty;
			}
			StopTypingIfAny();
			_onTypingComplete = onComplete;
			_fullText = message;
			_isTyping = true;
			SetVisible(visible: true);
			ApplyTextConstraints();
			ApplySmartAlignment(_fullText);
			_text.text = _fullText;
			_text.maxVisibleCharacters = 0;
			_typingRoutine = StartCoroutine(TypeRoutine(_fullText.Length));
		}

		public void CompleteInstant()
		{
			StopTypingIfAny();
			if (_text != null)
			{
				_text.text = _fullText ?? string.Empty;
				_text.maxVisibleCharacters = int.MaxValue;
			}
			_isTyping = false;
			Action onTypingComplete = _onTypingComplete;
			_onTypingComplete = null;
			onTypingComplete?.Invoke();
		}

		public void Hide()
		{
			StopTypingIfAny();
			_isTyping = false;
			_fullText = null;
			if (_text != null)
			{
				_text.text = string.Empty;
				_text.maxVisibleCharacters = int.MaxValue;
			}
			SetVisible(visible: false);
		}

		private void ApplyTextConstraints()
		{
			if (!(_text == null))
			{
				_text.enableWordWrapping = _enableWrapping;
				bool flag = _maxLines > 0;
				_text.maxVisibleLines = (flag ? _maxLines : int.MaxValue);
				_text.overflowMode = (flag ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow);
				_text.enableAutoSizing = _enableAutoSize;
				if (_enableAutoSize)
				{
					_text.fontSizeMax = _fontSizeMax;
					_text.fontSizeMin = _fontSizeMin;
				}
			}
		}

		private void ApplySmartAlignment(string fullText)
		{
			if (_smartAlign && !(_text == null) && !(_textRect == null))
			{
				ApplyTextConstraints();
				_text.text = fullText ?? string.Empty;
				_text.maxVisibleCharacters = int.MaxValue;
				_text.ForceMeshUpdate();
				bool num = _text.preferredWidth <= _centerThreshold;
				Vector2 pivot = _textRect.pivot;
				Vector2 anchoredPosition = _textRect.anchoredPosition;
				if (num)
				{
					_text.alignment = TextAlignmentOptions.Center;
					_textRect.pivot = new Vector2(0.5f, pivot.y);
					_textRect.anchoredPosition = new Vector2(_centerModeOffsetX, anchoredPosition.y);
				}
				else
				{
					_text.alignment = TextAlignmentOptions.Left;
					_textRect.pivot = new Vector2(0f, pivot.y);
					_textRect.anchoredPosition = new Vector2(_leftModeOffsetX, anchoredPosition.y);
				}
			}
		}

		private IEnumerator TypeRoutine(int length)
		{
			for (int i = 0; i < length; i++)
			{
				_text.maxVisibleCharacters = i + 1;
				yield return new WaitForSeconds(_charDelay);
			}
			_text.maxVisibleCharacters = int.MaxValue;
			_typingRoutine = null;
			_isTyping = false;
			Action onTypingComplete = _onTypingComplete;
			_onTypingComplete = null;
			onTypingComplete?.Invoke();
		}

		private void StopTypingIfAny()
		{
			if (_typingRoutine != null)
			{
				StopCoroutine(_typingRoutine);
				_typingRoutine = null;
			}
		}

		private void SetVisible(bool visible)
		{
			if (_root != null)
			{
				_root.SetActive(visible);
			}
			if (_canvas != null)
			{
				_canvas.enabled = visible;
			}
		}
	}
}
