using System.Collections;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.ChefDesk
{
	public class LocalSeasoningApplicator : MonoBehaviour
	{
		[Header("Refs")]
		[SerializeField]
		private Transform _tipPoint;

		[SerializeField]
		private ParticleSystem _sprinkleParticle;

		[SerializeField]
		private GameObject _previewRingPrefab;

		[Header("Follow")]
		[SerializeField]
		private float _heightOffset;

		[SerializeField]
		private Vector3 _rotationOffset;

		[Header("Apply")]
		[SerializeField]
		private float _applyInterval = 0.08f;

		[SerializeField]
		private float _castDistance = 0.25f;

		[SerializeField]
		private float _castRadius = 0.04f;

		[SerializeField]
		private LayerMask _receiverMask;

		[Tooltip("When plate and sushi colliders overlap, prefer a sushi hit within this distance of the closest hit.")]
		[SerializeField]
		private float _sushiSurfacePreferenceDistance = 0.06f;

		[Header("Tap Animation")]
		[SerializeField]
		private float _tapDownDistance = 0.025f;

		[SerializeField]
		private float _tapDownAngle = 12f;

		[SerializeField]
		private float _tapDownDuration = 0.04f;

		[SerializeField]
		private float _tapUpDuration = 0.06f;

		[SerializeField]
		private float _decalDelay = 0.06f;

		[Header("Feedback")]
		[SerializeField]
		private float _emptyToastCooldown = 0.75f;

		[SerializeField]
		private float _previewSurfaceOffset = 0.003f;

		[SerializeField]
		private Color _previewValidColor = new Color(0.15f, 1f, 0.25f, 0.85f);

		[SerializeField]
		private Color _previewInvalidColor = new Color(1f, 0.1f, 0.05f, 0.85f);

		[Header("Debug")]
		[SerializeField]
		private bool _debugDrawCast = true;

		[SerializeField]
		private Color _debugHitColor = Color.green;

		[SerializeField]
		private Color _debugMissColor = Color.red;

		private Camera _camera;

		private Plane _workPlane;

		private Seasoning _sourceSeasoning;

		private float _nextApplyTime;

		private float _nextEmptyToastTime;

		private bool _isInitialized;

		private bool _isTapAnimating;

		private bool _waitForInputRelease;

		private Coroutine _tapRoutine;

		private GameObject _previewRingInstance;

		private LineRenderer[] _previewLineRenderers;

		private Vector3 _animLocalOffset;

		private Quaternion _animLocalRotationOffset = Quaternion.identity;

		public Transform TipPoint => _tipPoint;

		public void Initialize(Camera camera, Plane workPlane, Seasoning sourceSeasoning)
		{
			StopActiveTapRoutine();
			StopParticle();
			ResetAnimationState();
			_camera = camera;
			_workPlane = workPlane;
			_sourceSeasoning = sourceSeasoning;
			_nextApplyTime = 0f;
			_nextEmptyToastTime = 0f;
			EnsurePreviewRing();
			SetPreviewRingVisible(isVisible: false);
			_isInitialized = true;
			_waitForInputRelease = Input.GetMouseButton(0);
		}

		private void Update()
		{
			if (!_isInitialized || _camera == null)
			{
				return;
			}
			UpdateFollow();
			UpdatePreview();
			if (_waitForInputRelease)
			{
				if (!Input.GetMouseButton(0))
				{
					_waitForInputRelease = false;
				}
			}
			else if (Input.GetMouseButtonDown(0))
			{
				TrySprinkle();
			}
		}

		private void UpdateFollow()
		{
			Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
			if (_workPlane.Raycast(ray, out var enter))
			{
				Vector3 vector = ray.GetPoint(enter) + Vector3.up * _heightOffset;
				Quaternion quaternion = Quaternion.Euler(_rotationOffset);
				Vector3 vector2 = quaternion * _animLocalOffset;
				Quaternion rotation = quaternion * _animLocalRotationOffset;
				base.transform.SetPositionAndRotation(vector + vector2, rotation);
			}
		}

		private void TrySprinkle()
		{
			if (Time.time < _nextApplyTime || _isTapAnimating)
			{
				return;
			}
			_nextApplyTime = Time.time + _applyInterval;
			if (!(_tipPoint == null) && !(_sourceSeasoning == null))
			{
				SeasoningReceiver receiver;
				Vector3 localPosition;
				SeasoningSurfaceType surfaceType;
				Vector3 worldNormal;
				bool canApply;
				if (!_sourceSeasoning.HasAnyLeft())
				{
					StopParticle();
					ShowEmptyToastThrottled();
				}
				else if (TryResolveCurrentTarget(out receiver, out localPosition, out surfaceType, out worldNormal, out canApply) && canApply)
				{
					StartTapRoutine(receiver, localPosition, surfaceType);
				}
			}
		}

		private bool TryResolveSeasoningHit(RaycastHit[] hits, out SeasoningReceiver receiver, out Vector3 localPosition, out SeasoningSurfaceType surfaceType, out Vector3 worldNormal, out bool canApply)
		{
			receiver = null;
			localPosition = default;
			surfaceType = SeasoningSurfaceType.Plate;
			worldNormal = Vector3.up;
			canApply = false;
			if (hits == null || hits.Length == 0)
			{
				return false;
			}
			int num = -1;
			float num2 = float.PositiveInfinity;
			for (int i = 0; i < hits.Length; i++)
			{
				RaycastHit raycastHit = hits[i];
				if (!(raycastHit.collider == null))
				{
					SeasoningReceiver componentInParent = raycastHit.collider.GetComponentInParent<SeasoningReceiver>();
					if (!(componentInParent == null) && !(raycastHit.distance >= num2))
					{
						num = i;
						num2 = raycastHit.distance;
						receiver = componentInParent;
					}
				}
			}
			if (num < 0 || receiver == null)
			{
				return false;
			}
			int num3 = num;
			float num4 = float.PositiveInfinity;
			float num5 = num2 + Mathf.Max(0f, _sushiSurfacePreferenceDistance);
			for (int j = 0; j < hits.Length; j++)
			{
				RaycastHit raycastHit2 = hits[j];
				if (!(raycastHit2.collider == null) && !(raycastHit2.distance > num5) && !(raycastHit2.collider.GetComponentInParent<SeasoningReceiver>() != receiver) && receiver.GetSurfaceType(raycastHit2.collider) == SeasoningSurfaceType.Sushi && !(raycastHit2.distance >= num4))
				{
					num4 = raycastHit2.distance;
					num3 = j;
				}
			}
			bool num6 = receiver.TryCreateApplication(hits[num3], out localPosition, out surfaceType);
			if (num6)
			{
				worldNormal = ((hits[num3].normal.sqrMagnitude > Mathf.Epsilon) ? hits[num3].normal.normalized : Vector3.up);
				canApply = receiver.CanApplySeasoningAt(localPosition, surfaceType);
			}
			return num6;
		}

		private bool TryResolveCurrentTarget(out SeasoningReceiver receiver, out Vector3 localPosition, out SeasoningSurfaceType surfaceType, out Vector3 worldNormal, out bool canApply)
		{
			receiver = null;
			localPosition = default;
			surfaceType = SeasoningSurfaceType.Plate;
			worldNormal = Vector3.up;
			canApply = false;
			if (_tipPoint == null)
			{
				return false;
			}
			RaycastHit[] hits = Physics.SphereCastAll(_tipPoint.position, _castRadius, Vector3.down, _castDistance, _receiverMask, QueryTriggerInteraction.Ignore);
			return TryResolveSeasoningHit(hits, out receiver, out localPosition, out surfaceType, out worldNormal, out canApply);
		}

		private void UpdatePreview()
		{
			if (!(_previewRingInstance == null))
			{
				if (!TryResolveCurrentTarget(out var receiver, out var localPosition, out var surfaceType, out var _, out var canApply))
				{
					SetPreviewRingVisible(isVisible: false);
					return;
				}
				Vector3 position = receiver.GetSurfaceWorldPoint(localPosition, surfaceType) + Vector3.up * Mathf.Max(0f, _previewSurfaceOffset);
				_previewRingInstance.transform.SetPositionAndRotation(position, Quaternion.identity);
				SetPreviewColor(canApply ? _previewValidColor : _previewInvalidColor);
				SetPreviewRingVisible(isVisible: true);
			}
		}

		private void EnsurePreviewRing()
		{
			if (!(_previewRingInstance != null) && !(_previewRingPrefab == null))
			{
				_previewRingInstance = Object.Instantiate(_previewRingPrefab);
				_previewRingInstance.name = _previewRingPrefab.name + " (Seasoning Preview)";
				_previewLineRenderers = _previewRingInstance.GetComponentsInChildren<LineRenderer>(includeInactive: true);
				SetPreviewColor(_previewInvalidColor);
			}
		}

		private void DestroyPreviewRing()
		{
			if (!(_previewRingInstance == null))
			{
				Object.Destroy(_previewRingInstance);
				_previewRingInstance = null;
				_previewLineRenderers = null;
			}
		}

		private void SetPreviewRingVisible(bool isVisible)
		{
			if (_previewRingInstance != null && _previewRingInstance.activeSelf != isVisible)
			{
				_previewRingInstance.SetActive(isVisible);
			}
		}

		private void SetPreviewColor(Color color)
		{
			if (_previewLineRenderers == null)
			{
				return;
			}
			for (int i = 0; i < _previewLineRenderers.Length; i++)
			{
				LineRenderer lineRenderer = _previewLineRenderers[i];
				if (lineRenderer == null)
				{
					continue;
				}
				lineRenderer.startColor = color;
				lineRenderer.endColor = color;
				Material material = lineRenderer.material;
				if (!(material == null))
				{
					if (material.HasProperty("_BaseColor"))
					{
						material.SetColor("_BaseColor", color);
					}
					if (material.HasProperty("_Color"))
					{
						material.SetColor("_Color", color);
					}
				}
			}
		}

		private void StartTapRoutine(SeasoningReceiver receiver, Vector3 localPosition, SeasoningSurfaceType surfaceType)
		{
			StopActiveTapRoutine();
			_tapRoutine = StartCoroutine(PlayTapAndApply(receiver, localPosition, surfaceType));
		}

		private IEnumerator PlayTapAndApply(SeasoningReceiver receiver, Vector3 localPosition, SeasoningSurfaceType surfaceType)
		{
			_isTapAnimating = true;
			Vector3 startOffset = Vector3.zero;
			Quaternion startRotationOffset = Quaternion.identity;
			Vector3 tapOffset = Vector3.down * _tapDownDistance;
			Quaternion tapRotationOffset = Quaternion.Euler(_tapDownAngle, 0f, 0f);
			float elapsed = 0f;
			while (elapsed < _tapDownDuration)
			{
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / _tapDownDuration);
				_animLocalOffset = Vector3.Lerp(startOffset, tapOffset, t);
				_animLocalRotationOffset = Quaternion.Slerp(startRotationOffset, tapRotationOffset, t);
				yield return null;
			}
			_animLocalOffset = tapOffset;
			_animLocalRotationOffset = tapRotationOffset;
			PlayParticle();
			if (_decalDelay > 0f)
			{
				yield return new WaitForSeconds(_decalDelay);
			}
			if (_isInitialized && receiver != null && _sourceSeasoning != null)
			{
				if (_sourceSeasoning.HasAnyLeft())
				{
					receiver.RequestApplySeasoning(_sourceSeasoning, localPosition, surfaceType);
				}
				else
				{
					StopParticle();
					ShowEmptyToastThrottled();
				}
			}
			elapsed = 0f;
			while (elapsed < _tapUpDuration)
			{
				elapsed += Time.deltaTime;
				float t2 = Mathf.Clamp01(elapsed / _tapUpDuration);
				_animLocalOffset = Vector3.Lerp(tapOffset, startOffset, t2);
				_animLocalRotationOffset = Quaternion.Slerp(tapRotationOffset, startRotationOffset, t2);
				yield return null;
			}
			ResetAnimationState();
			StopParticle();
			_tapRoutine = null;
		}

		private void ShowEmptyToastThrottled()
		{
			if (!(Time.time < _nextEmptyToastTime))
			{
				_nextEmptyToastTime = Time.time + _emptyToastCooldown;
				Singleton<NotifyManager>.Instance.ShowToast("This seasoning is empty");
			}
		}

		private void PlayParticle()
		{
			if (!(_sprinkleParticle == null) && !_sprinkleParticle.isPlaying)
			{
				_sprinkleParticle.Play();
			}
		}

		private void StopParticle()
		{
			if (!(_sprinkleParticle == null) && _sprinkleParticle.isPlaying)
			{
				_sprinkleParticle.Stop();
			}
		}

		private void StopActiveTapRoutine()
		{
			if (_tapRoutine != null)
			{
				StopCoroutine(_tapRoutine);
				_tapRoutine = null;
			}
			_isTapAnimating = false;
		}

		private void ResetAnimationState()
		{
			_animLocalOffset = Vector3.zero;
			_animLocalRotationOffset = Quaternion.identity;
			_isTapAnimating = false;
		}

		private void OnDisable()
		{
			StopActiveTapRoutine();
			StopParticle();
			ResetAnimationState();
			DestroyPreviewRing();
			_isInitialized = false;
		}

		private void OnDestroy()
		{
			StopActiveTapRoutine();
			StopParticle();
			ResetAnimationState();
			DestroyPreviewRing();
			_camera = null;
			_sourceSeasoning = null;
			_waitForInputRelease = false;
			_isInitialized = false;
		}

		private void OnDrawGizmos()
		{
			if (_debugDrawCast && !(_tipPoint == null))
			{
				Vector3 position = _tipPoint.position;
				Vector3 vector = position + Vector3.down * _castDistance;
				bool flag = Physics.SphereCast(position, _castRadius, Vector3.down, out var hitInfo, _castDistance, _receiverMask, QueryTriggerInteraction.Ignore);
				Gizmos.color = (flag ? _debugHitColor : _debugMissColor);
				Gizmos.DrawWireSphere(position, _castRadius);
				Gizmos.DrawWireSphere(vector, _castRadius);
				Gizmos.DrawLine(position + Vector3.left * _castRadius, vector + Vector3.left * _castRadius);
				Gizmos.DrawLine(position - Vector3.left * _castRadius, vector - Vector3.left * _castRadius);
				Gizmos.DrawLine(position + Vector3.forward * _castRadius, vector + Vector3.forward * _castRadius);
				Gizmos.DrawLine(position - Vector3.forward * _castRadius, vector - Vector3.forward * _castRadius);
				Gizmos.DrawLine(position, vector);
				if (flag)
				{
					Gizmos.color = Color.cyan;
					Gizmos.DrawSphere(hitInfo.point, 0.01f);
					Gizmos.DrawRay(hitInfo.point, hitInfo.normal * 0.08f);
				}
			}
		}
	}
}
