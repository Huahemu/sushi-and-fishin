using System;
using System.IO;
using UnityEngine;

namespace NGS.SLO.Shared
{
	public static class PathUtil
	{
		public static bool TryGetProjectRelative(string input, out string projectRelative)
		{
			projectRelative = null;
			if (string.IsNullOrWhiteSpace(input))
			{
				return false;
			}
			if (input.StartsWith("/"))
			{
				input = input.Remove(0, 1);
			}
			if (!input.EndsWith("/"))
			{
				input += "/";
			}
			string text = Application.dataPath.Replace('\\', '/');
			if (!Path.IsPathRooted(input) && input.Replace('\\', '/').StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
			{
				projectRelative = input.Replace('\\', '/');
				return true;
			}
			if (Path.IsPathRooted(input))
			{
				string text2 = Path.GetFullPath(input).Replace('\\', '/');
				if (text2.StartsWith(text, StringComparison.OrdinalIgnoreCase))
				{
					projectRelative = "Assets" + text2.Substring(text.Length);
					return true;
				}
				return false;
			}
			string text3 = Path.GetFullPath(Path.Combine(text, input)).Replace('\\', '/');
			if (text3.StartsWith(text, StringComparison.OrdinalIgnoreCase))
			{
				projectRelative = "Assets" + text3.Substring(text.Length);
				return true;
			}
			return false;
		}
	}
}
