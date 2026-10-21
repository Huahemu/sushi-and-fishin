namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::Unity.Collections;
using global::UnityEngine;
using global::UnityEngine.Experimental.Rendering;
using global::UnityEngine.Rendering;

[ExecuteInEditMode]
public class LightVolume : MonoBehaviour
{
	private struct GpuLight
	{
		public Vector3 Position;

		public float ConeSize;

		public Vector3 Direction;

		public float Radius;

		public Vector3 Color;

		public float Falloff;

		public float ConeFalloff;
	}

	public bool showVolumeGizmos = true;

	public float brightness = 1f;

	public float ambienceStrength = 1f;

	public float ambienceMin = 0.05f;

	public Color skyColor = Color.white;

	public FilterMode filterModeRT = FilterMode.Bilinear;

	public RenderTextureFormat formatRT = RenderTextureFormat.ARGBFloat;

	public Vector3Int gridRes;

	public Vector3 gridOffset;

	public int rayCount = 128;

	public float raySpacing = 1.5f;

	public Vector3 boundsSizeMultiplier = Vector3.one;

	public Vector3 boundsCenterOffset = Vector3.zero;

	[Tooltip("Colliders matching this mask will be used for light tracing, colliders not matching will be ignored")]
	public LayerMask occluderMask = -1;

	[Tooltip("Radius (in texels) for how much to box blur the output texture")]
	public int blurRadius;

	public GameObject sceneParent;

	public ComputeShader computeShader;

	public RayTracingShader rayTracingShader;

	public Texture3D lightMap;

	public string fileName = "LightVolumeBakeTexture";

	public List<BakedVolumeLight> allLightsFound;

	public List<MeshRenderer> allMeshRenderersFound;

	internal static LightVolume instance;

	private bool RaytracingShaderNotSupported => !SystemInfo.supportsRayTracingShaders;

	private void SetShaderVars()
	{
		Shader.SetGlobalFloat("brightness", brightness);
		Shader.SetGlobalFloat("ambienceStrength", ambienceStrength);
		Shader.SetGlobalFloat("ambienceMin", ambienceMin);
		Shader.SetGlobalVector("gridRes", (Vector3)gridRes);
		Shader.SetGlobalFloat("raySpacing", raySpacing);
		Shader.SetGlobalVector("gridOffset", gridOffset);
	}

	public void SetSize()
	{
		Shader.SetGlobalTexture("_LightMap", null);
		GameObject gameObject = ((sceneParent == null) ? base.gameObject : sceneParent);
		Bounds totalBounds = GetTotalBounds(gameObject);
		gridOffset = totalBounds.center;
		gridRes = new Vector3Int(Mathf.CeilToInt((totalBounds.size.x + 3f) / raySpacing), Mathf.CeilToInt((totalBounds.size.y + 3f) / raySpacing), Mathf.CeilToInt((totalBounds.size.z + 3f) / raySpacing));
	}

	private Bounds GetTotalBounds(GameObject gameObject)
	{
		Bounds result = default;
		bool flag = true;
		MeshRenderer[] componentsInChildren = gameObject.GetComponentsInChildren<MeshRenderer>();
		foreach (MeshRenderer meshRenderer in componentsInChildren)
		{
			if (flag)
			{
				result = meshRenderer.bounds;
			}
			else
			{
				result.Encapsulate(meshRenderer.bounds);
			}
			flag = false;
		}
		Vector3 vector = new Vector3(result.center.x, result.min.y, result.center.z);
		Vector3 lhs = Vector3.Scale(result.size, boundsSizeMultiplier);
		lhs = Vector3.Max(lhs, Vector3.one * 0.01f);
		Vector3 center = new Vector3(vector.x, vector.y + lhs.y * 0.5f, vector.z) + boundsCenterOffset;
		result.size = lhs;
		result.center = center;
		return result;
	}

	private void OnDrawGizmosSelected()
	{
		if (showVolumeGizmos)
		{
			Gizmos.color = Color.black;
			Gizmos.DrawWireCube(gridOffset - Vector3.one * 0.25f, (Vector3)gridRes * raySpacing);
			Gizmos.color = Color.white;
			Gizmos.DrawWireCube(gridOffset + Vector3.one * 0.25f, (Vector3)gridRes * raySpacing);
		}
	}

	private void Awake()
	{
		instance = this;
	}

	private void Start()
	{
		SetShaderVars();
		Shader.SetGlobalTexture("_LightMap", lightMap);
	}

	public void Bake()
	{
		if (!computeShader)
		{
			Debug.LogError("Cannot bake at runtime (serialize the ComputeShader if you want to do this)");
			return;
		}
		if (!rayTracingShader)
		{
			Debug.LogError("Cannot bake at runtime (serialize the RayTracingShader if you want to do this)");
			return;
		}
		SetSize();
		RenderTexture inputTex = RunBake();
		RenderTexture renderTexture = RunBlur(inputTex);
		renderTexture.name = "LightVolumeRenderTexture";
		SetShaderVars();
		Shader.SetGlobalTexture("_LightMap", renderTexture);
		StartCoroutine(SaveTex(renderTexture));
	}

	private RenderTexture RunBake()
	{
		rayTracingShader.SetVector("gridRadius", new Vector3(gridRes.x, gridRes.y, gridRes.z) * (raySpacing / 2f));
		rayTracingShader.SetVector("gridOffset", gridOffset);
		rayTracingShader.SetVector("skyColor", skyColor);
		rayTracingShader.SetInt("rayCount", rayCount);
		int num = BuildLights(out var toDispose);
		BuildMeshes(out var toDispose2, num);
		RenderTexture renderTexture = Create3DTexture(FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, gridRes);
		rayTracingShader.SetTexture("lightMap", renderTexture);
		for (int i = 0; i < num + 1; i++)
		{
			rayTracingShader.SetInt("doLightIndex", i);
			rayTracingShader.Dispatch("RaygenShader", gridRes.x, gridRes.y, gridRes.z);
		}
		toDispose.Dispose();
		toDispose2.Dispose();
		return renderTexture;
	}

	private static RenderTexture Create3DTexture(FilterMode filterMode, RenderTextureFormat format, Vector3Int resolution)
	{
		RenderTexture obj = new RenderTexture(resolution.x, resolution.y, 0)
		{
			enableRandomWrite = true,
			format = format,
			dimension = TextureDimension.Tex3D,
			volumeDepth = resolution.z,
			wrapMode = TextureWrapMode.Clamp,
			filterMode = filterMode,
			hideFlags = HideFlags.DontSave
		};
		if (!obj.Create())
		{
			throw new Exception("Failed to create texture");
		}
		return obj;
	}

	private int BuildLights(out ComputeBuffer toDispose)
	{
		List<GpuLight> list = new List<GpuLight>();
		GameObject obj = ((sceneParent == null) ? base.gameObject : sceneParent);
		if (allLightsFound == null)
		{
			allLightsFound = new List<BakedVolumeLight>();
		}
		allLightsFound.Clear();
		BakedVolumeLight[] componentsInChildren = obj.GetComponentsInChildren<BakedVolumeLight>();
		foreach (BakedVolumeLight bakedVolumeLight in componentsInChildren)
		{
			allLightsFound.Add(bakedVolumeLight);
			Vector3 vector = new Vector3(bakedVolumeLight.color.r, bakedVolumeLight.color.g, bakedVolumeLight.color.b);
			float coneSize = bakedVolumeLight.mode switch
			{
				BakedVolumeLight.LightModes.Spot => bakedVolumeLight.coneSize * (MathF.PI / 180f), 
				BakedVolumeLight.LightModes.Point => 0f, 
				_ => throw new Exception(), 
			};
			list.Add(new GpuLight
			{
				Position = bakedVolumeLight.transform.position,
				ConeSize = coneSize,
				Direction = bakedVolumeLight.transform.forward,
				Radius = bakedVolumeLight.radius,
				Color = vector * bakedVolumeLight.intensity,
				Falloff = bakedVolumeLight.falloff,
				ConeFalloff = bakedVolumeLight.coneFalloff
			});
		}
		int count = list.Count;
		if (count == 0)
		{
			list.Add(default);
		}
		ComputeBuffer computeBuffer = new ComputeBuffer(list.Count, 52);
		computeBuffer.SetData(list);
		rayTracingShader.SetBuffer("lightBuffer", computeBuffer);
		rayTracingShader.SetInt("lightBufferLength", count);
		toDispose = computeBuffer;
		return count;
	}

	private void BuildMeshes(out IDisposable toDispose, int lightCountForDebug)
	{
		int value = occluderMask.value;
		GameObject obj = ((sceneParent == null) ? base.gameObject : sceneParent);
		if (allMeshRenderersFound == null)
		{
			allMeshRenderersFound = new List<MeshRenderer>();
		}
		allMeshRenderersFound.Clear();
		RayTracingAccelerationStructure rayTracingAccelerationStructure = new RayTracingAccelerationStructure();
		uint num = 0u;
		int num2 = 0;
		MeshRenderer[] componentsInChildren = obj.GetComponentsInChildren<MeshRenderer>();
		foreach (MeshRenderer meshRenderer in componentsInChildren)
		{
			if (((1 << meshRenderer.gameObject.layer) & value) == 0)
			{
				continue;
			}
			allMeshRenderersFound.Add(meshRenderer);
			Mesh sharedMesh = meshRenderer.GetComponent<MeshFilter>().sharedMesh;
			if (!(sharedMesh == null))
			{
				int subMeshCount = sharedMesh.subMeshCount;
				for (int j = 0; j < subMeshCount; j++)
				{
					num += sharedMesh.GetIndexCount(j);
				}
				num2 += sharedMesh.vertexCount;
				RayTracingSubMeshFlags[] array = new RayTracingSubMeshFlags[subMeshCount];
				for (int k = 0; k < array.Length; k++)
				{
					array[k] = RayTracingSubMeshFlags.Enabled | RayTracingSubMeshFlags.ClosestHitOnly;
				}
				rayTracingAccelerationStructure.AddInstance(meshRenderer, array);
			}
		}
		rayTracingAccelerationStructure.Build();
		Debug.Log($"Light Volume Baker found: {lightCountForDebug} lights, {allMeshRenderersFound.Count} meshes, {num} indices, {num2} vertices");
		rayTracingShader.SetAccelerationStructure("g_SceneAccelStruct", rayTracingAccelerationStructure);
		toDispose = rayTracingAccelerationStructure;
	}

	private RenderTexture RunBlur(RenderTexture inputTex)
	{
		if (blurRadius <= 0)
		{
			return inputTex;
		}
		computeShader.SetInt("blurRadius", blurRadius);
		Vector3Int resolution = new Vector3Int(inputTex.width, inputTex.height, inputTex.volumeDepth);
		RenderTexture renderTexture = Create3DTexture(inputTex.filterMode, inputTex.format, resolution);
		for (int i = 0; i < 3; i++)
		{
			computeShader.SetTexture(1, "blurInputLightMap", inputTex);
			computeShader.SetTexture(1, "lightMap", renderTexture);
			computeShader.SetInt("blurAxis", i);
			uint num = 4u;
			uint num2 = 4u;
			uint num3 = 4u;
			long num4 = (resolution.x + num - 1) / num;
			long num5 = (resolution.y + num2 - 1) / num2;
			long num6 = (resolution.z + num3 - 1) / num3;
			computeShader.Dispatch(1, (int)num4, (int)num5, (int)num6);
			RenderTexture renderTexture2 = inputTex;
			RenderTexture renderTexture3 = renderTexture;
			renderTexture = renderTexture2;
			inputTex = renderTexture3;
		}
		UnityEngine.Object.DestroyImmediate(renderTexture);
		return inputTex;
	}

	private IEnumerator SaveTex(RenderTexture renderTexture)
	{
		AsyncGPUReadbackRequest downloadedData = AsyncGPUReadback.Request(renderTexture);
		while (!downloadedData.done)
		{
			yield return null;
		}
		if (downloadedData.hasError)
		{
			Debug.LogError("AsyncGPUReadback error while baking light volume texture.");
			UnityEngine.Object.DestroyImmediate(renderTexture);
			yield break;
		}
		byte[] array = new byte[downloadedData.layerDataSize * downloadedData.layerCount];
		for (int i = 0; i < downloadedData.layerCount; i++)
		{
			NativeArray<byte>.Copy(downloadedData.GetData<byte>(i), 0, array, i * downloadedData.layerDataSize, downloadedData.layerDataSize);
		}
		if (!lightMap || lightMap.width != renderTexture.width || lightMap.height != renderTexture.height || lightMap.depth != renderTexture.volumeDepth || lightMap.graphicsFormat != renderTexture.graphicsFormat)
		{
			if ((bool)lightMap)
			{
				UnityEngine.Object.DestroyImmediate(lightMap);
			}
			lightMap = new Texture3D(renderTexture.width, renderTexture.height, renderTexture.volumeDepth, renderTexture.graphicsFormat, TextureCreationFlags.None);
		}
		lightMap.name = fileName;
		lightMap.wrapMode = renderTexture.wrapMode;
		lightMap.filterMode = renderTexture.filterMode;
		lightMap.SetPixelData(array, 0);
		lightMap.Apply();
		Shader.SetGlobalTexture("_LightMap", lightMap);
		UnityEngine.Object.DestroyImmediate(renderTexture);
	}

	public static LightVolume Instance()
	{
		if (instance == null)
		{
			instance = UnityEngine.Object.FindAnyObjectByType<LightVolume>();
		}
		return instance;
	}

	internal Color SamplePosition(Vector3 worldPos)
	{
		worldPos -= gridOffset;
		worldPos += raySpacing * (Vector3)gridRes * 0.5f;
		worldPos.x /= raySpacing;
		worldPos.y /= raySpacing;
		worldPos.z /= raySpacing;
		return lightMap.GetPixel((int)worldPos.x, (int)worldPos.y, (int)worldPos.z);
	}

	public float SamplePositionAlpha(Vector3 worldPos)
	{
		worldPos -= gridOffset;
		worldPos += raySpacing * (Vector3)gridRes * 0.5f;
		worldPos.x /= raySpacing;
		worldPos.y /= raySpacing;
		worldPos.z /= raySpacing;
		return lightMap.GetPixel((int)worldPos.x, (int)worldPos.y, (int)worldPos.z).a;
	}
}

}
