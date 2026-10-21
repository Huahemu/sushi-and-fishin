using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class OverlayFX : MonoBehaviour
{
	public enum GradientAxis
	{
		X = 0,
		Y = 1,
		Z = 2
	}

	public enum UvDirection
	{
		X = 0,
		Y = 1,
		Z = 2
	}

	public static class ShaderKeywordController
	{
		private static readonly string[] GradientAxisKeywords = new string[3] { "_GRADIENT_AXIS_X", "_GRADIENT_AXIS_Y", "_GRADIENT_AXIS_Z" };

		private static readonly string[] UvDirectionKeywords = new string[3] { "_UV_DIRECTION_X", "_UV_DIRECTION_Y", "_UV_DIRECTION_Z" };

		public static void SetGradientAxis(Material mat, GradientAxis axis)
		{
			for (int i = 0; i < GradientAxisKeywords.Length; i++)
			{
				if (i == (int)axis)
				{
					mat.EnableKeyword(GradientAxisKeywords[i]);
				}
				else
				{
					mat.DisableKeyword(GradientAxisKeywords[i]);
				}
			}
		}

		public static void SetUvDirection(Material mat, UvDirection uv)
		{
			for (int i = 0; i < UvDirectionKeywords.Length; i++)
			{
				if (i == (int)uv)
				{
					mat.EnableKeyword(UvDirectionKeywords[i]);
				}
				else
				{
					mat.DisableKeyword(UvDirectionKeywords[i]);
				}
			}
		}
	}

	public Material overlayMaterial;

	public Renderer targetRenderer;

	public List<ParticleSystem> particleSystems = new List<ParticleSystem>();

	private float particleSizeMultiplier = 0.15f;

	public Vector3 rendererTrueForward = Vector3.zero;

	private void OnEnable()
	{
		if (Application.isPlaying)
		{
			StripRuntimeOverlay();
			EnsureRuntimeOverlay();
		}
		SyncParticleSystems();
	}

	private void OnValidate()
	{
		SyncParticleSystems();
	}

	private void OnDrawGizmosSelected()
	{
		SyncParticleSystems();
	}

	private bool IsOverlayMatch(Material m)
	{
		if ((bool)m && m.shader == overlayMaterial?.shader)
		{
			return m.name.StartsWith(overlayMaterial.name);
		}
		return false;
	}

	private void EnsureRuntimeOverlay()
	{
		if (overlayMaterial == null)
		{
			return;
		}
		Renderer renderer = targetRenderer;
		if (renderer == null)
		{
			return;
		}
		Material[] sharedMaterials = renderer.sharedMaterials;
		foreach (Material m in sharedMaterials)
		{
			if (IsOverlayMatch(m))
			{
				return;
			}
		}
		Material[] sharedMaterials2 = renderer.sharedMaterials;
		int num = Array.FindIndex(sharedMaterials2, (Material material2) => material2 == null);
		bool num2 = num == -1;
		Material material = UnityEngine.Object.Instantiate(overlayMaterial);
		material.name = overlayMaterial.name + " (Runtime)";
		material.hideFlags = HideFlags.HideAndDontSave;
		UpdateBounds(material, renderer);
		if (num2)
		{
			List<Material> list = new List<Material>(sharedMaterials2) { material };
			renderer.sharedMaterials = list.ToArray();
		}
		else
		{
			sharedMaterials2[num] = material;
			renderer.sharedMaterials = sharedMaterials2;
		}
	}

	private void StripRuntimeOverlay()
	{
		Renderer renderer = targetRenderer;
		if (renderer == null)
		{
			return;
		}
		Material[] sharedMaterials = renderer.sharedMaterials;
		bool flag = false;
		for (int i = 0; i < sharedMaterials.Length; i++)
		{
			if ((bool)sharedMaterials[i] && sharedMaterials[i].name.EndsWith("(Runtime)"))
			{
				sharedMaterials[i] = null;
				flag = true;
			}
		}
		if (flag)
		{
			renderer.sharedMaterials = sharedMaterials;
		}
	}

	private void SyncParticleSystems()
	{
		if (targetRenderer == null)
		{
			return;
		}
		Vector3 vector = Vector3.one;
		if (targetRenderer is MeshRenderer meshRenderer && meshRenderer.TryGetComponent<MeshFilter>(out var component) && component.sharedMesh != null)
		{
			vector = Vector3.Scale(component.sharedMesh.bounds.size, component.transform.lossyScale);
		}
		else if (targetRenderer is SkinnedMeshRenderer skinnedMeshRenderer && skinnedMeshRenderer.sharedMesh != null)
		{
			vector = Vector3.Scale(skinnedMeshRenderer.sharedMesh.bounds.size, skinnedMeshRenderer.transform.lossyScale);
		}
		float startSizeMultiplier = Mathf.Max(vector.x, vector.y, vector.z) * particleSizeMultiplier;
		foreach (ParticleSystem particleSystem in particleSystems)
		{
			if ((bool)particleSystem)
			{
				ParticleSystem.ShapeModule shape = particleSystem.shape;
				shape.enabled = true;
				if (targetRenderer is MeshRenderer meshRenderer2)
				{
					shape.shapeType = ParticleSystemShapeType.MeshRenderer;
					shape.meshRenderer = meshRenderer2;
				}
				else if (targetRenderer is SkinnedMeshRenderer skinnedMeshRenderer2)
				{
					shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
					shape.skinnedMeshRenderer = skinnedMeshRenderer2;
				}
				ParticleSystem.MainModule main = particleSystem.main;
				main.startSizeMultiplier = startSizeMultiplier;
			}
		}
	}

	private void UpdateBounds(Material mat, Renderer rend)
	{
		Bounds localBounds = rend.localBounds;
		mat.SetVector("_LocalBoundsMinimum", localBounds.min);
		mat.SetVector("_LocalBoundsMaximum", localBounds.max);
		if (rend.TryGetComponent<SkinnedMeshRenderer>(out var component))
		{
			rendererTrueForward = component.rootBone.transform.rotation.eulerAngles;
		}
		if (rendererTrueForward.x <= 90f)
		{
			ShaderKeywordController.SetGradientAxis(mat, GradientAxis.Y);
			ShaderKeywordController.SetUvDirection(mat, UvDirection.X);
		}
		else
		{
			if (!(rendererTrueForward.x >= 270f))
			{
				return;
			}
			ShaderKeywordController.SetGradientAxis(mat, GradientAxis.Z);
			ShaderKeywordController.SetUvDirection(mat, UvDirection.Z);
			foreach (ParticleSystem particleSystem in particleSystems)
			{
				if (!(particleSystem == null))
				{
					ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = particleSystem.velocityOverLifetime;
					if (velocityOverLifetime.enabled && velocityOverLifetime.orbitalX.mode == ParticleSystemCurveMode.Constant && velocityOverLifetime.orbitalY.mode == ParticleSystemCurveMode.Constant && velocityOverLifetime.orbitalZ.mode == ParticleSystemCurveMode.Constant)
					{
						float constant = Mathf.Max(velocityOverLifetime.orbitalX.constant, velocityOverLifetime.orbitalY.constant, velocityOverLifetime.orbitalZ.constant);
						velocityOverLifetime.orbitalX = new ParticleSystem.MinMaxCurve(0f);
						velocityOverLifetime.orbitalY = new ParticleSystem.MinMaxCurve(0f);
						velocityOverLifetime.orbitalZ = new ParticleSystem.MinMaxCurve(constant);
					}
				}
			}
		}
	}
}
