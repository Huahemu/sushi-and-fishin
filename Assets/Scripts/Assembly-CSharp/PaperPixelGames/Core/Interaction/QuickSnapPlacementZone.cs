using UnityEngine;
using UnityEngine.Serialization;

namespace PaperPixelGames.Core.Interaction
{
	public class QuickSnapPlacementZone : MonoBehaviour, IQuickSnapPlacementZone
	{
		private enum PlacementAxis
		{
			X = 0,
			Y = 1,
			Z = 2
		}

		[SerializeField]
		private SurfaceTags _tags;

		[SerializeField]
		private PlacementAxis _movementAxis;

		[SerializeField]
		private float _xOffset;

		[FormerlySerializedAs("_clampWorldY")]
		[SerializeField]
		private float _yOffset;

		[FormerlySerializedAs("_clampWorldZ")]
		[SerializeField]
		private float _zOffset;

		[SerializeField]
		private bool _overrideRotation;

		[SerializeField]
		private Vector3 _rotationOverride;

		[SerializeField]
		private Color _gizmoColor = new Color(0f, 1f, 0.65f, 0.9f);

		[SerializeField]
		[Min(0.05f)]
		private float _gizmoSphereRadius = 0.08f;

		public SurfaceTags Tags => _tags;

		public float XOffset => _xOffset;

		public float YOffset => _yOffset;

		public float ZOffset => _zOffset;

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
			Vector3 vector = GetRotationIndependentBasePosition() + new Vector3(_xOffset, _yOffset, _zOffset);
			SetAxisValue(ref vector, _movementAxis, GetAxisValue(hitPoint, _movementAxis));
			return vector;
		}

		public Quaternion GetPlacementRotation(Quaternion defaultRotation)
		{
			if (!_overrideRotation)
			{
				return defaultRotation;
			}
			return Quaternion.Euler(_rotationOverride);
		}

		private Vector3 GetRotationIndependentBasePosition()
		{
			Vector3 localPosition = base.transform.localPosition;
			if (base.transform.parent == null)
			{
				return localPosition;
			}
			return base.transform.parent.TransformPoint(localPosition);
		}

		private void OnDrawGizmosSelected()
		{
			Vector3 rotationIndependentBasePosition = GetRotationIndependentBasePosition();
			Vector3 vector = rotationIndependentBasePosition + new Vector3(_xOffset, _yOffset, _zOffset);
			float value = GetAxisValue(rotationIndependentBasePosition, _movementAxis) - 0.5f;
			float value2 = GetAxisValue(rotationIndependentBasePosition, _movementAxis) + 0.5f;
			Collider component2;
			if (TryGetComponent<BoxCollider>(out var component))
			{
				int movementAxis = (int)_movementAxis;
				float num = component.center[movementAxis] * base.transform.lossyScale[movementAxis];
				float num2 = component.size[movementAxis] * 0.5f * Mathf.Abs(base.transform.lossyScale[movementAxis]);
				float num3 = GetAxisValue(rotationIndependentBasePosition, _movementAxis) + num;
				value = num3 - num2;
				value2 = num3 + num2;
			}
			else if (TryGetComponent<Collider>(out component2))
			{
				value = GetAxisValue(component2.bounds.min, _movementAxis);
				value2 = GetAxisValue(component2.bounds.max, _movementAxis);
			}
			Vector3 vector2 = vector;
			Vector3 vector3 = vector;
			SetAxisValue(ref vector2, _movementAxis, value);
			SetAxisValue(ref vector3, _movementAxis, value2);
			Gizmos.color = _gizmoColor;
			Gizmos.DrawLine(vector2, vector3);
			Gizmos.DrawWireSphere(vector, _gizmoSphereRadius);
		}

		private static float GetAxisValue(Vector3 vector, PlacementAxis axis)
		{
			return axis switch
			{
				PlacementAxis.X => vector.x, 
				PlacementAxis.Y => vector.y, 
				PlacementAxis.Z => vector.z, 
				_ => vector.x, 
			};
		}

		private static void SetAxisValue(ref Vector3 vector, PlacementAxis axis, float value)
		{
			switch (axis)
			{
			case PlacementAxis.X:
				vector.x = value;
				break;
			case PlacementAxis.Y:
				vector.y = value;
				break;
			case PlacementAxis.Z:
				vector.z = value;
				break;
			}
		}
	}
}
