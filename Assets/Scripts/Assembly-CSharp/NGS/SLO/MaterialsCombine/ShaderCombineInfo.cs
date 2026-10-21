using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.SLO.MaterialsCombine
{
	[Serializable]
	public class ShaderCombineInfo
	{
		[SerializeField]
		private bool _allowedCombine;

		[SerializeField]
		private Shader _shader;

		[SerializeField]
		private List<ShaderProperty> _shaderProperties;

		[SerializeField]
		private int _mainTextureIndex;

		public bool AllowedCombine
		{
			get
			{
				return _allowedCombine;
			}
			set
			{
				_allowedCombine = value;
			}
		}

		public Shader Shader => _shader;

		public IReadOnlyList<ShaderProperty> ShaderProperties => _shaderProperties;

		public string MainTextureName
		{
			get
			{
				if (_mainTextureIndex < 0)
				{
					return "";
				}
				return _shaderProperties[_mainTextureIndex].name;
			}
		}

		public ShaderCombineInfo(Shader shader)
		{
			_shader = shader;
			ResetProperties();
		}

		public void SetPropertyValues(ShaderProperty property, int propertyIndex)
		{
			if (propertyIndex < 0 || propertyIndex >= _shaderProperties.Count)
			{
				throw new IndexOutOfRangeException("PropertyIndex is out of range");
			}
			if (property.name != _shaderProperties[propertyIndex].name)
			{
				throw new ArgumentException("Properties Name Mismatch");
			}
			if (property.kind != _shaderProperties[propertyIndex].kind)
			{
				throw new ArgumentException("Properties Kind Mismatch");
			}
			_shaderProperties[propertyIndex] = property;
		}

		public void SetMainTexture(string textureName)
		{
			for (int i = 0; i < _shaderProperties.Count; i++)
			{
				ShaderProperty shaderProperty = _shaderProperties[i];
				if (shaderProperty.name == textureName)
				{
					if (shaderProperty.kind != ShaderPropertyKind.Texture2D)
					{
						throw new ArgumentException("MainTexture should be Texture2D");
					}
					_mainTextureIndex = i;
					return;
				}
			}
			throw new ArgumentException("Texture with name " + textureName + " not found");
		}

		public void ResetProperties()
		{
			_allowedCombine = true;
			_mainTextureIndex = -1;
			GatherShaderInfo(_shader);
		}

		private void GatherShaderInfo(Shader shader)
		{
			int propertyCount = shader.GetPropertyCount();
			_shaderProperties = new List<ShaderProperty>();
			for (int i = 0; i < propertyCount; i++)
			{
				string propertyName = shader.GetPropertyName(i);
				ShaderPropertyType propertyType = shader.GetPropertyType(i);
				ShaderPropertyKind kind = ShaderPropertyKind.Undefined;
				switch (propertyType)
				{
				case ShaderPropertyType.Texture:
					if (shader.GetPropertyTextureDimension(i) == TextureDimension.Tex2D)
					{
						kind = ShaderPropertyKind.Texture2D;
						if (_mainTextureIndex < 0)
						{
							_mainTextureIndex = i;
						}
					}
					else
					{
						kind = ShaderPropertyKind.Texture;
					}
					break;
				case ShaderPropertyType.Float:
				case ShaderPropertyType.Range:
					kind = ShaderPropertyKind.Float;
					break;
				case ShaderPropertyType.Color:
					kind = ShaderPropertyKind.Color;
					break;
				case ShaderPropertyType.Vector:
					kind = ShaderPropertyKind.Vector;
					break;
				}
				_shaderProperties.Add(new ShaderProperty(kind, propertyName));
			}
		}
	}
}
