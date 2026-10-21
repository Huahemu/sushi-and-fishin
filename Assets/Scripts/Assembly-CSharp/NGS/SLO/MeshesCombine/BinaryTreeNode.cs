using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
	[Serializable]
	public class BinaryTreeNode
	{
		[SerializeReference]
		private BinaryTreeNode _left;

		[SerializeReference]
		private BinaryTreeNode _right;

		[SerializeField]
		private bool _isLeaf;

		[SerializeField]
		private Bounds _bounds;

		[SerializeField]
		private List<BinaryTreeData> _datas;

		public BinaryTreeNode Left => _left;

		public BinaryTreeNode Right => _right;

		public bool IsLeaf => _isLeaf;

		public bool HasChilds => _left != null;

		public bool HasData
		{
			get
			{
				if (_datas != null)
				{
					return _datas.Count > 0;
				}
				return false;
			}
		}

		public Bounds Bounds => _bounds;

		public Vector3 Center => _bounds.center;

		public Vector3 Size => _bounds.size;

		public IReadOnlyList<BinaryTreeData> Datas => _datas;

		public BinaryTreeNode(Vector3 center, Vector3 size, bool isLeaf)
		{
			_bounds = new Bounds(center, size);
			_isLeaf = isLeaf;
		}

		public void SetChilds(BinaryTreeNode left, BinaryTreeNode right)
		{
			_left = left;
			_right = right;
		}

		public bool Contains(Vector3 point)
		{
			return _bounds.Contains(point);
		}

		public void AddData(BinaryTreeData data)
		{
			if (_datas == null)
			{
				_datas = new List<BinaryTreeData>();
			}
			if (!_datas.Contains(data))
			{
				_datas.Add(data);
			}
		}
	}
}
