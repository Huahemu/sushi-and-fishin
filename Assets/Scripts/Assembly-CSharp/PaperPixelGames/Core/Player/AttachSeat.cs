using System;
using UnityEngine;

namespace PaperPixelGames.Core.Player
{
	[Serializable]
	public class AttachSeat
	{
		[field: SerializeField]
		public Transform AttachPoint { get; private set; }

		[field: SerializeField]
		public Vector3 PositionOffset { get; private set; }

		[field: SerializeField]
		public Vector3 RotationOffset { get; private set; }

		[field: SerializeField]
		public Vector3 LookAt { get; private set; }

		public AttachSeat()
		{
		}

		public AttachSeat(Transform attachPoint, Vector3 positionOffset, Vector3 rotationOffset, Vector3 lookAt)
		{
			AttachPoint = attachPoint;
			PositionOffset = positionOffset;
			RotationOffset = rotationOffset;
			LookAt = lookAt;
		}
	}
}
