using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Boss
{
	public sealed class CrabBossLocalRock : CrabBossLocalRockSlot
	{
		[Header("Optional Telegraph")]
		[SerializeField]
		private GameObject _telegraphVisualRoot;

		[SerializeField]
		[Min(0f)]
		private float _telegraphSurfaceOffset = 0.03f;

		private Vector3 _startPosition;

		private Vector3 _endPosition;

		private Vector3 _moveDirection;

		private Quaternion _rotation;

		private double _moveStartTime;

		private double _moveEndTime;

		private float _moveSpeed;

		private bool _usesEndPosition;

		private bool _configured;

		private Vector3 _telegraphPosition;

		private bool _showTelegraph;

		public bool TryGetCollisionBox(Quaternion rotation, out Vector3 centerOffset, out Vector3 halfExtents)
		{
			centerOffset = Vector3.zero;
			halfExtents = Vector3.zero;
			MeshCollider component = GetComponent<MeshCollider>();
			if (component == null || component.sharedMesh == null)
			{
				return false;
			}
			Bounds bounds = component.sharedMesh.bounds;
			Vector3 lossyScale = base.transform.lossyScale;
			centerOffset = rotation * Vector3.Scale(bounds.center, lossyScale);
			halfExtents = Vector3.Scale(bounds.extents, new Vector3(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)));
			if (halfExtents.x > 0f && halfExtents.y > 0f)
			{
				return halfExtents.z > 0f;
			}
			return false;
		}

		private void FixedUpdate()
		{
			if (_configured && !(NetworkManager.Singleton == null))
			{
				UpdateFlight(NetworkManager.Singleton.ServerTime.Time);
			}
		}

		private void UpdateFlight(double now)
		{
			base.transform.SetPositionAndRotation(EvaluatePosition(now), _rotation);
			if (now >= _moveStartTime)
			{
				_showTelegraph = false;
				CrabBossLocalRockSlot.SetActiveIfNeeded(_telegraphVisualRoot, active: false);
			}
		}

		private Vector3 EvaluatePosition(double now)
		{
			float num = Mathf.Max(0f, (float)(now - _moveStartTime));
			if (!_usesEndPosition)
			{
				return _startPosition + _moveDirection * (_moveSpeed * num);
			}
			float num2 = Mathf.Max(0.001f, (float)(_moveEndTime - _moveStartTime));
			return Vector3.Lerp(_startPosition, _endPosition, num / num2);
		}

		private void CatchUpToServerTime()
		{
			if (NetworkManager.Singleton != null)
			{
				UpdateFlight(NetworkManager.Singleton.ServerTime.Time);
			}
		}

		private void LateUpdate()
		{
			if (_configured && _showTelegraph)
			{
				UpdateTelegraphPosition();
			}
		}

		public void BeginFlight(Vector3 startPosition, Vector3 endPosition, Quaternion rotation, double flightStartTime, double flightEndTime)
		{
			_startPosition = startPosition;
			_endPosition = endPosition;
			_rotation = rotation;
			_moveStartTime = flightStartTime;
			_moveEndTime = flightEndTime;
			_usesEndPosition = true;
			_configured = true;
			_showTelegraph = false;
			PrepareVisual(startPosition, rotation);
			CrabBossLocalRockSlot.SetActiveIfNeeded(_telegraphVisualRoot, active: false);
			CatchUpToServerTime();
		}

		public void BeginFall(Vector3 spawnPosition, Vector3 telegraphPosition, Quaternion rotation, double fallStartTime, float fallSpeed)
		{
			_startPosition = spawnPosition;
			_moveDirection = Vector3.down;
			_rotation = rotation;
			_moveStartTime = fallStartTime;
			_moveSpeed = fallSpeed;
			_usesEndPosition = false;
			_configured = true;
			_telegraphPosition = telegraphPosition + Vector3.up * _telegraphSurfaceOffset;
			_showTelegraph = _telegraphVisualRoot != null;
			PrepareVisual(spawnPosition, rotation);
			CrabBossLocalRockSlot.SetActiveIfNeeded(_telegraphVisualRoot, _showTelegraph);
			UpdateTelegraphPosition();
			CatchUpToServerTime();
		}

		public override void ResetSlot()
		{
			_configured = false;
			_showTelegraph = false;
			CrabBossLocalRockSlot.SetActiveIfNeeded(_telegraphVisualRoot, active: false);
			base.ResetSlot();
		}

		public override void PlayImpact(CrabBossRockImpactType impactType, Vector3 position, Vector3 normal)
		{
			_configured = false;
			_showTelegraph = false;
			CrabBossLocalRockSlot.SetActiveIfNeeded(_telegraphVisualRoot, active: false);
			base.PlayImpact(impactType, position, normal);
		}

		public override void PlayPhysicsImpact(CrabBossRockImpactType impactType, Vector3 impactPosition, Vector3 impactNormal, Vector3 physicsPosition, Quaternion physicsRotation)
		{
			_configured = false;
			_showTelegraph = false;
			CrabBossLocalRockSlot.SetActiveIfNeeded(_telegraphVisualRoot, active: false);
			base.PlayPhysicsImpact(impactType, impactPosition, impactNormal, physicsPosition, physicsRotation);
		}

		private void UpdateTelegraphPosition()
		{
			if (_showTelegraph && _telegraphVisualRoot != null)
			{
				_telegraphVisualRoot.transform.position = _telegraphPosition;
			}
		}
	}
}
