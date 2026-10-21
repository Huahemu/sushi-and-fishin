using UnityEngine;

namespace PaperPixelGames.Core.Interaction
{
	public class FixedSnapPlacementZone : MonoBehaviour, IQuickSnapPlacementZone
	{
		[SerializeField]
		private SurfaceTags _tags;

		[SerializeField]
		private Transform _placementPoint;

		[SerializeField]
		private bool _usePlacementPointRotation = true;

		[SerializeField]
		private bool _overrideRotation;

		[SerializeField]
		private Vector3 _rotationOverride;

		[SerializeField]
		private Color _gizmoColor = new Color(0.15f, 0.75f, 1f, 0.9f);

		[SerializeField]
		[Min(0.05f)]
		private float _gizmoSphereRadius = 0.08f;

		public SurfaceTags Tags => _tags;

		public bool HasTag(SurfaceTags tags)
		{
			if (_tags != SurfaceTags.None)
			{
				return (_tags & tags) != 0;
			}
			return true;
		}

		public Vector3 GetPlacementPoint(Vector3 hitPoint)
		{
			if (!(_placementPoint != null))
			{
				return base.transform.position;
			}
			return _placementPoint.position;
		}

		public Quaternion GetPlacementRotation(Quaternion defaultRotation)
		{
			if (_overrideRotation)
			{
				return Quaternion.Euler(_rotationOverride);
			}
			if (_usePlacementPointRotation && _placementPoint != null)
			{
				return _placementPoint.rotation;
			}
			return defaultRotation;
		}

		private void OnDrawGizmosSelected()
		{
			Vector3 vector = ((_placementPoint != null) ? _placementPoint.position : base.transform.position);
			Gizmos.color = _gizmoColor;
			Gizmos.DrawWireSphere(vector, _gizmoSphereRadius);
			Gizmos.DrawLine(base.transform.position, vector);
		}
	}
}
