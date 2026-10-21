using DG.Tweening;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.ChefDesk
{
	public class ChefDeskBook : MonoBehaviour
	{
		[SerializeField]
		private ChefDeskBookCanvas chefDeskBookCanvas;

		[Header("Hover")]
		[SerializeField]
		private Transform _hoverVisualTarget;

		private float _hoverScaleMultiplier = 1.04f;

		private float _hoverDuration = 0.18f;

		private Tween _hoverTween;

		private Vector3 _baseScale;

		private bool _hoverInitialized;

		private bool _isHovering;

		public void OpenBookUI()
		{
			chefDeskBookCanvas.ToggleBookUI(isVisible: true);
		}

		private void EnsureHoverInit()
		{
			if (!_hoverInitialized)
			{
				if (_hoverVisualTarget == null)
				{
					_hoverVisualTarget = base.transform;
				}
				_baseScale = _hoverVisualTarget.localScale;
				_hoverInitialized = true;
			}
		}

		public void ClientSetHoverVisual(bool isHovering)
		{
			EnsureHoverInit();
			if (_isHovering != isHovering)
			{
				_isHovering = isHovering;
				_hoverTween?.Kill();
				if (_isHovering)
				{
					_hoverTween = _hoverVisualTarget.DOScale(_baseScale * _hoverScaleMultiplier, _hoverDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
						.SetLink(base.gameObject);
				}
				else
				{
					_hoverTween = _hoverVisualTarget.DOScale(_baseScale, _hoverDuration).SetEase(Ease.OutSine).SetLink(base.gameObject);
				}
			}
		}

		private void OnDisable()
		{
			_hoverTween?.Kill();
			if (_hoverInitialized && _hoverVisualTarget != null)
			{
				_hoverVisualTarget.localScale = _baseScale;
			}
			_isHovering = false;
		}
	}
}
