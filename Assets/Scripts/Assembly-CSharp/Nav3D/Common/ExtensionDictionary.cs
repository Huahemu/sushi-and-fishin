using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Nav3D.Common
{
	public static class ExtensionDictionary
	{
		public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> _Dictionary, TKey _Key) where TValue : new()
		{
			if (_Dictionary.TryGetValue(_Key, out var value))
			{
				return value;
			}
			value = new TValue();
			_Dictionary.Add(_Key, value);
			return value;
		}

		public static TValue GetOrAdd<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> _Dictionary, TKey _Key) where TValue : new()
		{
			if (_Dictionary.TryGetValue(_Key, out var value))
			{
				return value;
			}
			value = new TValue();
			_Dictionary.TryAdd(_Key, value);
			return value;
		}
	}
}
