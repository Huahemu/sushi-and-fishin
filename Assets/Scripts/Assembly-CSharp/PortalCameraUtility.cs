using UnityEngine;

public static class PortalCameraUtility
{
	public struct MinMax3D
	{
		public float xMin;

		public float xMax;

		public float yMin;

		public float yMax;

		public float zMin;

		public float zMax;

		public MinMax3D(float min, float max)
		{
			xMin = min;
			xMax = max;
			yMin = min;
			yMax = max;
			zMin = min;
			zMax = max;
		}

		public void AddPoint(Vector3 point)
		{
			xMin = Mathf.Min(xMin, point.x);
			xMax = Mathf.Max(xMax, point.x);
			yMin = Mathf.Min(yMin, point.y);
			yMax = Mathf.Max(yMax, point.y);
			zMin = Mathf.Min(zMin, point.z);
			zMax = Mathf.Max(zMax, point.z);
		}
	}

	private static readonly Vector3[] cubeCornerOffsets = new Vector3[8]
	{
		new Vector3(1f, 1f, 1f),
		new Vector3(-1f, 1f, 1f),
		new Vector3(-1f, -1f, 1f),
		new Vector3(-1f, -1f, -1f),
		new Vector3(-1f, 1f, -1f),
		new Vector3(1f, -1f, -1f),
		new Vector3(1f, 1f, -1f),
		new Vector3(1f, -1f, 1f)
	};

	private static readonly Plane[] frustumPlanes = new Plane[6];

	public static bool VisibleFromCamera(Renderer renderer, Camera camera)
	{
		GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
		return GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds);
	}

	public static bool BoundsOverlap(MeshFilter nearObject, MeshFilter farObject, Camera camera)
	{
		MinMax3D screenRectFromBounds = GetScreenRectFromBounds(nearObject, camera);
		MinMax3D screenRectFromBounds2 = GetScreenRectFromBounds(farObject, camera);
		if (screenRectFromBounds2.zMax > screenRectFromBounds.zMin)
		{
			if (screenRectFromBounds2.xMax < screenRectFromBounds.xMin || screenRectFromBounds2.xMin > screenRectFromBounds.xMax)
			{
				return false;
			}
			if (screenRectFromBounds2.yMax < screenRectFromBounds.yMin || screenRectFromBounds2.yMin > screenRectFromBounds.yMax)
			{
				return false;
			}
			return true;
		}
		return false;
	}

	public static MinMax3D GetScreenRectFromBounds(MeshFilter renderer, Camera mainCamera)
	{
		MinMax3D result = new MinMax3D(float.MaxValue, float.MinValue);
		Bounds bounds = renderer.sharedMesh.bounds;
		bool flag = false;
		for (int i = 0; i < 8; i++)
		{
			Vector3 position = bounds.center + Vector3.Scale(bounds.extents, cubeCornerOffsets[i]);
			Vector3 position2 = renderer.transform.TransformPoint(position);
			Vector3 point = mainCamera.WorldToViewportPoint(position2);
			if (point.z > 0f)
			{
				flag = true;
			}
			else
			{
				point.x = ((point.x <= 0.5f) ? 1 : 0);
				point.y = ((point.y <= 0.5f) ? 1 : 0);
			}
			result.AddPoint(point);
		}
		if (!flag)
		{
			return default;
		}
		return result;
	}
}
