using System.Collections.Generic;
using DG.Tweening;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace PaperPixelGames.Core.UI
{
	public class CatchInfoUI : MonoBehaviour
	{
		[SerializeField]
		private RectTransform _root;

		[SerializeField]
		private CanvasGroup _canvasGroup;

		[SerializeField]
		private GameObject _uiFishIconInfo;

		[SerializeField]
		private RectTransform _uiFishIcon;

		[SerializeField]
		private RectTransform _uiFishTarget;

		[SerializeField]
		private List<Sprite> _raritySprites;

		[SerializeField]
		private Image _fishImage;

		[SerializeField]
		private Image _rarityImage;

		[SerializeField]
		private TextMeshProUGUI _fishNameText;

		[SerializeField]
		private TextMeshProUGUI _rarityText;

		[SerializeField]
		private TextMeshProUGUI _zoneText;

		[SerializeField]
		private TextMeshProUGUI _weightText;

		[SerializeField]
		private TextMeshProUGUI _filletText;

		[FormerlySerializedAs("_recipesRoot")]
		[SerializeField]
		[Tooltip("Parent transform for fillet icons.")]
		private RectTransform _filletsRoot;

		[FormerlySerializedAs("_recipeIconPrefab")]
		[SerializeField]
		[Tooltip("Prefab with an Image component for fillet icons.")]
		private Image _filletIconPrefab;

		private readonly List<Image> _spawnedFilletIcons = new List<Image>();

		[SerializeField]
		[Min(0f)]
		private float _fadeDuration = 0.2f;

		[SerializeField]
		[Min(0f)]
		private float _scaleDuration = 0.25f;

		[SerializeField]
		private float _scalePunch = 0.1f;

		[SerializeField]
		private Ease _scaleEase = Ease.OutBack;

		[SerializeField]
		[Min(0f)]
		private float _slideDistance = 600f;

		private Vector2 _shownAnchoredPos;

		private Vector2 _hiddenAnchoredPos;

		private bool _positionsInitialized;

		private Sequence _animSequence;

		private Coroutine _hideCor;

		private void Awake()
		{
			_canvasGroup.DOFade(0f, 0f);
			InitializeSlidePositions();
		}

		private void InitializeSlidePositions()
		{
			if (!_positionsInitialized && !(_root == null) && !(_canvasGroup == null))
			{
				_shownAnchoredPos = _root.anchoredPosition;
				_hiddenAnchoredPos = _shownAnchoredPos + Vector2.right * _slideDistance;
				_root.anchoredPosition = _hiddenAnchoredPos;
				_canvasGroup.alpha = 0f;
				_positionsInitialized = true;
			}
		}

		public void UpdateCatchInfo(CreatureSO data, float weightKg)
		{
			if (data == null)
			{
				return;
			}
			FishSO fishSO = data as FishSO;
			SetRarityImage(data.Rarity);
			if (_fishImage != null)
			{
				_fishImage.sprite = data.Sprite;
			}
			if (_fishNameText != null)
			{
				_fishNameText.text = data.LocalizedName;
			}
			if (_filletText != null)
			{
				if (fishSO == null)
				{
					_filletText.text = string.Empty;
				}
				else
				{
					int num = Mathf.Max(fishSO.MinYieldRange, fishSO.MaxYieldRange - 1);
					_filletText.text = ((fishSO.MinYieldRange == num) ? $"{num} fillet" : $"{fishSO.MinYieldRange}-{num} fillets");
				}
			}
			if (_rarityText != null)
			{
				_rarityText.text = $"{data.Rarity}";
			}
			if (_zoneText != null)
			{
				_zoneText.text = $"{data.ZoneLayer}";
			}
			if (_weightText != null)
			{
				_weightText.text = $"{weightKg:F2} kg";
			}
			UpdateFilletIcons(fishSO);
			PlayShowAnimation();
		}

		private void SetRarityImage(CreatureRarity rarity)
		{
			if (_rarityImage == null || _raritySprites == null)
			{
				return;
			}
			switch (rarity)
			{
			case CreatureRarity.Common:
				if (_raritySprites.Count > 0)
				{
					_rarityImage.sprite = _raritySprites[0];
				}
				break;
			case CreatureRarity.Rare:
				if (_raritySprites.Count > 1)
				{
					_rarityImage.sprite = _raritySprites[1];
				}
				break;
			case CreatureRarity.Epic:
				if (_raritySprites.Count > 2)
				{
					_rarityImage.sprite = _raritySprites[2];
				}
				break;
			case CreatureRarity.Legendary:
				if (_raritySprites.Count > 3)
				{
					_rarityImage.sprite = _raritySprites[3];
				}
				break;
			case CreatureRarity.Mystic:
				if (_raritySprites.Count > 4)
				{
					_rarityImage.sprite = _raritySprites[4];
				}
				break;
			}
		}

		private void UpdateFilletIcons(FishSO data)
		{
			ClearFilletIcons();
			if (_filletsRoot == null || _filletIconPrefab == null)
			{
				return;
			}
			IReadOnlyList<ProcessedIngredientSO> readOnlyList = data?.IngredientSO?.ProcessedIngredients;
			if (readOnlyList == null || readOnlyList.Count == 0)
			{
				return;
			}
			for (int i = 0; i < readOnlyList.Count; i++)
			{
				Sprite sprite = readOnlyList[i]?.Sprite;
				if (!(sprite == null))
				{
					Image image = Object.Instantiate(_filletIconPrefab, _filletsRoot);
					image.sprite = sprite;
					_spawnedFilletIcons.Add(image);
				}
			}
		}

		private void ClearFilletIcons()
		{
			foreach (Image spawnedFilletIcon in _spawnedFilletIcons)
			{
				if (spawnedFilletIcon != null)
				{
					Object.Destroy(spawnedFilletIcon.gameObject);
				}
			}
			_spawnedFilletIcons.Clear();
		}

		private void PlayShowAnimation()
		{
			InitializeSlidePositions();
			if (_root == null || _canvasGroup == null)
			{
				return;
			}
			if (_animSequence != null && _animSequence.IsActive())
			{
				_animSequence.Kill();
			}
			_root.anchoredPosition = _hiddenAnchoredPos;
			_canvasGroup.alpha = 0f;
			_animSequence = DOTween.Sequence();
			if (_uiFishIcon != null && _uiFishTarget != null && _uiFishIconInfo != null)
			{
				Vector3 position = _uiFishIcon.position;
				_uiFishIcon.gameObject.SetActive(value: true);
				_uiFishIcon.position = _uiFishTarget.position;
				_uiFishIcon.localScale = Vector3.one * 1.5f;
				_uiFishIconInfo.SetActive(value: false);
				_animSequence.Join(_uiFishIcon.DOMove(position, 0.75f).SetEase(Ease.InCirc).OnComplete(() =>
				{
					_uiFishIcon.gameObject.SetActive(value: false);
					_uiFishIconInfo.SetActive(value: true);
				})).Join(_uiFishIcon.DOScale(1f, 0.75f));
			}
			_animSequence.AppendCallback(() =>
			{
				_canvasGroup.alpha = 0f;
				_root.anchoredPosition = _hiddenAnchoredPos;
			}).Append(DOTween.Sequence().Join(_canvasGroup.DOFade(1f, _fadeDuration)).Join(_root.DOAnchorPos(_shownAnchoredPos, _scaleDuration).SetEase(_scaleEase)));
			if (_hideCor != null)
			{
				Singleton<DelayHelper>.Instance.StopCoroutine(_hideCor);
			}
			_hideCor = Singleton<DelayHelper>.Instance.DelayAction(5f, PlayHideAnimation);
		}

		private void PlayHideAnimation()
		{
			InitializeSlidePositions();
			if (!(_root == null) && !(_canvasGroup == null))
			{
				if (_animSequence != null && _animSequence.IsActive())
				{
					_animSequence.Kill();
				}
				_animSequence = DOTween.Sequence();
				_animSequence.Join(_canvasGroup.DOFade(0f, _fadeDuration)).Join(_root.DOAnchorPos(_hiddenAnchoredPos, _scaleDuration).SetEase(Ease.InBack));
			}
		}
	}
}
