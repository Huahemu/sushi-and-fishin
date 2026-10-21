using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Nav3D.Common
{
	public class ConcurrentHashSet<T> : IEnumerable<T>, IEnumerable
	{
		private readonly ConcurrentDictionary<T, byte> m_Dictionary = new ConcurrentDictionary<T, byte>();

		public int Count => m_Dictionary.Count;

		public IEnumerator<T> GetEnumerator()
		{
			return m_Dictionary.Keys.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return m_Dictionary.GetEnumerator();
		}

		public bool TryAdd(T _Value)
		{
			return m_Dictionary.TryAdd(_Value, 0);
		}

		public bool TryRemove(T _Value)
		{
			byte value;
			return m_Dictionary.TryRemove(_Value, out value);
		}

		public void Clear()
		{
			m_Dictionary.Clear();
		}

		public HashSet<T> GetHashSetCopy()
		{
			return new HashSet<T>(m_Dictionary.Keys);
		}
	}
}
