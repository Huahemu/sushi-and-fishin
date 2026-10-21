using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;

namespace PaperPixelGames.Core.PlayerTools.Harpoon
{
	public sealed class HarpoonChallengeSpamPromptFeedback : MonoBehaviour
	{
		[Header("Content")]
		[SerializeField]
		private RectTransform _root;

		[SerializeField]
		private List<TextMeshProUGUI> _labels = new List<TextMeshProUGUI>();

		[SerializeField]
		private string _promptText = "SPAM";

		[SerializeField]
		private Color _textColor = new Color(1f, 0.88f, 0.22f, 0.95f);

		[SerializeField]
		private Color _outlineColor = new Color(0f, 0.1f, 0.16f, 0.85f);

		[Header("Timing")]
		[SerializeField]
		[Min(0.03f)]
		private float _spawnInterval = 0.14f;

		[SerializeField]
		[Min(0.1f)]
		private float _labelDuration = 0.95f;

		[SerializeField]
		[Min(0f)]
		private float _largeIntroDuration = 2.4f;

		[Header("Motion")]
		[SerializeField]
		private Vector2 _radiusRange = new Vector2(95f, 245f);

		[SerializeField]
		private Vector2 _driftRange = new Vector2(16f, 34f);

		[SerializeField]
		private Vector2 _fontSizeRange = new Vector2(24f, 34f);

		[SerializeField]
		[Min(0.1f)]
		private float _largeStartScale = 1.35f;

		[SerializeField]
		[Min(0.1f)]
		private float _normalStartScale = 0.92f;

		[SerializeField]
		[Min(0.1f)]
		private float _endScale = 0.58f;

		[SerializeField]
		[Min(0f)]
		private float _punchScale = 0.12f;

		private readonly List<Sequence> _sequences = new List<Sequence>();

		private Coroutine _spawnRoutine;

		private int _nextLabelIndex;

		private float _startedAt;

		private bool _isPlaying;

		private string _activeLocalizationKey;

		private string _activePromptText;

		private Color _activeTextColor;

		private Color _activeOutlineColor;

		public void Play(RectTransform preferredRoot)
		{
			Play(preferredRoot, "gameplay.harpoon.spam", _promptText, _textColor, _outlineColor);
		}

		public void Play(RectTransform preferredRoot, string localizationKey, string fallbackText, Color textColor, Color outlineColor)
		{
			if (CanPlay())
			{
				_activeLocalizationKey = localizationKey;
				_activePromptText = fallbackText;
				_activeTextColor = textColor;
				_activeOutlineColor = outlineColor;
				PrepareLabels();
				StopTweens(instant: false);
				if (_spawnRoutine != null)
				{
					StopCoroutine(_spawnRoutine);
				}
				_root.gameObject.SetActive(value: true);
				_startedAt = Time.unscaledTime;
				_nextLabelIndex = 0;
				_isPlaying = true;
				_spawnRoutine = StartCoroutine(SpawnRoutine());
			}
		}

		public void Stop(bool instant = false)
		{
			_isPlaying = false;
			if (_spawnRoutine != null)
			{
				StopCoroutine(_spawnRoutine);
				_spawnRoutine = null;
			}
			StopTweens(instant);
		}

		private void OnDisable()
		{
			Stop(instant: true);
		}

		private void OnDestroy()
		{
			Stop(instant: true);
		}

		private IEnumerator SpawnRoutine()
		{
			WaitForSecondsRealtime wait = new WaitForSecondsRealtime(_spawnInterval);
			while (_isPlaying)
			{
				SpawnLabel();
				yield return wait;
			}
		}

		private void SpawnLabel()
		{
			if (_labels.Count == 0)
			{
				return;
			}
			int nextLabelIndex = _nextLabelIndex;
			_nextLabelIndex = (_nextLabelIndex + 1) % _labels.Count;
			TextMeshProUGUI label = _labels[nextLabelIndex];
			if (label == null)
			{
				return;
			}
			if (_sequences[nextLabelIndex] != null && _sequences[nextLabelIndex].IsActive())
			{
				_sequences[nextLabelIndex].Kill();
			}
			float t = ((_largeIntroDuration > 0f) ? Mathf.Clamp01((Time.unscaledTime - _startedAt) / _largeIntroDuration) : 1f);
			float num = Mathf.Lerp(_largeStartScale, _normalStartScale, t);
			float f = UnityEngine.Random.Range(0f, 360f) * (MathF.PI / 180f);
			float num2 = UnityEngine.Random.Range(Mathf.Min(_radiusRange.x, _radiusRange.y), Mathf.Max(_radiusRange.x, _radiusRange.y));
			Vector2 vector = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
			Vector2 vector2 = vector * num2;
			Vector2 vector3 = vector * UnityEngine.Random.Range(_driftRange.x, _driftRange.y);
			RectTransform rectTransform = label.rectTransform;
			rectTransform.anchoredPosition = vector2;
			rectTransform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-10f, 10f));
			rectTransform.localScale = Vector3.one * num;
			rectTransform.SetAsLastSibling();
			label.text = LocalizationDatabase.GetString("UiTable", _activeLocalizationKey, _activePromptText);
			label.fontSize = UnityEngine.Random.Range(_fontSizeRange.x, _fontSizeRange.y);
			label.color = _activeTextColor;
			label.alpha = 0f;
			label.gameObject.SetActive(value: true);
			float num3 = _labelDuration * 0.46f;
			float duration = Mathf.Max(0.05f, _labelDuration - num3);
			Sequence sequence = DOTween.Sequence();
			sequence.SetUpdate(isIndependentUpdate: true);
			sequence.Append(label.DOFade(_activeTextColor.a, 0.08f).SetEase(Ease.OutQuad));
			sequence.Join(rectTransform.DOScale(num + _punchScale, 0.12f).SetEase(Ease.OutBack));
			sequence.Append(rectTransform.DOScale(_endScale, _labelDuration - 0.12f).SetEase(Ease.InOutSine));
			sequence.Join(rectTransform.DOAnchorPos(vector2 + vector3, _labelDuration).SetEase(Ease.OutSine));
			sequence.Insert(num3, label.DOFade(0f, duration).SetEase(Ease.InQuad));
			sequence.OnComplete(() =>
			{
				if (label != null)
				{
					label.gameObject.SetActive(value: false);
				}
			});
			_sequences[nextLabelIndex] = sequence;
		}

		private bool CanPlay()
		{
			if (_root == null)
			{
				UtilityMethods.Log("[HarpoonChallengeSpamPromptFeedback] Root is not assigned.", LogType.Warning);
				return false;
			}
			if (_labels == null || _labels.Count == 0)
			{
				UtilityMethods.Log("[HarpoonChallengeSpamPromptFeedback] Labels are not assigned.", LogType.Warning);
				return false;
			}
			return true;
		}

		private void PrepareLabels()
		{
			while (_sequences.Count < _labels.Count)
			{
				_sequences.Add(null);
			}
			while (_sequences.Count > _labels.Count)
			{
				_sequences.RemoveAt(_sequences.Count - 1);
			}
			for (int i = 0; i < _labels.Count; i++)
			{
				TextMeshProUGUI textMeshProUGUI = _labels[i];
				if (!(textMeshProUGUI == null))
				{
					textMeshProUGUI.raycastTarget = false;
					textMeshProUGUI.overflowMode = TextOverflowModes.Overflow;
					textMeshProUGUI.color = _activeTextColor;
					textMeshProUGUI.outlineColor = _activeOutlineColor;
					textMeshProUGUI.gameObject.SetActive(value: false);
				}
			}
		}

		private void StopTweens(bool instant)
		{
			for (int i = 0; i < _sequences.Count; i++)
			{
				if (_sequences[i] != null && _sequences[i].IsActive())
				{
					_sequences[i].Kill(instant);
				}
				_sequences[i] = null;
			}
			foreach (TextMeshProUGUI label in _labels)
			{
				if (!(label == null))
				{
					label.DOKill();
					label.rectTransform.DOKill();
					label.gameObject.SetActive(value: false);
				}
			}
		}
	}
}
