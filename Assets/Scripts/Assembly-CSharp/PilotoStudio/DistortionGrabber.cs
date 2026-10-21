using UnityEngine;
using UnityEngine.Rendering;

namespace PilotoStudio
{
	[RequireComponent(typeof(ParticleSystem))]
	public class DistortionGrabber : MonoBehaviour
	{
		private static readonly int OpaqueTexID = Shader.PropertyToID("_CameraOpaqueTexture");

		private static readonly int TempTexID = Shader.PropertyToID("_DistortionTempRT");

		private Camera _camera;

		private CommandBuffer _buffer;

		private ParticleSystem _fx;

		private bool _active;

		private void Awake()
		{
			if (GraphicsSettings.currentRenderPipeline != null)
			{
				base.enabled = false;
				return;
			}
			_camera = Camera.main;
			_fx = GetComponent<ParticleSystem>();
		}

		private void LateUpdate()
		{
			bool flag = _fx.IsAlive(withChildren: true) && _fx.GetComponent<Renderer>().isVisible;
			if (flag && !_active)
			{
				EnableEffect();
			}
			else if (!flag && _active)
			{
				DisableEffect();
			}
		}

		private void EnableEffect()
		{
			_buffer = new CommandBuffer
			{
				name = "Distortion Grab"
			};
			_buffer.GetTemporaryRT(TempTexID, Screen.width, Screen.height, 0, FilterMode.Bilinear);
			_buffer.Blit(BuiltinRenderTextureType.CurrentActive, TempTexID);
			_buffer.SetGlobalTexture(OpaqueTexID, TempTexID);
			_camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, _buffer);
			_camera.depthTextureMode |= DepthTextureMode.Depth;
			_active = true;
		}

		private void DisableEffect()
		{
			if (_buffer != null)
			{
				_camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, _buffer);
				_buffer.Release();
				_buffer = null;
			}
			_active = false;
		}

		private void OnDisable()
		{
			DisableEffect();
		}
	}
}
