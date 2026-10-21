using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Settings;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kamgam.SettingsGenerator
{
	public class GraphicsQualityPresetConnection : Connection<bool>, IConnectionWithSettingsAccess
	{
		private const string AmbientOcclusionSettingId = "ambientOcclusionEnabled";

		private readonly List<GraphicsQualityPreset> _presets;

		private readonly MSAAConnection _msaaConnection = new MSAAConnection();

		private readonly AntiAliasingConnection _antiAliasingConnection = new AntiAliasingConnection
		{
			LimitToMainCamera = true,
			IncludeMSAA = true
		};

		private Settings _settings;

		private bool _enabled = true;

		public GraphicsQualityPresetConnection(List<GraphicsQualityPreset> presets)
		{
			_presets = presets;
		}

		public override bool Get()
		{
			return _enabled;
		}

		public override void Set(bool value)
		{
			_enabled = value;
			NotifyListenersIfChanged(value);
		}

		public void SetSettings(Settings settings)
		{
			_settings = settings;
		}

		public Settings GetSettings()
		{
			return _settings;
		}

		public override void OnQualityChanged(int qualityLevel)
		{
			if (_enabled)
			{
				ApplyPreset(qualityLevel);
			}
			base.OnQualityChanged(qualityLevel);
		}

		private void ApplyPreset(int qualityLevel)
		{
			if (TryGetPreset(qualityLevel, out var preset))
			{
				RuntimeSettings.SetRenderDistance(preset.RenderDistance);
				ApplyRenderPipelinePreset(preset);
				UtilityMethods.Log($"[GraphicsQualityPreset] quality:{preset.QualityName} renderDistance:{preset.RenderDistance:0.##} " + $"ao:{preset.AmbientOcclusionEnabled} msaaIndex:{preset.MsaaOptionIndex} shadowDistance:{preset.ShadowDistance:0.##}");
				NotifyListenersIfChanged(value: true);
			}
		}

		private bool TryGetPreset(int qualityLevel, out GraphicsQualityPreset preset)
		{
			preset = default;
			if (_presets == null || _presets.Count == 0)
			{
				return false;
			}
			string b = ((qualityLevel >= 0 && qualityLevel < QualitySettings.names.Length) ? QualitySettings.names[qualityLevel] : string.Empty);
			for (int i = 0; i < _presets.Count; i++)
			{
				if (string.Equals(_presets[i].QualityName, b, StringComparison.OrdinalIgnoreCase))
				{
					preset = _presets[i];
					return true;
				}
			}
			if (qualityLevel >= 0 && qualityLevel < _presets.Count)
			{
				preset = _presets[qualityLevel];
				return true;
			}
			return false;
		}

		private void ApplyRenderPipelinePreset(GraphicsQualityPreset preset)
		{
			UniversalRenderPipelineAsset universalRenderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
			if (universalRenderPipelineAsset != null)
			{
				universalRenderPipelineAsset.shadowDistance = Mathf.Max(0f, preset.ShadowDistance);
			}
			ApplyAmbientOcclusionPreset(preset.AmbientOcclusionEnabled);
			_msaaConnection.Set(preset.MsaaOptionIndex);
			_antiAliasingConnection.Set(preset.AntiAliasingOptionIndex);
		}

		private void ApplyAmbientOcclusionPreset(bool enabled)
		{
			if (_settings == null)
			{
				UtilityMethods.Log("[GraphicsQualityPreset] Ambient occlusion preset could not be applied because Settings is missing.", LogType.Error);
				return;
			}
			SettingBool settingBool = _settings.GetBool("ambientOcclusionEnabled");
			if (settingBool == null)
			{
				UtilityMethods.Log("[GraphicsQualityPreset] Ambient occlusion setting was not found | id:ambientOcclusionEnabled", LogType.Error);
				return;
			}
			bool flag = settingBool.GetValue() != enabled;
			settingBool.SetValue(enabled);
			if (!flag)
			{
				settingBool.PushToConnection();
			}
		}
	}
}
