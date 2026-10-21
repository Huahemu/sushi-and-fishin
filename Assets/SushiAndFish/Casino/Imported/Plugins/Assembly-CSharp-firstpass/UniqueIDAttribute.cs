namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class UniqueIDAttribute : PropertyAttribute
{
	public string GroupName { get; private set; }

	public UniqueIDAttribute(string groupName = "")
	{
		GroupName = groupName;
	}
}

}
