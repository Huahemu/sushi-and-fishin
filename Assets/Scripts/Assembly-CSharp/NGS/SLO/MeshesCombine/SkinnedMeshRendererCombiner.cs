using System.Collections.Generic;
using NGS.SLO.Shared;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
	public class SkinnedMeshRendererCombiner
	{
		private ISkinnedMeshCombiner _skinnedMeshCombiner;

		public SkinnedMeshRendererCombiner(ISkinnedMeshCombiner meshCombiner)
		{
			_skinnedMeshCombiner = meshCombiner;
		}

		public MeshCombineResult CreateCombinedObject(IList<MeshCombineInstance> instances, MeshCombineOptions options, out GameObject combinedGO)
		{
			Transform[] bones = null;
			MeshCombineResult result = _skinnedMeshCombiner.Combine(instances, out bones);
			if (!result.success)
			{
				combinedGO = null;
				return result;
			}
			combinedGO = new GameObject("Combined_SkinnedMeshRenderer");
			combinedGO.gameObject.layer = instances[0].renderer.gameObject.layer;
			SkinnedMeshRenderer skinnedMeshRenderer = combinedGO.AddComponent<SkinnedMeshRenderer>();
			skinnedMeshRenderer.sharedMesh = result.combinedMesh;
			SkinnedMeshRenderer skinnedMeshRenderer2 = instances[0].renderer as SkinnedMeshRenderer;
			int submeshIndex = instances[0].submeshIndex;
			skinnedMeshRenderer.bones = bones;
			skinnedMeshRenderer.sharedMaterial = skinnedMeshRenderer2.GetSharedMaterial(submeshIndex);
			skinnedMeshRenderer.sharedMesh = result.combinedMesh;
			CopySkinnedMeshRendererSettings(skinnedMeshRenderer2, skinnedMeshRenderer);
			return result;
		}

		private void CopySkinnedMeshRendererSettings(SkinnedMeshRenderer source, SkinnedMeshRenderer destination)
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
			destination.quality = source.quality;
			destination.updateWhenOffscreen = source.updateWhenOffscreen;
			destination.skinnedMotionVectors = source.skinnedMotionVectors;
		}
	}
}
