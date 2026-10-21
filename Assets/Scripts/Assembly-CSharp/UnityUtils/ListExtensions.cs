using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityUtils
{
	public static class ListExtensions
	{
		private static readonly Random random = new Random();

		public static bool IsNullOrEmpty<T>(this IList<T> list)
		{
			if (list != null)
			{
				return !list.Any();
			}
			return true;
		}

		public static List<T> Clone<T>(this IList<T> list)
		{
			List<T> list2 = new List<T>();
			foreach (T item in list)
			{
				list2.Add(item);
			}
			return list2;
		}

		public static void Swap<T>(this IList<T> list, int indexA, int indexB)
		{
			T val = list[indexB];
			T val2 = list[indexA];
			T val3 = (list[indexA] = val);
			val3 = (list[indexB] = val2);
		}

		public static T GetRandomElement<T>(this IEnumerable<T> collection)
		{
			if (collection == null || !collection.Any())
			{
				return default;
			}
			int index = random.Next(collection.Count());
			return collection.ElementAt(index);
		}

		public static T GetRandomEnumValue<T>() where T : Enum
		{
			Array values = Enum.GetValues(typeof(T));
			if (values.Length <= 1)
			{
				throw new ArgumentException("Enum must have at least two values to exclude the first element.");
			}
			return (T)values.GetValue(random.Next(1, values.Length));
		}

		public static void Shuffle<T>(this IList<T> list)
		{
			for (int num = list.Count - 1; num > 0; num--)
			{
				int indexB = random.Next(0, num + 1);
				list.Swap(num, indexB);
			}
		}
	}
}
