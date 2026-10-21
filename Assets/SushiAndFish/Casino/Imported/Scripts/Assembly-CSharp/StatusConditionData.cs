namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

[Serializable]
public class StatusConditionData : ChallengeConditionData
{
	[Header("Status Settings")]
	[Tooltip("The buff type to check for")]
	public PlayerBuffType requiredBuff;

	[Tooltip("Whether the buff must be active (true) or must not be active (false)")]
	public bool requireBuffActive = true;

	public override bool Evaluate(ChallengeContext context)
	{
		bool flag = context.HadBuff(requiredBuff);
		if (!requireBuffActive)
		{
			return !flag;
		}
		return flag;
	}

	public override float GetProgress(ChallengeContext context)
	{
		bool flag = context.HadBuff(requiredBuff);
		if (!(requireBuffActive ? flag : (!flag)))
		{
			return 0f;
		}
		return 1f;
	}

	public override string GetProgressText(ChallengeContext context)
	{
		bool flag = context.HadBuff(requiredBuff);
		string buffName = GetBuffName(requiredBuff);
		if (requireBuffActive)
		{
			if (flag)
			{
				return buffName + " active (✓)";
			}
			return buffName + " required";
		}
		if (!flag)
		{
			return "No " + buffName + " (✓)";
		}
		return buffName + " not allowed";
	}

	private string GetBuffName(PlayerBuffType buffType)
	{
		return buffType switch
		{
			PlayerBuffType.TipsyFortune => "Drink buff", 
			PlayerBuffType.InspiringMelody => "Melody buff", 
			PlayerBuffType.Immunity => "Immunity buff", 
			_ => "Buff", 
		};
	}
}

}
