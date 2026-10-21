namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.IO;
using global::System.Linq;
using global::Sirenix.Serialization;
using global::UnityEngine;

[ExecuteAlways]
public class SceneMaterialAuditor : MonoBehaviour
{
	[Serializable]
	public class MaterialRecord
	{
		public Material material;

		public string materialName;

		public string shaderName;

		public string assetPath;

		public string folder;

		public List<string> users;

		public int UserCount
		{
			get
			{
				if (users == null)
				{
					return 0;
				}
				return users.Count;
			}
		}
	}

	[Serializable]
	private class MaterialExport
	{
		public List<MaterialRecord> records;
	}

	[Serializable]
	private class ShaderGroup
	{
		public string shaderName;

		public List<FolderGroup> folders;
	}

	[Serializable]
	private class FolderGroup
	{
		public string folder;

		public List<MaterialRecord> records;
	}

	[Header("Scan Options")]
	[Tooltip("Include inactive GameObjects in the scan.")]
	public bool includeInactive = true;

	[Tooltip("Include SpriteRenderers in the scan.")]
	public bool includeSpriteRenderers = true;

	[Tooltip("Include all Renderer types (ParticleSystemRenderer, LineRenderer, etc.). If false, only MeshRenderer + SkinnedMeshRenderer.")]
	public bool includeAllRenderers = true;

	[Tooltip("If true, scans will include materials that are not project assets (e.g., built-in / runtime-created).")]
	public bool includeNonAssetMaterials = true;

	[Header("Results Summary")]
	[SerializeField]
	private string lastScanInfo;

	[SerializeField]
	private int uniqueMaterialCount;

	[SerializeField]
	private int totalRendererCount;

	[SerializeField]
	private List<MaterialRecord> materials = new List<MaterialRecord>();

	[OdinSerialize]
	private Dictionary<string, Dictionary<string, List<MaterialRecord>>> grouped = new Dictionary<string, Dictionary<string, List<MaterialRecord>>>();

	[Header("Optional Auto Refresh")]
	[Tooltip("If enabled, will rescan when options change in Inspector (Editor only).")]
	public bool rescanOnValidate;

	public IReadOnlyList<MaterialRecord> Materials => materials;

	[ContextMenu("Scan Scene Materials")]
	public void Scan()
	{
		materials.Clear();
		grouped.Clear();
		Dictionary<Material, MaterialRecord> dictionary = new Dictionary<Material, MaterialRecord>(256);
		Renderer[] array = UnityEngine.Object.FindObjectsByType<Renderer>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
		totalRendererCount = array.Length;
		foreach (Renderer renderer in array)
		{
			if (renderer == null || (!includeAllRenderers && !(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) || (!includeSpriteRenderers && renderer is SpriteRenderer))
			{
				continue;
			}
			Material[] sharedMaterials = renderer.sharedMaterials;
			if (sharedMaterials == null || sharedMaterials.Length == 0)
			{
				continue;
			}
			string transformPath = GetTransformPath(renderer.transform);
			foreach (Material material in sharedMaterials)
			{
				if (material == null)
				{
					continue;
				}
				string empty = string.Empty;
				bool flag = !string.IsNullOrEmpty(empty);
				if (includeNonAssetMaterials || flag)
				{
					if (!dictionary.TryGetValue(material, out var value))
					{
						value = new MaterialRecord
						{
							material = material,
							materialName = material.name,
							shaderName = (material.shader ? material.shader.name : "(No Shader)"),
							assetPath = (flag ? empty : "(Non-Asset / Built-in / Runtime)"),
							folder = (flag ? NormalizeFolder(Path.GetDirectoryName(empty)) : "(No Asset Folder)"),
							users = new List<string>(4)
						};
						dictionary.Add(material, value);
					}
					if (!value.users.Contains(transformPath))
					{
						value.users.Add(transformPath);
					}
				}
			}
		}
		materials = dictionary.Values.OrderByDescending((MaterialRecord r) => r.UserCount).ThenBy((MaterialRecord r) => r.shaderName, StringComparer.OrdinalIgnoreCase).ThenBy((MaterialRecord r) => r.folder, StringComparer.OrdinalIgnoreCase)
			.ThenBy((MaterialRecord r) => r.materialName, StringComparer.OrdinalIgnoreCase)
			.ToList();
		uniqueMaterialCount = materials.Count;
		BuildGroupedView();
	}

	[ContextMenu("Clear Results")]
	public void Clear()
	{
		materials.Clear();
		uniqueMaterialCount = 0;
		totalRendererCount = 0;
		grouped.Clear();
	}

	private void OnValidate()
	{
	}

	private void BuildGroupedView()
	{
		grouped = new Dictionary<string, Dictionary<string, List<MaterialRecord>>>(StringComparer.OrdinalIgnoreCase);
		foreach (MaterialRecord material in materials)
		{
			string key = (string.IsNullOrEmpty(material.shaderName) ? "(No Shader)" : material.shaderName);
			string key2 = (string.IsNullOrEmpty(material.folder) ? "(No Folder)" : material.folder);
			if (!grouped.TryGetValue(key, out var value))
			{
				value = new Dictionary<string, List<MaterialRecord>>(StringComparer.OrdinalIgnoreCase);
				grouped.Add(key, value);
			}
			if (!value.TryGetValue(key2, out var value2))
			{
				value2 = new List<MaterialRecord>();
				value.Add(key2, value2);
			}
			value2.Add(material);
		}
		foreach (Dictionary<string, List<MaterialRecord>> value3 in grouped.Values)
		{
			foreach (List<MaterialRecord> value4 in value3.Values)
			{
				value4.Sort((MaterialRecord a, MaterialRecord b) =>
				{
					int num = b.UserCount.CompareTo(a.UserCount);
					return (num != 0) ? num : string.Compare(a.materialName, b.materialName, StringComparison.OrdinalIgnoreCase);
				});
			}
		}
	}

	private static string GetTransformPath(Transform t)
	{
		if (t == null)
		{
			return "(null)";
		}
		Stack<string> stack = new Stack<string>(16);
		Transform transform = t;
		while (transform != null)
		{
			stack.Push(transform.name);
			transform = transform.parent;
		}
		return string.Join("/", stack);
	}

	private static string NormalizeFolder(string folder)
	{
		if (string.IsNullOrEmpty(folder))
		{
			return "(Root)";
		}
		folder = folder.Replace("\\", "/");
		return folder;
	}
}

}
