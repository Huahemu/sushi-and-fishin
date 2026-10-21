using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Core.Interaction
{
	public sealed class ThrowRecoveryStation : MonoBehaviour
	{
		private const float RayEndPadding = 0.02f;

		private static readonly HashSet<ThrowRecoveryStation> ActiveStations = new HashSet<ThrowRecoveryStation>();

		[SerializeField]
		private Transform _usePoint;

		[SerializeField]
		[Min(0.5f)]
		private float _detectionRadius = 2.5f;

		[SerializeField]
		[Min(0f)]
		private float _visibilityOriginHeight = 1.4f;

		[SerializeField]
		[Min(0f)]
		private float _recoveryForwardOffset = 0.45f;

		[SerializeField]
		[Min(0f)]
		private float _groundClearance = 0.03f;

		private void OnEnable()
		{
			ActiveStations.Add(this);
		}

		private void OnDisable()
		{
			ActiveStations.Remove(this);
		}

		public static bool TryGetRecoveryPosition(BaseHoldable holdable, out Vector3 recoveryPosition)
		{
			recoveryPosition = default;
			if (holdable == null || !TryGetCombinedBounds(holdable, out var bounds))
			{
				return false;
			}
			ThrowRecoveryStation throwRecoveryStation = null;
			float num = float.MaxValue;
			foreach (ThrowRecoveryStation activeStation in ActiveStations)
			{
				if (!(activeStation == null) && !(activeStation._usePoint == null))
				{
					float sqrMagnitude = (bounds.center - activeStation.transform.position).sqrMagnitude;
					if (!(sqrMagnitude > activeStation._detectionRadius * activeStation._detectionRadius) && !(sqrMagnitude >= num) && activeStation.IsHiddenByStation(bounds))
					{
						throwRecoveryStation = activeStation;
						num = sqrMagnitude;
					}
				}
			}
			if (throwRecoveryStation == null)
			{
				return false;
			}
			recoveryPosition = throwRecoveryStation.BuildRecoveryPosition(bounds);
			return true;
		}

		private bool IsHiddenByStation(Bounds itemBounds)
		{
			Vector3 origin = _usePoint.position + Vector3.up * _visibilityOriginHeight;
			Vector3 vector = base.transform.right * Mathf.Max(0.05f, itemBounds.extents.x * 0.75f);
			Vector3 vector2 = Vector3.up * Mathf.Max(0.05f, itemBounds.extents.y * 0.75f);
			if (IsSampleBlocked(origin, itemBounds.center) && IsSampleBlocked(origin, itemBounds.center + vector2) && IsSampleBlocked(origin, itemBounds.center - vector2) && IsSampleBlocked(origin, itemBounds.center + vector))
			{
				return IsSampleBlocked(origin, itemBounds.center - vector);
			}
			return false;
		}

		private bool IsSampleBlocked(Vector3 origin, Vector3 sample)
		{
			Vector3 vector = sample - origin;
			float magnitude = vector.magnitude;
			if (magnitude <= 0.02f)
			{
				return false;
			}
			RaycastHit[] array = Physics.RaycastAll(origin, vector / magnitude, Mathf.Max(0f, magnitude - 0.02f), -1, QueryTriggerInteraction.Ignore);
			for (int i = 0; i < array.Length; i++)
			{
				Collider collider = array[i].collider;
				if (collider != null && (collider.transform == base.transform || collider.transform.IsChildOf(base.transform)))
				{
					return true;
				}
			}
			return false;
		}

		private Vector3 BuildRecoveryPosition(Bounds itemBounds)
		{
			Vector3 vector = _usePoint.position - base.transform.position;
			vector.y = 0f;
			if (vector.sqrMagnitude < 0.0001f)
			{
				vector = -base.transform.forward;
			}
			else
			{
				vector.Normalize();
			}
			float num = Mathf.Max(0.05f, itemBounds.center.y - itemBounds.min.y);
			Vector3 result = _usePoint.position + vector * _recoveryForwardOffset;
			result.y = _usePoint.position.y + num + _groundClearance;
			return result;
		}

		private static bool TryGetCombinedBounds(BaseHoldable holdable, out Bounds bounds)
		{
			Collider[] componentsInChildren = holdable.GetComponentsInChildren<Collider>(includeInactive: true);
			bounds = default;
			bool flag = false;
			foreach (Collider collider in componentsInChildren)
			{
				if (!(collider == null) && collider.enabled && !collider.isTrigger)
				{
					if (!flag)
					{
						bounds = collider.bounds;
						flag = true;
					}
					else
					{
						bounds.Encapsulate(collider.bounds);
					}
				}
			}
			return flag;
		}
	}
}
