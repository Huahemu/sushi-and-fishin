using System.Collections.Generic;
using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	[CreateAssetMenu(fileName = "GraphicsQualityPresetConnection", menuName = "SettingsGenerator/ConnectionCustom/GraphicsQualityPresetConnection", order = 5)]
	public class GraphicsQualityPresetConnectionSO : BoolConnectionSO
	{
		public List<GraphicsQualityPreset> Presets = new List<GraphicsQualityPreset>
		{
			new GraphicsQualityPreset
			{
				QualityName = "High",
				RenderDistance = 500f,
				AmbientOcclusionEnabled = true,
				MsaaOptionIndex = 3,
				AntiAliasingOptionIndex = 6,
				ShadowDistance = 25f
			},
			new GraphicsQualityPreset
			{
				QualityName = "Medium",
				RenderDistance = 300f,
				AmbientOcclusionEnabled = true,
				MsaaOptionIndex = 2,
				AntiAliasingOptionIndex = 5,
				ShadowDistance = 15f
			},
			new GraphicsQualityPreset
			{
				QualityName = "Low",
				RenderDistance = 100f,
				AmbientOcclusionEnabled = false,
				MsaaOptionIndex = 0,
				AntiAliasingOptionIndex = 0,
				ShadowDistance = 0f
			},
			new GraphicsQualityPreset
			{
				QualityName = "Ultra",
				RenderDistance = 750f,
				AmbientOcclusionEnabled = true,
				MsaaOptionIndex = 3,
				AntiAliasingOptionIndex = 6,
				ShadowDistance = 40f
			}
		};

		protected GraphicsQualityPresetConnection _connection;

		public override IConnection<bool> GetConnection()
		{
			if (_connection == null)
			{
				Create();
			}
			return _connection;
		}

		public void Create()
		{
			_connection = new GraphicsQualityPresetConnection(Presets);
		}

		public override void DestroyConnection()
		{
			if (_connection != null)
			{
				_connection.Destroy();
			}
			_connection = null;
		}
	}
}
