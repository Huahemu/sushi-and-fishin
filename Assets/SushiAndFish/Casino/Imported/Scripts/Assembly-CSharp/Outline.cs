namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.Linq;
using global::UnityEngine;

public class Outline : MonoBehaviour
{
	public enum Mode
	{
		OutlineAll = 0,
		OutlineVisible = 1,
		OutlineHidden = 2,
		OutlineAndSilhouette = 3,
		SilhouetteOnly = 4
	}

	[Serializable]
	private class ListVector3
	{
		public List<Vector3> data;
	}

	private static HashSet<Mesh> registeredMeshes = new HashSet<Mesh>();

	[SerializeField]
	private Mode outlineMode;

	[SerializeField]
	private Color outlineColor = Color.white;

	[SerializeField]
	[Range(0f, 10f)]
	private float outlineWidth = 2f;

	[Header("Optional")]
	[SerializeField]
	[Tooltip("Precompute enabled: Per-vertex calculations are performed in the editor and serialized with the object. Precompute disabled: Per-vertex calculations are performed at runtime in Awake(). This may cause a pause for large meshes.")]
	private bool precomputeOutline;

	[SerializeField]
	[HideInInspector]
	private List<Mesh> bakeKeys = new List<Mesh>();

	[SerializeField]
	[HideInInspector]
	private List<ListVector3> bakeValues = new List<ListVector3>();

	private Renderer[] renderers;

	private Material outlineMaskMaterial;

	private Material outlineFillMaterial;

	private bool needsUpdate;

	public Mode OutlineMode
	{
		get
		{
			return outlineMode;
		}
		set
		{
			outlineMode = value;
			needsUpdate = true;
		}
	}

	public Color OutlineColor
	{
		get
		{
			return outlineColor;
		}
		set
		{
			outlineColor = value;
			needsUpdate = true;
		}
	}

	public float OutlineWidth
	{
		get
		{
			return outlineWidth;
		}
		set
		{
			outlineWidth = value;
			needsUpdate = true;
		}
	}

	private void Awake()
	{
		CacheRenderers();
		outlineMaskMaterial = UnityEngine.Object.Instantiate(SushiAndFish.Casino.Original.CasinoResources.Load<Material>("Materials/OutlineMask"));
		outlineFillMaterial = UnityEngine.Object.Instantiate(SushiAndFish.Casino.Original.CasinoResources.Load<Material>("Materials/OutlineFill"));
		outlineMaskMaterial.name = "OutlineMask (Instance)";
		outlineFillMaterial.name = "OutlineFill (Instance)";
		LoadSmoothNormals();
		needsUpdate = true;
	}

	public void CacheRenderers()
	{
		renderers = (from r in GetComponentsInChildren<Renderer>()
			where r.gameObject.layer != SushiAndFish.Casino.CasinoLayers.NameToLayer("DisableOutline")
			select r).ToArray();
	}

	private void OnEnable()
	{
		Renderer[] array = renderers;
		foreach (Renderer renderer in array)
		{
			if (!(renderer == null))
			{
				List<Material> list = renderer.sharedMaterials.ToList();
				list.Add(outlineMaskMaterial);
				list.Add(outlineFillMaterial);
				renderer.materials = list.ToArray();
			}
		}
	}

	private void OnValidate()
	{
		needsUpdate = true;
		if ((!precomputeOutline && bakeKeys.Count != 0) || bakeKeys.Count != bakeValues.Count)
		{
			bakeKeys.Clear();
			bakeValues.Clear();
		}
		if (precomputeOutline && bakeKeys.Count == 0)
		{
			Bake();
		}
	}

	private void Update()
	{
		if (needsUpdate)
		{
			needsUpdate = false;
			UpdateMaterialProperties();
		}
	}

	private void OnDisable()
	{
		Renderer[] array = renderers;
		foreach (Renderer renderer in array)
		{
			if (!(renderer == null))
			{
				List<Material> list = renderer.sharedMaterials.ToList();
				list.Remove(outlineMaskMaterial);
				list.Remove(outlineFillMaterial);
				renderer.materials = list.ToArray();
			}
		}
	}

	private void OnDestroy()
	{
		UnityEngine.Object.Destroy(outlineMaskMaterial);
		UnityEngine.Object.Destroy(outlineFillMaterial);
	}

	private void Bake()
	{
		HashSet<Mesh> hashSet = new HashSet<Mesh>();
		MeshFilter[] componentsInChildren = GetComponentsInChildren<MeshFilter>();
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			if (hashSet.Add(meshFilter.sharedMesh))
			{
				List<Vector3> data = SmoothNormals(meshFilter.sharedMesh);
				bakeKeys.Add(meshFilter.sharedMesh);
				bakeValues.Add(new ListVector3
				{
					data = data
				});
			}
		}
	}

	private void LoadSmoothNormals()
	{
		MeshFilter[] componentsInChildren = GetComponentsInChildren<MeshFilter>();
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			if (meshFilter.sharedMesh && registeredMeshes.Add(meshFilter.sharedMesh))
			{
				int num = bakeKeys.IndexOf(meshFilter.sharedMesh);
				List<Vector3> uvs = ((num >= 0) ? bakeValues[num].data : SmoothNormals(meshFilter.sharedMesh));
				meshFilter.sharedMesh.SetUVs(3, uvs);
				Renderer component = meshFilter.GetComponent<Renderer>();
				if (component != null)
				{
					CombineSubmeshes(meshFilter.sharedMesh, component.sharedMaterials);
				}
			}
		}
		SkinnedMeshRenderer[] componentsInChildren2 = GetComponentsInChildren<SkinnedMeshRenderer>();
		foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren2)
		{
			if (skinnedMeshRenderer.sharedMesh && registeredMeshes.Add(skinnedMeshRenderer.sharedMesh))
			{
				skinnedMeshRenderer.sharedMesh.uv4 = new Vector2[skinnedMeshRenderer.sharedMesh.vertexCount];
				CombineSubmeshes(skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.sharedMaterials);
			}
		}
	}

	private List<Vector3> SmoothNormals(Mesh mesh)
	{
		IEnumerable<IGrouping<Vector3, KeyValuePair<Vector3, int>>> enumerable = from pair in mesh.vertices.Select((Vector3 vertex, int index) => new KeyValuePair<Vector3, int>(vertex, index))
			group pair by pair.Key;
		List<Vector3> list = new List<Vector3>(mesh.normals);
		foreach (IGrouping<Vector3, KeyValuePair<Vector3, int>> item in enumerable)
		{
			if (item.Count() == 1)
			{
				continue;
			}
			Vector3 zero = Vector3.zero;
			foreach (KeyValuePair<Vector3, int> item2 in item)
			{
				zero += list[item2.Value];
			}
			zero.Normalize();
			foreach (KeyValuePair<Vector3, int> item3 in item)
			{
				list[item3.Value] = zero;
			}
		}
		return list;
	}

	private void CombineSubmeshes(Mesh mesh, Material[] materials)
	{
		if (mesh.subMeshCount != 1 && mesh.subMeshCount <= materials.Length)
		{
			mesh.subMeshCount++;
			mesh.SetTriangles(mesh.triangles, mesh.subMeshCount - 1);
		}
	}

	private void UpdateMaterialProperties()
	{
		outlineFillMaterial.SetColor("_OutlineColor", outlineColor);
		switch (outlineMode)
		{
		case Mode.OutlineAll:
			outlineMaskMaterial.SetFloat("_ZTest", 8f);
			outlineFillMaterial.SetFloat("_ZTest", 8f);
			outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
			break;
		case Mode.OutlineVisible:
			outlineMaskMaterial.SetFloat("_ZTest", 8f);
			outlineFillMaterial.SetFloat("_ZTest", 4f);
			outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
			break;
		case Mode.OutlineHidden:
			outlineMaskMaterial.SetFloat("_ZTest", 8f);
			outlineFillMaterial.SetFloat("_ZTest", 5f);
			outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
			break;
		case Mode.OutlineAndSilhouette:
			outlineMaskMaterial.SetFloat("_ZTest", 4f);
			outlineFillMaterial.SetFloat("_ZTest", 8f);
			outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
			break;
		case Mode.SilhouetteOnly:
			outlineMaskMaterial.SetFloat("_ZTest", 4f);
			outlineFillMaterial.SetFloat("_ZTest", 5f);
			outlineFillMaterial.SetFloat("_OutlineWidth", 0f);
			break;
		}
	}
}

}
