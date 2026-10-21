using System;
using System.Collections.Generic;
using System.Linq;

namespace Nav3D.Common
{
	public static class ExtensionIEnumerable
	{
		public static List<T> Copy<T>(this List<T> _List)
		{
			return new List<T>(_List);
		}

		public static T Second<T>(this T[] _Array)
		{
			if (_Array == null || _Array.Length < 2)
			{
				throw new ArgumentException("List must be not null and has length greater then one");
			}
			return _Array[1];
		}

		public static int FindIndex<T>(this T[] _Array, Predicate<T> _Match)
		{
			for (int i = 0; i < _Array.Length; i++)
			{
				if (_Match(_Array[i]))
				{
					return i;
				}
			}
			return -1;
		}

		public static void RemoveFirst<T>(this List<T> _List)
		{
			if (_List == null || !_List.Any())
			{
				throw new ArgumentException("List must not to be null or empty");
			}
			_List.RemoveAt(0);
		}

		public static void RemoveLast<T>(this List<T> _List)
		{
			if (_List == null || !_List.Any())
			{
				throw new ArgumentException("List must not to be null or empty");
			}
			_List.RemoveAt(_List.Count - 1);
		}

		public static T Second<T>(this List<T> _List)
		{
			if (_List == null || _List.Count < 2)
			{
				throw new ArgumentException("List must be not null and has length greater then one");
			}
			return _List[1];
		}

		public static T[] Copy<T>(this T[] _Array)
		{
			T[] array = new T[_Array.Length];
			_Array.CopyTo(array, 0);
			return array;
		}

		public static Dictionary<K, V> Copy<K, V>(this Dictionary<K, V> _Dictionary)
		{
			Dictionary<K, V> dictionary = new Dictionary<K, V>(_Dictionary.Count);
			_Dictionary.ForEach((KeyValuePair<K, V> _Kvp) =>
			{
				dictionary.Add(_Kvp.Key, _Kvp.Value);
			});
			return dictionary;
		}

		public static void AddRange<T>(this HashSet<T> _HashSet, IEnumerable<T> _Enumerable)
		{
			_Enumerable.ForEach((T _Element) =>
			{
				_HashSet.Add(_Element);
			});
		}

		public static void AddRange<K, V>(this Dictionary<K, V> _Dictionary, IDictionary<K, V> _OtherDictionary)
		{
			_OtherDictionary.ForEach((KeyValuePair<K, V> _Kvp) =>
			{
				_Dictionary.Add(_Kvp.Key, _Kvp.Value);
			});
		}

		public static P Copy<P, T>(this P _Enumerable) where P : IEnumerable<T>, IList<T>, new()
		{
			P result = new P();
			foreach (T item in _Enumerable)
			{
				result.Add(item);
			}
			return result;
		}

		public static void ForEach<T>(this IEnumerable<T> _Enumerable, Action<T> _Action)
		{
			foreach (T item in _Enumerable)
			{
				_Action(item);
			}
		}

		public static T MinBy<T>(this IEnumerable<T> _Enumerable, Func<T, float> _MinFunc)
		{
			return _Enumerable.OrderBy(_MinFunc).First();
		}

		public static T MinBy<T>(this IEnumerable<T> _Enumerable, Func<T, float> _MinFunc, out float _Min)
		{
			_Min = float.MaxValue;
			T result = default;
			foreach (T item in _Enumerable)
			{
				float num = _MinFunc(item);
				if (!(num >= _Min))
				{
					_Min = num;
					result = item;
				}
			}
			return result;
		}

		public static bool IsNullOrEmpty<T>(this IEnumerable<T> _Enumerable)
		{
			if (_Enumerable != null)
			{
				return !_Enumerable.Any();
			}
			return true;
		}
	}
}
