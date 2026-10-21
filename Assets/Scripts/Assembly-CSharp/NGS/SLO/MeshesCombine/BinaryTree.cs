using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
	[Serializable]
	public class BinaryTree
	{
		[SerializeReference]
		private BinaryTreeNode _root;

		[SerializeField]
		private int _height;

		[SerializeField]
		private float _cellSize;

		public BinaryTreeNode Root => _root;

		public BinaryTree(float cellSize)
		{
			_height = 0;
			_cellSize = Mathf.Max(cellSize, 0.01f);
		}

		public void CreateTree(IReadOnlyList<BinaryTreeData> datas)
		{
			if (_root == null)
			{
				_root = CreateRoot(datas);
				_height = 1;
			}
			foreach (BinaryTreeData data in datas)
			{
				if (!_root.Contains(data.position))
				{
					GrowTreeUp(data);
				}
				Add(_root, data, 1);
			}
		}

		public void GetNotEmptyLeafs(List<BinaryTreeNode> outLeafs)
		{
			if (_root != null)
			{
				GetNotEmptyLeafs(_root, outLeafs);
			}
		}

		public void Clear()
		{
			_root = null;
			_height = 0;
		}

		private BinaryTreeNode CreateRoot(IReadOnlyList<BinaryTreeData> datas)
		{
			Vector3 center = Vector3.one * float.MaxValue;
			foreach (BinaryTreeData data in datas)
			{
				center.x = Mathf.Min(data.position.x, center.x);
				center.y = Mathf.Min(data.position.y, center.y);
				center.z = Mathf.Min(data.position.z, center.z);
			}
			return new BinaryTreeNode(center, Vector3.one * _cellSize, isLeaf: true);
		}

		private BinaryTreeNode ExpandRoot(BinaryTreeNode root, BinaryTreeData target)
		{
			Bounds bounds = root.Bounds;
			Vector3 position = target.position;
			Vector3 center = Vector3.zero;
			Vector3 size = Vector3.zero;
			Vector3 center2 = Vector3.zero;
			bool flag = false;
			for (int i = 0; i < 3; i++)
			{
				if (position[i] < bounds.min[i])
				{
					size = bounds.size;
					size[i] *= 2f;
					center = bounds.center;
					center[i] -= bounds.size[i] / 2f;
					center2 = bounds.center;
					center2[i] -= bounds.size[i];
					break;
				}
				if (position[i] > bounds.max[i])
				{
					size = bounds.size;
					size[i] *= 2f;
					center = bounds.center;
					center[i] += bounds.size[i] / 2f;
					center2 = bounds.center;
					center2[i] += bounds.size[i];
					flag = true;
					break;
				}
			}
			BinaryTreeNode binaryTreeNode = new BinaryTreeNode(center, size, isLeaf: false);
			BinaryTreeNode binaryTreeNode2 = new BinaryTreeNode(center2, bounds.size, root.IsLeaf);
			if (flag)
			{
				binaryTreeNode.SetChilds(_root, binaryTreeNode2);
			}
			else
			{
				binaryTreeNode.SetChilds(binaryTreeNode2, _root);
			}
			return binaryTreeNode;
		}

		private void GrowTreeUp(BinaryTreeData target)
		{
			if (!_root.Contains(target.position))
			{
				_root = ExpandRoot(_root, target);
				_height++;
				GrowTreeUp(target);
			}
		}

		private void GrowTreeDown(BinaryTreeNode node, BinaryTreeData target, int currentDepth)
		{
			if (node.HasChilds)
			{
				throw new Exception("GrowTreeDown::" + currentDepth + " node already has childs");
			}
			Bounds bounds = node.Bounds;
			Vector3 vector;
			Vector3 size;
			if (bounds.size.x > bounds.size.y && bounds.size.x > bounds.size.z)
			{
				vector = new Vector3(bounds.size.x / 4f, 0f, 0f);
				size = new Vector3(bounds.size.x / 2f, bounds.size.y, bounds.size.z);
			}
			else if (bounds.size.y > bounds.size.x || bounds.size.y > bounds.size.z)
			{
				vector = new Vector3(0f, bounds.size.y / 4f, 0f);
				size = new Vector3(bounds.size.x, bounds.size.y / 2f, bounds.size.z);
			}
			else
			{
				vector = new Vector3(0f, 0f, bounds.size.z / 4f);
				size = new Vector3(bounds.size.x, bounds.size.y, bounds.size.z / 2f);
			}
			bool flag = currentDepth == _height;
			BinaryTreeNode binaryTreeNode = new BinaryTreeNode(bounds.center - vector, size, flag);
			BinaryTreeNode binaryTreeNode2 = new BinaryTreeNode(bounds.center + vector, size, flag);
			node.SetChilds(binaryTreeNode, binaryTreeNode2);
			if (!flag)
			{
				if (binaryTreeNode.Contains(target.position))
				{
					GrowTreeDown(binaryTreeNode, target, currentDepth + 1);
				}
				if (binaryTreeNode2.Contains(target.position))
				{
					GrowTreeDown(binaryTreeNode2, target, currentDepth + 1);
				}
			}
		}

		private void Add(BinaryTreeNode node, BinaryTreeData data, int currentDepth)
		{
			if (node.IsLeaf)
			{
				node.AddData(data);
				return;
			}
			if (!node.HasChilds)
			{
				GrowTreeDown(node, data, currentDepth + 1);
			}
			BinaryTreeNode left = node.Left;
			BinaryTreeNode right = node.Right;
			if (left.Contains(data.position))
			{
				Add(left, data, currentDepth + 1);
			}
			else
			{
				Add(right, data, currentDepth + 1);
			}
		}

		private void GetNotEmptyLeafs(BinaryTreeNode current, List<BinaryTreeNode> outLeafs)
		{
			if (current.IsLeaf && current.HasData)
			{
				outLeafs.Add(current);
			}
			else if (current.HasChilds)
			{
				GetNotEmptyLeafs(current.Left, outLeafs);
				GetNotEmptyLeafs(current.Right, outLeafs);
			}
		}
	}
}
