using System;
using System.Linq;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
	public static class MaterialUtil
	{
		private static readonly string[] UnityStandardShaderNames = new string[3] { "Standard", "Standard (Specular setup)", "Autodesk Interactive" };

		public static bool IsUnityStandardShader(Shader shader)
		{
			if (UnityStandardShaderNames.Contains(shader.name))
			{
				return true;
			}
			return false;
		}

		public static Vector2 GetTextureOffset(Material material, string textureName)
		{
			if (!material.HasProperty(textureName))
			{
				throw new ArgumentException("MaterialsUtil::trying to get TextureOffset of not existed property");
			}
			if (IsUnityStandardShader(material.shader))
			{
				if (textureName.StartsWith("_Detail"))
				{
					return material.GetTextureOffset("_DetailAlbedoMap");
				}
				return material.GetTextureOffset("_MainTex");
			}
			return material.GetTextureOffset(textureName);
		}

		public static Vector2 GetTextureScale(Material material, string textureName)
		{
			if (!material.HasProperty(textureName))
			{
				throw new ArgumentException("MaterialsUtil::trying to get TextureScale of not existed property");
			}
			if (IsUnityStandardShader(material.shader))
			{
				if (textureName.StartsWith("_Detail"))
				{
					return material.GetTextureScale("_DetailAlbedoMap");
				}
				return material.GetTextureScale("_MainTex");
			}
			return material.GetTextureScale(textureName);
		}
	}
}
