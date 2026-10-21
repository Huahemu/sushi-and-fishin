using System.Collections;
using DG.Tweening;
using PaperPixelGames.Framework.Utility;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Kitchen
{
	public class CuttingViewManager : MonoBehaviour
	{
		[Header("Cut Point")]
		[SerializeField]
		private RectTransform _cutPointUI;

		[SerializeField]
		private Graphic[] _cutPointGraphics;

		[Header("Idle Pulse")]
		[SerializeField]
		private RectTransform _pulseTarget;

		[SerializeField]
		private bool _enableIdlePulse = true;

		[SerializeField]
		[Min(1f)]
		private float _idlePulseScale = 1.08f;

		[SerializeField]
		[Min(0.05f)]
		private float _idlePulseDuration = 0.55f;

		[Header("Cut Point Animation")]
		[SerializeField]
		[Min(0.01f)]
		private float _spawnDuration = 0.12f;

		[SerializeField]
		[Min(0.01f)]
		private float _successFeedbackDuration = 0.16f;

		[SerializeField]
		[Min(1f)]
		private float _successPopScale = 1.35f;

		[SerializeField]
		[Min(1f)]
		private float _hoverScale = 1.12f;

		[SerializeField]
		[Min(0.01f)]
		private float _hoverLerpIn = 12f;

		[SerializeField]
		[Min(0.01f)]
		private float _hoverLerpOut = 7f;

		[Header("Cut Point Colors")]
		[SerializeField]
		private Color _normalColor = new Color(1f, 0.62f, 0.24f, 0.82f);

		[SerializeField]
		private Color _hoverColor = new Color(1f, 0.78f, 0.38f, 0.96f);

		[SerializeField]
		private Color _successColor = new Color(1f, 0.9f, 0.64f, 1f);

		[Header("Camera")]
		[SerializeField]
		private CinemachineCamera _cuttingCinemachineCamera;

		[SerializeField]
		private float _cameraBlendTime;

		private EventBinding<DeskCam> _deskCamEvent;

		private CinemachineBrain _cinemachineBrain;

		private Camera _playerCamera;

		private Coroutine _showRoutine;

		private Sequence _cutPointSequence;

		private Tween _idlePulseTween;

		private Vector3 _cutPointBaseScale = Vector3.one;

		private Vector3 _pulseTargetBaseScale = Vector3.one;

		private float _hoverT;

		private float _rootFeedbackScale = 1f;

		private float _rootPulseScale = 1f;

		private bool _pulseUsesCutPointRoot;

		private bool _isCuttingViewActive;

		private bool _canDisplayCutPoint;

		private bool _isCutPointInteractable;

		public Vector3 CutPoint { get; private set; }

		public Camera PlayerCamera => _playerCamera;

		public RectTransform CutPointUI => _cutPointUI;

		public float CameraBlendTime => _cameraBlendTime;

		public float SuccessFeedbackDuration => _successFeedbackDuration;

		public bool IsCutPointInteractable
		{
			get
			{
				if (_isCuttingViewActive && _canDisplayCutPoint && _isCutPointInteractable && _cutPointUI != null)
				{
					return _cutPointUI.gameObject.activeInHierarchy;
				}
				return false;
			}
		}

		private void Awake()
		{
			CacheCutPointComponents();
			HideCutPointImmediate();
		}

		private void Start()
		{
			_deskCamEvent = new EventBinding<DeskCam>(OnDeskCamSet);
			EventBus<DeskCam>.Register(_deskCamEvent);
		}

		private void OnDestroy()
		{
			EventBus<DeskCam>.Deregister(_deskCamEvent);
			StopShowRoutine();
			KillCutPointTween();
			StopIdlePulse();
		}

		public void SetCuttingViewActive(bool isActive)
		{
			_isCuttingViewActive = isActive;
			_canDisplayCutPoint = false;
			_isCutPointInteractable = false;
			if (_cuttingCinemachineCamera != null)
			{
				_cuttingCinemachineCamera.enabled = isActive;
			}
			StopShowRoutine();
			KillCutPointTween();
			StopIdlePulse();
			if (isActive)
			{
				HideCutPointImmediate();
				_showRoutine = StartCoroutine(WaitForBlendThenShowCutUI());
			}
			else
			{
				HideCutPointImmediate();
			}
		}

		public void GenerateRandomCutPoint(GameObject ingredientObject)
		{
			if (ingredientObject == null)
			{
				UtilityMethods.Log("[CuttingViewManager] Ingredient object is null. Cannot generate cut point.", LogType.Error);
				return;
			}
			MeshRenderer componentInChildren = ingredientObject.GetComponentInChildren<MeshRenderer>();
			if (componentInChildren == null)
			{
				UtilityMethods.Log("[CuttingViewManager] MeshRenderer not found. Cannot calculate cut point.", LogType.Error);
				return;
			}
			Bounds bounds = componentInChildren.bounds;
			Vector3 center = bounds.center;
			Vector3 extents = bounds.extents;
			float num = extents.x * 0.8f;
			float num2 = extents.z * 0.8f;
			float num3 = Random.Range(0f - num, num);
			float num4 = Random.Range(0f - num2, num2);
			float x = center.x + num3;
			float z = center.z + num4;
			CutPoint = new Vector3(x, bounds.max.y + 0.05f, z);
			if (!(_cutPointUI == null))
			{
				_cutPointUI.position = CutPoint;
				float z2 = Random.Range(0f, 360f);
				_cutPointUI.localRotation = Quaternion.Euler(0f, 0f, z2);
				if (_isCuttingViewActive && _canDisplayCutPoint)
				{
					ShowCutPoint();
				}
			}
		}

		public void SetCutPoint(Vector3 cutPoint, Vector3 displayPosition, float rotationZ = 0f)
		{
			CutPoint = cutPoint;
			if (!(_cutPointUI == null))
			{
				_cutPointUI.position = displayPosition;
				_cutPointUI.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
				if (_isCuttingViewActive && _canDisplayCutPoint)
				{
					ShowCutPoint();
				}
			}
		}

		public void SetCutPointInteractionEnabled(bool enabled)
		{
			_isCutPointInteractable = enabled && _isCuttingViewActive && _canDisplayCutPoint && _cutPointUI != null && _cutPointUI.gameObject.activeInHierarchy;
			if (!_isCutPointInteractable)
			{
				_hoverT = 0f;
				ApplyCutPointRootScale();
				SetCutPointGraphicsColor(_normalColor);
			}
		}

		public float PlayCutPointSuccessFeedback(float? unusedRotationZ = null)
		{
			if (_cutPointUI == null)
			{
				return 0f;
			}
			_isCutPointInteractable = false;
			_hoverT = 0f;
			StopIdlePulse();
			KillCutPointTween();
			SetCutPointGraphicsColor(_successColor);
			_rootFeedbackScale = 1f;
			_rootPulseScale = 1f;
			ApplyCutPointRootScale();
			_cutPointSequence = DOTween.Sequence();
			_cutPointSequence.Join(DOVirtual.Float(1f, _successPopScale, _successFeedbackDuration, (float value) =>
			{
				_rootFeedbackScale = value;
				ApplyCutPointRootScale();
			}).SetEase(Ease.OutQuad));
			_cutPointSequence.OnComplete(HideCutPointImmediate);
			return _successFeedbackDuration;
		}

		public void SetCutPointHoverState(bool inside)
		{
			if (IsCutPointInteractable && !(_cutPointUI == null))
			{
				float target = (inside ? 1f : 0f);
				float num = (inside ? _hoverLerpIn : _hoverLerpOut);
				_hoverT = Mathf.MoveTowards(_hoverT, target, num * Time.deltaTime);
				_hoverT = Mathf.Clamp01(_hoverT);
				ApplyCutPointRootScale();
				Color cutPointGraphicsColor = Color.Lerp(_normalColor, _hoverColor, _hoverT);
				SetCutPointGraphicsColor(cutPointGraphicsColor);
			}
		}

		private IEnumerator WaitForBlendThenShowCutUI()
		{
			UtilityMethods.Log("[CuttingViewManager] Blend started.");
			yield return new WaitForSeconds(_cameraBlendTime);
			UtilityMethods.Log("[CuttingViewManager] Blend finished.");
			_showRoutine = null;
			_canDisplayCutPoint = true;
			if (_isCuttingViewActive && CutPoint != Vector3.zero)
			{
				ShowCutPoint();
			}
		}

		private void ShowCutPoint()
		{
			if (!(_cutPointUI == null))
			{
				StopIdlePulse();
				KillCutPointTween();
				_hoverT = 0f;
				_rootPulseScale = 1f;
				_rootFeedbackScale = 0.82f;
				_isCutPointInteractable = true;
				_cutPointUI.gameObject.SetActive(value: true);
				if (_pulseTarget != null)
				{
					_pulseTarget.localScale = _pulseTargetBaseScale;
				}
				ApplyCutPointRootScale();
				SetCutPointGraphicsColor(_normalColor);
				_cutPointSequence = DOTween.Sequence();
				_cutPointSequence.Join(DOVirtual.Float(0.82f, 1f, _spawnDuration, (float value) =>
				{
					_rootFeedbackScale = value;
					ApplyCutPointRootScale();
				}).SetEase(Ease.OutBack));
				_cutPointSequence.OnComplete(StartIdlePulse);
			}
		}

		private void StartIdlePulse()
		{
			if (!_enableIdlePulse || _cutPointUI == null || !_cutPointUI.gameObject.activeInHierarchy)
			{
				return;
			}
			StopIdlePulse();
			if (_pulseTarget == null)
			{
				return;
			}
			if (_pulseUsesCutPointRoot)
			{
				_rootPulseScale = 1f;
				_idlePulseTween = DOVirtual.Float(1f, _idlePulseScale, _idlePulseDuration, (float value) =>
				{
					_rootPulseScale = value;
					ApplyCutPointRootScale();
				}).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
			}
			else
			{
				_pulseTarget.localScale = _pulseTargetBaseScale;
				_idlePulseTween = _pulseTarget.DOScale(_pulseTargetBaseScale * _idlePulseScale, _idlePulseDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
			}
		}

		private void StopIdlePulse()
		{
			if (_idlePulseTween != null && _idlePulseTween.IsActive())
			{
				_idlePulseTween.Kill();
			}
			_idlePulseTween = null;
			_rootPulseScale = 1f;
			if (_pulseTarget != null)
			{
				_pulseTarget.localScale = _pulseTargetBaseScale;
			}
			ApplyCutPointRootScale();
		}

		private void ApplyCutPointRootScale()
		{
			if (!(_cutPointUI == null))
			{
				float num = Mathf.Lerp(1f, _hoverScale, _hoverT);
				float num2 = _rootFeedbackScale * num * _rootPulseScale;
				_cutPointUI.localScale = _cutPointBaseScale * num2;
			}
		}

		private void HideCutPointImmediate()
		{
			_isCutPointInteractable = false;
			_hoverT = 0f;
			_rootFeedbackScale = 1f;
			_rootPulseScale = 1f;
			if (!(_cutPointUI == null))
			{
				_cutPointUI.gameObject.SetActive(value: false);
				_cutPointUI.localScale = _cutPointBaseScale;
				if (_pulseTarget != null)
				{
					_pulseTarget.localScale = _pulseTargetBaseScale;
				}
			}
		}

		private void CacheCutPointComponents()
		{
			if (!(_cutPointUI == null))
			{
				_cutPointBaseScale = _cutPointUI.localScale;
				if (_cutPointBaseScale == Vector3.zero)
				{
					_cutPointBaseScale = Vector3.one;
				}
				if (_pulseTarget == null)
				{
					_pulseTarget = _cutPointUI;
				}
				_pulseUsesCutPointRoot = _pulseTarget == _cutPointUI;
				_pulseTargetBaseScale = _pulseTarget.localScale;
				if (_pulseTargetBaseScale == Vector3.zero)
				{
					_pulseTargetBaseScale = Vector3.one;
				}
				if (_cutPointGraphics == null || _cutPointGraphics.Length == 0)
				{
					_cutPointGraphics = _cutPointUI.GetComponentsInChildren<Graphic>(includeInactive: true);
				}
			}
		}

		private void SetCutPointGraphicsColor(Color color)
		{
			if (_cutPointGraphics == null)
			{
				return;
			}
			for (int i = 0; i < _cutPointGraphics.Length; i++)
			{
				if (_cutPointGraphics[i] != null)
				{
					_cutPointGraphics[i].color = color;
				}
			}
		}

		private void KillCutPointTween()
		{
			if (_cutPointSequence != null && _cutPointSequence.IsActive())
			{
				_cutPointSequence.Kill();
			}
			_cutPointSequence = null;
			if (_cutPointUI != null)
			{
				_cutPointUI.DOKill();
			}
		}

		private void StopShowRoutine()
		{
			if (_showRoutine != null)
			{
				StopCoroutine(_showRoutine);
				_showRoutine = null;
			}
		}

		private void OnDeskCamSet(DeskCam deskCamEvent)
		{
			if (deskCamEvent.CinemachineBrain == null)
			{
				_cinemachineBrain = null;
				_playerCamera = null;
				return;
			}
			_cinemachineBrain = deskCamEvent.CinemachineBrain;
			Camera camera = _cinemachineBrain.OutputCamera ?? _cinemachineBrain.GetComponent<Camera>();
			if (camera != null)
			{
				_playerCamera = camera;
				UtilityMethods.Log("[CuttingViewManager] Player camera assigned from CinemachineBrain.");
			}
			else
			{
				UtilityMethods.Log("[CuttingViewManager] Failed to resolve player camera from CinemachineBrain.", LogType.Warning);
			}
		}

		private void OnDrawGizmos()
		{
			if (!(CutPoint == Vector3.zero))
			{
				Gizmos.color = Color.red;
				Gizmos.DrawSphere(CutPoint, 0.02f);
				Gizmos.color = Color.yellow;
				Gizmos.DrawWireSphere(CutPoint, 0.04f);
			}
		}
	}
}
