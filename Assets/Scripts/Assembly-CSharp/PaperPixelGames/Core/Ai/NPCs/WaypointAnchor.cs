using PaperPixelGames.Framework.Enums;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace PaperPixelGames.Core.Ai.NPCs
{
	[ExecuteAlways]
	public class WaypointAnchor : MonoBehaviour
	{
		[Header("Waypoint")]
		[SerializeField]
		private ResidentActivityType _activityType;

		[FormerlySerializedAs("index")]
		[SerializeField]
		private WayPointIndex _index;

		[Header("NavMesh Snap")]
		[SerializeField]
		private bool _autoSnapInEditor = true;

		[SerializeField]
		private bool _moveTransformToNavMesh = true;

		[SerializeField]
		private float _sampleRadius = 0.5f;

		[SerializeField]
		private int _areaMask = -1;

		[SerializeField]
		private float _verticalOffset;

		[Tooltip("Keep the authored furniture pose after validating nearby navigation. This prevents a seated resident being moved beside a bench by NavMesh projection.")]
		[SerializeField]
		private bool _useAuthoredPose;

		[Header("Debug")]
		[SerializeField]
		private bool _drawGizmos = true;

		[SerializeField]
		private Color _validColor = Color.green;

		[SerializeField]
		private Color _invalidColor = Color.red;

		[SerializeField]
		private Color _sampleRadiusColor = Color.yellow;

		private Vector3 _navMeshPosition;

		private bool _hasValidNavMeshPosition;

		public ResidentActivityType Category => _activityType;

		public WayPointIndex Index => _index;

		public bool UseAuthoredPose => _useAuthoredPose;

		public string GlobalKey => Category.ToString() + "_" + _index;

		public bool HasValidNavMeshPosition => _hasValidNavMeshPosition;

		public Vector3 NavMeshPosition
		{
			get
			{
				if (!_hasValidNavMeshPosition)
				{
					return base.transform.position;
				}
				return _navMeshPosition;
			}
		}

		private void OnEnable()
		{
			RefreshNavMeshPosition(moveTransform: false);
			WaypointDirectory.Register(this);
		}

		private void OnDisable()
		{
			WaypointDirectory.Unregister(this);
		}

		[ContextMenu("Refresh NavMesh Position")]
		public void RefreshNavMeshPosition()
		{
			RefreshNavMeshPosition(moveTransform: false);
		}

		[ContextMenu("Snap Transform To NavMesh")]
		public void SnapTransformToNavMesh()
		{
			RefreshNavMeshPosition(moveTransform: true);
		}

		public bool TryGetNavMeshPosition(out Vector3 position)
		{
			if (_hasValidNavMeshPosition || RefreshNavMeshPosition(moveTransform: false))
			{
				position = _navMeshPosition;
				return true;
			}
			position = base.transform.position;
			return false;
		}

		public bool RefreshNavMeshPosition(bool moveTransform)
		{
			if (TrySampleNavMesh(out var hit))
			{
				_navMeshPosition = ApplyOffset(hit.position);
				_hasValidNavMeshPosition = true;
				if (moveTransform)
				{
					base.transform.position = _navMeshPosition;
				}
				return true;
			}
			_hasValidNavMeshPosition = false;
			return false;
		}

		private bool TrySampleNavMesh(out NavMeshHit hit)
		{
			if (NavMesh.SamplePosition(base.transform.position, out hit, _sampleRadius, _areaMask))
			{
				return true;
			}
			if (Mathf.Approximately(_verticalOffset, 0f))
			{
				return false;
			}
			return NavMesh.SamplePosition(base.transform.position - Vector3.up * _verticalOffset, out hit, _sampleRadius, _areaMask);
		}

		private Vector3 ApplyOffset(Vector3 position)
		{
			return position + Vector3.up * _verticalOffset;
		}

		private void OnDrawGizmos()
		{
			if (_drawGizmos)
			{
				bool num = TrySampleNavMesh(out var hit);
				Vector3 vector = (num ? ApplyOffset(hit.position) : base.transform.position);
				Gizmos.color = (num ? _validColor : _invalidColor);
				Gizmos.DrawSphere(vector, 0.25f);
				Gizmos.color = _sampleRadiusColor;
				Gizmos.DrawWireSphere(base.transform.position, _sampleRadius);
				if (num)
				{
					Gizmos.DrawLine(base.transform.position, vector);
				}
			}
		}
	}
}
