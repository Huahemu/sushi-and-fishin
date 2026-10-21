using System;
using UnityEngine;

namespace PaperPixelGames.Core
{
	public static class ReleaseVersion
	{
		private const string DisplayPrefix = "v";

		public static string Value => Application.version;

		public static string DisplayValue => "v" + Value;

		public static int BuildNumber
		{
			get
			{
				if (!TryGetBuildNumber(Value, out var buildNumber))
				{
					return 0;
				}
				return buildNumber;
			}
		}

		public static bool IsKnownMismatch(string localVersion, string remoteVersion)
		{
			if (!string.IsNullOrWhiteSpace(localVersion) && !string.IsNullOrWhiteSpace(remoteVersion))
			{
				return !string.Equals(localVersion.Trim(), remoteVersion.Trim(), StringComparison.Ordinal);
			}
			return false;
		}

		public static bool TryGetBuildNumber(string version, out int buildNumber)
		{
			buildNumber = 0;
			if (string.IsNullOrWhiteSpace(version))
			{
				return false;
			}
			int num = version.LastIndexOf('.');
			if (num < 0 || num >= version.Length - 1)
			{
				return false;
			}
			if (int.TryParse(version.Substring(num + 1), out buildNumber))
			{
				return buildNumber > 0;
			}
			return false;
		}
	}
}
