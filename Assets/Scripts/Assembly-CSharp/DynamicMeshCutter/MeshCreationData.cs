using UnityEngine;

namespace DynamicMeshCutter
{
	public class MeshCreationData
	{
		public GameObject[] CreatedObjects;

		public MeshTarget[] CreatedTargets;

		public MeshCreationData(int size)
		{
			CreatedObjects = new GameObject[size];
			CreatedTargets = new MeshTarget[size];
		}
	}
}
