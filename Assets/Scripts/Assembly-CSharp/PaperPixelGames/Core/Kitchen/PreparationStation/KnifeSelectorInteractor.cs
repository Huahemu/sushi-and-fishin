using DG.Tweening;
using EPOOutline;
using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.PreparationStation
{
	public class KnifeSelectorInteractor : PrepStationUIHandlerBase
	{
		[Header("Knife Data")]
		[SerializeField]
		private int _knifeID;

		[SerializeField]
		private ProcessedType _processedType;

		[Header("References")]
		[SerializeField]
		private GameObject _cutInfoGO;

		[SerializeField]
		private Outlinable _outlinable;

		[Header("Availability")]
		[SerializeField]
		private bool _disableCollidersWhenUnavailable = true;

		[Header("Unavailable Visual Feedback")]
		[SerializeField]
		private Renderer[] _availabilityRenderers;

		[SerializeField]
		private Color _availableColor = Color.white;

		[SerializeField]
		private Color _unavailableColor = new Color(0.35f, 0.35f, 0.35f, 0.65f);

		[SerializeField]
		private string _colorPropertyName = "_BaseColor";

		[Header("Hover Animation")]
		[SerializeField]
		private float _hoverRotateAmountZ = 10f;

		[SerializeField]
		private float _hoverRotateDuration = 0.1f;

		[SerializeField]
		private int _hoverLoopCount = 4;

		private Vector3 _baseEuler;

		private Collider[] _colliders;

		private MaterialPropertyBlock _propertyBlock;

		private bool _isAvailable;

		private bool _availabilityStateApplied;

		private bool _isHovering;

		private bool _isQuestHighlightActive;

		protected override bool RequiresValidStateForClick => true;

		protected override bool RequiresValidStateForHover => true;

		private void Awake()
		{
			CacheReferences();
			ResetHoverVisuals(instant: true);
			RefreshAvailability(force: true);
		}

		private void OnEnable()
		{
			RefreshAvailability(force: true);
			RefreshQuestHighlight(force: true);
		}

		private void LateUpdate()
		{
			RefreshAvailability();
			RefreshQuestHighlight(force: false);
		}

		private void OnDisable()
		{
			_isHovering = false;
			SetQuestHighlightActive(active: false);
			ResetHoverVisuals(instant: true);
		}

		public void RefreshAvailability(bool force = false)
		{
			if (!(_prepStation != null) || !_prepStation.HasItemOnPrepTable())
			{
				ClearAvailabilityState(force);
				return;
			}
			bool flag = EvaluateAvailability();
			if (force || !_availabilityStateApplied || _isAvailable != flag)
			{
				_isAvailable = flag;
				_availabilityStateApplied = true;
				ApplyAvailabilityState(flag);
			}
		}

		private void ClearAvailabilityState(bool force)
		{
			if (force || _availabilityStateApplied)
			{
				_isAvailable = true;
				_availabilityStateApplied = false;
				SetCollidersActive(active: true);
				ClearMaterialState();
				SetQuestHighlightActive(active: false);
				ResetHoverVisuals(instant: true);
			}
		}

		private void ClearMaterialState()
		{
			if (_availabilityRenderers == null || _availabilityRenderers.Length == 0)
			{
				return;
			}
			for (int i = 0; i < _availabilityRenderers.Length; i++)
			{
				Renderer renderer = _availabilityRenderers[i];
				if (!(renderer == null))
				{
					renderer.SetPropertyBlock(null);
				}
			}
		}

		protected override void OnHoverEnter()
		{
			RefreshAvailability(force: true);
			if (!_isAvailable)
			{
				_isHovering = false;
				return;
			}
			_isHovering = true;
			PlayHoverAnimation();
			SetHoverFeedbackActive(active: true);
		}

		protected override void OnHoverExit()
		{
			_isHovering = false;
			ResetHoverVisuals(instant: false);
		}

		protected override void OnClick()
		{
			RefreshAvailability(force: true);
			if (_isAvailable && base.IsInValidState)
			{
				_prepStation.ClientKnifeSelected(_knifeID, _processedType);
				SetQuestHighlightActive(active: false);
				ResetHoverVisuals(instant: false);
			}
		}

		private void CacheReferences()
		{
			if (_modelObj != null)
			{
				_baseEuler = _modelObj.transform.localEulerAngles;
			}
			_colliders = GetComponentsInChildren<Collider>(includeInactive: true);
			if (_availabilityRenderers == null || _availabilityRenderers.Length == 0)
			{
				if (_modelObj != null)
				{
					_availabilityRenderers = _modelObj.GetComponentsInChildren<Renderer>(includeInactive: true);
				}
				else
				{
					_availabilityRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
				}
			}
			_propertyBlock = new MaterialPropertyBlock();
		}

		private bool EvaluateAvailability()
		{
			if (_prepStation == null)
			{
				return false;
			}
			if (!base.IsInValidState)
			{
				return false;
			}
			return _prepStation.CanSelectKnife(_knifeID, _processedType);
		}

		private void ApplyAvailabilityState(bool available)
		{
			if (!available)
			{
				SetQuestHighlightActive(active: false);
				ResetHoverVisuals(instant: true);
			}
			SetCollidersActive(available);
			if (available)
			{
				ClearMaterialState();
			}
			else
			{
				ApplyMaterialState(available: false);
			}
		}

		private void RefreshQuestHighlight(bool force)
		{
			bool flag = _isAvailable && _prepStation != null && _prepStation.ShouldShowQuestKnifeHighlight(_processedType);
			if (force || _isQuestHighlightActive != flag)
			{
				SetQuestHighlightActive(flag);
			}
		}

		private void SetQuestHighlightActive(bool active)
		{
			_isQuestHighlightActive = active;
			if (_outlinable != null)
			{
				_outlinable.enabled = active || _isHovering;
			}
		}

		private void SetCollidersActive(bool active)
		{
			if (!_disableCollidersWhenUnavailable || _colliders == null)
			{
				return;
			}
			for (int i = 0; i < _colliders.Length; i++)
			{
				if (!(_colliders[i] == null))
				{
					_colliders[i].enabled = active;
				}
			}
		}

		private void ApplyMaterialState(bool available)
		{
			if (_availabilityRenderers == null || _availabilityRenderers.Length == 0)
			{
				return;
			}
			Color value = (available ? _availableColor : _unavailableColor);
			for (int i = 0; i < _availabilityRenderers.Length; i++)
			{
				Renderer renderer = _availabilityRenderers[i];
				if (!(renderer == null))
				{
					renderer.GetPropertyBlock(_propertyBlock);
					_propertyBlock.SetColor(_colorPropertyName, value);
					renderer.SetPropertyBlock(_propertyBlock);
				}
			}
		}

		private void PlayHoverAnimation()
		{
			if (_modelObj == null)
			{
				return;
			}
			Transform target = _modelObj.transform;
			target.DOKill();
			Vector3 endValue = new Vector3(_baseEuler.x, _baseEuler.y, _baseEuler.z + _hoverRotateAmountZ);
			target.DOLocalRotate(endValue, _hoverRotateDuration).SetLoops(_hoverLoopCount, LoopType.Yoyo).SetEase(Ease.InOutSine)
				.OnComplete(() =>
				{
					if (!(_modelObj == null))
					{
						_modelObj.transform.DOLocalRotate(_baseEuler, _hoverRotateDuration).SetEase(Ease.OutSine);
					}
				});
		}

		private void SetHoverFeedbackActive(bool active)
		{
			_isHovering = active;
			if (_outlinable != null)
			{
				_outlinable.enabled = active || _isQuestHighlightActive;
			}
			if (_cutInfoGO != null)
			{
				_cutInfoGO.SetActive(active);
			}
		}

		private void ResetHoverVisuals(bool instant)
		{
			if (_modelObj != null)
			{
				Transform transform = _modelObj.transform;
				transform.DOKill();
				if (instant)
				{
					transform.localEulerAngles = _baseEuler;
				}
				else
				{
					transform.DOLocalRotate(_baseEuler, _hoverRotateDuration).SetEase(Ease.OutSine);
				}
			}
			SetHoverFeedbackActive(active: false);
		}
	}
}
