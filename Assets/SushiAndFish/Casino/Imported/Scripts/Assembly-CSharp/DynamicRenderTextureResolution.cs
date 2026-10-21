namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::UnityEngine;
using global::UnityEngine.UI;

public class DynamicRenderTextureResolution : MonoBehaviour
{
	[SerializeField]
	private Camera sourceCamera;

	[SerializeField]
	private RenderTexture renderTextureAsset;

	[SerializeField]
	private RawImage previewImage;

	[Range(0.25f, 2f)]
	[SerializeField]
	private float scale = 1f;

	[SerializeField]
	private bool useEvenDimensions = true;

	private int lastW;

	private int lastH;

	private float lastScale;

	private Coroutine refreshRoutine;

	private void OnEnable()
	{
		SettingsLayout.SettingsChanged += OnSettingsChanged;
		StartCoroutine(ApplyNextFrame());
	}

	private void OnDisable()
	{
		SettingsLayout.SettingsChanged -= OnSettingsChanged;
		if (sourceCamera != null && sourceCamera.targetTexture == renderTextureAsset)
		{
			sourceCamera.targetTexture = null;
		}
		if (refreshRoutine != null)
		{
			StopCoroutine(refreshRoutine);
			refreshRoutine = null;
		}
	}

	private void OnValidate()
	{
		if (Application.isPlaying && isActiveAndEnabled)
		{
			CheckAndApply();
		}
	}

	private void OnSettingsChanged(SettingsLayout source, SettingItemBase entry)
	{
		if (!(entry == null) && !string.IsNullOrWhiteSpace(entry.key))
		{
			string text = entry.key.Trim().ToLowerInvariant();
			if (text == "resolution" || text == "display")
			{
				StartCoroutine(ApplyNextFrame());
			}
		}
	}

	private IEnumerator ApplyNextFrame()
	{
		yield return null;
		CheckAndApply();
	}

	private void CheckAndApply()
	{
		if (!(sourceCamera == null) && !(renderTextureAsset == null))
		{
			int num = Mathf.Max(1, Mathf.RoundToInt((float)Screen.width * scale));
			int num2 = Mathf.Max(1, Mathf.RoundToInt((float)Screen.height * scale));
			if (useEvenDimensions)
			{
				num &= -2;
				num2 &= -2;
				num = Mathf.Max(2, num);
				num2 = Mathf.Max(2, num2);
			}
			if (num != lastW || num2 != lastH || !Mathf.Approximately(scale, lastScale))
			{
				Apply(num, num2);
			}
		}
	}

	private void Apply(int targetW, int targetH)
	{
		if (!(sourceCamera == null) && !(renderTextureAsset == null))
		{
			lastW = targetW;
			lastH = targetH;
			lastScale = scale;
			RebuildRenderTexture(renderTextureAsset, targetW, targetH);
			Debug.Log($"[DynamicRenderTextureResolution] Render texture resolution changed to {targetW}x{targetH}");
			if (previewImage != null && previewImage.texture != renderTextureAsset)
			{
				previewImage.texture = renderTextureAsset;
			}
			if (refreshRoutine != null)
			{
				StopCoroutine(refreshRoutine);
			}
			refreshRoutine = StartCoroutine(RefreshCameraNextFrame(targetW, targetH));
		}
	}

	private IEnumerator RefreshCameraNextFrame(int w, int h)
	{
		RenderTexture rt = renderTextureAsset;
		if (rt == null || !rt.IsCreated())
		{
			yield break;
		}
		if (sourceCamera.targetTexture == rt)
		{
			sourceCamera.targetTexture = null;
		}
		yield return null;
		if (rt == null || !rt.IsCreated())
		{
			Debug.LogError($"[DynamicRenderTextureResolution] RenderTexture is invalid after creation. Resolution: {w}x{h}");
			yield break;
		}
		float aspect = (float)w / (float)h;
		sourceCamera.aspect = aspect;
		sourceCamera.targetTexture = rt;
		if (sourceCamera.orthographic)
		{
			float orthographicSize = sourceCamera.orthographicSize;
			sourceCamera.orthographicSize = orthographicSize + 0.0001f;
			sourceCamera.orthographicSize = orthographicSize;
		}
		else
		{
			float fieldOfView = sourceCamera.fieldOfView;
			sourceCamera.fieldOfView = fieldOfView + 0.0001f;
			sourceCamera.fieldOfView = fieldOfView;
		}
		sourceCamera.ResetProjectionMatrix();
		if(CasinoRuntimeContext.Active) SushiAndFish.Casino.CasinoHostRepair.RepairOffscreenCamera(sourceCamera);
		sourceCamera.Render();
	}

	private static void RebuildRenderTexture(RenderTexture rt, int w, int h)
	{
		if (rt == null)
		{
			return;
		}
		try
		{
			if (rt.IsCreated())
			{
				rt.Release();
			}
			rt.width = w;
			rt.height = h;
			rt.Create();
			if (!rt.IsCreated())
			{
				Debug.LogError($"[DynamicRenderTextureResolution] Failed to create RenderTexture {w}x{h}. GPU may be out of memory. Error: 0x8007000e");
				int num = Mathf.Max(256, w / 2);
				int num2 = Mathf.Max(256, h / 2);
				rt.width = num;
				rt.height = num2;
				rt.Create();
				if (!rt.IsCreated())
				{
					Debug.LogError($"[DynamicRenderTextureResolution] Failed to create fallback RenderTexture {num}x{num2}. Disabling component.");
					return;
				}
				Debug.LogWarning($"[DynamicRenderTextureResolution] Using fallback resolution: {num}x{num2} instead of {w}x{h}");
			}
		}
		catch (Exception ex)
		{
			Debug.LogError($"[DynamicRenderTextureResolution] Exception creating RenderTexture {w}x{h}: {ex.Message}");
		}
	}
}

}
