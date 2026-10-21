using System;
using System.Collections.Generic;
using System.Threading;
using Nav3D.Common;
using Nav3D.Obstacles.Serialization;
using UnityEngine;

namespace Nav3D.Obstacles
{
	public abstract class Node
	{
		protected Octree m_Octree;

		protected readonly float m_Size;

		protected readonly int m_GridLayer;

		protected Vector3Int m_Index;

		protected readonly bool m_Occupied;

		protected readonly int m_ID;

		private Bounds? m_Bounds;

		public int GridLayer => m_GridLayer;

		public Vector3Int Index => m_Index;

		public Bounds Bounds
		{
			get
			{
				Bounds valueOrDefault = m_Bounds.GetValueOrDefault();
				if (!m_Bounds.HasValue)
				{
					valueOrDefault = UtilsSpatialGrid.GetBucketBounds(m_Index, m_Size);
					m_Bounds = valueOrDefault;
					return valueOrDefault;
				}
				return valueOrDefault;
			}
		}

		public int ID => m_ID;

		public static Node Create(Octree _Octree, SpatialGrid _SpatialGrid, TriangleStorage _ObstacleTriangles, float _Size, Vector3Int _Index, int _GridLayer, bool _Occupied, Func<int> _GetID, int _MaxGridLayer, bool _NeedCheckMaxLayer, CancellationToken _CancellationToken, int _ParallelFactor)
		{
			if (_Occupied && _GridLayer < _MaxGridLayer)
			{
				return new Fork(_Octree, _SpatialGrid, _ObstacleTriangles, _Size, _Index, _GridLayer, _Occupied, _GetID, _MaxGridLayer, _NeedCheckMaxLayer, _CancellationToken, _ParallelFactor);
			}
			return new Leaf(_Octree, _SpatialGrid, _Size, _Index, _GridLayer, _Occupied, _GetID);
		}

		protected Node(Octree _Octree, float _Size, Vector3Int _Index, int _GridLayer, bool _Occupied, Func<int> _GetID)
		{
			m_Octree = _Octree;
			m_Size = _Size;
			m_Index = _Index;
			m_GridLayer = _GridLayer;
			m_Occupied = _Occupied;
			m_ID = _GetID();
		}

		protected Node(int _ID, Vector3Int _Index, float _Size, int _GridLayer, bool _Occupied)
		{
			m_ID = _ID;
			m_Index = _Index;
			m_Size = _Size;
			m_GridLayer = _GridLayer;
			m_Occupied = _Occupied;
		}

		public abstract Leaf GetClosestOrEmbracingLeaf(Vector3 _Point);

		public virtual void SetOctreeReference(Octree _Octree)
		{
			m_Octree = _Octree;
		}

		public virtual void GetSerializableInstances(List<NodeSerializable> _Nodes, ObstacleSerializingProgress _Progress)
		{
			if (!m_Octree.ConstructionProgress.CancellationToken.IsCancellationRequested)
			{
				_Nodes.Add(GetSerializableInstance());
				_Progress.SetNodesPackingProgress(_Nodes.Count, m_Octree.NodeCount);
			}
		}

		protected bool IsOccupy(TriangleStorage _TriangleStorage, Bounds _Bounds)
		{
			return _TriangleStorage.IsIntersect(_Bounds);
		}

		protected abstract NodeSerializable GetSerializableInstance();
	}
}
