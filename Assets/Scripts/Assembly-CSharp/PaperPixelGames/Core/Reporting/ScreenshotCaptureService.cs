using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace PaperPixelGames.Core.Reporting
{
	public class ScreenshotCaptureService : MonoBehaviour, IScreenshotCaptureService
	{
		[SerializeField]
		private int _maxWidth = 1920;

		[SerializeField]
		private int _maxHeight = 1080;

		[SerializeField]
		[Min(1f)]
		private float _captureTimeoutSeconds = 10f;

		private readonly HashSet<TaskCompletionSource<Texture2D>> _captures = new HashSet<TaskCompletionSource<Texture2D>>();

		public Task<Texture2D> CaptureAsync()
		{
			if (!base.isActiveAndEnabled)
			{
				return Task.FromException<Texture2D>(new InvalidOperationException("Screenshot service is inactive."));
			}
			TaskCompletionSource<Texture2D> taskCompletionSource = new TaskCompletionSource<Texture2D>(TaskCreationOptions.RunContinuationsAsynchronously);
			_captures.Add(taskCompletionSource);
			StartCoroutine(CaptureRoutine(taskCompletionSource));
			return taskCompletionSource.Task;
		}

		private IEnumerator CaptureRoutine(TaskCompletionSource<Texture2D> tcs)
		{
			try
			{
				yield return new WaitForEndOfFrame();
				if (!tcs.Task.IsCompleted)
				{
					BeginReadback(tcs);
					float timeoutAt = Time.realtimeSinceStartup + _captureTimeoutSeconds;
					while (!tcs.Task.IsCompleted && Time.realtimeSinceStartup < timeoutAt)
					{
						yield return null;
					}
					if (!tcs.Task.IsCompleted)
					{
						tcs.TrySetException(new TimeoutException("Screenshot GPU readback timed out."));
					}
				}
			}
			finally
			{
				ScreenshotCaptureService screenshotCaptureService = this;
				tcs.TrySetCanceled();
				screenshotCaptureService._captures.Remove(tcs);
			}
		}

		private void BeginReadback(TaskCompletionSource<Texture2D> tcs)
		{
			int width = Mathf.Min(Screen.width, _maxWidth);
			int height = Mathf.Min(Screen.height, _maxHeight);
			RenderTexture rt = null;
			try
			{
				rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
				ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);
				AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, (AsyncGPUReadbackRequest request) =>
				{
					try
					{
						if (!tcs.Task.IsCompleted)
						{
							if (request.hasError)
							{
								throw new Exception("Screenshot capture failed.");
							}
							Texture2D texture2D = CreateFlippedTexture(request.GetData<Color32>(), width, height);
							if (!tcs.TrySetResult(texture2D))
							{
								UnityEngine.Object.Destroy(texture2D);
							}
						}
					}
					catch (Exception ex)
					{
						tcs.TrySetException(ex);
						Debug.LogError($"[ScreenshotCaptureService] Capture callback failed: {ex}");
					}
					finally
					{
						RenderTexture.ReleaseTemporary(rt);
					}
				});
			}
			catch (Exception exception)
			{
				if (rt != null)
				{
					RenderTexture.ReleaseTemporary(rt);
				}
				tcs.TrySetException(exception);
			}
		}

		private void OnDisable()
		{
			foreach (TaskCompletionSource<Texture2D> capture in _captures)
			{
				capture.TrySetCanceled();
			}
			_captures.Clear();
		}

		private static Texture2D CreateFlippedTexture(NativeArray<Color32> source, int width, int height)
		{
			Color32[] array = new Color32[source.Length];
			for (int i = 0; i < height; i++)
			{
				int num = i * width;
				int num2 = (height - 1 - i) * width;
				for (int j = 0; j < width; j++)
				{
					array[num2 + j] = source[num + j];
				}
			}
			Texture2D texture2D = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: false);
			try
			{
				texture2D.SetPixels32(array);
				texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
				return texture2D;
			}
			catch
			{
				UnityEngine.Object.Destroy(texture2D);
				throw;
			}
		}
	}
}
