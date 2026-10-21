using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class WaterExitDropletBlurEffect : MonoBehaviour
{
	private static readonly int ProgressPropertyId = Shader.PropertyToID("_Progress");

	private static readonly int PatternTransformPropertyId = Shader.PropertyToID("_PatternTransform");

	[SerializeField]
	[Min(0.1f)]
	private float _duration = 1.85f;

	private readonly List<ParticleSystemRenderer> _dropletRenderers = new List<ParticleSystemRenderer>();

	private MaterialPropertyBlock _propertyBlock;

	private Vector4 _patternTransform = new Vector4(0f, 0f, 1f, 1f);

	private float _elapsedTime;

	private bool _captureRequested;

	private void Awake()
	{
		_propertyBlock = new MaterialPropertyBlock();
		ParticleSystemRenderer[] componentsInChildren = GetComponentsInChildren<ParticleSystemRenderer>(includeInactive: true);
		foreach (ParticleSystemRenderer particleSystemRenderer in componentsInChildren)
		{
			Material sharedMaterial = particleSystemRenderer.sharedMaterial;
			if (sharedMaterial != null && sharedMaterial.HasProperty(ProgressPropertyId))
			{
				_dropletRenderers.Add(particleSystemRenderer);
			}
		}
	}

	private void OnEnable()
	{
		WaterExitSceneColorRendererFeature.RequestCapture();
		_captureRequested = true;
		RandomizePattern();
		_elapsedTime = 0f;
		ApplyProgress(0f);
	}

	private void OnDisable()
	{
		if (_captureRequested)
		{
			WaterExitSceneColorRendererFeature.ReleaseCapture();
			_captureRequested = false;
		}
	}

	private void Update()
	{
		_elapsedTime += Time.deltaTime;
		float num = Mathf.Clamp01(_elapsedTime / _duration);
		ApplyProgress(num);
		if (num >= 1f)
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private void ApplyProgress(float progress)
	{
		foreach (ParticleSystemRenderer dropletRenderer in _dropletRenderers)
		{
			if (!(dropletRenderer == null))
			{
				dropletRenderer.GetPropertyBlock(_propertyBlock);
				_propertyBlock.SetFloat(ProgressPropertyId, progress);
				_propertyBlock.SetVector(PatternTransformPropertyId, _patternTransform);
				dropletRenderer.SetPropertyBlock(_propertyBlock);
			}
		}
	}

	private void RandomizePattern()
	{
		System.Random random = new System.Random((((GetInstanceID() * 397) ^ Time.frameCount) * 397) ^ Environment.TickCount);
		float x = (float)random.NextDouble();
		float y = random.Next(0, 2);
		float z = Mathf.Lerp(0.88f, 1.12f, (float)random.NextDouble());
		float w = Mathf.Lerp(0.92f, 1.08f, (float)random.NextDouble());
		_patternTransform = new Vector4(x, y, z, w);
	}
}
