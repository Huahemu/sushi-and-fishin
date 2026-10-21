using System;
using System.Collections.Generic;

namespace Nav3D.Pathfinding
{
	public class MinHeap<T> where T : IComparable<T>
	{
		private const int DEFAULT_HEAP_SIZE = 32;

		private List<T> m_Data;

		public int Count => m_Data.Count;

		public int Capacity => m_Data.Capacity;

		public MinHeap()
		{
			m_Data = new List<T>(32);
		}

		public MinHeap(int _Capacity)
		{
			if (_Capacity < 1)
			{
				throw new ArgumentException("Capacity must be greater than zero");
			}
			m_Data = new List<T>(_Capacity);
		}

		public void Add(T _Item)
		{
			int num = m_Data.Count;
			int num2 = num - 1 >> 1;
			m_Data.Add(_Item);
			while (num > 0 && _Item.CompareTo(m_Data[num2]) < 0)
			{
				m_Data[num] = m_Data[num2];
				num = num2;
				num2 = num - 1 >> 1;
			}
			m_Data[num] = _Item;
		}

		public T GetMin()
		{
			if (m_Data.Count == 0)
			{
				throw new InvalidOperationException("Cannot get min, heap is empty.");
			}
			return m_Data[0];
		}

		public T PopMin()
		{
			if (m_Data.Count == 0)
			{
				throw new InvalidOperationException("Cannot pop min, heap is empty.");
			}
			T result = m_Data[0];
			m_Data[0] = m_Data[m_Data.Count - 1];
			int num = 0;
			T value = m_Data[num];
			while (true)
			{
				int num2 = (num << 1) + 1;
				if (num2 >= m_Data.Count)
				{
					break;
				}
				int num3 = (num << 1) + 2;
				int num4 = ((num3 < m_Data.Count && m_Data[num2].CompareTo(m_Data[num3]) >= 0) ? num3 : num2);
				if (value.CompareTo(m_Data[num4]) < 0)
				{
					break;
				}
				m_Data[num] = m_Data[num4];
				num = num4;
			}
			m_Data[num] = value;
			m_Data.RemoveAt(m_Data.Count - 1);
			return result;
		}

		public void Clear()
		{
			m_Data = new List<T>(32);
		}

		public List<T> ToList()
		{
			return m_Data;
		}

		public override string ToString()
		{
			string text = "MinHeap{";
			if (m_Data.Count > 0)
			{
				text += m_Data[0];
				for (int i = 1; i < m_Data.Count; i++)
				{
					text = text + ", " + m_Data[i];
				}
			}
			text += "}";
			return base.ToString();
		}

		public void TestValidity()
		{
			for (int i = 1; i < m_Data.Count; i++)
			{
				int index = i - 1 >> 1;
				if (m_Data[index].CompareTo(m_Data[i]) > 0)
				{
					throw new Exception("Parent " + index + " greater than child " + i + "\n" + ToString());
				}
			}
		}
	}
}
