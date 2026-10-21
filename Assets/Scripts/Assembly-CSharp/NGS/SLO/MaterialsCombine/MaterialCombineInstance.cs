using System;
using NGS.SLO.Shared;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
	[Serializable]
	public class MaterialCombineInstance
	{
		public interface IReadableInstanceData
		{
			Vector2 UVOffset { get; }

			Vector2 UVScale { get; }

			Vector2 MainTextureOffset { get; }

			Vector2 MainTextureScale { get; }

			Vector2Int MainTextureRepeats { get; }

			Vector2 MainTextureTargetSize { get; }

			Vector2 MainTextureDownscale { get; }
		}

		[Serializable]
		private struct InstanceData : IReadableInstanceData
		{
			[field: SerializeField]
			public Vector2 UVOffset { get; set; }

			[field: SerializeField]
			public Vector2 UVScale { get; set; }

			[field: SerializeField]
			public Vector2 MainTextureOffset { get; set; }

			[field: SerializeField]
			public Vector2 MainTextureScale { get; set; }

			[field: SerializeField]
			public Vector2Int MainTextureRepeats { get; set; }

			[field: SerializeField]
			public Vector2 MainTextureTargetSize { get; set; }

			[field: SerializeField]
			public Vector2 MainTextureDownscale { get; set; }
		}

		[SerializeField]
		private ShaderCombineInfo _shaderInfo;

		[SerializeField]
		private Material _material;

		[SerializeField]
		private int _materialIndex;

		[SerializeField]
		private Renderer _renderer;

		[SerializeField]
		private Mesh _mesh;

		[SerializeField]
		private InstanceData _data;

		[SerializeField]
		private bool _readyForCombine;

		[SerializeField]
		private string _reason;

		public ShaderCombineInfo ShaderInfo => _shaderInfo;

		public Material Material => _material;

		public int MaterialIndex => _materialIndex;

		public Renderer Renderer => _renderer;

		public Mesh Mesh => _mesh;

		public bool ReadyForCombine => _readyForCombine;

		public string Reason => _reason;

		public IReadableInstanceData Data => _data;

		public MaterialCombineInstance(Renderer renderer, int materialIndex)
		{
			_renderer = renderer;
			_materialIndex = materialIndex;
			_material = renderer.GetSharedMaterial(materialIndex);
			if (_material == null)
			{
				throw new NullReferenceException("Trying to create CombineInstance for empty material");
			}
			_data = default;
			SetNotReadyForCombine("Data Not Gathered");
		}

		public void GatherCombineData(ShaderCombineInfo shaderInfo, MaterialsCombineOptions combineOptions)
		{
			try
			{
				_shaderInfo = shaderInfo;
				if (!shaderInfo.AllowedCombine)
				{
					SetNotReadyForCombine("Shader Not Allowed For Combine");
					return;
				}
				_renderer.TryGetSharedMesh(out _mesh);
				if (_mesh == null)
				{
					SetNotReadyForCombine("Mesh Not Found");
					return;
				}
				if (_materialIndex >= _mesh.subMeshCount)
				{
					SetNotReadyForCombine("MaterialIndex More Then Submeshes Count");
					return;
				}
				GatherUVData();
				if (Mathf.Abs(_data.UVScale.x) < 0.0001f || Mathf.Abs(_data.UVScale.y) < 0.0001f)
				{
					SetNotReadyForCombine("UVScale equals zero");
					return;
				}
				if (string.IsNullOrWhiteSpace(shaderInfo.MainTextureName))
				{
					SetNotReadyForCombine("ShaderInfo missed MainTextureName");
					return;
				}
				Texture2D texture2D = _material.GetTexture(shaderInfo.MainTextureName) as Texture2D;
				if (texture2D == null)
				{
					SetNotReadyForCombine("MainTexture Not Found");
					return;
				}
				GatherMainTextureData(texture2D, combineOptions);
				if (Mathf.Abs(_data.MainTextureScale.x) < 0.0001f || Mathf.Abs(_data.MainTextureScale.y) < 0.0001f)
				{
					SetNotReadyForCombine("MainTexture Scale Equals Zero");
				}
				else if (_data.MainTextureTargetSize.x >= (float)combineOptions.maxAtlasSize || _data.MainTextureTargetSize.y >= (float)combineOptions.maxAtlasSize)
				{
					SetNotReadyForCombine("MainTexture size more or equal to maxAtlasSize");
				}
				else if (_data.MainTextureRepeats.x == 0 || _data.MainTextureRepeats.y == 0)
				{
					SetNotReadyForCombine("TextureRepeats equals to zero");
				}
				else if (Mathf.Max(_data.MainTextureDownscale.x, _data.MainTextureDownscale.y) > (float)combineOptions.maxTileTextureDownscale)
				{
					SetNotReadyForCombine("Too Much Main Texture Downscale");
				}
				else
				{
					SetReadyForCombine();
				}
			}
			catch (Exception ex)
			{
				SetNotReadyForCombine("Error during gathering data: " + ex.Message + "\n" + ex.StackTrace);
			}
		}

		public Vector2Int CalculateTextureRepeats(Vector2 textureScale, Vector2 textureOffset)
		{
			if (_shaderInfo == null)
			{
				Debug.Log("MaterialCombineInstance::CalculateTextureRepeats - needed to GatherCombineData first");
				return Vector2Int.zero;
			}
			textureScale.x = Mathf.Abs(textureScale.x);
			textureScale.y = Mathf.Abs(textureScale.y);
			textureOffset.x = Mathf.Repeat(textureOffset.x, 1f);
			textureOffset.y = Mathf.Repeat(textureOffset.y, 1f);
			return new Vector2Int(Mathf.CeilToInt(Mathf.Abs(_data.UVScale.x + Mathf.Repeat(_data.UVOffset.x, 1f)) * textureScale.x + textureOffset.x), Mathf.CeilToInt(Mathf.Abs(_data.UVScale.y + Mathf.Repeat(_data.UVOffset.y, 1f)) * textureScale.y + textureOffset.y));
		}

		private void GatherUVData()
		{
			int[] indices = _mesh.GetIndices(_materialIndex);
			Vector2[] uv = _mesh.uv;
			Vector2 uVOffset = Vector2.one * float.MaxValue;
			Vector2 vector = Vector2.one * float.MinValue;
			foreach (int num in indices)
			{
				Vector2 vector2 = uv[num];
				vector2.x = vector2.x.SnapToInt();
				vector2.y = vector2.y.SnapToInt();
				uVOffset.x = Mathf.Min(uVOffset.x, vector2.x);
				uVOffset.y = Mathf.Min(uVOffset.y, vector2.y);
				vector.x = Mathf.Max(vector.x, vector2.x);
				vector.y = Mathf.Max(vector.y, vector2.y);
			}
			_data.UVScale = new Vector2(Mathf.Abs(vector.x - uVOffset.x), Mathf.Abs(vector.y - uVOffset.y));
			_data.UVOffset = uVOffset;
		}

		private void GatherMainTextureData(Texture2D mainTexture, MaterialsCombineOptions combineOptions)
		{
			Vector2 textureScale = _material.GetTextureScale(_shaderInfo.MainTextureName);
			Vector2 textureOffset = _material.GetTextureOffset(_shaderInfo.MainTextureName);
			textureScale.x = Mathf.Abs(textureScale.x);
			textureScale.y = Mathf.Abs(textureScale.y);
			textureOffset.x = Mathf.Repeat(textureOffset.x, 1f);
			textureOffset.y = Mathf.Repeat(textureOffset.y, 1f);
			Vector2Int mainTextureRepeats = CalculateTextureRepeats(textureScale, textureOffset);
			Vector2 vector = new Vector2(mainTexture.width, mainTexture.height);
			Vector2 vector2 = new Vector2(vector.x * (float)mainTextureRepeats.x, vector.y * (float)mainTextureRepeats.y);
			float b = (float)(int)Mathf.Max(combineOptions.maxTileTextureUpscaledSize, vector.x, vector.y) / Mathf.Max(vector2.x, vector2.y);
			b = Mathf.Min(1f, b);
			_data.MainTextureOffset = textureOffset;
			_data.MainTextureScale = textureScale;
			_data.MainTextureRepeats = mainTextureRepeats;
			_data.MainTextureTargetSize = new Vector2((vector2.x * b).FloorToPowerOfTwo(), (vector2.y * b).FloorToPowerOfTwo());
			_data.MainTextureDownscale = new Vector2(vector2.x / _data.MainTextureTargetSize.x, vector2.y / _data.MainTextureTargetSize.y);
		}

		private void SetReadyForCombine()
		{
			_readyForCombine = true;
			_reason = "";
		}

		private void SetNotReadyForCombine(string reason)
		{
			_readyForCombine = false;
			_reason = reason;
		}
	}
}
