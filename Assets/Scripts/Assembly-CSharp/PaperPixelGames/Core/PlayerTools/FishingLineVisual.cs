using CableSystem;
using UnityEngine;

namespace PaperPixelGames.Core.PlayerTools
{
	public sealed class FishingLineVisual : MonoBehaviour
	{
		private Cable _cable;

		private GameObject _lineOrigin;

		private GameObject _lineTarget;

		private Material _runtimeMaterial;

		private bool _initialized;

		public void Initialize(Transform lineOriginParent, Vector3 localOriginOffset, float width, int segmentCount)
		{
			if (!_initialized && !(lineOriginParent == null))
			{
				base.transform.SetParent(lineOriginParent, worldPositionStays: false);
				_lineOrigin = new GameObject("FishingLineOrigin");
				_lineOrigin.transform.SetParent(base.transform, worldPositionStays: false);
				_lineOrigin.transform.localPosition = localOriginOffset;
				_lineTarget = new GameObject("FishingLineTarget");
				_lineTarget.transform.SetParent(base.transform, worldPositionStays: true);
				_lineTarget.transform.position = _lineOrigin.transform.position;
				_cable = base.gameObject.AddComponent<Cable>();
				_cable.InitialPoint = _lineOrigin;
				_cable.DestinyPoint = _lineTarget;
				_cable.Segments = Mathf.Clamp(segmentCount, 1, 32);
				_cable.CableWidth = Mathf.Max(0.002f, width);
				_cable.DisableSegmentColliders = true;
				_cable.VisualOnly = true;
				_cable.UseGuidePath = true;
				_cable.GuidePathUseSag = true;
				_cable.GuidePathSag = 0.025f;
				_cable.GuidePathMaxSag = 0.45f;
				Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
				if (shader == null)
				{
					shader = Shader.Find("Sprites/Default");
				}
				if (shader != null)
				{
					_runtimeMaterial = new Material(shader)
					{
						name = "FishingLine_Runtime"
					};
					_runtimeMaterial.color = new Color(0.88f, 0.94f, 0.96f, 0.82f);
					_cable.CableMaterial = _runtimeMaterial;
				}
				_initialized = true;
				SetVisible(visible: false);
			}
		}

		public void SetLine(Vector3 targetPosition, float normalizedTension, bool visible)
		{
			if (_initialized)
			{
				_lineTarget.transform.position = targetPosition;
				_cable.GuidePathSag = Mathf.Lerp(0.045f, 0.002f, Mathf.Clamp01(normalizedTension));
				_cable.GuidePathMaxSag = Mathf.Lerp(0.6f, 0.05f, Mathf.Clamp01(normalizedTension));
				SetVisible(visible);
			}
		}

		public void SetVisible(bool visible)
		{
			if (_cable != null)
			{
				_cable.enabled = visible;
			}
			LineRenderer component = GetComponent<LineRenderer>();
			if (component != null)
			{
				component.enabled = visible;
			}
		}

		public void ResetLine()
		{
			if (_initialized)
			{
				_lineTarget.transform.position = _lineOrigin.transform.position;
				SetVisible(visible: false);
			}
		}

		private void OnDestroy()
		{
			if (_runtimeMaterial != null)
			{
				Object.Destroy(_runtimeMaterial);
			}
		}
	}
}
