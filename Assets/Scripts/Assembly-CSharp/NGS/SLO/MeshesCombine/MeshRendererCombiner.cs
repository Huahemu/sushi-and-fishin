using System.Collections.Generic;
using NGS.SLO.Shared;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
	public class MeshRendererCombiner
	{
		private IMeshCombiner _meshCombiner;

		public MeshRendererCombiner(IMeshCombiner meshCombiner)
		{
			_meshCombiner = meshCombiner;
		}

		public MeshCombineResult CreateCombinedObject(IList<MeshCombineInstance> instances, MeshCombineOptions options, out GameObject combinedGO)
		{
			MeshCombineResult result = _meshCombiner.Combine(instances);
			if (!result.success)
			{
				combinedGO = null;
				return result;
			}
			if (options.useLightweightBuffers)
			{
				VertexBufferUtil.ToLightweightBuffer(result.combinedMesh);
			}
			combinedGO = new GameObject("Combined_MeshRenderer");
			combinedGO.isStatic = instances[0].renderer.gameObject.isStatic;
			combinedGO.gameObject.layer = instances[0].renderer.gameObject.layer;
			MeshRenderer meshRenderer = combinedGO.AddComponent<MeshRenderer>();
			MeshFilter meshFilter = combinedGO.AddComponent<MeshFilter>();
			MeshRenderer meshRenderer2 = instances[0].renderer as MeshRenderer;
			int submeshIndex = instances[0].submeshIndex;
			meshRenderer.sharedMaterial = meshRenderer2.GetSharedMaterial(submeshIndex);
			meshFilter.sharedMesh = result.combinedMesh;
			CopyMeshRendererSettings(meshRenderer2, meshRenderer);
			if (options.rebuildUV2)
			{
				meshRenderer.lightmapIndex = -1;
			}
			return result;
		}

		private void CopyMeshRendererSettings(MeshRenderer source, MeshRenderer destination)
		{
			destination.enabled = source.enabled;
			destination.shadowCastingMode = source.shadowCastingMode;
			destination.receiveShadows = source.receiveShadows;
			destination.motionVectorGenerationMode = source.motionVectorGenerationMode;
			destination.lightProbeUsage = source.lightProbeUsage;
			destination.reflectionProbeUsage = source.reflectionProbeUsage;
			destination.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
			destination.renderingLayerMask = source.renderingLayerMask;
			destination.lightmapIndex = source.lightmapIndex;
			destination.lightmapScaleOffset = new Vector4(1f, 1f, 0f, 0f);
		}
	}
}
