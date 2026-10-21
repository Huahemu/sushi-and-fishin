using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Bozo.ModularCharacters
{
	public class DecalBaker : MonoBehaviour
	{
		public OutfitSystem system;

		public Outfit outfit;

		public SkinnedMeshRenderer smr;

		public Transform anchorProjector;

		public Transform axisProjector;

		public Transform oriProjector;

		public Transform decalProjector;

		public Material material;

		public List<Mesh> bakedMeshes = new List<Mesh>();

		public Vector3 postion;

		public Quaternion rotation;

		public Vector3 scale;

		public Dictionary<string, Transform> bones;

		public List<DecalData> decalDatas = new List<DecalData>();

		public bool debug = true;

		public UnityAction<Outfit> onDecalChanged;

		public bool initalized;

		public void Init()
		{
			system = GetComponentInParent<OutfitSystem>();
			outfit = GetComponent<Outfit>();
			bones = system.GetBones();
			smr = outfit.skinnedRenderer;
			if (!(outfit == null) && !(system == null))
			{
				anchorProjector = new GameObject("Anchor" + decalProjector).transform;
				axisProjector = new GameObject("Anchor Axis" + axisProjector).transform;
				oriProjector = new GameObject("Projector Orientation" + oriProjector).transform;
				decalProjector = new GameObject("Projector Decal" + decalProjector).transform;
				decalProjector.parent = oriProjector;
				oriProjector.parent = axisProjector;
				axisProjector.parent = anchorProjector;
				anchorProjector.parent = base.transform;
				material = new Material(Shader.Find("Hidden/BoZo_Decal"));
				material.SetTexture("_SourceTex", outfit.material.mainTexture);
				while (bakedMeshes.Count < outfit.skinnedRenderers.Length)
				{
					Mesh mesh = new Mesh();
					mesh.name = "Runtime Decal Bake Mesh";
					bakedMeshes.Add(mesh);
				}
				initalized = true;
			}
		}

		public void BakeDecal(RenderTexture rt, RenderTexture crt, DecalData data)
		{
			if (!initalized)
			{
				Init();
			}
			SetPosition(data);
			Matrix4x4 worldToLocalMatrix = decalProjector.worldToLocalMatrix;
			material.SetTexture("_BaseTex", rt);
			material.SetMatrix("_DecalWorldToLocal", worldToLocalMatrix);
			material.SetTexture("_DecalTex", data.decalTexture);
			material.SetColor("_Color", data.color);
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = rt;
			material.SetPass(0);
			SkinnedMeshRenderer[] skinnedRenderers = outfit.skinnedRenderers;
			for (int i = 0; i < skinnedRenderers.Length; i++)
			{
				SkinnedMeshRenderer skinnedMeshRenderer = skinnedRenderers[i];
				if (!(skinnedMeshRenderer == null) && !(skinnedMeshRenderer.sharedMesh == null))
				{
					Mesh mesh = bakedMeshes[i];
					skinnedMeshRenderer.BakeMesh(mesh);
					Graphics.DrawMeshNow(mesh, skinnedMeshRenderer.transform.localToWorldMatrix);
				}
			}
			Graphics.Blit(rt, crt);
			RenderTexture.active = active;
		}

		private void OnDestroy()
		{
			if (anchorProjector == null)
			{
				return;
			}
			Object.Destroy(anchorProjector.gameObject);
			foreach (Mesh bakedMesh in bakedMeshes)
			{
				if ((bool)bakedMesh)
				{
					Object.Destroy(bakedMesh);
				}
			}
		}

		public void SetPosition(DecalData data)
		{
			SetParent(data.parent);
			if (data.parent.EndsWith("_r"))
			{
				data.pos.z = 0f - data.pos.z;
			}
			decalProjector.localPosition = new Vector3(data.pos.x, data.pos.y, data.pos.z);
			decalProjector.localRotation = Quaternion.AngleAxis(data.roll, Vector3.forward);
			decalProjector.localScale = new Vector3(data.scale, data.scale, 0.15f);
			Vector3 one = Vector3.one;
			one = ((!(data.parent == "head")) ? new Vector3(data.yaw, 0f, data.pitch) : new Vector3(data.pitch, data.yaw));
			axisProjector.localRotation = Quaternion.Euler(one);
		}

		public void SetParent(string boneName)
		{
			anchorProjector.localScale = Vector3.one;
			oriProjector.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			axisProjector.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			decalProjector.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			anchorProjector.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			Transform transform = bones[boneName];
			anchorProjector.parent = transform;
			anchorProjector.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			anchorProjector.localScale = Vector3.one;
			Vector3 vector = decalProjector.up;
			if (boneName.EndsWith("_r"))
			{
				vector = -vector;
			}
			if (boneName == "head")
			{
				vector = -Vector3.forward;
			}
			oriProjector.position = transform.position + -vector * 0.05f;
			oriProjector.LookAt(anchorProjector);
		}

		public void SetBoundsPosition(Vector3 pos)
		{
			SkinnedMeshRenderer[] skinnedRenderers = outfit.skinnedRenderers;
			foreach (SkinnedMeshRenderer obj in skinnedRenderers)
			{
				Bounds localBounds = obj.localBounds;
				localBounds.center = new Vector3(0f, 0f, pos.z * 2f);
				localBounds.extents += new Vector3(0.5f, 0.5f, 0.5f);
				obj.localBounds = localBounds;
			}
		}
	}
}
