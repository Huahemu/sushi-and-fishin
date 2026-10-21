using System;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
	[RequireComponent(typeof(Renderer))]
	public class MaterialCombineSourceBackup : MonoBehaviour
	{
		[SerializeField]
		private Mesh _storedMesh;

		[SerializeField]
		private Material[] _storedMaterials;

		[SerializeField]
		private GameObject _backupObject;

		[SerializeField]
		public BackupOption _backupOption;

		[SerializeField]
		private bool _backupCreated;

		public bool BackupCreated => _backupCreated;

		public BackupOption BackupOption
		{
			get
			{
				return _backupOption;
			}
			set
			{
				if (BackupCreated)
				{
					Debug.Log("Can't change backup option since backup already created. Revert first");
				}
				else
				{
					_backupOption = value;
				}
			}
		}

		public void CreateBackup()
		{
			if (_backupCreated)
			{
				Debug.Log("MaterialCombineSourceBackup::" + base.gameObject.name + " backup already created. Revert first");
				return;
			}
			if (!ReadyToCreateBackup(out var failureMessage))
			{
				Debug.Log("MaterialCombineSourceBackup::" + base.gameObject.name + " can't gather data: " + failureMessage);
				return;
			}
			if (_backupOption == BackupOption.StoreInComponent)
			{
				StoreDataInComponent();
				_backupCreated = true;
				return;
			}
			if (_backupOption == BackupOption.CreateBackupObject)
			{
				CreateBackupObject();
				_backupCreated = true;
				return;
			}
			throw new ArgumentException("MaterialCombineSourceBackup::unknown BackupOption");
		}

		public void RevertMaterial(Material revertMaterial)
		{
			if (!CheckBackup(out var failureMessage))
			{
				Debug.Log("MaterialCombineSourceBackup::" + base.gameObject.name + " can't revert material: " + failureMessage);
				DestroyBackup();
				_backupCreated = false;
				return;
			}
			Renderer component = GetComponent<Renderer>();
			Material[] array = null;
			Mesh source = null;
			Mesh destination = null;
			if (_backupOption == BackupOption.StoreInComponent)
			{
				source = _storedMesh;
				array = _storedMaterials;
				if (component is SkinnedMeshRenderer)
				{
					destination = (component as SkinnedMeshRenderer).sharedMesh;
				}
				else if (component is MeshRenderer)
				{
					destination = component.GetComponent<MeshFilter>().sharedMesh;
				}
			}
			else if (_backupOption == BackupOption.CreateBackupObject)
			{
				array = _backupObject.GetComponent<Renderer>().sharedMaterials;
				if (component is SkinnedMeshRenderer)
				{
					source = _backupObject.GetComponent<SkinnedMeshRenderer>().sharedMesh;
					destination = (component as SkinnedMeshRenderer).sharedMesh;
				}
				else if (component is MeshRenderer)
				{
					source = _backupObject.GetComponent<MeshFilter>().sharedMesh;
					destination = component.GetComponent<MeshFilter>().sharedMesh;
				}
			}
			Material[] sharedMaterials = component.sharedMaterials;
			for (int i = 0; i < sharedMaterials.Length; i++)
			{
				if (sharedMaterials[i] == revertMaterial)
				{
					CopyUV(source, destination, i);
					sharedMaterials[i] = array[i];
					Debug.Log("Material " + array[i].name + " reverted");
				}
			}
			component.sharedMaterials = sharedMaterials;
		}

		public void RevertAll()
		{
			if (!CheckBackup(out var failureMessage))
			{
				Debug.Log("MaterialCombineSourceBackup::" + base.gameObject.name + " can't revert all: " + failureMessage);
				DestroyBackup();
				_backupCreated = false;
				return;
			}
			Renderer component = GetComponent<Renderer>();
			if (_backupOption == BackupOption.StoreInComponent)
			{
				component.sharedMaterials = _storedMaterials;
				if (component is SkinnedMeshRenderer)
				{
					(component as SkinnedMeshRenderer).sharedMesh = _storedMesh;
				}
				else if (component is MeshRenderer)
				{
					component.GetComponent<MeshFilter>().sharedMesh = _storedMesh;
				}
			}
			else if (_backupOption == BackupOption.CreateBackupObject)
			{
				component.sharedMaterials = _backupObject.GetComponent<Renderer>().sharedMaterials;
				if (component is SkinnedMeshRenderer)
				{
					(component as SkinnedMeshRenderer).sharedMesh = _backupObject.GetComponent<SkinnedMeshRenderer>().sharedMesh;
				}
				else if (component is MeshRenderer)
				{
					component.GetComponent<MeshFilter>().sharedMesh = _backupObject.GetComponent<MeshFilter>().sharedMesh;
				}
			}
			Debug.Log("'" + base.gameObject.name + "' mesh and materials reverted");
		}

		public void DestroyBackup()
		{
			if (_backupObject != null)
			{
				UnityEngine.Object.DestroyImmediate(_backupObject);
			}
			_storedMesh = null;
			_storedMaterials = null;
			_backupCreated = false;
		}

		private bool ReadyToCreateBackup(out string failureMessage)
		{
			Renderer component = GetComponent<Renderer>();
			if (component is MeshRenderer)
			{
				MeshFilter component2 = component.GetComponent<MeshFilter>();
				if (component2 == null)
				{
					failureMessage = "MeshFilter is missing";
					return false;
				}
				if (component2.sharedMesh == null)
				{
					failureMessage = "Mesh is missing";
					return false;
				}
				failureMessage = "";
				return true;
			}
			if (component is SkinnedMeshRenderer)
			{
				if ((component as SkinnedMeshRenderer).sharedMesh == null)
				{
					failureMessage = "Mesh is missing";
					return false;
				}
				failureMessage = "";
				return true;
			}
			failureMessage = "Unsupported renderer type";
			return false;
		}

		private bool CheckBackup(out string failureMessage)
		{
			if (!BackupCreated)
			{
				failureMessage = "Backup Not Created";
				return false;
			}
			if (_backupOption == BackupOption.StoreInComponent)
			{
				if (_storedMesh == null)
				{
					failureMessage = "Stored Mesh Is Missed";
					return false;
				}
				if (_storedMaterials == null)
				{
					failureMessage = "Stored Materials Is Missed";
					return false;
				}
			}
			else if (_backupOption == BackupOption.CreateBackupObject && _backupObject == null)
			{
				failureMessage = "Backup Object Is Missed";
				return false;
			}
			failureMessage = "";
			return true;
		}

		private void StoreDataInComponent()
		{
			Renderer component = GetComponent<Renderer>();
			_storedMaterials = component.sharedMaterials;
			if (component is MeshRenderer)
			{
				_storedMesh = component.GetComponent<MeshFilter>().sharedMesh;
			}
			else if (component is SkinnedMeshRenderer)
			{
				_storedMesh = (component as SkinnedMeshRenderer).sharedMesh;
			}
		}

		private void CreateBackupObject()
		{
			_backupObject = new GameObject(base.gameObject.name + "_Backup");
			_backupObject.transform.SetParent(base.transform, worldPositionStays: false);
			_backupObject.transform.SetPositionAndRotation(base.transform.position, base.transform.rotation);
			_backupObject.transform.localScale = Vector3.one;
			Renderer component = GetComponent<Renderer>();
			if (component is MeshRenderer)
			{
				MeshRenderer meshRenderer = _backupObject.AddComponent<MeshRenderer>();
				MeshFilter meshFilter = _backupObject.AddComponent<MeshFilter>();
				MeshFilter component2 = component.GetComponent<MeshFilter>();
				meshFilter.sharedMesh = component2.sharedMesh;
				meshRenderer.sharedMaterials = component.sharedMaterials;
				CopyCommonRendererProperties(component, meshRenderer);
			}
			else
			{
				if (!(component is SkinnedMeshRenderer))
				{
					Debug.LogError("CreateBackupObject: Unsupported renderer type: " + component.GetType());
					return;
				}
				SkinnedMeshRenderer skinnedMeshRenderer = component as SkinnedMeshRenderer;
				SkinnedMeshRenderer skinnedMeshRenderer2 = _backupObject.AddComponent<SkinnedMeshRenderer>();
				skinnedMeshRenderer2.sharedMesh = skinnedMeshRenderer.sharedMesh;
				skinnedMeshRenderer2.sharedMaterials = skinnedMeshRenderer.sharedMaterials;
				skinnedMeshRenderer2.bones = skinnedMeshRenderer.bones;
				skinnedMeshRenderer2.rootBone = skinnedMeshRenderer.rootBone;
				CopyCommonRendererProperties(skinnedMeshRenderer, skinnedMeshRenderer2);
				skinnedMeshRenderer2.updateWhenOffscreen = skinnedMeshRenderer.updateWhenOffscreen;
			}
			_backupObject.SetActive(value: false);
		}

		private void CopyCommonRendererProperties(Renderer source, Renderer destination)
		{
			destination.shadowCastingMode = source.shadowCastingMode;
			destination.receiveShadows = source.receiveShadows;
			destination.motionVectorGenerationMode = source.motionVectorGenerationMode;
			destination.lightProbeUsage = source.lightProbeUsage;
			destination.reflectionProbeUsage = source.reflectionProbeUsage;
			destination.sortingLayerID = source.sortingLayerID;
			destination.sortingLayerName = source.sortingLayerName;
			destination.sortingOrder = source.sortingOrder;
			destination.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
			destination.lightmapIndex = source.lightmapIndex;
			destination.realtimeLightmapIndex = source.realtimeLightmapIndex;
			destination.lightmapScaleOffset = source.lightmapScaleOffset;
			destination.realtimeLightmapScaleOffset = source.realtimeLightmapScaleOffset;
		}

		private void CopyUV(Mesh source, Mesh destination, int subMeshIndex)
		{
			int[] indices = source.GetIndices(subMeshIndex);
			Vector2[] uv = source.uv;
			Vector2[] uv2 = destination.uv;
			foreach (int num in indices)
			{
				uv2[num] = uv[num];
			}
			destination.uv = uv2;
		}
	}
}
