using UnityEngine;

namespace PaperPixelGames.Framework.Indicator
{
	public sealed class OffscreenIndicatorTarget : MonoBehaviour, IOffscreenIndicatorTarget
	{
		[SerializeField]
		private OffscreenIndicatorVisualSettings _visualSettings = new OffscreenIndicatorVisualSettings();

		public OffscreenIndicatorVisualSettings Settings => GetSettings();

		public bool ShowDistance => GetSettings().DistanceVisible;

		public Vector3 WorldOffset => GetSettings().WorldOffset;

		Sprite IOffscreenIndicatorTarget.Icon => GetSettings().IconSprite;

		bool IOffscreenIndicatorTarget.ShowDistance => GetSettings().DistanceVisible;

		bool IOffscreenIndicatorTarget.IsValid => this != null;

		public Vector3 GetWorldPosition()
		{
			return base.transform.position + GetSettings().WorldOffset;
		}

		public void UpdateIcon(Sprite icon)
		{
			GetSettings().IconSprite = icon;
		}

		private OffscreenIndicatorVisualSettings GetSettings()
		{
			if (_visualSettings == null)
			{
				_visualSettings = new OffscreenIndicatorVisualSettings();
			}
			return _visualSettings;
		}
	}
}
