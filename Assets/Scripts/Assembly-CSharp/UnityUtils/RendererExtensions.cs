using UnityEngine;

namespace UnityUtils
{
	public static class RendererExtensions
	{
		public static void EnableZWrite(this Renderer renderer)
		{
			Material[] materials = renderer.materials;
			foreach (Material material in materials)
			{
				if (material.HasProperty("_Color"))
				{
					material.SetInt("_ZWrite", 1);
					material.renderQueue = 3000;
				}
			}
		}

		public static void DisableZWrite(this Renderer renderer)
		{
			Material[] materials = renderer.materials;
			foreach (Material material in materials)
			{
				if (material.HasProperty("_Color"))
				{
					material.SetInt("_ZWrite", 0);
					material.renderQueue = 3100;
				}
			}
		}
	}
}
