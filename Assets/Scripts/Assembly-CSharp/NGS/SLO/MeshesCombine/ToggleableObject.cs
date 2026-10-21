using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
	public class ToggleableObject : MonoBehaviour
	{
		public void SetEnable(bool enable)
		{
			LODGroup component = GetComponent<LODGroup>();
			if (component != null)
			{
				SetLODGroupEnable(component, enable);
				return;
			}
			Renderer component2 = GetComponent<Renderer>();
			if (component2 == null)
			{
				Debug.Log("ToggleableObject::no LODs or Renderers found");
			}
			else
			{
				SetRendererEnable(component2, enable);
			}
		}

		private void SetRendererEnable(Renderer renderer, bool enable)
		{
			renderer.enabled = enable;
		}

		private void SetLODGroupEnable(LODGroup group, bool enable)
		{
			LOD[] lODs = group.GetLODs();
			for (int i = 0; i < lODs.Length; i++)
			{
				Renderer[] renderers = lODs[i].renderers;
				foreach (Renderer renderer in renderers)
				{
					if (!(renderer == null))
					{
						renderer.enabled = enable;
					}
				}
			}
		}
	}
}
