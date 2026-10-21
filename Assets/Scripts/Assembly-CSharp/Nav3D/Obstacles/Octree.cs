using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nav3D.API;
using Nav3D.Common;
using Nav3D.Obstacles.Serialization;
using Nav3D.Pathfinding;
using UnityEngine;

namespace Nav3D.Obstacles
{
	public class Octree
	{
		public static float BUCKET_STEP_OVER_GAP = 1E-05f;

		private readonly string EMPTY_LEAVES_ERROR = "[Octree]:FindPathInternal: There is no embracing leaf: A: {0}, ALeaf is null: {1}, B: {2}, BLeaf is null: {3}";

		private Node[] m_Roots;

		private float m_MinBucketSizeBase;

		private float m_MinBucketSizeReal;

		private volatile int m_CurNodeID = -1;

		private float[] m_BucketSizes;

		public ObstacleInfoBase ObstacleInfo { get; private set; }

		public int LayersCount { get; private set; }

		public int NodeCount { get; private set; }

		public GraphConstructionProgress ConstructionProgress { get; private set; }

		private int MaxLayer => LayersCount - 1;

		public Octree(ObstacleInfoBase _ObstacleInfo, float _MinBucketSize, CancellationToken _CancellationToken, GraphConstructionProgress _ConstructionProgress = null)
		{
			ObstacleInfo = _ObstacleInfo;
			ConstructionProgress = _ConstructionProgress;
			BuildTree(_MinBucketSize, _CancellationToken);
		}

		public Octree(ObstacleInfoBase _ObstacleInfo, Node[] _Roots, int _LayersCount, float _MinBucketSizeBase, float _MinBucketSizeReal, int _NodesCount)
		{
			ObstacleInfo = _ObstacleInfo;
			m_Roots = _Roots;
			LayersCount = _LayersCount;
			m_MinBucketSizeBase = _MinBucketSizeBase;
			m_MinBucketSizeReal = _MinBucketSizeReal;
			FillBucketSizesCache();
			NodeCount = _NodesCount;
			ConstructionProgress = GraphConstructionProgress.COMPLETED;
		}

		public OctreePathfindingResult FindPath(Vector3 _PointA, Vector3 _PointB, bool _TryRepositionStartToFreeLeaf, bool _TryRepositionTargetToFreeLeaf, CancellationToken _CancellationTokenExternal, CancellationToken _CancellationTokenTimeout)
		{
			return FindPathInternal(_PointA, _PointB, _TryRepositionStartToFreeLeaf, _TryRepositionTargetToFreeLeaf, _CancellationTokenExternal, _CancellationTokenTimeout);
		}

		public bool SegmentIntersectOccupiedLeaf(Segment3 _Segment3)
		{
			return m_Roots.Any((Node _Root) => SegmentIntersectOccupiedLeaf(_Root, _Segment3));
		}

		public bool PointInsideOccupiedLeaf(Vector3 _Point)
		{
			return m_Roots.Any((Node _Root) => PointInsideOccupiedLeaf(_Root, _Point));
		}

		public float GetBucketSizeOnLayer(int _Layer)
		{
			return m_BucketSizes[_Layer];
		}

		public Vector3Int GetBucketIndexOnLayer(int _Layer, Leaf _Leaf)
		{
			return UtilsSpatialGrid.GetBucketIndex(_Leaf.Bounds.center, GetBucketSizeOnLayer(_Layer));
		}

		public float GetCrossingRegionsMinRes(Bounds _Bounds)
		{
			if (ObstacleParticularResolutionManager.Instance.TryGetCrossingRegions(_Bounds, out var _CrossingRegions))
			{
				float resolution = _CrossingRegions.Min((Nav3DParticularResolutionRegion _Region) => _Region.MinBucketSize);
				return GetClosestBucketSize(resolution, m_MinBucketSizeBase);
			}
			return m_MinBucketSizeBase;
		}

		public bool TryGetEmbracingRegionsMinRes(Bounds _Bounds, out float _BucketSize, out int _RegionsCount)
		{
			if (ObstacleParticularResolutionManager.Instance.TryGetEmbracingRegions(_Bounds, out var _EmbracingRegions))
			{
				float resolution = _EmbracingRegions.Min((Nav3DParticularResolutionRegion _Region) => _Region.MinBucketSize);
				_RegionsCount = _EmbracingRegions.Count;
				_BucketSize = GetClosestBucketSize(resolution, m_MinBucketSizeBase);
				return true;
			}
			_BucketSize = m_MinBucketSizeBase;
			_RegionsCount = 0;
			return false;
		}

		public int GetLayersCount(float _MinBucketSize, out float _BoundsSize)
		{
			return GetLayersCount(ObstacleInfo.Bounds.GetMaxSize(), _MinBucketSize, out _BoundsSize);
		}

		public OctreeSerializable GetSerializableInstance(ObstacleSerializingProgress _Progress, int _ID)
		{
			return new OctreeSerializable(GetNodesSerializable(_Progress), m_Roots.Select((Node _Root) => _Root.ID).ToList(), LayersCount, m_MinBucketSizeBase, m_MinBucketSizeReal, _ID);
		}

		private int GetNextNodeID()
		{
			return Interlocked.Increment(ref m_CurNodeID);
		}

		private NodeSerializable[] GetNodesSerializable(ObstacleSerializingProgress _Progress)
		{
			List<NodeSerializable> list = new List<NodeSerializable>(m_CurNodeID + 1);
			Node[] roots = m_Roots;
			for (int i = 0; i < roots.Length; i++)
			{
				roots[i].GetSerializableInstances(list, _Progress);
			}
			return list.ToArray();
		}

		private void BuildTree(float _MinBucketSize, CancellationToken _CancellationToken)
		{
			m_MinBucketSizeBase = _MinBucketSize;
			ConstructionProgress.SetStatus(GraphConstructionProgress.ConstructionStatus.FILLING_TRIANGLE_STORAGE);
			TriangleStorage triangleStorage = new TriangleStorage(ObstacleInfo.Triangles, ConstructionProgress);
			ConstructionProgress.SetStatus(GraphConstructionProgress.ConstructionStatus.TREE_CONSTRUCTION_PREPARATION);
			float num = _MinBucketSize;
			int layersCount;
			int num2 = (layersCount = GetLayersCount(m_MinBucketSizeBase, out var _BoundsSize));
			bool num3 = ObstacleParticularResolutionManager.Instance.HasCrossingBoundables(ObstacleInfo.Bounds);
			if (num3)
			{
				num = GetCrossingRegionsMinRes(ObstacleInfo.Bounds);
				if (num != m_MinBucketSizeBase)
				{
					num2 = GetLayersCount(num, out var _);
				}
			}
			if (num3 && num2 > layersCount)
			{
				LayersCount = num2;
				m_MinBucketSizeReal = num;
			}
			else
			{
				LayersCount = layersCount;
				m_MinBucketSizeReal = m_MinBucketSizeBase;
			}
			FillBucketSizesCache();
			SpatialGrid spatialGrid = new SpatialGrid(LayersCount, m_MinBucketSizeReal, ConstructionProgress);
			int parallelFactor = (int)Mathf.Log(Mathf.ClosestPowerOfTwo((int)Mathf.Ceil(Mathf.Ceil(Mathf.Pow(2f, layersCount) * m_MinBucketSizeBase / m_MinBucketSizeBase) / 10f)), 2f) - 1;
			ConstructionProgress.SetStatus(GraphConstructionProgress.ConstructionStatus.TREE_CONSTRUCTION);
			BuildRoots(_BoundsSize, GetRootsIndices(_BoundsSize), triangleStorage, spatialGrid, layersCount, parallelFactor, _CancellationToken);
			ConstructionProgress.SetStatus(GraphConstructionProgress.ConstructionStatus.GRAPH_CONNECTIONS_BUILDING);
			spatialGrid.FormLeavesConnections(ConstructionProgress);
			NodeCount = m_CurNodeID + 1;
			ConstructionProgress.SetStatus(GraphConstructionProgress.ConstructionStatus.FINISHED);
		}

		private void BuildRoots(float _RootSize, Vector3Int[] _RootsIndices, TriangleStorage _TriangleStorage, SpatialGrid _SpatialGrid, int _BaseLayersCount, int _ParallelFactor, CancellationToken _CancellationToken)
		{
			try
			{
				ConstructionProgress.SetTotalRootsCount(GetTreeMaxPower(_RootsIndices.Length, MaxLayer));
				int maxLayer = _BaseLayersCount - 1;
				List<Task> list = new List<Task>(_RootsIndices.Length);
				ConcurrentHashSet<Node> roots = new ConcurrentHashSet<Node>();
				foreach (Vector3Int rootIndex in _RootsIndices)
				{
					Bounds rootBounds = UtilsSpatialGrid.GetBucketBounds(rootIndex, _RootSize);
					list.Add(Task.Run(() =>
					{
						bool flag = ObstacleParticularResolutionManager.Instance.HasCrossingBoundables(rootBounds);
						if (flag && TryGetEmbracingRegionsMinRes(rootBounds, out var _BucketSize, out var _RegionsCount) && ObstacleParticularResolutionManager.Instance.GetCrossingBoundablesCount(rootBounds) == _RegionsCount)
						{
							maxLayer = GetLayersCount(_BucketSize, out var _) - 1;
							flag = false;
						}
						Node value = Node.Create(this, _SpatialGrid, _TriangleStorage, _RootSize, rootIndex, 0, _Occupied: true, GetNextNodeID, maxLayer, flag, _CancellationToken, _ParallelFactor);
						roots.TryAdd(value);
					}, _CancellationToken));
				}
				Task.WaitAll(list.ToArray());
				m_Roots = roots.ToArray();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}

		private int GetTreeMaxPower(int _RootsCount, int _MaxLayer)
		{
			return _RootsCount * (int)Mathf.Pow(8f, _MaxLayer);
		}

		private Leaf GetClosestOrEmbracingLeaf(Vector3 _Point)
		{
			return m_Roots.MinBy((Node _Root) => (_Root.Bounds.center - _Point).sqrMagnitude).GetClosestOrEmbracingLeaf(_Point);
		}

		private Leaf GetNeighborFreeLeaf(Leaf _Leaf, Vector3 _InsidePoint)
		{
			Vector3 closestFaceCenterPlusNormal = _Leaf.Bounds.GetClosestFaceCenterPlusNormal(_InsidePoint, out var _Normal);
			return GetClosestOrEmbracingLeaf(closestFaceCenterPlusNormal + _Normal * m_MinBucketSizeReal * 0.5f);
		}

		private OctreePathfindingResult FindPathInternal(Vector3 _PointA, Vector3 _PointB, bool _TryRepositionStartToFreeLeaf, bool _TryRepositionGoalToFreeLeaf, CancellationToken _CancellationTokenExternal, CancellationToken _CancellationTokenTimeout)
		{
			Leaf leaf = GetClosestOrEmbracingLeaf(_PointA);
			Leaf leaf2 = GetClosestOrEmbracingLeaf(_PointB);
			if (leaf == null || leaf2 == null)
			{
				Debug.LogError(string.Format(EMPTY_LEAVES_ERROR, _PointA.ToStringExt(), leaf == null, _PointB.ToStringExt(), leaf2 == null));
				return new OctreePathfindingResult(new List<Vector3> { _PointA, _PointB }, PathfindingResultCode.SUCCEEDED);
			}
			if (leaf.Occupied)
			{
				if (!_TryRepositionStartToFreeLeaf)
				{
					return new OctreePathfindingResult(null, PathfindingResultCode.START_POINT_INSIDE_OBSTACLE);
				}
				leaf = GetNeighborFreeLeaf(leaf, _PointA);
				if (leaf.Occupied)
				{
					return new OctreePathfindingResult(null, PathfindingResultCode.START_POINT_INSIDE_OBSTACLE);
				}
			}
			if (leaf2.Occupied)
			{
				if (!_TryRepositionGoalToFreeLeaf)
				{
					return new OctreePathfindingResult(null, PathfindingResultCode.TARGET_POINT_INSIDE_OBSTACLE);
				}
				leaf2 = GetNeighborFreeLeaf(leaf2, _PointB);
				if (leaf2.Occupied)
				{
					return new OctreePathfindingResult(null, PathfindingResultCode.TARGET_POINT_INSIDE_OBSTACLE);
				}
			}
			PathResolverResult path = new AStar
			{
				StartLeaf = leaf,
				GoalLeaf = leaf2,
				CancellationTokenControl = _CancellationTokenExternal,
				CancellationTokenTimeout = _CancellationTokenTimeout
			}.GetPath();
			if (path.ResultCode != PathfindingResultCode.SUCCEEDED)
			{
				return new OctreePathfindingResult(null, path.ResultCode);
			}
			List<Leaf> path2 = path.Path;
			List<Vector3> list = new List<Vector3> { _PointA };
			if (path2.Any())
			{
				for (int i = 0; i < path2.Count - 1; i++)
				{
					Leaf leaf3 = path2[i];
					Leaf otherLeaf = path2[i + 1];
					list.Add(leaf3.NavigationBounds.center);
					list.Add(leaf3.GetAdjacentContactPoint(otherLeaf));
				}
				list.Add(path2.Last().NavigationBounds.center);
			}
			list.Add(_PointB);
			return new OctreePathfindingResult(list, PathfindingResultCode.SUCCEEDED);
		}

		private bool SegmentIntersectOccupiedLeaf(Node _CurrentNode, Segment3 _Segment3)
		{
			if (!_CurrentNode.Bounds.IntersectSegment(_Segment3))
			{
				return false;
			}
			if (_CurrentNode is Leaf leaf)
			{
				return leaf.Occupied;
			}
			return (_CurrentNode as Fork).ChildrenMap.Any((KeyValuePair<ForkChildOctIndex, Node> _ChildData) => SegmentIntersectOccupiedLeaf(_ChildData.Value, _Segment3));
		}

		private bool PointInsideOccupiedLeaf(Node _CurrentNode, Vector3 _Point)
		{
			if (_CurrentNode.Bounds.Contains(_Point))
			{
				if (_CurrentNode is Leaf leaf)
				{
					return leaf.Occupied;
				}
				foreach (KeyValuePair<ForkChildOctIndex, Node> item in (_CurrentNode as Fork).ChildrenMap)
				{
					if (PointInsideOccupiedLeaf(item.Value, _Point))
					{
						return true;
					}
				}
			}
			return false;
		}

		private Vector3Int[] GetRootsIndices(float _BoundSize)
		{
			return (from _Corner in ObstacleInfo.Bounds.GetCornerPoints()
				select UtilsSpatialGrid.GetBucketIndex(_Corner, _BoundSize)).Distinct().ToArray();
		}

		private float GetClosestBucketSize(float _Resolution, float _MinBucketSize)
		{
			float num = _MinBucketSize;
			if (_Resolution < num)
			{
				float num2 = 0.5f;
				do
				{
					num *= num2;
				}
				while (num > _Resolution);
			}
			else if (_Resolution > num)
			{
				float num2 = 2f;
				while (num < _Resolution)
				{
					num *= num2;
				}
			}
			return num;
		}

		private int GetLayersCount(float _MaxBucketSize, float _MinBucketSize, out float _BoundsSize)
		{
			int num = 1;
			float num2 = _MinBucketSize;
			while (num2 < _MaxBucketSize && num < 10)
			{
				num2 *= 2f;
				num++;
			}
			_BoundsSize = num2;
			return num;
		}

		private void FillBucketSizesCache()
		{
			m_BucketSizes = new float[LayersCount];
			for (int i = 0; i < LayersCount; i++)
			{
				m_BucketSizes[i] = m_MinBucketSizeReal * Mathf.Pow(2f, LayersCount - i - 1);
			}
		}
	}
}
