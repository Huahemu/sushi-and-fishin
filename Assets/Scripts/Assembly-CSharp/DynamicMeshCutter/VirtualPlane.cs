using UnityEngine;

namespace DynamicMeshCutter
{
	public class VirtualPlane
	{
		private Vector3 _localposition;

		private Vector3 _localNormal;

		private Vector3 _worldPosition;

		private Vector3 _worldNormal;

		public Vector3 LocalPosition => _localposition;

		public Vector3 LocalNormal => _localNormal;

		public Vector3 WorldPosition => _worldPosition;

		public Vector3 WorldNormal => _worldNormal;

		public VirtualPlane(Vector3 localPosition, Vector3 localNormal, Vector3 worldPosition, Vector3 worldNormal)
		{
			_localposition = localPosition;
			_localNormal = localNormal.normalized;
			_worldPosition = worldPosition;
			_worldNormal = worldNormal;
		}

		public int GetSide(Vector3 point)
		{
			Vector3 normalized = (point - _localposition).normalized;
			if (!(Vector3.Dot(_localNormal, normalized) <= 0f))
			{
				return 0;
			}
			return 1;
		}

		public void Raycast(Ray ray, out float distance)
		{
			Vector3 origin = ray.origin;
			Vector3 direction = ray.direction;
			Vector3 localNormal = _localNormal;
			float num = Vector3.Dot(localNormal, _localposition);
			Vector3 a = origin + (num - Vector3.Dot(localNormal, origin)) / Vector3.Dot(localNormal, direction) * direction;
			distance = Vector3.Distance(a, origin);
		}
	}
}
