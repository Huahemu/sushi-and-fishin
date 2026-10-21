using UnityEngine;
using UnityEngine.SceneManagement;

namespace DynamicMeshCutter
{
	public class PlaneBehaviour : CutterBehaviour
	{
		public float DebugPlaneLength = 2f;

		public void Cut()
		{
			GameObject[] rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
			foreach (GameObject gameObject in rootGameObjects)
			{
				if (gameObject.activeInHierarchy)
				{
					MeshTarget[] componentsInChildren = gameObject.GetComponentsInChildren<MeshTarget>();
					foreach (MeshTarget target in componentsInChildren)
					{
						Cut(target, base.transform.position, base.transform.forward, null, OnCreated);
					}
				}
			}
		}

		private void OnCreated(Info info, MeshCreationData cData)
		{
			MeshCreation.TranslateCreatedObjects(info, cData.CreatedObjects, cData.CreatedTargets, Separation);
		}
	}
}
