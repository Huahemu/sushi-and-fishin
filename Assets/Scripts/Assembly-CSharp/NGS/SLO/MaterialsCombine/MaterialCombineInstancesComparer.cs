using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
	public static class MaterialCombineInstancesComparer
	{
		public static bool HasEqualParameters(ShaderCombineInfo shaderInfo, MaterialsCombineOptions combineOptions, MaterialCombineInstance instanceA, MaterialCombineInstance instanceB)
		{
			if (instanceA.Material.shader != instanceB.Material.shader)
			{
				return false;
			}
			foreach (ShaderProperty shaderProperty in shaderInfo.ShaderProperties)
			{
				if (shaderProperty.ignore)
				{
					continue;
				}
				if (shaderProperty.kind == ShaderPropertyKind.Texture2D)
				{
					if (!Compare2DTextureProperty(shaderProperty, instanceA, instanceB, combineOptions.fillEmptyTextures))
					{
						return false;
					}
				}
				else if (shaderProperty.kind == ShaderPropertyKind.Texture)
				{
					if (!CompareTextureProperty(shaderProperty, instanceA, instanceB))
					{
						return false;
					}
				}
				else if (shaderProperty.kind == ShaderPropertyKind.Float)
				{
					if (!CompareFloatProperty(shaderProperty, instanceA, instanceB))
					{
						return false;
					}
				}
				else if (shaderProperty.kind == ShaderPropertyKind.Color)
				{
					if (!CompareColorProperty(shaderProperty, instanceA, instanceB))
					{
						return false;
					}
				}
				else if (shaderProperty.kind == ShaderPropertyKind.Vector && !CompareVectorProperty(shaderProperty, instanceA, instanceB))
				{
					return false;
				}
			}
			return true;
		}

		private static bool Compare2DTextureProperty(ShaderProperty property, MaterialCombineInstance instanceA, MaterialCombineInstance instanceB, bool fillEmptyTextures)
		{
			Texture texture = instanceA.Material.GetTexture(property.name);
			Texture texture2 = instanceB.Material.GetTexture(property.name);
			if (texture == texture2)
			{
				return true;
			}
			if (!property.combine2DTexture)
			{
				return false;
			}
			if ((texture == null || texture2 == null) && !fillEmptyTextures)
			{
				return false;
			}
			return true;
		}

		private static bool CompareTextureProperty(ShaderProperty property, MaterialCombineInstance instanceA, MaterialCombineInstance instanceB)
		{
			Texture texture = instanceA.Material.GetTexture(property.name);
			Texture texture2 = instanceB.Material.GetTexture(property.name);
			return texture == texture2;
		}

		private static bool CompareFloatProperty(ShaderProperty property, MaterialCombineInstance instanceA, MaterialCombineInstance instanceB)
		{
			float num = instanceA.Material.GetFloat(property.name);
			float num2 = instanceB.Material.GetFloat(property.name);
			if (Mathf.Abs(num - num2) > property.threshold + Mathf.Epsilon)
			{
				return false;
			}
			return true;
		}

		private static bool CompareColorProperty(ShaderProperty property, MaterialCombineInstance instanceA, MaterialCombineInstance instanceB)
		{
			Color color = instanceA.Material.GetColor(property.name);
			Color color2 = instanceB.Material.GetColor(property.name);
			if (Mathf.Abs(color.r - color2.r) > property.threshold + Mathf.Epsilon)
			{
				return false;
			}
			if (Mathf.Abs(color.g - color2.g) > property.threshold + Mathf.Epsilon)
			{
				return false;
			}
			if (Mathf.Abs(color.b - color2.b) > property.threshold + Mathf.Epsilon)
			{
				return false;
			}
			if (Mathf.Abs(color.a - color2.a) > property.threshold + Mathf.Epsilon)
			{
				return false;
			}
			return true;
		}

		private static bool CompareVectorProperty(ShaderProperty property, MaterialCombineInstance instanceA, MaterialCombineInstance instanceB)
		{
			Vector4 vector = instanceA.Material.GetVector(property.name);
			Vector4 vector2 = instanceB.Material.GetVector(property.name);
			if (Mathf.Abs(vector.x - vector2.x) > property.threshold + Mathf.Epsilon)
			{
				return false;
			}
			if (Mathf.Abs(vector.y - vector2.y) > property.threshold + Mathf.Epsilon)
			{
				return false;
			}
			if (Mathf.Abs(vector.z - vector2.z) > property.threshold + Mathf.Epsilon)
			{
				return false;
			}
			if (Mathf.Abs(vector.w - vector2.w) > property.threshold + Mathf.Epsilon)
			{
				return false;
			}
			return true;
		}
	}
}
