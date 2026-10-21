using DG.Tweening;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.ChefDesk
{
	public class MouseIconTweenMover : MonoBehaviour
	{
		public enum MoveDirection
		{
			BottomToTop = 0,
			LeftToRight = 1,
			TopToBottom = 2
		}

		[Header("Movement Settings")]
		[SerializeField]
		private MoveDirection moveDirection;

		[SerializeField]
		private float moveDistance = 400f;

		[SerializeField]
		private float duration = 1f;

		[SerializeField]
		private Ease ease = Ease.InOutSine;

		private RectTransform _rectTransform;

		private Vector2 _startAnchoredPosition;

		private Tween _moveTween;

		private void Awake()
		{
			_rectTransform = base.transform as RectTransform;
		}

		private void OnEnable()
		{
			CacheStartPosition();
			StartMovement();
		}

		private void OnDisable()
		{
			StopMovement();
		}

		private void CacheStartPosition()
		{
			_startAnchoredPosition = _rectTransform.anchoredPosition;
		}

		public void StartMovement()
		{
			_moveTween?.Kill();
			_moveTween = null;
			Vector2 vector = moveDirection switch
			{
				MoveDirection.BottomToTop => Vector2.up * moveDistance, 
				MoveDirection.TopToBottom => Vector2.down * moveDistance, 
				MoveDirection.LeftToRight => Vector2.right * moveDistance, 
				_ => Vector2.zero, 
			};
			_rectTransform.anchoredPosition = _startAnchoredPosition;
			_moveTween = _rectTransform.DOAnchorPos(_startAnchoredPosition + vector, duration).SetEase(ease).SetLoops(-1, LoopType.Restart);
		}

		public void StopMovement()
		{
			_moveTween?.Kill();
			_moveTween = null;
			_rectTransform.anchoredPosition = _startAnchoredPosition;
		}

		public void SetDirection(MoveDirection direction)
		{
			moveDirection = direction;
			if (base.isActiveAndEnabled)
			{
				StartMovement();
			}
		}
	}
}
