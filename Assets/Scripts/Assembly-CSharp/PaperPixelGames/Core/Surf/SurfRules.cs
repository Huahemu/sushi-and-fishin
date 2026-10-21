namespace PaperPixelGames.Core.Surf
{
	public static class SurfRules
	{
		public static bool IsRiding(SurfboardState state)
		{
			if (state != SurfboardState.Prone)
			{
				return state == SurfboardState.Standing;
			}
			return true;
		}

		public static bool ReturnHome(SurfExitReason reason)
		{
			if (reason != SurfExitReason.Stuck && reason != SurfExitReason.Disconnect && reason != SurfExitReason.EndDay && reason != SurfExitReason.OutOfBounds)
			{
				return reason == SurfExitReason.Teleport;
			}
			return true;
		}

		public static bool CanMount(float depth, float heightAboveWater, float cooldown)
		{
			if (float.IsFinite(depth) && float.IsFinite(heightAboveWater) && depth >= 0.8f && heightAboveWater >= -1.8f && heightAboveWater <= 0.7f)
			{
				return cooldown <= 0f;
			}
			return false;
		}

		public static bool AcceptInput(uint expectedSession, uint receivedSession, ulong holder, ulong sender)
		{
			if (expectedSession == receivedSession)
			{
				return holder == sender;
			}
			return false;
		}
	}
}
