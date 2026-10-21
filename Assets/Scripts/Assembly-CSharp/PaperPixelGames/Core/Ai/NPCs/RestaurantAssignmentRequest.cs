using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public readonly struct RestaurantAssignmentRequest
	{
		public readonly int TableId;

		public readonly int SeatIndex;

		public readonly ResidentRestaurantTargetType TargetType;

		public readonly Transform SeatTransform;

		public readonly Vector3 TargetDestination;

		public readonly Quaternion TargetRotation;

		public readonly float RequestTime;

		public RestaurantAssignmentRequest(int tableId, int seatIndex, ResidentRestaurantTargetType targetType, Transform seatTransform)
		{
			TableId = tableId;
			SeatIndex = seatIndex;
			TargetType = targetType;
			SeatTransform = seatTransform;
			TargetDestination = ((seatTransform != null) ? seatTransform.position : Vector3.zero);
			TargetRotation = ((seatTransform != null) ? seatTransform.rotation : Quaternion.identity);
			RequestTime = Time.time;
		}
	}
}
