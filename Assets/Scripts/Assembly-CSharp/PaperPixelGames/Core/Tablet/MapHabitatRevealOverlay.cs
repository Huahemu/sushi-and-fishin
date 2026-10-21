using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class MapHabitatRevealOverlay : MonoBehaviour
	{
		[SerializeField]
		private RawImage _overlayImage;

		[SerializeField]
		[Min(64f)]
		private int _textureSize = 512;

		[SerializeField]
		[Range(0f, 1f)]
		private float _coveredAlpha = 0.72f;

		[SerializeField]
		[Range(0.01f, 2f)]
		private float _revealAnimationDuration = 0.65f;

		[SerializeField]
		[Range(0f, 0.35f)]
		private float _edgeFeather = 0.045f;

		[SerializeField]
		[Range(0f, 0.2f)]
		private float _edgeNoiseStrength = 0.025f;

		[SerializeField]
		private Vector2 _fallbackRevealRadiusUV = new Vector2(0.08f, 0.08f);

		private Texture2D _texture;

		private Color32[] _pixels;

		private Coroutine _revealRoutine;

		private IReadOnlyList<MapHabitatMarker> _currentMarkers;

		private HashSet<byte> _currentDiscovered = new HashSet<byte>();

		public void Render(IReadOnlyList<MapHabitatMarker> markers, IReadOnlyCollection<byte> discovered, byte? animatedSeaLayerIndex)
		{
			_currentMarkers = markers;
			_currentDiscovered = ((discovered != null) ? new HashSet<byte>(discovered) : new HashSet<byte>());
			if (!(_overlayImage == null))
			{
				_overlayImage.gameObject.SetActive(value: true);
				Texture2D orCreateTexture = GetOrCreateTexture();
				_overlayImage.texture = orCreateTexture;
				if (_revealRoutine != null)
				{
					StopCoroutine(_revealRoutine);
					_revealRoutine = null;
				}
				if (animatedSeaLayerIndex.HasValue && _currentDiscovered.Contains(animatedSeaLayerIndex.Value))
				{
					_revealRoutine = StartCoroutine(AnimateReveal(animatedSeaLayerIndex.Value));
				}
				else
				{
					DrawMask(null, 1f);
				}
			}
		}

		public void Hide()
		{
			if (_revealRoutine != null)
			{
				StopCoroutine(_revealRoutine);
				_revealRoutine = null;
			}
			if (_overlayImage != null)
			{
				_overlayImage.gameObject.SetActive(value: false);
			}
		}

		private IEnumerator AnimateReveal(byte seaLayerIndex)
		{
			float duration = Mathf.Max(0.01f, _revealAnimationDuration);
			float elapsed = 0f;
			while (elapsed < duration)
			{
				elapsed += Time.unscaledDeltaTime;
				float t = Mathf.Clamp01(elapsed / duration);
				t = Mathf.SmoothStep(0f, 1f, t);
				DrawMask(seaLayerIndex, t);
				yield return null;
			}
			DrawMask(null, 1f);
			_revealRoutine = null;
		}

		private Texture2D GetOrCreateTexture()
		{
			if (_texture != null && _texture.width == _textureSize && _texture.height == _textureSize)
			{
				return _texture;
			}
			_texture = new Texture2D(_textureSize, _textureSize, TextureFormat.RGBA32, mipChain: false)
			{
				name = "MapHabitatReveal_Underwater",
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear
			};
			_pixels = new Color32[_textureSize * _textureSize];
			return _texture;
		}

		private void DrawMask(byte? animatedSeaLayerIndex, float animatedProgress)
		{
			Texture2D orCreateTexture = GetOrCreateTexture();
			Color32 color = new Color(0f, 0f, 0f, _coveredAlpha);
			for (int i = 0; i < _pixels.Length; i++)
			{
				_pixels[i] = color;
			}
			if (_currentMarkers != null)
			{
				for (int j = 0; j < _currentMarkers.Count; j++)
				{
					MapHabitatMarker mapHabitatMarker = _currentMarkers[j];
					if (!(mapHabitatMarker == null) && _currentDiscovered.Contains(mapHabitatMarker.SeaLayerIndex))
					{
						float value = ((animatedSeaLayerIndex.HasValue && animatedSeaLayerIndex.Value == mapHabitatMarker.SeaLayerIndex) ? animatedProgress : 1f);
						DrawMarkerReveal(mapHabitatMarker, Mathf.Clamp01(value));
					}
				}
			}
			orCreateTexture.SetPixels32(_pixels);
			orCreateTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		}

		private void DrawMarkerReveal(MapHabitatMarker marker, float progress)
		{
			if (!(progress <= 0f))
			{
				DrawFallbackEllipse(marker, progress);
			}
		}

		private void DrawFallbackEllipse(MapHabitatMarker marker, float progress)
		{
			Vector2 markerUV = GetMarkerUV(marker);
			Vector2 vector = ((marker != null) ? marker.RevealRadiusUV : _fallbackRevealRadiusUV);
			Vector2 vector2 = new Vector2(Mathf.Max(0.001f, vector.x * Mathf.Lerp(0.05f, 1f, progress)), Mathf.Max(0.001f, vector.y * Mathf.Lerp(0.05f, 1f, progress)));
			int num = Mathf.Clamp(Mathf.FloorToInt((markerUV.x - vector2.x) * (float)_textureSize), 0, _textureSize - 1);
			int num2 = Mathf.Clamp(Mathf.CeilToInt((markerUV.x + vector2.x) * (float)_textureSize), 0, _textureSize - 1);
			int num3 = Mathf.Clamp(Mathf.FloorToInt((markerUV.y - vector2.y) * (float)_textureSize), 0, _textureSize - 1);
			int num4 = Mathf.Clamp(Mathf.CeilToInt((markerUV.y + vector2.y) * (float)_textureSize), 0, _textureSize - 1);
			float num5 = Mathf.Max(0.001f, _edgeFeather);
			for (int i = num3; i <= num4; i++)
			{
				for (int j = num; j <= num2; j++)
				{
					float num6 = ((float)j + 0.5f) / (float)_textureSize;
					float num7 = ((float)i + 0.5f) / (float)_textureSize;
					float num8 = (num6 - markerUV.x) / vector2.x;
					float num9 = (num7 - markerUV.y) / vector2.y;
					float num10 = Mathf.Sqrt(num8 * num8 + num9 * num9);
					num10 += (GetEdgeNoise(j, i) - 0.5f) * _edgeNoiseStrength;
					if (!(num10 > 1f + num5))
					{
						float value = 1f - Mathf.InverseLerp(1f, 1f + num5, num10);
						int num11 = i * _textureSize + j;
						Color32 color = _pixels[num11];
						byte b = (byte)Mathf.RoundToInt(_coveredAlpha * 255f * (1f - Mathf.Clamp01(value)));
						if (b < color.a)
						{
							color.a = b;
							_pixels[num11] = color;
						}
					}
				}
			}
		}

		private Vector2 GetMarkerUV(MapHabitatMarker marker)
		{
			if (_overlayImage == null || marker == null || marker.RectTransform == null)
			{
				return new Vector2(0.5f, 0.5f);
			}
			RectTransform rectTransform = _overlayImage.rectTransform;
			Vector3 vector = rectTransform.InverseTransformPoint(marker.RectTransform.position);
			Rect rect = rectTransform.rect;
			if (Mathf.Approximately(rect.width, 0f) || Mathf.Approximately(rect.height, 0f))
			{
				return new Vector2(0.5f, 0.5f);
			}
			float value = Mathf.InverseLerp(rect.xMin, rect.xMax, vector.x);
			return new Vector2(y: Mathf.Clamp01(Mathf.InverseLerp(rect.yMin, rect.yMax, vector.y)), x: Mathf.Clamp01(value));
		}

		private static float GetEdgeNoise(int x, int y)
		{
			int num = x * 374761393 + y * 668265263;
			uint num2 = ((uint)num ^ ((uint)num >> 13)) * 1274126177;
			return (float)((num2 ^ (num2 >> 16)) & 0xFFFFFF) / 16777215f;
		}
	}
}
