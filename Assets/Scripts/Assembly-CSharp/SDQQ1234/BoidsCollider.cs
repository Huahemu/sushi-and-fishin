using System.Collections.Generic;
using UnityEngine;

namespace SDQQ1234
{
	[RequireComponent(typeof(Collider))]
	public class BoidsCollider : MonoBehaviour
	{
		public enum ColliderType
		{
			Sphere = 0,
			Box = 1,
			Capsule = 2,
			Unsupported = 3
		}

		public static List<BoidsCollider> AllObstacles = new List<BoidsCollider>();

		public ColliderType type { get; private set; }

		public Collider col { get; private set; }

		private void OnEnable()
		{
			col = GetComponent<Collider>();
			if (col is SphereCollider)
			{
				type = ColliderType.Sphere;
			}
			else if (col is BoxCollider)
			{
				type = ColliderType.Box;
			}
			else if (col is CapsuleCollider)
			{
				type = ColliderType.Capsule;
			}
			else
			{
				type = ColliderType.Unsupported;
			}
			if (type != ColliderType.Unsupported)
			{
				AllObstacles.Add(this);
			}
		}

		private void OnDisable()
		{
			AllObstacles.Remove(this);
		}
	}
}
