using System;
using UnityEngine;

namespace PaperPixelGames.Core.Surf
{
	[Serializable]
	public sealed class SurfCameraSettings
	{
		[Min(1f)]
		public float proneDistance = 4.5f;

		[Min(1f)]
		public float standingDistance = 5.2f;

		[Min(0f)]
		public float speedPullback = 1.2f;

		[Range(45f, 90f)]
		public float fieldOfView = 65f;

		[Range(0f, 15f)]
		public float speedFieldOfView = 6f;

		[Range(1f, 30f)]
		public float horizontalFollow = 18f;

		[Range(1f, 20f)]
		public float verticalFollow = 5f;

		[Range(0.1f, 0.5f)]
		public float collisionRadius = 0.25f;

		[Min(0.2f)]
		public float waterClearance = 0.65f;
	}
}
