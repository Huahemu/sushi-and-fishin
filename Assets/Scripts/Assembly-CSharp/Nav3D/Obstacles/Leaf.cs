using System;
using System.Collections.Generic;
using System.Linq;
using Nav3D.Common;
using Nav3D.Obstacles.Serialization;
using UnityEngine;

namespace Nav3D.Obstacles
{
	public class Leaf : Node
	{
		private readonly HashSet<Leaf> m_FreeAdjacents = new HashSet<Leaf>();

		private Bounds? m_NavigationBounds;

		public bool Occupied => m_Occupied;

		public HashSet<Leaf> FreeAdjacents => m_FreeAdjacents;

		public Bounds NavigationBounds
		{
			get
			{
				Bounds valueOrDefault = m_NavigationBounds.GetValueOrDefault();
				if (!m_NavigationBounds.HasValue)
				{
					valueOrDefault = (m_Octree.ObstacleInfo.Bounds.Embrace(base.Bounds) ? base.Bounds : m_Octree.ObstacleInfo.Bounds.Intersection(base.Bounds));
					m_NavigationBounds = valueOrDefault;
					return valueOrDefault;
				}
				return valueOrDefault;
			}
		}

		public List<Vector3Int> FacesDirections { get; } = new List<Vector3Int>
		{
			Vector3Int.left,
			Vector3Int.right,
			Vector3Int.up,
			Vector3Int.down,
			Vector3Int.forward,
			Vector3Int.back
		};

		public Leaf(Octree _Octree, SpatialGrid _SpatialGrid, float _Size, Vector3Int _Index, int _GridLayer, bool _Occupied, Func<int> _GetID)
			: base(_Octree, _Size, _Index, _GridLayer, _Occupied, _GetID)
		{
			_SpatialGrid.AddLeaf(this);
			_SpatialGrid.SubtractLeafFromProgress(_GridLayer, _Processed: true);
		}

		public Leaf(int _ID, Vector3Int _Index, float _Size, int _GridLayer, bool _Occupied)
			: base(_ID, _Index, _Size, _GridLayer, _Occupied)
		{
		}

		public override Leaf GetClosestOrEmbracingLeaf(Vector3 _Point)
		{
			return this;
		}

		public void AddFreeAdjacent(Leaf _Adjacent)
		{
			m_FreeAdjacents.Add(_Adjacent);
		}

		public Vector3 GetAdjacentContactPoint(Leaf _OtherLeaf)
		{
			return GetAdjacentContactPointInternal(_OtherLeaf);
		}

		public void RemoveFaceConsideration(Vector3Int _Face)
		{
			FacesDirections.Remove(_Face);
		}

		public void SetFreeAdjacents(Dictionary<int, Node> _NodeMap, int[] _FreeAdjacentIDs)
		{
			foreach (int key in _FreeAdjacentIDs)
			{
				if (_NodeMap.TryGetValue(key, out var value))
				{
					m_FreeAdjacents.Add((Leaf)value);
				}
			}
		}

		public override void SetOctreeReference(Octree _Octree)
		{
			base.SetOctreeReference(_Octree);
			m_Index = m_Octree.GetBucketIndexOnLayer(m_GridLayer, this);
		}

		private Vector3 GetAdjacentContactPointInternal(Leaf _OtherLeaf)
		{
			Vector3 extents = NavigationBounds.extents;
			Vector3 center = NavigationBounds.center;
			if (m_GridLayer == _OtherLeaf.m_GridLayer)
			{
				if (m_Index.x != _OtherLeaf.m_Index.x)
				{
					if (m_Index.x < _OtherLeaf.m_Index.x)
					{
						return center + new Vector3(extents.x, 0f, 0f);
					}
					return center + new Vector3(0f - extents.x, 0f, 0f);
				}
				if (m_Index.y != _OtherLeaf.m_Index.y)
				{
					if (m_Index.y < _OtherLeaf.m_Index.y)
					{
						return center + new Vector3(0f, extents.y, 0f);
					}
					return center + new Vector3(0f, 0f - extents.y, 0f);
				}
				if (m_Index.z != _OtherLeaf.m_Index.z)
				{
					if (m_Index.z < _OtherLeaf.m_Index.z)
					{
						return center + new Vector3(0f, 0f, extents.z);
					}
					return center + new Vector3(0f, 0f, 0f - extents.z);
				}
				return center;
			}
			int num = Mathf.Abs(m_GridLayer - _OtherLeaf.m_GridLayer);
			Leaf leaf;
			Leaf leaf2;
			if (m_GridLayer < _OtherLeaf.m_GridLayer)
			{
				leaf = _OtherLeaf;
				leaf2 = this;
			}
			else
			{
				leaf = this;
				leaf2 = _OtherLeaf;
			}
			Vector3Int vector3Int = leaf.m_Index;
			Vector3Int index = leaf2.m_Index;
			for (int i = 0; i < num; i++)
			{
				vector3Int = UtilsSpatialGrid.GetParentIndex(vector3Int);
			}
			Vector3 extents2 = leaf.NavigationBounds.extents;
			Vector3 center2 = leaf.NavigationBounds.center;
			if (vector3Int.x != index.x)
			{
				if (vector3Int.x < index.x)
				{
					return center2 + new Vector3(extents2.x, 0f, 0f);
				}
				return center2 + new Vector3(0f - extents2.x, 0f, 0f);
			}
			if (vector3Int.y != index.y)
			{
				if (vector3Int.y < index.y)
				{
					return center2 + new Vector3(0f, extents2.y, 0f);
				}
				return center2 + new Vector3(0f, 0f - extents2.y, 0f);
			}
			if (vector3Int.z != index.z)
			{
				if (vector3Int.z < index.z)
				{
					return center2 + new Vector3(0f, 0f, extents2.z);
				}
				return center2 + new Vector3(0f, 0f, 0f - extents2.z);
			}
			Debug.LogError(string.Format("{0}: Positive infinity. {1} {2}, {3} {4}", "GetAdjacentContactPointInternal", leaf.m_GridLayer, leaf2.m_GridLayer, vector3Int, index));
			return Vector3.positiveInfinity;
		}

		protected override NodeSerializable GetSerializableInstance()
		{
			return new LeafSerializable(m_ID, m_Index, m_Size, (byte)m_GridLayer, Occupied, m_FreeAdjacents.Select((Leaf _Adjacent) => _Adjacent.ID).ToArray());
		}
	}
}
