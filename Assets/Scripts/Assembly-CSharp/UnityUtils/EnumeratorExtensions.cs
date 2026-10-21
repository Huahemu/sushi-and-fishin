using System.Collections.Generic;

namespace UnityUtils
{
	public static class EnumeratorExtensions
	{
		public static IEnumerable<T> ToEnumerable<T>(this IEnumerator<T> e)
		{
			while (e.MoveNext())
			{
				yield return e.Current;
			}
		}
	}
}
