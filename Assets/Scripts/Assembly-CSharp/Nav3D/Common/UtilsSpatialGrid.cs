using Nav3D.Obstacles;
using UnityEngine;

namespace Nav3D.Common
{
	public static class UtilsSpatialGrid
	{
		public static float GetBucketSizePOTFactored(float _MinBucketSize, int _PowerOfTwo)
		{
			return _MinBucketSize * Mathf.Pow(2f, _PowerOfTwo);
		}

		public static float GetBucketSizeOnGridLayer(float _MinBucketSize, int _Layer, int _LayersCount)
		{
			return GetBucketSizePOTFactored(_MinBucketSize, _LayersCount - _Layer - 1);
		}

		public static Vector3Int GetBucketIndex(Vector3 _Point, float _BucketSize)
		{
			return new Vector3Int(Mathf.FloorToInt(_Point.x / _BucketSize), Mathf.FloorToInt(_Point.y / _BucketSize), Mathf.FloorToInt(_Point.z / _BucketSize));
		}

		public static Bounds GetBucketBounds(Vector3 _InsidePoint, float _BucketSize)
		{
			return GetBucketBounds(GetBucketIndex(_InsidePoint, _BucketSize), _BucketSize);
		}

		public static Bounds GetBucketBounds(Vector3Int _Index, float _BucketSize)
		{
			return new Bounds(new Vector3((float)_Index.x * _BucketSize + 0.5f * _BucketSize, (float)_Index.y * _BucketSize + 0.5f * _BucketSize, (float)_Index.z * _BucketSize + 0.5f * _BucketSize), new Vector3(_BucketSize, _BucketSize, _BucketSize));
		}

		public static Vector3Int GetParentIndex(Vector3Int _Index)
		{
			return new Vector3Int(Mathf.FloorToInt((float)_Index.x * 0.5f), Mathf.FloorToInt((float)_Index.y * 0.5f), Mathf.FloorToInt((float)_Index.z * 0.5f));
		}

		public static (ForkChildOctIndex OctIndex, Vector3Int GridIndex)[] GetChildrenIndices1(Vector3Int _BucketIndex)
		{
			Vector3Int item = new Vector3Int(_BucketIndex.x * 2, _BucketIndex.y * 2, _BucketIndex.z * 2);
			return new (ForkChildOctIndex, Vector3Int)[8]
			{
				(ForkChildOctIndex.I000, item),
				(ForkChildOctIndex.I001, new Vector3Int(item.x, item.y, item.z + 1)),
				(ForkChildOctIndex.I010, new Vector3Int(item.x, item.y + 1, item.z)),
				(ForkChildOctIndex.I011, new Vector3Int(item.x, item.y + 1, item.z + 1)),
				(ForkChildOctIndex.I100, new Vector3Int(item.x + 1, item.y, item.z)),
				(ForkChildOctIndex.I101, new Vector3Int(item.x + 1, item.y, item.z + 1)),
				(ForkChildOctIndex.I110, new Vector3Int(item.x + 1, item.y + 1, item.z)),
				(ForkChildOctIndex.I111, new Vector3Int(item.x + 1, item.y + 1, item.z + 1))
			};
		}
	}
}
