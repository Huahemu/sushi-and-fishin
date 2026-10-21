using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class MapSeaAreaMarker : MonoBehaviour
	{
		[SerializeField]
		private byte _seaLayerIndex;

		[SerializeField]
		private byte _seaAreaIndex;

		public byte SeaLayerIndex => _seaLayerIndex;

		public byte SeaAreaIndex => _seaAreaIndex;

		public RectTransform RectTransform => base.transform as RectTransform;

		public void Configure(byte seaLayerIndex, byte seaAreaIndex)
		{
			_seaLayerIndex = seaLayerIndex;
			_seaAreaIndex = seaAreaIndex;
		}

		public void SetVisible(bool visible)
		{
			if (base.gameObject.activeSelf != visible)
			{
				base.gameObject.SetActive(visible);
			}
		}
	}
}
