using UnityEngine;

namespace PaperPixelGames.Core.Boss
{
	public static class CrabBossAnimatorParameterExtensions
	{
		public static string ToParameterName(this CrabBossAnimatorTrigger trigger)
		{
			if (trigger != CrabBossAnimatorTrigger.None)
			{
				return trigger.ToString();
			}
			return null;
		}

		public static string ToParameterName(this CrabBossAnimatorBool parameter)
		{
			if (parameter != CrabBossAnimatorBool.None)
			{
				return parameter.ToString();
			}
			return null;
		}

		public static string ToParameterName(this CrabBossAttackAnimation animation)
		{
			if (animation != CrabBossAttackAnimation.None)
			{
				return animation.ToString();
			}
			return null;
		}

		public static AnimatorControllerParameterType ToParameterType(this CrabBossAttackAnimation animation)
		{
			if (animation != CrabBossAttackAnimation.RockRain && animation != CrabBossAttackAnimation.ExpandingPulse)
			{
				return AnimatorControllerParameterType.Trigger;
			}
			return AnimatorControllerParameterType.Bool;
		}
	}
}
