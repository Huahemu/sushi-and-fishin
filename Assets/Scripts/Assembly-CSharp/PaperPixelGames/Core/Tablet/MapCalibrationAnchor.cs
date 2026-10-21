using System;
using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	[Serializable]
	public sealed class MapCalibrationAnchor
	{
		[SerializeField]
		private string _name;

		[SerializeField]
		private bool _enabled = true;

		[SerializeField]
		private Vector3 _worldPosition;

		[SerializeField]
		private Vector2 _mapUV = new Vector2(0.5f, 0.5f);

		public string Name => _name;

		public bool Enabled => _enabled;

		public Vector3 WorldPosition => _worldPosition;

		public Vector2 MapUV => _mapUV;
	}
}
