using System.Collections.Generic;
using System.Linq;
using Nav3D.API;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Obstacles
{
	public abstract class ObstacleInfoBase
	{
		public Bounds Bounds { get; protected set; }

		public List<Triangle> Triangles { get; protected set; }

		public abstract List<int> IDs { get; }

		public abstract void ReplaceID(int _OldID, int _NewID);

		public override string ToString()
		{
			return "\t" + GetType().Name + ": IDs: " + string.Join(", ", IDs) + "\n";
		}

		public static List<ObstacleInfoBase> GroupInfos(List<ObstacleInfoBase> _ProcessingInfo)
		{
			List<ObstacleInfoBase> list = _ProcessingInfo.Copy();
			bool flag;
			do
			{
				flag = false;
				for (int i = 0; i < list.Count; i++)
				{
					ObstacleInfoBase obstacleInfoBase = list[i];
					for (int j = i + 1; j < list.Count; j++)
					{
						ObstacleInfoBase obstacleInfoBase2 = list[j];
						if (obstacleInfoBase.Intersects(obstacleInfoBase2))
						{
							list[i] = obstacleInfoBase.CombineWith(obstacleInfoBase2);
							list.RemoveAt(j);
							flag = true;
							break;
						}
					}
					if (flag)
					{
						break;
					}
				}
			}
			while (flag);
			return list;
		}

		public bool Intersects(ObstacleInfoBase _Other)
		{
			return Bounds.Intersects(_Other.Bounds);
		}

		public ObstacleInfoBase CombineWith(ObstacleInfoBase _OtherObstacleInfo)
		{
			return ObstacleInfoGrouped.CombineObstacleInfos(this, _OtherObstacleInfo);
		}

		protected void ComputeBounds()
		{
			Bounds bounds = ExtensionBounds.TrianglesBounds(Triangles);
			float num = TryGetEmbracingRegionsMaxRes(bounds);
			Bounds bounds2 = bounds.CeilSizeToMultiple(num).Enlarge(num);
			Bounds = new Bounds(bounds2.center, bounds2.size + new Vector3(Octree.BUCKET_STEP_OVER_GAP, Octree.BUCKET_STEP_OVER_GAP, Octree.BUCKET_STEP_OVER_GAP));
		}

		private float TryGetEmbracingRegionsMaxRes(Bounds _Bounds)
		{
			if (ObstacleParticularResolutionManager.Instance.TryGetCrossingRegions(_Bounds, out var _CrossingRegions))
			{
				float resolution = _CrossingRegions.Max((Nav3DParticularResolutionRegion _Region) => _Region.MinBucketSize);
				return GetClosestBucketSize(resolution, ObstacleManager.Instance.MinBucketSize);
			}
			return ObstacleManager.Instance.MinBucketSize;
		}

		private float GetClosestBucketSize(float _Resolution, float _MinBucketSize)
		{
			if (_Resolution <= _MinBucketSize)
			{
				return _MinBucketSize;
			}
			float num = _MinBucketSize;
			float num2 = 2f;
			while (num < _Resolution)
			{
				num *= num2;
			}
			return num;
		}
	}
}
