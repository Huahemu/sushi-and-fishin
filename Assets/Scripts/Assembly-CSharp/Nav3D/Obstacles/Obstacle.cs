using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nav3D.Common;
using Nav3D.LocalAvoidance;
using Nav3D.Obstacles.Serialization;
using UnityEngine;

namespace Nav3D.Obstacles
{
	public class Obstacle : IBoundable
	{
		private readonly float m_MinBucketSize;

		private readonly ObstacleInfoBase m_ObstacleInfo;

		private Octree m_Octree;

		private CancellationTokenSource m_CTS;

		private TriangleStorage m_TriangleStorage;

		private GraphConstructionProgress m_TriangleStorageConstructionProgress;

		public bool Doomed { get; private set; }

		public ObstacleInfoBase ObstacleInfo => m_ObstacleInfo;

		public Bounds Bounds => m_ObstacleInfo.Bounds;

		public int OctreeLayersCount => m_Octree.LayersCount;

		public bool IsStatic { get; private set; }

		public GraphConstructionProgress ConstructionProgress { get; private set; }

		public int NodesCount => m_Octree?.NodeCount ?? 0;

		public Obstacle(ObstacleInfoBase _ObstacleInfo, float _MinBucketSize)
		{
			m_ObstacleInfo = _ObstacleInfo;
			m_MinBucketSize = _MinBucketSize;
			ConstructionProgress = GraphConstructionProgress.INITIAL;
			TryCreateTriangleStorage();
		}

		public Obstacle(ObstacleInfoBase _ObstacleInfo, Octree _Octree, float _MinBucketSize)
		{
			m_ObstacleInfo = _ObstacleInfo;
			m_Octree = _Octree;
			m_MinBucketSize = _MinBucketSize;
			IsStatic = true;
			ConstructionProgress = GraphConstructionProgress.COMPLETED;
			TryCreateTriangleStorage();
		}

		public OctreePathfindingResult FindPath(Vector3 _PointA, Vector3 _PointB, bool _TryRepositionStartToFreeLeaf, bool _TryRepositionTargetToFreeLeaf, CancellationToken _CancellationTokenExternal, CancellationToken _CancellationTokenTimeout)
		{
			return m_Octree.FindPath(_PointA, _PointB, _TryRepositionStartToFreeLeaf, _TryRepositionTargetToFreeLeaf, _CancellationTokenExternal, _CancellationTokenTimeout);
		}

		public bool PointInsideOccupiedLeaf(Vector3 _Point)
		{
			return m_Octree.PointInsideOccupiedLeaf(_Point);
		}

		public bool SegmentIntersectOccupiedLeaf(Segment3 _Segment3)
		{
			if (Bounds.IntersectSegment(_Segment3))
			{
				return m_Octree.SegmentIntersectOccupiedLeaf(_Segment3);
			}
			return false;
		}

		public void ConstructGraph()
		{
			m_CTS = new CancellationTokenSource();
			m_Octree = new Octree(m_ObstacleInfo, m_MinBucketSize, m_CTS.Token, ConstructionProgress);
		}

		public void Invalidate()
		{
			m_CTS?.Cancel();
			if (m_TriangleStorageConstructionProgress != null)
			{
				m_TriangleStorageConstructionProgress.CancelConstruction();
			}
			Doomed = true;
		}

		public List<Triangle> GetIntersectedOccupiedTriangles(Bounds _Bounds)
		{
			return m_TriangleStorage.GetIntersectedTriangles(_Bounds);
		}

		public void RecreateTriangleStorage(float _BucketSize)
		{
			if (m_TriangleStorageConstructionProgress != null)
			{
				m_TriangleStorageConstructionProgress.CancelConstruction();
			}
			m_TriangleStorageConstructionProgress = GraphConstructionProgress.INITIAL;
			Task.Run(() =>
			{
				m_TriangleStorage = new TriangleStorage(ObstacleInfo.Triangles, m_TriangleStorageConstructionProgress, _BucketSize);
			});
		}

		public OctreeSerializable GetSerializableOctreeInstance(ObstacleSerializingProgress _Progress, int _ID)
		{
			return m_Octree.GetSerializableInstance(_Progress, _ID);
		}

		private void TryCreateTriangleStorage()
		{
			float storageBucketSize = AgentManager.Instance.StorageBucketSize;
			if (storageBucketSize <= 0f)
			{
				m_TriangleStorage = TriangleStorage.EMPTY;
			}
			else
			{
				RecreateTriangleStorage(storageBucketSize);
			}
		}
	}
}
