using System.Collections.Generic;
using UnityEngine;

namespace DynamicMeshCutter
{
	public class DynamicRagdoll : MonoBehaviour, ISerializationCallbackReceiver
	{
		public int[] Assignments;

		public Dictionary<int, DynamicRagdollPart> Parts = new Dictionary<int, DynamicRagdollPart>();

		[SerializeField]
		private List<DynamicRagdollPart> _values = new List<DynamicRagdollPart>();

		[SerializeField]
		private List<int> _keys = new List<int>();

		public Renderer Renderer;

		public bool IsRagdollKinematic;

		public void SetRagdollKinematic(bool value)
		{
			foreach (KeyValuePair<int, DynamicRagdollPart> part in Parts)
			{
				if (part.Value.Rigidbody != null)
				{
					if (value)
					{
						part.Value.Rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
					}
					part.Value.Rigidbody.isKinematic = value;
					if (!value)
					{
						part.Value.Rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
					}
				}
			}
		}

		public void OnBeforeSerialize()
		{
			_keys.Clear();
			_values.Clear();
			foreach (KeyValuePair<int, DynamicRagdollPart> part in Parts)
			{
				_keys.Add(part.Key);
				_values.Add(part.Value);
			}
		}

		public void OnAfterDeserialize()
		{
			Parts.Clear();
			for (int i = 0; i < _keys.Count; i++)
			{
				Parts.Add(_keys[i], _values[i]);
			}
		}
	}
}
