namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::UnityEngine;

public class ReplaceAllMaterialShaders : MonoBehaviour
{
	public Shader targetShader;

	private void Update()
	{
		MeshRenderer[] componentsInChildren = GetComponentsInChildren<MeshRenderer>();
		List<Material> list = new List<Material>();
		MeshRenderer[] array = componentsInChildren;
		for (int i = 0; i < array.Length; i++)
		{
			Material[] sharedMaterials = array[i].sharedMaterials;
			foreach (Material material in sharedMaterials)
			{
				if (!list.Contains(material) && material != null && material.shader != targetShader)
				{
					material.shader = targetShader;
					list.Add(material);
				}
			}
		}
	}
}

}
