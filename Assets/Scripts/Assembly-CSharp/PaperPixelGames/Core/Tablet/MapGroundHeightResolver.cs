using System;
using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class MapGroundHeightResolver
	{
		private const int InitialHitBufferCapacity = 32;

		private const int MaximumHitBufferCapacity = 256;

		private RaycastHit[] _hitBuffer = new RaycastHit[32];

		public bool TryResolveGroundHeight(Vector2 worldXZ, float rayStartY, float rayDistance, LayerMask groundMask, Transform additionalGroundRoot, out float groundY, out bool hitBufferSaturated)
		{
			groundY = 0f;
			hitBufferSaturated = false;
			if (!IsFinite(worldXZ.x) || !IsFinite(worldXZ.y) || !IsFinite(rayStartY) || !IsFinite(rayDistance) || rayDistance <= 0f || groundMask.value == 0)
			{
				return false;
			}
			Ray ray = new Ray(new Vector3(worldXZ.x, rayStartY, worldXZ.y), Vector3.down);
			if (!TryResolveGroundPoint(ray, rayDistance, groundMask, additionalGroundRoot, out var groundPoint, out hitBufferSaturated))
			{
				return false;
			}
			groundY = groundPoint.y;
			return true;
		}

		public bool TryResolveGroundPoint(Ray ray, float rayDistance, LayerMask groundMask, Transform additionalGroundRoot, out Vector3 groundPoint, out bool hitBufferSaturated)
		{
			groundPoint = default;
			hitBufferSaturated = false;
			if (!IsFinite(ray.origin.x) || !IsFinite(ray.origin.y) || !IsFinite(ray.origin.z) || !IsFinite(ray.direction.x) || !IsFinite(ray.direction.y) || !IsFinite(ray.direction.z) || ray.direction.sqrMagnitude <= Mathf.Epsilon || !IsFinite(rayDistance) || rayDistance <= 0f || groundMask.value == 0)
			{
				return false;
			}
			int num;
			while (true)
			{
				num = Physics.RaycastNonAlloc(ray, _hitBuffer, rayDistance, groundMask, QueryTriggerInteraction.Ignore);
				if (num < _hitBuffer.Length)
				{
					break;
				}
				if (_hitBuffer.Length >= 256)
				{
					hitBufferSaturated = true;
					return false;
				}
				int newSize = Mathf.Min(_hitBuffer.Length * 2, 256);
				Array.Resize(ref _hitBuffer, newSize);
			}
			float num2 = float.PositiveInfinity;
			bool result = false;
			for (int i = 0; i < num; i++)
			{
				RaycastHit raycastHit = _hitBuffer[i];
				if (IsAcceptedGroundCollider(raycastHit.collider, additionalGroundRoot) && !(raycastHit.distance >= num2))
				{
					num2 = raycastHit.distance;
					groundPoint = raycastHit.point;
					result = true;
				}
			}
			return result;
		}

		private static bool IsAcceptedGroundCollider(Collider collider, Transform additionalGroundRoot)
		{
			if (!(collider is TerrainCollider))
			{
				if (additionalGroundRoot != null && collider != null)
				{
					return collider.transform.IsChildOf(additionalGroundRoot);
				}
				return false;
			}
			return true;
		}

		private static bool IsFinite(float value)
		{
			if (!float.IsNaN(value))
			{
				return !float.IsInfinity(value);
			}
			return false;
		}
	}
}
