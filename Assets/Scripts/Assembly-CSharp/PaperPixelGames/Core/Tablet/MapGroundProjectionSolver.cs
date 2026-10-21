using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class MapGroundProjectionSolver
	{
		private const float ConvergedErrorPixels = 0.5f;

		private const float MinimumDownwardDirection = 0.0001f;

		public bool TryMapUVToGroundWorld(MapProjection projection, MapDefinitionSO definition, MapGroundHeightResolver groundHeightResolver, Vector2 mapUV, float rayStartY, float rayDistance, LayerMask groundMask, Transform additionalGroundRoot, out Vector3 worldPosition, out float errorPixels, out bool converged, out bool hitBufferSaturated)
		{
			worldPosition = default;
			errorPixels = float.PositiveInfinity;
			converged = false;
			hitBufferSaturated = false;
			if (projection == null || definition == null || groundHeightResolver == null || !projection.IsValid)
			{
				return false;
			}
			rayDistance = Mathf.Max(1f, rayDistance);
			if (projection.UsesElevation)
			{
				if (!projection.TryCreateTiltedOrthographicRay(mapUV, rayStartY, out var ray))
				{
					return false;
				}
				float num = 0f - ray.direction.y;
				if (num < 0.0001f)
				{
					return false;
				}
				float rayDistance2 = rayDistance / num;
				if (!groundHeightResolver.TryResolveGroundPoint(ray, rayDistance2, groundMask, additionalGroundRoot, out worldPosition, out hitBufferSaturated))
				{
					return false;
				}
			}
			else
			{
				if (!projection.TryMapUVToWorldXZ(mapUV, out var worldXZ) || !groundHeightResolver.TryResolveGroundHeight(worldXZ, rayStartY, rayDistance, groundMask, additionalGroundRoot, out var groundY, out hitBufferSaturated))
				{
					return false;
				}
				worldPosition = new Vector3(worldXZ.x, groundY, worldXZ.y);
			}
			if (!TryCalculateErrorPixels(projection, definition, mapUV, worldPosition, out errorPixels))
			{
				return false;
			}
			converged = errorPixels <= 0.5f;
			return true;
		}

		private static bool TryCalculateErrorPixels(MapProjection projection, MapDefinitionSO definition, Vector2 targetMapUV, Vector3 worldPosition, out float errorPixels)
		{
			errorPixels = float.PositiveInfinity;
			if (!projection.TryWorldToMapUV(worldPosition, out var mapUV))
			{
				return false;
			}
			errorPixels = Vector2.Scale(b: new Vector2(Mathf.Max(1f, Mathf.Abs(definition.ReferenceResolution.x)), Mathf.Max(1f, Mathf.Abs(definition.ReferenceResolution.y))), a: mapUV - targetMapUV).magnitude;
			return IsFinite(errorPixels);
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
