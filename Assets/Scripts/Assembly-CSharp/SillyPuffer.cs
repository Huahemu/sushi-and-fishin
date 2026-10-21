using System;
using DG.Tweening;
using UnityEngine;

public class SillyPuffer : MonoBehaviour
{
	[Header("Animated Target")]
	[SerializeField]
	private Transform _target;

	[Header("Graph")]
	[SerializeField]
	private GameObject _graph;

	[Header("Move Target")]
	[SerializeField]
	private Transform _moveTarget;

	[Header("Rotate Target")]
	[SerializeField]
	private Transform _rotateTarget;

	[Header("Look Settings")]
	[SerializeField]
	private Vector3 _lookRotationOffsetEuler;

	[SerializeField]
	private bool _keepLookingAtRotateTargetDuringLoop;

	[Header("Move Settings")]
	[SerializeField]
	private float _moveDuration = 0.35f;

	[SerializeField]
	private Ease _moveEase = Ease.OutSine;

	[Header("Rotate Settings")]
	[SerializeField]
	private float _rotateDuration = 0.2f;

	[SerializeField]
	private Ease _rotateEase = Ease.OutSine;

	[Header("Reset Move Settings")]
	[SerializeField]
	private float _resetMoveDuration = 0.35f;

	[SerializeField]
	private Ease _resetMoveEase = Ease.InOutSine;

	[Header("Reset Rotate Settings")]
	[SerializeField]
	private float _resetRotateDuration = 0.2f;

	[SerializeField]
	private Ease _resetRotateEase = Ease.InOutSine;

	[Header("Loop Settings")]
	[SerializeField]
	private float _loopRadius = 0.6f;

	[SerializeField]
	private float _loopDuration = 0.6f;

	[SerializeField]
	private bool _useXPlane;

	private Vector3 _startLocalPosition;

	private Quaternion _startLocalRotation;

	private Quaternion _cachedLookLocalRotation;

	private Sequence _playSequence;

	private Sequence _resetSequence;

	private bool _hasCachedStartTransform;

	private void Awake()
	{
		if (_target == null)
		{
			_target = base.transform;
		}
		CacheStartTransform();
		SetGraphActive(active: false);
	}

	public void PlayMovement()
	{
		if (CanPlayMovement())
		{
			KillAllSequences();
			if (!_hasCachedStartTransform)
			{
				CacheStartTransform();
			}
			ResetTargetToStart();
			SetGraphActive(active: true);
			Vector3 moveTargetLocalPosition = GetMoveTargetLocalPosition();
			Vector3 vector = moveTargetLocalPosition + GetLoopOffset(0f);
			CacheLookRotation(vector);
			_playSequence = DOTween.Sequence();
			_playSequence.Append(CreateMoveTween(vector)).Join(CreateRotateTween()).Append(CreateLoopTween(moveTargetLocalPosition))
				.OnKill(() =>
				{
					_playSequence = null;
				});
		}
	}

	public void StopMovement()
	{
		KillAllSequences();
		SetGraphActive(active: false);
	}

	public void ResetMovement()
	{
		if (HasValidTarget())
		{
			KillAllSequences();
			SetGraphActive(active: true);
			_resetSequence = DOTween.Sequence();
			_resetSequence.Join(CreateResetMoveTween()).Join(CreateResetRotateTween()).OnComplete(OnResetMovementCompleted)
				.OnKill(() =>
				{
					_resetSequence = null;
				});
		}
	}

	public void ResetPuffer()
	{
		if (HasValidTarget())
		{
			KillAllSequences();
			ResetTargetToStart();
			SetGraphActive(active: false);
		}
	}

	public void CacheCurrentAsStart()
	{
		if (HasValidTarget())
		{
			CacheStartTransform();
		}
	}

	private bool CanPlayMovement()
	{
		if (HasValidTarget())
		{
			return _moveTarget != null;
		}
		return false;
	}

	private bool HasValidTarget()
	{
		if (_target != null)
		{
			return true;
		}
		_target = base.transform;
		return _target != null;
	}

	private void CacheStartTransform()
	{
		if (!(_target == null))
		{
			_startLocalPosition = _target.localPosition;
			_startLocalRotation = _target.localRotation;
			_hasCachedStartTransform = true;
		}
	}

	private void ResetTargetToStart()
	{
		_target.localPosition = _startLocalPosition;
		_target.localRotation = _startLocalRotation;
	}

	private Tween CreateMoveTween(Vector3 targetLocalPosition)
	{
		return _target.DOLocalMove(targetLocalPosition, _moveDuration).SetEase(_moveEase);
	}

	private Tween CreateRotateTween()
	{
		return _target.DOLocalRotateQuaternion(_cachedLookLocalRotation, _rotateDuration).SetEase(_rotateEase);
	}

	private Tween CreateResetMoveTween()
	{
		return _target.DOLocalMove(_startLocalPosition, _resetMoveDuration).SetEase(_resetMoveEase);
	}

	private Tween CreateResetRotateTween()
	{
		return _target.DOLocalRotateQuaternion(_startLocalRotation, _resetRotateDuration).SetEase(_resetRotateEase);
	}

	private Tween CreateLoopTween(Vector3 centerLocalPosition)
	{
		float angle = 0f;
		return DOTween.To(() => angle, (float value) =>
		{
			angle = value;
			Vector3 vector = centerLocalPosition + GetLoopOffset(angle);
			_target.localPosition = vector;
			if (_keepLookingAtRotateTargetDuringLoop)
			{
				ApplyLookRotationInstant(vector);
			}
		}, MathF.PI * 2f, _loopDuration).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);
	}

	private Vector3 GetLoopOffset(float rad)
	{
		if (_useXPlane)
		{
			return new Vector3(Mathf.Sin(rad) * _loopRadius, Mathf.Cos(rad) * _loopRadius, 0f);
		}
		return new Vector3(0f, Mathf.Cos(rad) * _loopRadius, Mathf.Sin(rad) * _loopRadius);
	}

	private Vector3 GetMoveTargetLocalPosition()
	{
		if (_target.parent == null)
		{
			return _moveTarget.position;
		}
		return _target.parent.InverseTransformPoint(_moveTarget.position);
	}

	private Transform GetRotateTarget()
	{
		if (_rotateTarget != null)
		{
			return _rotateTarget;
		}
		return _moveTarget;
	}

	private void CacheLookRotation(Vector3 fromLocalPosition)
	{
		Transform rotateTarget = GetRotateTarget();
		if (rotateTarget == null)
		{
			_cachedLookLocalRotation = _target.localRotation;
		}
		else if (_target.parent != null)
		{
			Vector3 vector = _target.parent.InverseTransformPoint(rotateTarget.position) - fromLocalPosition;
			if (vector.sqrMagnitude <= 0.0001f)
			{
				_cachedLookLocalRotation = _target.localRotation;
				return;
			}
			Quaternion cachedLookLocalRotation = Quaternion.LookRotation(vector.normalized, Vector3.up);
			cachedLookLocalRotation *= Quaternion.Euler(_lookRotationOffsetEuler);
			_cachedLookLocalRotation = cachedLookLocalRotation;
		}
		else
		{
			Vector3 vector2 = rotateTarget.position - fromLocalPosition;
			if (vector2.sqrMagnitude <= 0.0001f)
			{
				_cachedLookLocalRotation = _target.localRotation;
				return;
			}
			Quaternion cachedLookLocalRotation2 = Quaternion.LookRotation(vector2.normalized, Vector3.up);
			cachedLookLocalRotation2 *= Quaternion.Euler(_lookRotationOffsetEuler);
			_cachedLookLocalRotation = cachedLookLocalRotation2;
		}
	}

	private void ApplyLookRotationInstant(Vector3 fromLocalPosition)
	{
		CacheLookRotation(fromLocalPosition);
		_target.localRotation = _cachedLookLocalRotation;
	}

	private Vector3 GetWorldPositionFromLocalPosition(Vector3 localPosition)
	{
		if (_target.parent == null)
		{
			return localPosition;
		}
		return _target.parent.TransformPoint(localPosition);
	}

	private Quaternion ConvertWorldRotationToLocalRotation(Quaternion worldRotation)
	{
		if (_target.parent == null)
		{
			return worldRotation;
		}
		return Quaternion.Inverse(_target.parent.rotation) * worldRotation;
	}

	private void OnResetMovementCompleted()
	{
		ResetTargetToStart();
		SetGraphActive(active: false);
	}

	private void KillAllSequences()
	{
		KillPlaySequence();
		KillResetSequence();
	}

	private void KillPlaySequence()
	{
		if (_playSequence != null)
		{
			_playSequence.Kill();
			_playSequence = null;
		}
	}

	private void KillResetSequence()
	{
		if (_resetSequence != null)
		{
			_resetSequence.Kill();
			_resetSequence = null;
		}
	}

	private void SetGraphActive(bool active)
	{
		if (!(_graph == null) && _graph.activeSelf != active)
		{
			_graph.SetActive(active);
		}
	}

	private void OnDisable()
	{
		KillAllSequences();
		SetGraphActive(active: false);
	}
}
