using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Interaction.ToolTip
{
	public sealed class ToolTipUI : MonoBehaviour
	{
		[SerializeField]
		private CanvasGroup _group;

		[SerializeField]
		private RectTransform _root;

		[SerializeField]
		private TextMeshProUGUI _titleText;

		[SerializeField]
		private TextMeshProUGUI _bodyText;

		[SerializeField]
		private BillboardBehaviour _billboard;

		public bool IsHeaderOnlyPrefab;

		private Tween _fadeTween;

		private WorldInteractionOverlay _overlay;

		private float _fadeAlpha;

		private bool _projectedVisible;

		private Vector3 _preferredScale = Vector3.one;

		private float _backgroundAspect = 1.5f;

		public RectTransform Panel => _root;

		public bool IsVisible
		{
			get
			{
				if (base.isActiveAndEnabled && _projectedVisible && _group != null)
				{
					return _group.alpha > 0.01f;
				}
				return false;
			}
		}

		public void Initialize()
		{
			_overlay = WorldInteractionOverlay.ForScene(base.gameObject.scene);
			base.transform.SetParent(_overlay.Tooltips, worldPositionStays: false);
			if (_root == null)
			{
				_root = (RectTransform)base.transform;
			}
			RectTransform root = _root;
			RectTransform root2 = _root;
			Vector2 vector = (_root.pivot = new Vector2(0.5f, 0.5f));
			Vector2 anchorMin = (root2.anchorMax = vector);
			root.anchorMin = anchorMin;
			_root.localRotation = Quaternion.identity;
			_root.localScale = Vector3.one;
			if (_billboard != null)
			{
				_billboard.enabled = false;
			}
			Canvas[] componentsInChildren = GetComponentsInChildren<Canvas>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].overrideSorting = false;
			}
			GraphicRaycaster[] componentsInChildren2 = GetComponentsInChildren<GraphicRaycaster>(includeInactive: true);
			for (int i = 0; i < componentsInChildren2.Length; i++)
			{
				componentsInChildren2[i].enabled = false;
			}
			Graphic[] componentsInChildren3 = GetComponentsInChildren<Graphic>(includeInactive: true);
			for (int i = 0; i < componentsInChildren3.Length; i++)
			{
				componentsInChildren3[i].raycastTarget = false;
			}
			Image[] componentsInChildren4 = GetComponentsInChildren<Image>(includeInactive: true);
			foreach (Image image in componentsInChildren4)
			{
				if (!(image.transform.parent != _root))
				{
					if (image.sprite != null)
					{
						_backgroundAspect = image.sprite.rect.width / image.sprite.rect.height;
					}
					image.preserveAspect = true;
					RectTransform rectTransform = image.rectTransform;
					rectTransform.anchorMin = Vector2.zero;
					rectTransform.anchorMax = Vector2.one;
					anchorMin = (rectTransform.offsetMax = Vector2.zero);
					rectTransform.offsetMin = anchorMin;
				}
			}
			_overlay.Register(this);
			EnsureCanvasGroup();
			HideInstant();
		}

		public void SetData(in TooltipData data)
		{
			if (_titleText != null)
			{
				_titleText.text = data.Title ?? string.Empty;
			}
			bool hasBody = !IsHeaderOnlyPrefab && !data.OnlyHeader && _bodyText != null && !string.IsNullOrWhiteSpace(data.Body);
			if (_bodyText != null)
			{
				_bodyText.text = (hasBody ? data.Body : string.Empty);
				_bodyText.gameObject.SetActive(hasBody);
			}
			float num = (data.SetSize ? Mathf.Clamp(data.Size.x / 0.01f, 0.5f, 2f) : 1f);
			_preferredScale = new Vector3(num, num, 1f);
			ConfigureText(_titleText, 28f, hasBody ? 36 : 40);
			ConfigureText(_bodyText, 22f, 28f);
			float num2 = ((!hasBody && _titleText != null) ? Mathf.Clamp(_titleText.GetPreferredValues(float.PositiveInfinity, float.PositiveInfinity).x + 32f, 200f, 320f) : 320f);
			float num3 = Mathf.Max(num2, ContentHeight(num2) / 0.84f * _backgroundAspect);
			float num4 = num2;
			for (int i = 0; i < 10; i++)
			{
				float num5 = (num4 + num3) * 0.5f;
				if (ContentHeight(num5) <= num5 / _backgroundAspect * 0.84f)
				{
					num3 = num5;
				}
				else
				{
					num4 = num5;
				}
			}
			num2 = num3;
			float num6 = num2 / _backgroundAspect;
			_root.sizeDelta = new Vector2(num2, num6);
			float width = num2 - 32f;
			float num7 = TextHeight(_titleText, width);
			float num8 = (hasBody ? TextHeight(_bodyText, width) : 0f);
			float num9 = ((num7 > 0f && num8 > 0f) ? 8f : 0f);
			float num10 = num6 * 0.84f - 32f - num9;
			if (_titleText != null)
			{
				_titleText.enableAutoSizing = true;
			}
			if (_bodyText != null)
			{
				_bodyText.enableAutoSizing = true;
			}
			float num11 = Mathf.Max(0f, TextHeight(_titleText, width) - num7);
			float num12 = (hasBody ? Mathf.Max(0f, TextHeight(_bodyText, width) - num8) : 0f);
			float num13 = num11 + num12;
			float num14 = ((num13 > 0f) ? Mathf.Clamp01((num10 - num7 - num8) / num13) : 0f);
			num7 += num11 * num14;
			num8 += num12 * num14;
			float num15 = 16f + Mathf.Max(0f, num10 - num7 - num8) * 0.5f;
			LayoutText(_titleText, width, num7, 16f, num15);
			if (hasBody)
			{
				LayoutText(_bodyText, width, num8, 16f, num15 + num7 + num9);
			}
			float ContentHeight(float panelWidth)
			{
				float num16 = TextHeight(_titleText, panelWidth - 32f);
				float num17 = (hasBody ? TextHeight(_bodyText, panelWidth - 32f) : 0f);
				return 32f + num16 + num17 + ((num16 > 0f && num17 > 0f) ? 8f : 0f);
			}
		}

		private static void ConfigureText(TextMeshProUGUI text, float minFontSize, float maxFontSize)
		{
			if (!(text == null))
			{
				text.enableAutoSizing = false;
				text.fontSize = minFontSize;
				text.fontSizeMin = minFontSize;
				text.fontSizeMax = maxFontSize;
				text.textWrappingMode = TextWrappingModes.Normal;
				text.overflowMode = TextOverflowModes.Overflow;
				text.margin = Vector4.zero;
				text.alignment = TextAlignmentOptions.Center;
			}
		}

		private static float TextHeight(TextMeshProUGUI text, float width)
		{
			if (!(text == null) && !string.IsNullOrEmpty(text.text))
			{
				return Mathf.Ceil(text.GetPreferredValues(text.text, width, float.PositiveInfinity).y);
			}
			return 0f;
		}

		private static void LayoutText(TextMeshProUGUI text, float width, float height, float left, float top)
		{
			if (!(text == null))
			{
				RectTransform rectTransform = text.rectTransform;
				Vector2 vector = (rectTransform.pivot = new Vector2(0f, 1f));
				Vector2 anchorMin = (rectTransform.anchorMax = vector);
				rectTransform.anchorMin = anchorMin;
				rectTransform.anchoredPosition = new Vector2(left, 0f - top);
				rectTransform.sizeDelta = new Vector2(width, height);
			}
		}

		public void SetWorldPosition(Vector3 worldPos)
		{
			Camera main = Camera.main;
			Vector2 point = default;
			_projectedVisible = _overlay != null && _overlay.TryProject(main, worldPos, out point);
			if (_projectedVisible)
			{
				_overlay.PlaceTooltip(_root, point, main, _preferredScale);
			}
			SetAlpha(_fadeAlpha);
		}

		public void PrepareHidden()
		{
			EnsureCanvasGroup();
			KillTweens();
			SetActive(active: true);
			SetAlpha(0f);
		}

		public void ShowInstant(float alpha = 1f)
		{
			EnsureCanvasGroup();
			KillTweens();
			SetActive(active: true);
			SetAlpha(alpha);
		}

		public void HideInstant()
		{
			EnsureCanvasGroup();
			KillTweens();
			SetAlpha(0f);
			SetActive(active: false);
		}

		public Tween FadeIn(float duration)
		{
			EnsureCanvasGroup();
			KillTweens();
			SetActive(active: true);
			SetAlpha(0f);
			_fadeTween = DOTween.To(() => _fadeAlpha, SetAlpha, 1f, duration).SetEase(Ease.OutQuad).SetLink(base.gameObject, LinkBehaviour.KillOnDestroy);
			return _fadeTween;
		}

		public Tween FadeOut(float duration, Action onComplete = null)
		{
			EnsureCanvasGroup();
			KillTweens();
			_fadeTween = DOTween.To(() => _fadeAlpha, SetAlpha, 0f, duration).SetEase(Ease.InQuad).SetLink(base.gameObject, LinkBehaviour.KillOnDestroy)
				.OnComplete(() =>
				{
					SetActive(active: false);
					onComplete?.Invoke();
				});
			return _fadeTween;
		}

		public void KillTweens()
		{
			if (_fadeTween != null && _fadeTween.IsActive())
			{
				_fadeTween.Kill();
			}
			_fadeTween = null;
		}

		private void SetAlpha(float a)
		{
			_fadeAlpha = a;
			if (_group != null)
			{
				_group.alpha = (_projectedVisible ? a : 0f);
				_group.blocksRaycasts = false;
				_group.interactable = false;
			}
		}

		private void SetActive(bool active)
		{
			if (_root != null)
			{
				_root.gameObject.SetActive(active);
			}
			else
			{
				base.gameObject.SetActive(active);
			}
			if (_billboard != null)
			{
				_billboard.enabled = false;
			}
		}

		private void OnDestroy()
		{
			KillTweens();
			if (_overlay != null)
			{
				_overlay.Unregister(this);
			}
		}

		private void EnsureCanvasGroup()
		{
			if (_group == null && !TryGetComponent<CanvasGroup>(out _group))
			{
				_group = base.gameObject.AddComponent<CanvasGroup>();
			}
		}
	}
}
