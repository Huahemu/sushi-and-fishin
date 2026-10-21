using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public static class ResidentYawQuantize
	{
		public static ushort EncodeYaw(float yawDegrees)
		{
			yawDegrees = Mathf.Repeat(yawDegrees, 360f);
			return (ushort)Mathf.Clamp(Mathf.RoundToInt(yawDegrees / 360f * 65535f), 0, 65535);
		}

		public static float DecodeYaw(ushort yawQ)
		{
			return (float)(int)yawQ / 65535f * 360f;
		}
	}
}
