using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	[CreateAssetMenu(fileName = "MapDefinition", menuName = "PaperPixelGames/Tablet/Map Definition")]
	public sealed class MapDefinitionSO : ScriptableObject
	{
		[SerializeField]
		private MapLayer _layer;

		[SerializeField]
		private MapProjectionMode _projectionMode;

		[SerializeField]
		private Vector3 _tiltedViewDirection;

		[SerializeField]
		private Sprite _sprite;

		[SerializeField]
		private MapZoomSettings _zoomSettings = new MapZoomSettings(0.2f, 0.3f, 0.4f, 0.1f);

		[SerializeField]
		private float _markerRotationOffset = 270f;

		[SerializeField]
		private Vector2 _referenceResolution = new Vector2(4096f, 2304f);

		[SerializeField]
		private List<MapCalibrationAnchor> _calibrationAnchors = new List<MapCalibrationAnchor>();

		public MapLayer Layer => _layer;

		public MapProjectionMode ProjectionMode => _projectionMode;

		public Vector3 TiltedViewDirection => _tiltedViewDirection;

		public Sprite Sprite => _sprite;

		public MapZoomSettings ZoomSettings => _zoomSettings;

		public float MarkerRotationOffset => _markerRotationOffset;

		public Vector2 ReferenceResolution => _referenceResolution;

		public IReadOnlyList<MapCalibrationAnchor> CalibrationAnchors => _calibrationAnchors;
	}
}
