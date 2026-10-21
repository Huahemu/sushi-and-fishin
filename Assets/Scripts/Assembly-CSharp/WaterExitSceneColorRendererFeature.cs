using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public sealed class WaterExitSceneColorRendererFeature : ScriptableRendererFeature
{
	private sealed class WaterExitSceneColorPass : ScriptableRenderPass
	{
		private const string PassName = "Water Exit Scene Color";

		private const string TextureName = "_WaterExitSceneColorTexture";

		private static readonly int TexturePropertyId = Shader.PropertyToID("_WaterExitSceneColorTexture");

		private static readonly ProfilingSampler ProfilingSampler = new ProfilingSampler("Water Exit Scene Color");

		private RTHandle _sceneColorHandle;

		public WaterExitSceneColorPass(RenderPassEvent passEvent)
		{
			base.renderPassEvent = passEvent;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			UniversalResourceData universalResourceData = frameData.Get<UniversalResourceData>();
			UniversalCameraData universalCameraData = frameData.Get<UniversalCameraData>();
			if (universalCameraData.camera.cameraType == CameraType.Game && universalCameraData.renderType == CameraRenderType.Base && !universalResourceData.isActiveTargetBackBuffer)
			{
				RenderTextureDescriptor descriptor = GetDescriptor(universalCameraData.cameraTargetDescriptor);
				RenderingUtils.ReAllocateHandleIfNeeded(ref _sceneColorHandle, in descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, 1, 0f, "_WaterExitSceneColorTexture");
				Shader.SetGlobalTexture(TexturePropertyId, _sceneColorHandle);
				TextureHandle activeColorTexture = universalResourceData.activeColorTexture;
				TextureHandle textureHandle = renderGraph.ImportTexture(_sceneColorHandle);
				if (activeColorTexture.IsValid() && textureHandle.IsValid())
				{
					RenderGraphUtils.BlitMaterialParameters blitParameters = new RenderGraphUtils.BlitMaterialParameters(activeColorTexture, textureHandle, Blitter.GetBlitMaterial(TextureDimension.Tex2D), 0);
					renderGraph.AddBlitPass(blitParameters, "Water Exit Scene Color", returnBuilder: false, "C:\\Users\\PC1\\Desktop\\Projects\\Sushi\\Assets\\Scripts\\Core\\Ocean\\WaterExitSceneColorRendererFeature.cs", 142);
					universalResourceData.cameraColor = textureHandle;
				}
			}
		}

		public void Dispose()
		{
			_sceneColorHandle?.Release();
			_sceneColorHandle = null;
		}

		private static RenderTextureDescriptor GetDescriptor(RenderTextureDescriptor cameraDescriptor)
		{
			cameraDescriptor.depthBufferBits = 0;
			cameraDescriptor.msaaSamples = 1;
			return cameraDescriptor;
		}
	}

	private static int _captureRequestCount;

	private WaterExitSceneColorPass _sceneColorPass;

	public static void RequestCapture()
	{
		_captureRequestCount++;
	}

	public static void ReleaseCapture()
	{
		_captureRequestCount = Mathf.Max(0, _captureRequestCount - 1);
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetCaptureRequests()
	{
		_captureRequestCount = 0;
	}

	public override void Create()
	{
		_sceneColorPass = new WaterExitSceneColorPass(RenderPassEvent.AfterRenderingPostProcessing);
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		CameraData cameraData = renderingData.cameraData;
		if (_captureRequestCount > 0 && cameraData.cameraType == CameraType.Game && cameraData.renderType == CameraRenderType.Base)
		{
			renderer.EnqueuePass(_sceneColorPass);
		}
	}

	protected override void Dispose(bool disposing)
	{
		_sceneColorPass?.Dispose();
		_sceneColorPass = null;
	}
}
