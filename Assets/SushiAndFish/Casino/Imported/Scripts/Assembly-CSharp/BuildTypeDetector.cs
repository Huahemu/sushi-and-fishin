namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public static class BuildTypeDetector
{
	public enum BuildType
	{
		Unknown = 0,
		LocalBuild = 1,
		SteamBuild = 2
	}

	public static BuildType GetCurrentBuildType()
	{
		return BuildType.SteamBuild;
	}

	public static bool IsLocalBuild()
	{
		return GetCurrentBuildType() == BuildType.LocalBuild;
	}

	public static bool IsSteamBuild()
	{
		return GetCurrentBuildType() == BuildType.SteamBuild;
	}

	public static string GetBuildTypeString()
	{
		return GetCurrentBuildType() switch
		{
			BuildType.LocalBuild => "Local Build", 
			BuildType.SteamBuild => "Steam Build", 
			_ => "Unknown Build", 
		};
	}

	public static void LogBuildType()
	{
		Debug.Log("Current Build Type: " + GetBuildTypeString());
	}
}

}
