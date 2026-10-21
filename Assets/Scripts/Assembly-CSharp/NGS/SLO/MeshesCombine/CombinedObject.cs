using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
	[DisallowMultipleComponent]
	public class CombinedObject : ToggleableObject
	{
		public void Destroy(string dataFolder = "")
		{
			if ((bool)base.gameObject)
			{
				if (!string.IsNullOrEmpty(dataFolder) && !Application.isPlaying)
				{
					DestroyMeshFile(dataFolder);
				}
				Object.DestroyImmediate(base.gameObject);
			}
		}

		private void DestroyMeshFile(string dataFolder)
		{
		}

		private Mesh TryGetMesh()
		{
			MeshFilter component = GetComponent<MeshFilter>();
			if (component != null)
			{
				return component.sharedMesh;
			}
			SkinnedMeshRenderer component2 = GetComponent<SkinnedMeshRenderer>();
			if (component2 != null)
			{
				return component2.sharedMesh;
			}
			return null;
		}
	}
}
