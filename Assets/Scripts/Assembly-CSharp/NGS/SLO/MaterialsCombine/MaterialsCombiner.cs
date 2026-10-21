using System;
using System.Collections.Generic;
using System.Linq;
using NGS.SLO.Shared;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
	public class MaterialsCombiner
	{
		private ITexturePacker _texturePacker;

		public MaterialsCombiner(ITexturePacker texturePacker)
		{
			_texturePacker = texturePacker;
		}

		public MaterialCombineResult Combine(IReadOnlyList<MaterialCombineInstance> instances, MaterialsCombineOptions combineOptions)
		{
			try
			{
				if (instances == null || instances.Count == 0)
				{
					return new MaterialCombineResult("Tried to combine empty arguments");
				}
				ShaderCombineInfo shaderInfo = instances[0].ShaderInfo;
				if (shaderInfo == null)
				{
					return new MaterialCombineResult("Empty ShaderInfo in first instance");
				}
				for (int i = 0; i < instances.Count; i++)
				{
					if (!instances[i].ReadyForCombine)
					{
						return new MaterialCombineResult(instances[i].Material.name + " not ready for combine");
					}
				}
				Material material = new Material(instances[0].Material);
				material.name = CreateCombinedMaterialName(instances);
				Rect[] rects = null;
				Texture2D[] array = new Texture2D[instances.Count];
				foreach (ShaderProperty shaderProperty in shaderInfo.ShaderProperties)
				{
					if (shaderProperty.kind == ShaderPropertyKind.Texture2D && !shaderProperty.ignore && shaderProperty.combine2DTexture && AnyInstanceHasTexture(shaderProperty, instances))
					{
						GatherAndPrepareLayerTextures(shaderProperty, instances, array, combineOptions.fillEmptyTextures);
						Texture2D texture2D = _texturePacker.PackTextures(array, combineOptions.padding, combineOptions.maxAtlasSize, out rects);
						texture2D.name = CreateAtlasName(array);
						material.SetTexture(shaderProperty.name, texture2D);
						material.SetTextureScale(shaderProperty.name, Vector2.one);
						material.SetTextureOffset(shaderProperty.name, Vector2.zero);
					}
				}
				return new MaterialCombineResult(material, instances.ToArray(), rects);
			}
			catch (Exception ex)
			{
				Debug.LogError("MaterialCombiner::Error occured while combine materials : " + ex.Message + "\n" + ex.StackTrace);
				return new MaterialCombineResult(ex.Message + "\n" + ex.StackTrace);
			}
		}

		private string CreateCombinedMaterialName(IReadOnlyList<MaterialCombineInstance> instances)
		{
			string text = "";
			int num = Mathf.Min(3, instances.Count);
			for (int i = 0; i < num; i++)
			{
				string text2 = instances[i].Material.name;
				if (string.IsNullOrEmpty(text2))
				{
					text2 = Mathf.Abs(instances[i].Material.GetHashCode()).ToString();
				}
				if (text2.Length > 5)
				{
					text2 = text2.Remove(5);
				}
				text = text + text2 + "_";
			}
			return text.Remove(text.Length - 1);
		}

		private string CreateAtlasName(Texture2D[] textures)
		{
			string text = "";
			int num = Mathf.Min(3, textures.Length);
			for (int i = 0; i < num; i++)
			{
				string text2 = textures[i].name;
				if (string.IsNullOrEmpty(text2))
				{
					text2 = Mathf.Abs(textures[i].GetHashCode()).ToString();
				}
				if (text2.Length > 5)
				{
					text2 = text2.Remove(5);
				}
				text = text + text2 + "_";
			}
			return text.Remove(text.Length - 1);
		}

		private bool AnyInstanceHasTexture(ShaderProperty property, IReadOnlyList<MaterialCombineInstance> instances)
		{
			string name = property.name;
			foreach (MaterialCombineInstance instance in instances)
			{
				if (instance.Material.GetTexture(name) != null)
				{
					return true;
				}
			}
			return false;
		}

		private void GatherAndPrepareLayerTextures(ShaderProperty property, IReadOnlyList<MaterialCombineInstance> instances, Texture2D[] result, bool fillEmptyTextures)
		{
			for (int i = 0; i < instances.Count; i++)
			{
				MaterialCombineInstance materialCombineInstance = instances[i];
				Texture2D texture2D = materialCombineInstance.Material.GetTexture(property.name) as Texture2D;
				if (texture2D == null)
				{
					if (!fillEmptyTextures)
					{
						throw new ArgumentException("MaterialCombiner::" + materialCombineInstance.Material.name + " missed texture " + property.name + " and 'fillEmptyTextures' disabled");
					}
					texture2D = CreateEmptyTexture(property);
				}
				Vector2 textureScale = MaterialUtil.GetTextureScale(materialCombineInstance.Material, property.name);
				Vector2 textureOffset = MaterialUtil.GetTextureOffset(materialCombineInstance.Material, property.name);
				Vector2Int repeats = materialCombineInstance.CalculateTextureRepeats(textureScale, textureOffset);
				Vector2Int size = new Vector2Int((int)materialCombineInstance.Data.MainTextureTargetSize.x, (int)materialCombineInstance.Data.MainTextureTargetSize.y);
				if (repeats.x < 1 || repeats.y < 1 || repeats.x > materialCombineInstance.Data.MainTextureRepeats.x || repeats.y > materialCombineInstance.Data.MainTextureRepeats.y)
				{
					repeats = materialCombineInstance.Data.MainTextureRepeats;
				}
				result[i] = PrepareTexture(texture2D, repeats, size);
			}
		}

		private Texture2D PrepareTexture(Texture2D texture, Vector2Int repeats, Vector2Int size)
		{
			return TextureUtil.UncompressRepeatResize(texture, size.x, size.y, repeats.x, repeats.y);
		}

		private Texture2D CreateEmptyTexture(ShaderProperty property)
		{
			string text = property.name.ToLower();
			Color color = Color.black;
			if (text.Contains("normal"))
			{
				color = new Color(0.5f, 0.5f, 1f, 1f);
			}
			else if (text.Contains("occlusion") || text.Contains("ao"))
			{
				color = Color.white;
			}
			else if (text.Contains("albedo") || text.Contains("basecolor") || text.Contains("maintex") || text.Contains("diffuse"))
			{
				color = Color.white;
			}
			else if (text.Contains("roughness") || text.Contains("rgh"))
			{
				color = Color.white;
			}
			else if (text.Contains("height") || text.Contains("displacement"))
			{
				color = Color.black;
			}
			else if (text.Contains("detail") || text.Contains("detailmask"))
			{
				color = Color.white;
			}
			else if (text.Contains("opacity") || text.Contains("alpha"))
			{
				color = Color.white;
			}
			Texture2D texture2D = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
			texture2D.SetPixel(0, 0, color);
			texture2D.Apply();
			return texture2D;
		}
	}
}
