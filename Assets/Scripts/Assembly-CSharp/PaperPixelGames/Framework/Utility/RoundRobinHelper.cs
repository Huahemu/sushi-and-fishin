using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Framework.Utility
{
	public class RoundRobinHelper<T>
	{
		private LinkedList<T> _list = new LinkedList<T>();

		private LinkedListNode<T> _currentNode;

		public void Init(IEnumerable<T> items = null)
		{
			_list = new LinkedList<T>(items);
			if (_list.Count == 0)
			{
				UtilityMethods.Log("LinkedLink is empty so carefull with that!", LogType.Warning);
			}
			else
			{
				_currentNode = _list.First;
			}
		}

		public void Add(T item)
		{
			_list.AddLast(item);
			if (_currentNode == null)
			{
				_currentNode = _list.First;
			}
		}

		public bool Remove(T item)
		{
			bool num = _list.Remove(item);
			if (num && _currentNode.Value.Equals(item))
			{
				_currentNode = _list.First;
			}
			if (_list.Count == 0)
			{
				_currentNode = null;
			}
			return num;
		}

		public void Clear()
		{
			_list.Clear();
			_currentNode = null;
		}

		public bool Contains(T item)
		{
			return _list.Contains(item);
		}

		public T GetNext()
		{
			if (_list.Count == 0)
			{
				throw new InvalidOperationException("No items available for round-robin.");
			}
			if (_currentNode == null)
			{
				_currentNode = _list.First;
			}
			T value = _currentNode.Value;
			_currentNode = _currentNode.Next ?? _list.First;
			return value;
		}
	}
}
