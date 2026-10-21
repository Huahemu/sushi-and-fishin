using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DynamicMeshCutter
{
	public static class MeshCreation
	{
		private static float _ragdoll_vertex_threshold = 0.75f;

		public static MeshCreationData CreateObjects(Info info, Material defaultMaterial, int vertexCreationThreshold)
		{
			if (info.MeshTarget == null)
			{
				return null;
			}
			VirtualMesh[] createdMeshes = info.CreatedMeshes;
			MeshCreationData meshCreationData = new MeshCreationData(createdMeshes.Length);
			MeshTarget meshTarget = info.MeshTarget;
			Material[] array = GetMaterials(meshTarget.gameObject);
			if (meshTarget.CreateCutFaces)
			{
				Material[] array2 = new Material[array.Length + 1];
				array.CopyTo(array2, 0);
				array2[^1] = ((meshTarget.FaceMaterial != null) ? meshTarget.FaceMaterial : defaultMaterial);
				array = array2;
			}
			for (int i = 0; i < createdMeshes.Length; i++)
			{
				if (createdMeshes[i].Vertices.Length < vertexCreationThreshold)
				{
					continue;
				}
				int num = info.BT[i];
				Transform parent = null;
				GameObject root = null;
				VirtualMesh virtualMesh = createdMeshes[i];
				Mesh mesh = new Mesh();
				mesh.vertices = virtualMesh.Vertices;
				mesh.triangles = virtualMesh.Triangles;
				mesh.normals = virtualMesh.Normals;
				mesh.uv = virtualMesh.UVs;
				mesh.subMeshCount = virtualMesh.SubMeshCount;
				for (int j = 0; j < virtualMesh.SubMeshCount; j++)
				{
					mesh.SetIndices(virtualMesh.GetIndices(j), MeshTopology.Triangles, j);
				}
				Behaviour behaviour = meshTarget.DefaultBehaviour[num];
				if (virtualMesh.DynamicGroups != null)
				{
					int[] array3 = new int[virtualMesh.DynamicGroups.Keys.Count];
					int num2 = 0;
					foreach (int key in virtualMesh.DynamicGroups.Keys)
					{
						array3[num2++] = key;
					}
					for (int k = 0; k < meshTarget.GroupBehaviours.Count; k++)
					{
						if (meshTarget.GroupBehaviours[k].Passes(array3))
						{
							behaviour = meshTarget.GroupBehaviours[k].Behaviour;
							break;
						}
					}
				}
				switch (behaviour)
				{
				case Behaviour.Stone:
					CreateMesh(ref root, ref parent, meshTarget, mesh, virtualMesh, array, num);
					break;
				case Behaviour.Ragdoll:
				{
					DynamicRagdoll dynamicRagdoll = meshTarget.DynamicRagdoll;
					if (dynamicRagdoll != null && virtualMesh.DynamicGroups.Count > 1)
					{
						if (WillBeValidRagdoll(dynamicRagdoll, virtualMesh))
						{
							CreateRagdoll(ref root, ref parent, info, meshTarget, mesh, virtualMesh, array, num, behaviour);
						}
						else
						{
							CreateMesh(ref root, ref parent, meshTarget, mesh, virtualMesh, array, num, forcePhysics: true);
						}
					}
					else
					{
						CreateMesh(ref root, ref parent, meshTarget, mesh, virtualMesh, array, num, forcePhysics: true);
					}
					break;
				}
				case Behaviour.Animation:
					if (meshTarget.Animator != null)
					{
						CreateAnimatedMesh(ref root, ref parent, info, meshTarget, mesh, virtualMesh, array, num, behaviour);
						break;
					}
					Debug.LogWarning("Beahviour is set to Animation, but there was no Animator found in parent!");
					CreateMesh(ref root, ref parent, meshTarget, mesh, virtualMesh, array, num, forcePhysics: true);
					break;
				}
				string text = $"({i}/{createdMeshes.Length})";
				parent.name = text + parent.name;
				parent.name = parent.name.Replace("(Clone)", "");
				MeshTarget meshTarget2 = root.GetComponent<MeshTarget>();
				if (meshTarget2 == null)
				{
					meshTarget2 = root.AddComponent<MeshTarget>();
				}
				meshTarget2.GameobjectRoot = parent.gameObject;
				meshTarget2.OverrideFaceMaterial = meshTarget.OverrideFaceMaterial;
				meshTarget2.CreateCutFaces = meshTarget.CreateCutFaces;
				meshTarget2.SeparateMeshes = meshTarget.SeparateMeshes;
				meshTarget2.ApplyTranslation = meshTarget.ApplyTranslation;
				meshTarget2.GroupBehaviours = meshTarget.GroupBehaviours;
				meshTarget2.transform.localScale = meshTarget.transform.localScale;
				if (meshTarget.Inherit[num])
				{
					for (int l = 0; l < 2; l++)
					{
						meshTarget2.DefaultBehaviour[l] = meshTarget.DefaultBehaviour[l];
						meshTarget2.CreateRigidbody[l] = meshTarget.CreateRigidbody[l];
						meshTarget2.CreateMeshCollider[l] = meshTarget.CreateMeshCollider[l];
						meshTarget2.Physics[l] = meshTarget.Physics[l];
						meshTarget2.Inherit[l] = meshTarget.Inherit[l];
					}
				}
				else
				{
					for (int m = 0; m < 2; m++)
					{
						meshTarget2.DefaultBehaviour[m] = meshTarget.DefaultBehaviour[num];
						meshTarget2.CreateRigidbody[m] = meshTarget.CreateRigidbody[num];
						meshTarget2.Physics[m] = meshTarget.Physics[num];
						meshTarget2.CreateMeshCollider[m] = meshTarget.CreateMeshCollider[num];
						meshTarget2.Inherit[m] = false;
					}
				}
				meshCreationData.CreatedObjects[i] = parent.gameObject;
				meshCreationData.CreatedTargets[i] = meshTarget2;
			}
			return meshCreationData;
		}

		private static void CreateMesh(ref GameObject root, ref Transform parent, MeshTarget target, Mesh mesh, VirtualMesh vMesh, Material[] materials, int bt, bool forcePhysics = false)
		{
			parent = new GameObject(target.GameobjectRoot.name ?? "").transform;
			parent.transform.rotation = target.transform.rotation;
			parent.transform.position = target.transform.position;
			parent.gameObject.tag = target.GameobjectRoot.tag;
			root = new GameObject(target.gameObject.name ?? "");
			root.transform.position = target.transform.position;
			root.transform.rotation = target.transform.rotation;
			root.gameObject.tag = target.transform.tag;
			MeshFilter meshFilter = root.AddComponent<MeshFilter>();
			MeshRenderer meshRenderer = root.AddComponent<MeshRenderer>();
			meshFilter.mesh = mesh;
			meshRenderer.sharedMaterials = materials;
			Vector3 center = meshRenderer.bounds.center;
			parent.transform.position = center;
			root.transform.SetParent(parent, worldPositionStays: true);
			if (target.CreateRigidbody[bt])
			{
				Rigidbody rigidbody = parent.gameObject.AddComponent<Rigidbody>();
				rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
				rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
			}
			if (target.CreateMeshCollider[bt] && (vMesh.UniqueVerticesCount < 0 || (vMesh.UniqueVerticesCount > 3 && vMesh.Vertices.Length > 20)))
			{
				root.AddComponent<MeshCollider>().convex = true;
			}
		}

		private static bool WillBeValidRagdoll(DynamicRagdoll ragdoll, VirtualMesh vMesh)
		{
			foreach (int key in ragdoll.Parts.Keys)
			{
				if (vMesh.DynamicGroups.ContainsKey(key))
				{
					DynamicRagdollPart dynamicRagdollPart = ragdoll.Parts[key];
					float num = (float)vMesh.DynamicGroups[key].Length / (float)dynamicRagdollPart.Vertices.Length;
					if (dynamicRagdollPart.Colliders.Length != 0 && num > _ragdoll_vertex_threshold)
					{
						return true;
					}
				}
			}
			return false;
		}

		private static void TrimRagdoll(DynamicRagdoll ragdoll, MeshTarget target, VirtualMesh vMesh)
		{
			ragdoll.Assignments = vMesh.Assignments;
			int[] array = new int[ragdoll.Parts.Keys.Count];
			int num = 0;
			foreach (int key2 in ragdoll.Parts.Keys)
			{
				array[num++] = key2;
			}
			foreach (int key in array)
			{
				DynamicRagdollPart dynamicRagdollPart = ragdoll.Parts[key];
				if (vMesh.DynamicGroups.ContainsKey(key))
				{
					Vector3[] array2 = vMesh.DynamicGroups[key];
					float num2 = (float)array2.Length / (float)dynamicRagdollPart.Vertices.Length;
					if (dynamicRagdollPart.Colliders.Length == 0 || !(num2 > _ragdoll_vertex_threshold))
					{
						for (int j = 0; j < dynamicRagdollPart.Colliders.Length; j++)
						{
							Object.DestroyImmediate(dynamicRagdollPart.Colliders[j]);
						}
						dynamicRagdollPart.Colliders = new Collider[0];
					}
					dynamicRagdollPart.Vertices = array2;
					continue;
				}
				if (dynamicRagdollPart.Joint != null)
				{
					Object.DestroyImmediate(dynamicRagdollPart.Joint);
				}
				if (dynamicRagdollPart.Rigidbody != null)
				{
					Object.DestroyImmediate(dynamicRagdollPart.Rigidbody);
				}
				if (dynamicRagdollPart.Colliders != null)
				{
					for (int k = 0; k < dynamicRagdollPart.Colliders.Length; k++)
					{
						Object.DestroyImmediate(dynamicRagdollPart.Colliders[k]);
					}
				}
				Object.DestroyImmediate(dynamicRagdollPart);
				ragdoll.Parts.Remove(key);
			}
		}

		private static void CreateRagdoll(ref GameObject root, ref Transform parent, Info info, MeshTarget target, Mesh mesh, VirtualMesh vMesh, Material[] materials, int bt, Behaviour behaviour)
		{
			Transform transform = CreateSkinnedMeshRenderer(ref root, ref parent, info, target, mesh, vMesh, materials, bt, behaviour);
			parent.transform.position = target.GameobjectRoot.transform.position;
			parent.transform.rotation = target.GameobjectRoot.transform.rotation;
			DynamicRagdoll component = parent.GetComponent<DynamicRagdoll>();
			List<DynamicRagdollPart> list = component.Parts.Values.ToList();
			if (list.Count == 0)
			{
				Debug.LogError("This shouldnt happen. (Bugreport: Parts of ragdoll is 0)");
			}
			List<DynamicRagdollPart> list2 = new List<DynamicRagdollPart>();
			List<DynamicRagdollPart> list3 = component.Parts.Values.ToList();
			while (list3.Count > 0)
			{
				DynamicRagdollPart dynamicRagdollPart = list3[0];
				DynamicRagdollPart[] componentsInChildren = list3[0].GetComponentsInChildren<DynamicRagdollPart>();
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					if (list.Contains(componentsInChildren[i]))
					{
						list3.Remove(componentsInChildren[i]);
					}
				}
				DynamicRagdollPart componentInParentIgnoreSelf = dynamicRagdollPart.GetComponentInParentIgnoreSelf<DynamicRagdollPart>();
				if (componentInParentIgnoreSelf != null && list.Contains(componentInParentIgnoreSelf))
				{
					list3.Remove(dynamicRagdollPart);
					continue;
				}
				list3.Remove(dynamicRagdollPart);
				list2.Add(dynamicRagdollPart);
			}
			Transform[] componentsInChildren2 = transform.transform.GetComponentsInChildren<Transform>(includeInactive: true);
			List<Transform> list4 = new List<Transform>();
			for (int j = 0; j < componentsInChildren2.Length; j++)
			{
				list4.Add(componentsInChildren2[j]);
			}
			foreach (DynamicRagdollPart item in list2)
			{
				item.transform.SetParent(parent);
				Transform[] componentsInChildren3 = item.transform.GetComponentsInChildren<Transform>(includeInactive: true);
				for (int k = 0; k < componentsInChildren3.Length; k++)
				{
					list4.Remove(componentsInChildren3[k]);
				}
			}
			for (int l = 0; l < list4.Count; l++)
			{
				DynamicRagdollPart dynamicRagdollPart2 = list2[0];
				for (int m = 1; m < list2.Count; m++)
				{
					if (Vector3.Distance(list2[m].transform.position, list4[l].position) < Vector3.Distance(dynamicRagdollPart2.transform.position, target.transform.position))
					{
						dynamicRagdollPart2 = list2[m];
					}
				}
				list4[l].SetParent(dynamicRagdollPart2.transform);
			}
			if (list2.Count > 1)
			{
				for (int n = 0; n < list2.Count - 1; n++)
				{
					list2[n].Joint.connectedBody = list2[n + 1].Rigidbody;
				}
			}
			bool flag = false;
			for (int num = 0; num < list.Count; num++)
			{
				if (!flag && list[num].Colliders.Length != 0)
				{
					flag = true;
				}
				if (!(list[num].Joint == null) && list[num].Joint.connectedBody == null)
				{
					Rigidbody componentInParentIgnoreSelf2 = list[num].GetComponentInParentIgnoreSelf<Rigidbody>();
					if (componentInParentIgnoreSelf2 != null)
					{
						list[num].Joint.connectedBody = componentInParentIgnoreSelf2;
					}
					else if (!list2.Contains(list[num]))
					{
						Debug.LogError("false");
					}
				}
			}
			if (!flag)
			{
				Debug.LogError("Dynamic Ragdoll has no more collider");
			}
			switch (target.Physics[bt])
			{
			case RagdollPhysics.NonKinematic:
				component.SetRagdollKinematic(value: false);
				break;
			case RagdollPhysics.Kinematic:
				component.SetRagdollKinematic(value: true);
				break;
			case RagdollPhysics.LeaveAsIs:
				break;
			}
		}

		private static void CreateAnimatedMesh(ref GameObject root, ref Transform parent, Info info, MeshTarget target, Mesh mesh, VirtualMesh vMesh, Material[] materials, int bt, Behaviour behaviour)
		{
			Animator animator = target.Animator;
			if (target.IsSkinned)
			{
				CreateSkinnedMeshRenderer(ref root, ref parent, info, target, mesh, vMesh, materials, bt, behaviour);
			}
			else
			{
				parent = Object.Instantiate(target.Animator.gameObject).transform;
				root = parent.GetComponentInChildren<MeshTarget>().gameObject;
				MeshFilter component = root.GetComponent<MeshFilter>();
				MeshRenderer component2 = root.GetComponent<MeshRenderer>();
				component.mesh = mesh;
				component2.sharedMaterials = materials;
			}
			parent.transform.position = animator.transform.position;
			parent.transform.rotation = animator.transform.rotation;
			AnimatorStateInfo currentAnimatorStateInfo = animator.GetCurrentAnimatorStateInfo(0);
			Animator component3 = parent.gameObject.GetComponent<Animator>();
			component3.runtimeAnimatorController = animator.runtimeAnimatorController;
			component3.avatar = animator.avatar;
			component3.applyRootMotion = animator.applyRootMotion;
			component3.updateMode = animator.updateMode;
			component3.cullingMode = animator.cullingMode;
			component3.Play(currentAnimatorStateInfo.fullPathHash, 0, currentAnimatorStateInfo.normalizedTime);
		}

		public static Transform CreateSkinnedMeshRenderer(ref GameObject meshRoot, ref Transform parent, Info info, MeshTarget target, Mesh mesh, VirtualMesh vMesh, Material[] materials, int bt, Behaviour behaviour)
		{
			parent = Object.Instantiate(target.GameobjectRoot).transform;
			SkinnedMeshRenderer componentInChildren = parent.GetComponentInChildren<SkinnedMeshRenderer>();
			meshRoot = componentInChildren.gameObject;
			Transform rootBone = componentInChildren.rootBone;
			if (target.DynamicRagdoll != null)
			{
				TrimRagdoll(parent.GetComponent<DynamicRagdoll>(), target, vMesh);
			}
			if (target.Animator != null)
			{
				Animator component = parent.GetComponent<Animator>();
				if (behaviour != Behaviour.Animation)
				{
					Object.DestroyImmediate(component);
				}
			}
			mesh.bindposes = info.Bindposes;
			mesh.boneWeights = vMesh.BoneWeights;
			componentInChildren.sharedMesh = mesh;
			componentInChildren.sharedMaterials = materials;
			return rootBone;
		}

		public static void TranslateCreatedObjects(Info info, GameObject[] createdObjects, MeshTarget[] targets, float separation)
		{
			if (createdObjects == null)
			{
				return;
			}
			VirtualPlane plane = info.Plane;
			for (int i = 0; i < createdObjects.Length; i++)
			{
				if (!(createdObjects[i] == null) && !(targets[i] == null) && targets[i].ApplyTranslation)
				{
					GameObject obj = createdObjects[i];
					int num = 1;
					if (info.Sides[i] == 1)
					{
						num = -1;
					}
					Vector3 vector = num * plane.WorldNormal.normalized * separation;
					obj.transform.position += vector;
				}
			}
		}

		public static Material[] GetMaterials(GameObject target)
		{
			MeshRenderer component = target.GetComponent<MeshRenderer>();
			if (component != null)
			{
				return component.sharedMaterials;
			}
			SkinnedMeshRenderer component2 = target.GetComponent<SkinnedMeshRenderer>();
			if (component2 != null)
			{
				return component2.sharedMaterials;
			}
			return null;
		}

		public static T GetComponentInParentIgnoreSelf<T>(this Component target, bool includeInactive = false) where T : Component
		{
			Component[] componentsInParent = target.GetComponentsInParent<T>(includeInactive);
			componentsInParent = componentsInParent;
			foreach (Component component in componentsInParent)
			{
				if (component.transform.gameObject != target.transform.gameObject)
				{
					return component as T;
				}
			}
			return null;
		}

		public static void GetMeshInfo(MeshTarget target, out Mesh outMesh, out Matrix4x4[] outBindposes)
		{
			MeshFilter component = target.GetComponent<MeshFilter>();
			if (component != null)
			{
				outMesh = component.mesh;
				outBindposes = new Matrix4x4[0];
				return;
			}
			SkinnedMeshRenderer component2 = target.GetComponent<SkinnedMeshRenderer>();
			if (component2 != null)
			{
				Mesh mesh = new Mesh();
				component2.BakeMesh(mesh);
				mesh.boneWeights = component2.sharedMesh.boneWeights;
				outMesh = mesh;
				Matrix4x4 inverse = Matrix4x4.Scale(target.transform.localScale).inverse;
				outBindposes = new Matrix4x4[component2.bones.Length];
				bool flag = false;
				for (int i = 0; i < component2.bones.Length; i++)
				{
					if (component2.bones[i] == null)
					{
						flag = true;
						break;
					}
					outBindposes[i] = component2.bones[i].worldToLocalMatrix * target.transform.localToWorldMatrix * inverse;
				}
				if (flag)
				{
					outBindposes = new Matrix4x4[0];
				}
			}
			else
			{
				outMesh = null;
				outBindposes = new Matrix4x4[0];
			}
		}
	}
}
