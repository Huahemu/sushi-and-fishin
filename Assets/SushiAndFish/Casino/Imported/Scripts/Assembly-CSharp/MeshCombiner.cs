namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::System.Linq;
using global::UnityEngine;
using global::UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class MeshCombiner : MonoBehaviour
{
	private const int Mesh16BitBufferVertexLimit = 65535;

	[SerializeField]
	private bool createMultiMaterialMesh;

	[SerializeField]
	private bool combineInactiveChildren;

	[SerializeField]
	private bool deactivateCombinedChildren = true;

	[SerializeField]
	private bool deactivateCombinedChildrenMeshRenderers;

	[SerializeField]
	private bool generateUVMap;

	[SerializeField]
	private bool destroyCombinedChildren;

	[SerializeField]
	private bool addMeshCollider;

	[SerializeField]
	private bool onlyAffectChildren;

	[SerializeField]
	private bool combineOnStart;

	[SerializeField]
	private string folderPath = "Content/Baking/CombinedMeshes";

	[SerializeField]
	private bool setStaticAfterCombine;

	[SerializeField]
	[Tooltip("MeshFilters with Meshes which we don't want to combine into one Mesh.")]
	private MeshFilter[] meshFiltersToSkip = new MeshFilter[0];

	[SerializeField]
	[Tooltip("GameObjects to ignore when combining meshes. These GameObjects and their children will be skipped.")]
	private GameObject[] gameObjectsToIgnore = new GameObject[0];

	[Tooltip("Layers to ignore when combining meshes. Objects on these layers and their children will be skipped.")]
	public LayerMask layersToIgnore = 0;

	public bool CreateMultiMaterialMesh
	{
		get
		{
			return createMultiMaterialMesh;
		}
		set
		{
			createMultiMaterialMesh = value;
		}
	}

	public bool CombineInactiveChildren
	{
		get
		{
			return combineInactiveChildren;
		}
		set
		{
			combineInactiveChildren = value;
		}
	}

	public bool AddMeshCollider
	{
		get
		{
			return addMeshCollider;
		}
		set
		{
			addMeshCollider = value;
		}
	}

	public bool DeactivateCombinedChildren
	{
		get
		{
			return deactivateCombinedChildren;
		}
		set
		{
			deactivateCombinedChildren = value;
			CheckDeactivateCombinedChildren();
		}
	}

	public bool DeactivateCombinedChildrenMeshRenderers
	{
		get
		{
			return deactivateCombinedChildrenMeshRenderers;
		}
		set
		{
			deactivateCombinedChildrenMeshRenderers = value;
			CheckDeactivateCombinedChildren();
		}
	}

	public bool GenerateUVMap
	{
		get
		{
			return generateUVMap;
		}
		set
		{
			generateUVMap = value;
		}
	}

	public bool DestroyCombinedChildren
	{
		get
		{
			return destroyCombinedChildren;
		}
		set
		{
			destroyCombinedChildren = value;
			CheckDestroyCombinedChildren();
		}
	}

	public string FolderPath
	{
		get
		{
			return folderPath;
		}
		set
		{
			folderPath = value;
		}
	}

	public bool SetStaticAfterCombine
	{
		get
		{
			return setStaticAfterCombine;
		}
		set
		{
			setStaticAfterCombine = value;
		}
	}

	private void CheckDeactivateCombinedChildren()
	{
		if (deactivateCombinedChildren || deactivateCombinedChildrenMeshRenderers)
		{
			destroyCombinedChildren = false;
		}
	}

	private void CheckDestroyCombinedChildren()
	{
		if (destroyCombinedChildren)
		{
			deactivateCombinedChildren = false;
			deactivateCombinedChildrenMeshRenderers = false;
		}
	}

	private void Start()
	{
		if (combineOnStart)
		{
			if (onlyAffectChildren)
			{
				CombineMeshes(showCreatedMeshInfo: false);
			}
			else
			{
				FindAndCombineAllActiveMeshes(showCreatedMeshInfo: false);
			}
		}
	}

	public void FindAndCombineAllActiveMeshes(bool showCreatedMeshInfo)
	{
		if (onlyAffectChildren)
		{
			CombineMeshes(showCreatedMeshInfo);
			return;
		}
		(from mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
			where mf.gameObject.activeInHierarchy && mf.transform.parent == null && !meshFiltersToSkip.Contains(mf) && !ShouldSkipMeshFilter(mf) && !ShouldSkipGameObject(mf.gameObject)
			select mf).ToArray();
		MeshFilter[] array = meshFiltersToSkip;
		meshFiltersToSkip = new MeshFilter[0];
		CombineMeshes(showCreatedMeshInfo);
		meshFiltersToSkip = array;
		if (setStaticAfterCombine)
		{
			base.gameObject.isStatic = true;
		}
	}

	public void CombineMeshes(bool showCreatedMeshInfo)
	{
		Vector3 localScale = base.transform.localScale;
		int siblingIndex = base.transform.GetSiblingIndex();
		Transform parent = base.transform.parent;
		base.transform.parent = null;
		Quaternion rotation = base.transform.rotation;
		Vector3 position = base.transform.position;
		Vector3 localScale2 = base.transform.localScale;
		base.transform.rotation = Quaternion.identity;
		base.transform.position = Vector3.zero;
		base.transform.localScale = Vector3.one;
		if (!createMultiMaterialMesh)
		{
			CombineMeshesWithSingleMaterial(showCreatedMeshInfo);
		}
		else
		{
			CombineMeshesWithMutliMaterial(showCreatedMeshInfo);
		}
		base.transform.rotation = rotation;
		base.transform.position = position;
		base.transform.localScale = localScale2;
		base.transform.parent = parent;
		base.transform.SetSiblingIndex(siblingIndex);
		base.transform.localScale = localScale;
		MeshFilter component = GetComponent<MeshFilter>();
		if (component != null && component.sharedMesh != null && addMeshCollider)
		{
			MeshCollider meshCollider = base.gameObject.GetComponent<MeshCollider>();
			if (meshCollider == null)
			{
				meshCollider = base.gameObject.AddComponent<MeshCollider>();
			}
			meshCollider.sharedMesh = component.sharedMesh;
		}
		if (setStaticAfterCombine)
		{
			base.gameObject.isStatic = true;
		}
	}

	public void UndoCombine()
	{
		MeshRenderer[] componentsInChildren = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
		foreach (MeshRenderer meshRenderer in componentsInChildren)
		{
			if (!(meshRenderer == null) && !(meshRenderer.gameObject == base.gameObject))
			{
				meshRenderer.enabled = true;
				if (!meshRenderer.gameObject.activeSelf)
				{
					meshRenderer.gameObject.SetActive(value: true);
				}
			}
		}
		MeshFilter component = GetComponent<MeshFilter>();
		if (component != null)
		{
			component.sharedMesh = null;
		}
		MeshRenderer component2 = GetComponent<MeshRenderer>();
		if (component2 != null)
		{
			component2.sharedMaterials = new Material[0];
			component2.enabled = false;
		}
	}

	private MeshFilter[] GetMeshFiltersToCombine()
	{
		MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(combineInactiveChildren);
		if (meshFilters == null || meshFilters.Length == 0)
		{
			return new MeshFilter[0];
		}
		if (meshFilters.Length != 0 && meshFilters[0] != null)
		{
			meshFiltersToSkip = meshFiltersToSkip.Where((MeshFilter meshFilter) => meshFilter != meshFilters[0]).ToArray();
		}
		meshFiltersToSkip = meshFiltersToSkip.Where((MeshFilter meshFilter) => meshFilter != null).ToArray();
		gameObjectsToIgnore = gameObjectsToIgnore.Where((GameObject go) => go != null).ToArray();
		meshFilters = meshFilters.Where((MeshFilter meshFilter) => meshFilter != null && !ShouldSkipMeshFilter(meshFilter)).ToArray();
		meshFilters = meshFilters.Where((MeshFilter meshFilter) => !ShouldSkipGameObject(meshFilter.gameObject)).ToArray();
		int i;
		for (i = 0; i < meshFiltersToSkip.Length; i++)
		{
			meshFilters = meshFilters.Where((MeshFilter meshFilter) => meshFilter != meshFiltersToSkip[i]).ToArray();
		}
		return meshFilters;
	}

	private bool ShouldSkipMeshFilter(MeshFilter meshFilter)
	{
		if (meshFilter == null)
		{
			return false;
		}
		Transform parent = meshFilter.transform;
		while (parent != null)
		{
			if (((1 << parent.gameObject.layer) & layersToIgnore.value) != 0)
			{
				return true;
			}
			parent = parent.parent;
		}
		return false;
	}

	private bool ShouldSkipGameObject(GameObject gameObject)
	{
		if (gameObject == null)
		{
			return false;
		}
		Transform parent = gameObject.transform;
		while (parent != null)
		{
			if (gameObjectsToIgnore.Contains(parent.gameObject))
			{
				return true;
			}
			parent = parent.parent;
		}
		return false;
	}

	private void CombineMeshesWithSingleMaterial(bool showCreatedMeshInfo)
	{
		MeshFilter[] meshFiltersToCombine = GetMeshFiltersToCombine();
		if (meshFiltersToCombine.Length <= 1)
		{
			if (showCreatedMeshInfo)
			{
				Debug.LogWarning("No children meshes found to combine.");
			}
			return;
		}
		List<CombineInstance> list = new List<CombineInstance>();
		long num = 0L;
		for (int i = 1; i < meshFiltersToCombine.Length; i++)
		{
			if (meshFiltersToCombine[i] != null && meshFiltersToCombine[i].sharedMesh != null)
			{
				CombineInstance item = new CombineInstance
				{
					subMeshIndex = 0,
					mesh = meshFiltersToCombine[i].sharedMesh,
					transform = meshFiltersToCombine[i].transform.localToWorldMatrix
				};
				list.Add(item);
				num += item.mesh.vertices.Length;
			}
		}
		if (list.Count == 0)
		{
			if (showCreatedMeshInfo)
			{
				Debug.LogWarning("No valid meshes found to combine.");
			}
			return;
		}
		CombineInstance[] array = list.ToArray();
		MeshRenderer[] componentsInChildren = GetComponentsInChildren<MeshRenderer>(combineInactiveChildren);
		if (componentsInChildren.Length >= 2 && componentsInChildren[0] != null)
		{
			if (componentsInChildren[1] != null && componentsInChildren[1].sharedMaterial != null)
			{
				componentsInChildren[0].sharedMaterials = new Material[1];
				componentsInChildren[0].sharedMaterial = componentsInChildren[1].sharedMaterial;
			}
			else
			{
				componentsInChildren[0].sharedMaterials = new Material[0];
			}
		}
		else if (componentsInChildren.Length != 0 && componentsInChildren[0] != null)
		{
			componentsInChildren[0].sharedMaterials = new Material[0];
		}
		Mesh mesh = new Mesh();
		mesh.name = base.name;
		if (num > 65535)
		{
			mesh.indexFormat = IndexFormat.UInt32;
		}
		mesh.CombineMeshes(array);
		GenerateUV(mesh);
		meshFiltersToCombine[0].sharedMesh = mesh;
		DeactivateCombinedGameObjects(meshFiltersToCombine);
		if (showCreatedMeshInfo)
		{
			if (num <= 65535)
			{
				Debug.Log("<color=#00cc00><b>Mesh \"" + base.name + "\" was created from " + array.Length + " children meshes and has " + num + " vertices.</b></color>");
			}
			else
			{
				Debug.Log("<color=#ff3300><b>Mesh \"" + base.name + "\" was created from " + array.Length + " children meshes and has " + num + " vertices. Some old devices, like Android with Mali-400 GPU, do not support over 65535 vertices.</b></color>");
			}
		}
	}

	private void CombineMeshesWithMutliMaterial(bool showCreatedMeshInfo)
	{
		MeshFilter[] meshFiltersToCombine = GetMeshFiltersToCombine();
		if (meshFiltersToCombine.Length <= 1)
		{
			if (showCreatedMeshInfo)
			{
				Debug.LogWarning("No children meshes found to combine.");
			}
			return;
		}
		MeshRenderer[] array = new MeshRenderer[meshFiltersToCombine.Length];
		array[0] = GetComponent<MeshRenderer>();
		List<Material> list = new List<Material>();
		for (int i = 1; i < meshFiltersToCombine.Length; i++)
		{
			if (!(meshFiltersToCombine[i] != null) || !(meshFiltersToCombine[i].sharedMesh != null))
			{
				continue;
			}
			array[i] = meshFiltersToCombine[i].GetComponent<MeshRenderer>();
			if (!(array[i] != null))
			{
				continue;
			}
			Material[] sharedMaterials = array[i].sharedMaterials;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				if (sharedMaterials[j] != null && !list.Contains(sharedMaterials[j]))
				{
					list.Add(sharedMaterials[j]);
				}
			}
		}
		if (list.Count == 0)
		{
			if (showCreatedMeshInfo)
			{
				Debug.LogWarning("No materials found in children meshes to combine.");
			}
			return;
		}
		List<CombineInstance> list2 = new List<CombineInstance>();
		long num = 0L;
		for (int k = 0; k < list.Count; k++)
		{
			List<CombineInstance> list3 = new List<CombineInstance>();
			for (int l = 1; l < meshFiltersToCombine.Length; l++)
			{
				if (!(meshFiltersToCombine[l] != null) || !(meshFiltersToCombine[l].sharedMesh != null) || !(array[l] != null))
				{
					continue;
				}
				Material[] sharedMaterials2 = array[l].sharedMaterials;
				for (int m = 0; m < sharedMaterials2.Length; m++)
				{
					if (sharedMaterials2[m] != null && list[k] == sharedMaterials2[m])
					{
						CombineInstance item = new CombineInstance
						{
							subMeshIndex = m,
							mesh = meshFiltersToCombine[l].sharedMesh,
							transform = meshFiltersToCombine[l].transform.localToWorldMatrix
						};
						list3.Add(item);
						num += item.mesh.vertices.Length;
					}
				}
			}
			Mesh mesh = new Mesh();
			if (num > 65535)
			{
				mesh.indexFormat = IndexFormat.UInt32;
			}
			mesh.CombineMeshes(list3.ToArray(), mergeSubMeshes: true);
			list2.Add(new CombineInstance
			{
				subMeshIndex = 0,
				mesh = mesh,
				transform = Matrix4x4.identity
			});
		}
		if (list2.Count == 0)
		{
			if (showCreatedMeshInfo)
			{
				Debug.LogWarning("No valid meshes found to combine.");
			}
			return;
		}
		if (array[0] != null)
		{
			array[0].sharedMaterials = list.ToArray();
		}
		Mesh mesh2 = new Mesh();
		mesh2.name = base.name;
		if (num > 65535)
		{
			mesh2.indexFormat = IndexFormat.UInt32;
		}
		mesh2.CombineMeshes(list2.ToArray(), mergeSubMeshes: false);
		GenerateUV(mesh2);
		meshFiltersToCombine[0].sharedMesh = mesh2;
		DeactivateCombinedGameObjects(meshFiltersToCombine);
		if (showCreatedMeshInfo)
		{
			int num2 = meshFiltersToCombine.Length - 1;
			if (num <= 65535)
			{
				Debug.Log("<color=#00cc00><b>Mesh \"" + base.name + "\" was created from " + num2 + " children meshes and has " + list2.Count + " submeshes, and " + num + " vertices.</b></color>");
			}
			else
			{
				Debug.Log("<color=#ff3300><b>Mesh \"" + base.name + "\" was created from " + num2 + " children meshes and has " + list2.Count + " submeshes, and " + num + " vertices. Some old devices, like Android with Mali-400 GPU, do not support over 65535 vertices.</b></color>");
			}
		}
	}

	private void DeactivateCombinedGameObjects(MeshFilter[] meshFilters)
	{
		for (int i = 1; i < meshFilters.Length; i++)
		{
			if (meshFilters[i] == null)
			{
				continue;
			}
			if (!destroyCombinedChildren)
			{
				if (deactivateCombinedChildren)
				{
					meshFilters[i].gameObject.SetActive(value: false);
				}
				if (deactivateCombinedChildrenMeshRenderers)
				{
					MeshRenderer component = meshFilters[i].gameObject.GetComponent<MeshRenderer>();
					if (component != null)
					{
						component.enabled = false;
					}
				}
			}
			else
			{
				Object.Destroy(meshFilters[i].gameObject);
			}
		}
	}

	private void GenerateUV(Mesh combinedMesh)
	{
	}
}

}
